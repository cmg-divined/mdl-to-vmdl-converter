using GModMount.Source;
using System.Collections.Concurrent;

internal sealed class ConversionSummary
{
	public required string ModelOutputDirectory { get; init; }
	public required string VmdlPath { get; init; }
	public int SmdCount { get; init; }
	public int DmxCount { get; init; }
	public int BodyGroupCount { get; init; }
	public int HitboxSetCount { get; init; }
	public int PhysicsShapeCount { get; init; }
	public int PhysicsJointCount { get; init; }
	public int MaterialRemapCount { get; init; }
	public int MorphChannelCount { get; init; }
}

internal sealed class BatchConversionFailure
{
	public required string MdlPath { get; init; }
	public required string Error { get; init; }
}

internal sealed class BatchConversionSummary
{
	public required string OutputRoot { get; init; }
	public int TotalModels { get; init; }
	public int Succeeded { get; init; }
	public int Failed { get; init; }
	public int TotalSmdCount { get; init; }
	public int TotalDmxCount { get; init; }
	public int TotalMaterialRemapCount { get; init; }
	public int TotalMorphChannelCount { get; init; }
	public required IReadOnlyList<BatchConversionFailure> Failures { get; init; }
}

internal static class ConversionRunner
{
	public static ConversionSummary Run( ConverterOptions options, Action<string>? info = null, Action<string>? warn = null )
	{
		info ??= _ => { };
		warn ??= _ => { };

		Log.Verbose = options.Verbose;

		if ( string.IsNullOrWhiteSpace( options.MdlPath ) )
		{
			throw new ArgumentException( "Single-model conversion requires --mdl <file>." );
		}

		if ( !File.Exists( options.MdlPath ) )
		{
			throw new FileNotFoundException( $"MDL not found: {options.MdlPath}" );
		}

		return RunSingle( options, options.MdlPath, options.CopyShaders, info, warn );
	}

	public static BatchConversionSummary RunBatch( ConverterOptions options, Action<string>? info = null, Action<string>? warn = null )
	{
		info ??= _ => { };
		warn ??= _ => { };

		Log.Verbose = options.Verbose;

		if ( string.IsNullOrWhiteSpace( options.BatchRootDirectory ) )
		{
			throw new ArgumentException( "Batch conversion requires --batch <folder>." );
		}

		string batchRoot = Path.GetFullPath( options.BatchRootDirectory );
		if ( !Directory.Exists( batchRoot ) )
		{
			throw new DirectoryNotFoundException( $"Batch folder not found: {batchRoot}" );
		}

		SearchOption searchOption = options.RecursiveSearch ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
		List<string> mdlPaths = Directory.EnumerateFiles( batchRoot, "*.mdl", searchOption )
			.OrderBy( p => p, StringComparer.OrdinalIgnoreCase )
			.ToList();

		if ( mdlPaths.Count == 0 )
		{
			throw new FileNotFoundException( $"No .mdl files found under: {batchRoot}" );
		}

		string outputRoot = string.IsNullOrWhiteSpace( options.OutputDirectory )
			? Path.Combine( batchRoot, "_converted" )
			: options.OutputDirectory;
		Directory.CreateDirectory( outputRoot );
		string? batchGmodRoot = ResolveGmodRoot( options.GmodRootDirectory, batchRoot );
		if ( !string.IsNullOrWhiteSpace( batchGmodRoot ) && string.IsNullOrWhiteSpace( options.GmodRootDirectory ) )
		{
			info( $"Auto-detected GMod root: {batchGmodRoot}" );
		}

		if ( !string.IsNullOrWhiteSpace( options.VmdlFileName ) )
		{
			warn( "[warn] --vmdl is ignored in batch mode; output file names use each model's base name." );
		}

		if ( options.ConvertMaterials && options.CopyShaders )
		{
			ShaderCopyPipeline.Copy( options, outputRoot, info, warn );
		}

		var failures = new ConcurrentBag<BatchConversionFailure>();
		int succeeded = 0;
		int failed = 0;
		int totalSmdCount = 0;
		int totalDmxCount = 0;
		int totalMaterialRemapCount = 0;
		int totalMorphChannelCount = 0;

		var parallelOptions = new ParallelOptions
		{
			MaxDegreeOfParallelism = Math.Max( 1, options.MaxParallelism )
		};

		info( $"Batch conversion started: {mdlPaths.Count} model(s), threads={parallelOptions.MaxDegreeOfParallelism}, recursive={options.RecursiveSearch}" );

		Parallel.ForEach( mdlPaths, parallelOptions, mdlPath =>
		{
			string display = Path.GetRelativePath( batchRoot, mdlPath ).Replace( '\\', '/' );
			Action<string> scopedInfo = message => info( $"[{display}] {message}" );
			Action<string> scopedWarn = message => warn( $"[{display}] {message}" );

			try
			{
				ConverterOptions singleOptions = CreateSingleOptionsForBatch( options, mdlPath, outputRoot, batchGmodRoot );
				ConversionSummary summary = RunSingle( singleOptions, mdlPath, copyShaders: false, scopedInfo, scopedWarn );
				Interlocked.Increment( ref succeeded );
				Interlocked.Add( ref totalSmdCount, summary.SmdCount );
				Interlocked.Add( ref totalDmxCount, summary.DmxCount );
				Interlocked.Add( ref totalMaterialRemapCount, summary.MaterialRemapCount );
				Interlocked.Add( ref totalMorphChannelCount, summary.MorphChannelCount );
				scopedInfo( $"OK -> {summary.VmdlPath}" );
			}
			catch ( Exception ex )
			{
				Interlocked.Increment( ref failed );
				failures.Add( new BatchConversionFailure
				{
					MdlPath = mdlPath,
					Error = ex.Message
				} );
				scopedWarn( $"[fail] {ex.Message}" );
			}
		} );

		List<BatchConversionFailure> orderedFailures = failures
			.OrderBy( f => f.MdlPath, StringComparer.OrdinalIgnoreCase )
			.ToList();

		info( $"Batch finished. success={succeeded}, failed={failed}, smd={totalSmdCount}, dmx={totalDmxCount}, remaps={totalMaterialRemapCount}, morphs={totalMorphChannelCount}" );

		return new BatchConversionSummary
		{
			OutputRoot = outputRoot,
			TotalModels = mdlPaths.Count,
			Succeeded = succeeded,
			Failed = failed,
			TotalSmdCount = totalSmdCount,
			TotalDmxCount = totalDmxCount,
			TotalMaterialRemapCount = totalMaterialRemapCount,
			TotalMorphChannelCount = totalMorphChannelCount,
			Failures = orderedFailures
		};
	}

