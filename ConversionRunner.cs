using GModMount.Source;

internal sealed class ConversionSummary
{
	public required string ModelOutputDirectory { get; init; }
	public required string VmdlPath { get; init; }
	public int SmdCount { get; init; }
	public int BodyGroupCount { get; init; }
	public int HitboxSetCount { get; init; }
	public int PhysicsShapeCount { get; init; }
	public int PhysicsJointCount { get; init; }
	public int MaterialRemapCount { get; init; }
}

internal static class ConversionRunner
{
	public static ConversionSummary Run( ConverterOptions options, Action<string>? info = null, Action<string>? warn = null )
	{
		info ??= _ => { };
		warn ??= _ => { };

		Log.Verbose = options.Verbose;

		if ( !File.Exists( options.MdlPath ) )
		{
			throw new FileNotFoundException( $"MDL not found: {options.MdlPath}" );
		}

		string modelBaseName = Path.GetFileNameWithoutExtension( options.MdlPath );
		string modelDir = Path.GetDirectoryName( options.MdlPath ) ?? Directory.GetCurrentDirectory();

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

		string? gmodRoot = ResolveGmodRoot( options.GmodRootDirectory, options.MdlPath );
		string outputRoot = string.IsNullOrWhiteSpace( options.OutputDirectory )
			? Path.Combine( modelDir, modelBaseName + "_converted" )
			: options.OutputDirectory;
		string modelOutputDirectory = ResolveModelOutputDirectory( options, outputRoot, gmodRoot, options.MdlPath );

		Directory.CreateDirectory( outputRoot );
		Directory.CreateDirectory( modelOutputDirectory );

		info( "Loading Source model..." );
		SourceModel sourceModel = SourceModel.Load(
			options.MdlPath,
			vvdPath,
			vtxPath,
			string.IsNullOrEmpty( phyPath ) ? null! : phyPath
		);

		BuildContext buildContext = ConverterPipeline.Build( sourceModel, modelBaseName, modelOutputDirectory );
		buildContext.ModelAssetDirectory = ToAssetRelativePath( outputRoot, modelOutputDirectory );

		foreach ( MeshExport mesh in buildContext.Meshes )
		{
			SmdWriter.WriteMesh( Path.Combine( modelOutputDirectory, mesh.FileName ), buildContext, mesh );
		}

		if ( options.ConvertMaterials )
		{
			MaterialConversionResult materialResult = MaterialPipeline.Convert(
				buildContext,
				options,
				outputRoot,
				gmodRoot,
				options.MdlPath,
				info,
				warn
			);

			buildContext.MaterialRemaps.AddRange( materialResult.Remaps );

			if ( options.CopyShaders )
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
			SmdCount = buildContext.Meshes.Count,
			BodyGroupCount = buildContext.BodyGroups.Count,
			HitboxSetCount = buildContext.HitboxSets.Count,
			PhysicsShapeCount = buildContext.PhysicsShapes.Count,
			PhysicsJointCount = buildContext.PhysicsJoints.Count,
			MaterialRemapCount = buildContext.MaterialRemaps.Count
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

	internal static string? ResolveGmodRoot( string? explicitRoot, string mdlPath )
	{
		if ( !string.IsNullOrWhiteSpace( explicitRoot ) )
		{
			string normalized = Path.GetFullPath( explicitRoot );
			string directModels = Path.Combine( normalized, "models" );
			string directMaterials = Path.Combine( normalized, "materials" );
			if ( Directory.Exists( directModels ) && Directory.Exists( directMaterials ) )
			{
				return normalized;
			}

			string nested = Path.Combine( normalized, "garrysmod" );
			string nestedModels = Path.Combine( nested, "models" );
			string nestedMaterials = Path.Combine( nested, "materials" );
			if ( Directory.Exists( nestedModels ) && Directory.Exists( nestedMaterials ) )
			{
				return nested;
			}
		}

		DirectoryInfo? dir = new DirectoryInfo( Path.GetDirectoryName( mdlPath ) ?? string.Empty );
		while ( dir is not null )
		{
			if ( string.Equals( dir.Name, "models", StringComparison.OrdinalIgnoreCase )
				&& dir.Parent is not null
				&& string.Equals( dir.Parent.Name, "garrysmod", StringComparison.OrdinalIgnoreCase ) )
			{
				return dir.Parent.FullName;
			}

			dir = dir.Parent;
		}

		return null;
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
