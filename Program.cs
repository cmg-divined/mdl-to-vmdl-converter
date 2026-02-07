using GModMount.Source;

internal static class Program
{
	private static int Main( string[] args )
	{
		try
		{
			ConverterOptions options = ConverterOptions.Parse( args );
			Log.Verbose = options.Verbose;

			if ( !File.Exists( options.MdlPath ) )
			{
				Console.Error.WriteLine( $"MDL not found: {options.MdlPath}" );
				return 2;
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
				Console.Error.WriteLine( $"VVD not found: {vvdPath}" );
				return 2;
			}

			if ( !File.Exists( vtxPath ) )
			{
				Console.Error.WriteLine( $"VTX not found: {vtxPath}" );
				return 2;
			}

			string outputDir = string.IsNullOrWhiteSpace( options.OutputDirectory )
				? Path.Combine( modelDir, modelBaseName + "_converted" )
				: options.OutputDirectory;
			Directory.CreateDirectory( outputDir );

			Console.WriteLine( "Loading Source model..." );
			SourceModel sourceModel = SourceModel.Load(
				options.MdlPath,
				vvdPath,
				vtxPath,
				string.IsNullOrEmpty( phyPath ) ? null! : phyPath
			);

			BuildContext buildContext = ConverterPipeline.Build( sourceModel, modelBaseName, outputDir );

			foreach ( MeshExport mesh in buildContext.Meshes )
			{
				SmdWriter.WriteMesh( Path.Combine( outputDir, mesh.FileName ), buildContext, mesh );
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

			string vmdlPath = Path.Combine( outputDir, vmdlFileName );
			VmdlWriter.Write( vmdlPath, buildContext );

			Console.WriteLine( "Conversion complete." );
			Console.WriteLine( $"  SMD files: {buildContext.Meshes.Count}" );
			Console.WriteLine( $"  VMDL file: {vmdlPath}" );
			Console.WriteLine( $"  Bodygroups: {buildContext.BodyGroups.Count}" );
			Console.WriteLine( $"  Hitbox sets: {buildContext.HitboxSets.Count}" );
			Console.WriteLine( $"  Physics shapes: {buildContext.PhysicsShapes.Count}" );
			Console.WriteLine( $"  Physics joints: {buildContext.PhysicsJoints.Count}" );
			return 0;
		}
		catch ( Exception ex )
		{
			Console.Error.WriteLine( "Conversion failed:" );
			Console.Error.WriteLine( ex.ToString() );
			return 1;
		}
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
}

internal sealed class ConverterOptions
{
	public required string MdlPath { get; init; }
	public string? VvdPath { get; init; }
	public string? VtxPath { get; init; }
	public string? PhyPath { get; init; }
	public required string OutputDirectory { get; init; }
	public string VmdlFileName { get; init; } = string.Empty;
	public bool Verbose { get; init; }

	public static ConverterOptions Parse( string[] args )
	{
		if ( args.Length == 0 )
		{
			PrintUsage();
			throw new ArgumentException( "Missing arguments." );
		}

		string? mdl = null;
		string? vvd = null;
		string? vtx = null;
		string? phy = null;
		string output = string.Empty;
		string vmdlName = string.Empty;
		bool verbose = false;

		for ( int i = 0; i < args.Length; i++ )
		{
			string arg = args[i];
			if ( string.Equals( arg, "--help", StringComparison.OrdinalIgnoreCase ) || string.Equals( arg, "-h", StringComparison.OrdinalIgnoreCase ) )
			{
				PrintUsage();
				Environment.Exit( 0 );
			}

			if ( string.Equals( arg, "--verbose", StringComparison.OrdinalIgnoreCase ) || string.Equals( arg, "-v", StringComparison.OrdinalIgnoreCase ) )
			{
				verbose = true;
				continue;
			}

			if ( arg.StartsWith( "--", StringComparison.Ordinal ) )
			{
				if ( i + 1 >= args.Length )
				{
					throw new ArgumentException( $"Missing value for {arg}" );
				}

				string value = args[i + 1];
				i++;
				switch ( arg.ToLowerInvariant() )
				{
					case "--mdl": mdl = value; break;
					case "--vvd": vvd = value; break;
					case "--vtx": vtx = value; break;
					case "--phy": phy = value; break;
					case "--out": output = value; break;
					case "--vmdl": vmdlName = value; break;
					default: throw new ArgumentException( $"Unknown option: {arg}" );
				}
			}
			else if ( mdl is null )
			{
				mdl = arg;
			}
			else
			{
				throw new ArgumentException( $"Unexpected argument: {arg}" );
			}
		}

		if ( string.IsNullOrWhiteSpace( mdl ) )
		{
			throw new ArgumentException( "You must pass an MDL path." );
		}

		return new ConverterOptions
		{
			MdlPath = Path.GetFullPath( mdl ),
			VvdPath = string.IsNullOrWhiteSpace( vvd ) ? null : Path.GetFullPath( vvd ),
			VtxPath = string.IsNullOrWhiteSpace( vtx ) ? null : Path.GetFullPath( vtx ),
			PhyPath = string.IsNullOrWhiteSpace( phy ) ? null : Path.GetFullPath( phy ),
			OutputDirectory = string.IsNullOrWhiteSpace( output ) ? string.Empty : Path.GetFullPath( output ),
			VmdlFileName = vmdlName,
			Verbose = verbose
		};
	}

	private static void PrintUsage()
	{
		Console.WriteLine( "MDL to VMDL converter" );
		Console.WriteLine( "Usage:" );
		Console.WriteLine( "  MdlToVmdlConverter <model.mdl> [--out <dir>] [--vmdl <name>] [--verbose]" );
		Console.WriteLine( "  MdlToVmdlConverter --mdl <model.mdl> [--vvd <file>] [--vtx <file>] [--phy <file>] [--out <dir>]" );
	}
}