	private static ConversionSummary RunSingle(
		ConverterOptions options,
		string mdlPath,
		bool copyShaders,
		Action<string> info,
		Action<string> warn )
	{
		string modelBaseName = Path.GetFileNameWithoutExtension( mdlPath );
		string modelDir = Path.GetDirectoryName( mdlPath ) ?? Directory.GetCurrentDirectory();

		string vvdPath = options.VvdPath ?? Path.Combine( modelDir, modelBaseName + ".vvd" );
		string vtxPath = options.VtxPath ?? ResolveVtxPath( modelDir, modelBaseName );
		string phyPath = options.PhyPath ?? Path.Combine( modelDir, modelBaseName + ".phy" );
		if ( !File.Exists( phyPath ) )
		{
			phyPath = string.Empty;
		}

		if ( !File.Exists( vvdPath ) )
		{
			throw new FileNotFoundException( $"VVD not found: {vvdPath}" );
		}

		if ( !File.Exists( vtxPath ) )
		{
			throw new FileNotFoundException( $"VTX not found: {vtxPath}" );
		}

		string? gmodRoot = ResolveGmodRoot( options.GmodRootDirectory, mdlPath );
		string outputRoot = string.IsNullOrWhiteSpace( options.OutputDirectory )
			? Path.Combine( modelDir, modelBaseName + "_converted" )
			: options.OutputDirectory;
		string modelOutputDirectory = ResolveModelOutputDirectory( options, outputRoot, gmodRoot, mdlPath );

		Directory.CreateDirectory( outputRoot );
		Directory.CreateDirectory( modelOutputDirectory );

		info( "Loading Source model..." );
		SourceModel sourceModel = SourceModel.Load(
			mdlPath,
			vvdPath,
			vtxPath,
			string.IsNullOrEmpty( phyPath ) ? null! : phyPath
		);

		BuildContext buildContext = ConverterPipeline.Build( sourceModel, modelBaseName, modelOutputDirectory );
		buildContext.ModelAssetDirectory = ToAssetRelativePath( outputRoot, modelOutputDirectory );

		int smdCount = 0;
		int dmxCount = 0;
		foreach ( MeshExport mesh in buildContext.Meshes )
		{
			if ( mesh.Morphs.Count > 0 )
			{
				string dmxFileName = Path.ChangeExtension( mesh.FileName, ".dmx" ) ?? (mesh.FileName + ".dmx");
				mesh.FileName = dmxFileName;
				DmxWriter.WriteMesh( Path.Combine( modelOutputDirectory, dmxFileName ), buildContext, mesh );
				dmxCount++;
			}
			else
			{
				SmdWriter.WriteMesh( Path.Combine( modelOutputDirectory, mesh.FileName ), buildContext, mesh );
				smdCount++;
			}
		}

		if ( options.ConvertMaterials )
		{
			MaterialConversionResult materialResult = MaterialPipeline.Convert(
				buildContext,
				options,
				outputRoot,
				gmodRoot,
				mdlPath,
				info,
				warn
			);

			buildContext.MaterialRemaps.AddRange( materialResult.Remaps );

			if ( copyShaders )
			{
				ShaderCopyPipeline.Copy( options, outputRoot, info, warn );
			}
		}

		string vmdlFileName = options.VmdlFileName;
		if ( string.IsNullOrWhiteSpace( vmdlFileName ) )
		{
			vmdlFileName = modelBaseName + ".vmdl";
		}
		if ( !vmdlFileName.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase ) )
		{
			vmdlFileName += ".vmdl";
		}

		string vmdlPath = Path.Combine( modelOutputDirectory, vmdlFileName );
		VmdlWriter.Write( vmdlPath, buildContext );

		return new ConversionSummary
		{
			ModelOutputDirectory = modelOutputDirectory,
			VmdlPath = vmdlPath,
			SmdCount = smdCount,
			DmxCount = dmxCount,
			BodyGroupCount = buildContext.BodyGroups.Count,
			HitboxSetCount = buildContext.HitboxSets.Count,
			PhysicsShapeCount = buildContext.PhysicsShapes.Count,
			PhysicsJointCount = buildContext.PhysicsJoints.Count,
			MaterialRemapCount = buildContext.MaterialRemaps.Count,
			MorphChannelCount = buildContext.MorphChannelCount
		};
	}

	private static ConverterOptions CreateSingleOptionsForBatch( ConverterOptions baseOptions, string mdlPath, string outputRoot, string? batchGmodRoot )
	{
		return new ConverterOptions
		{
			MdlPath = mdlPath,
			BatchRootDirectory = null,
			RecursiveSearch = baseOptions.RecursiveSearch,
			MaxParallelism = baseOptions.MaxParallelism,
			VvdPath = null,
			VtxPath = null,
			PhyPath = null,
			OutputDirectory = outputRoot,
			VmdlFileName = string.Empty,
			GmodRootDirectory = string.IsNullOrWhiteSpace( baseOptions.GmodRootDirectory ) ? batchGmodRoot : baseOptions.GmodRootDirectory,
			ShaderSourceDirectory = baseOptions.ShaderSourceDirectory,
			PreserveModelRelativePath = baseOptions.PreserveModelRelativePath,
			ConvertMaterials = baseOptions.ConvertMaterials,
			CopyShaders = baseOptions.CopyShaders,
			Verbose = baseOptions.Verbose,
			MaterialProfileOverride = baseOptions.MaterialProfileOverride
		};
	}

	private static string ResolveModelOutputDirectory( ConverterOptions options, string outputRoot, string? gmodRoot, string mdlPath )
	{
		if ( !options.PreserveModelRelativePath )
		{
			return outputRoot;
		}

		if ( string.IsNullOrWhiteSpace( gmodRoot ) )
		{
			return outputRoot;
		}

		if ( !TryGetModelRelativeDirectory( mdlPath, gmodRoot, out string modelRelativeDirectory ) )
		{
			return outputRoot;
		}

		if ( string.IsNullOrWhiteSpace( modelRelativeDirectory ) )
		{
			return EnsureModelsRoot( outputRoot );
		}

		return Path.Combine( EnsureModelsRoot( outputRoot ), modelRelativeDirectory );
	}

	private static string EnsureModelsRoot( string outputRoot )
	{
		string rootName = new DirectoryInfo( outputRoot ).Name;
		if ( string.Equals( rootName, "models", StringComparison.OrdinalIgnoreCase ) )
		{
			return outputRoot;
		}

		return Path.Combine( outputRoot, "models" );
	}

	internal static bool TryGetModelRelativeDirectory( string mdlPath, string gmodRoot, out string relativeDirectory )
	{
		relativeDirectory = string.Empty;
		string modelsRoot = Path.Combine( gmodRoot, "models" );

		string mdlDirectory = Path.GetDirectoryName( mdlPath ) ?? string.Empty;
		if ( string.IsNullOrWhiteSpace( mdlDirectory ) || !Directory.Exists( modelsRoot ) )
		{
			return false;
		}

		string fullModelsRoot = Path.GetFullPath( modelsRoot );
		string fullMdlDirectory = Path.GetFullPath( mdlDirectory );

		if ( !fullMdlDirectory.StartsWith( fullModelsRoot, StringComparison.OrdinalIgnoreCase ) )
		{
			return false;
		}

		relativeDirectory = Path.GetRelativePath( fullModelsRoot, fullMdlDirectory )
			.Replace( '\\', Path.DirectorySeparatorChar )
			.Trim();
		return true;
	}

	internal static string? ResolveGmodRoot( string? explicitRoot, string referencePath )
	{
		if ( !string.IsNullOrWhiteSpace( explicitRoot ) )
		{
			string normalized = Path.GetFullPath( explicitRoot );
			if ( IsValidGmodRoot( normalized ) )
			{
				return normalized;
			}

			string nested = Path.Combine( normalized, "garrysmod" );
			if ( IsValidGmodRoot( nested ) )
			{
				return nested;
			}
		}

		DirectoryInfo? dir = GetSearchStartDirectory( referencePath );
		while ( dir is not null )
		{
			if ( IsValidGmodRoot( dir.FullName ) )
			{
				return dir.FullName;
			}

			string nested = Path.Combine( dir.FullName, "garrysmod" );
			if ( IsValidGmodRoot( nested ) )
			{
				return nested;
			}

			if ( string.Equals( dir.Name, "models", StringComparison.OrdinalIgnoreCase ) && dir.Parent is not null )
			{
				if ( string.Equals( dir.Parent.Name, "garrysmod", StringComparison.OrdinalIgnoreCase ) && IsValidGmodRoot( dir.Parent.FullName ) )
				{
					return dir.Parent.FullName;
				}
			}

			dir = dir.Parent;
		}

		return null;
	}

	private static bool IsValidGmodRoot( string rootDirectory )
	{
		if ( string.IsNullOrWhiteSpace( rootDirectory ) )
		{
			return false;
		}

		string models = Path.Combine( rootDirectory, "models" );
		string materials = Path.Combine( rootDirectory, "materials" );
		return Directory.Exists( models ) && Directory.Exists( materials );
	}

	private static DirectoryInfo? GetSearchStartDirectory( string referencePath )
	{
		if ( string.IsNullOrWhiteSpace( referencePath ) )
		{
			return null;
		}

		string normalized = Path.GetFullPath( referencePath );
		if ( Directory.Exists( normalized ) )
		{
			return new DirectoryInfo( normalized );
		}

		string? directory = Path.GetDirectoryName( normalized );
		if ( string.IsNullOrWhiteSpace( directory ) )
		{
			return null;
		}

		return new DirectoryInfo( directory );
	}

	private static string ResolveVtxPath( string modelDir, string baseName )
	{
		string[] candidates =
		[
			Path.Combine( modelDir, baseName + ".dx90.vtx" ),
			Path.Combine( modelDir, baseName + ".dx80.vtx" ),
			Path.Combine( modelDir, baseName + ".sw.vtx" ),
			Path.Combine( modelDir, baseName + ".vtx" )
		];

		foreach ( string candidate in candidates )
		{
			if ( File.Exists( candidate ) )
			{
				return candidate;
			}
		}

		return candidates[0];
	}

	private static string ToAssetRelativePath( string outputRoot, string absoluteDirectory )
	{
		string outputRootFull = Path.GetFullPath( outputRoot );
		string absoluteDirectoryFull = Path.GetFullPath( absoluteDirectory );
		if ( !absoluteDirectoryFull.StartsWith( outputRootFull, StringComparison.OrdinalIgnoreCase ) )
		{
			return string.Empty;
		}

		string relative = Path.GetRelativePath( outputRootFull, absoluteDirectoryFull )
			.Replace( '\\', '/' )
			.Trim( '/' );
		return relative;
	}
}
