using System.Windows.Forms;

internal static class Program
{
	[STAThread]
	private static int Main( string[] args )
	{
		if ( args.Length == 0 || args.Any( IsGuiArg ) )
		{
			RunGui();
			return 0;
		}

		try
		{
			ConverterOptions options = ConverterOptions.Parse( args );
			ConversionSummary summary = ConversionRunner.Run(
				options,
				message => Console.WriteLine( message ),
				warning => Console.Error.WriteLine( warning )
			);

			Console.WriteLine( "Conversion complete." );
			Console.WriteLine( $"  Model output: {summary.ModelOutputDirectory}" );
			Console.WriteLine( $"  VMDL: {summary.VmdlPath}" );
			Console.WriteLine( $"  SMD files: {summary.SmdCount}" );
			Console.WriteLine( $"  Bodygroups: {summary.BodyGroupCount}" );
			Console.WriteLine( $"  Hitbox sets: {summary.HitboxSetCount}" );
			Console.WriteLine( $"  Physics shapes: {summary.PhysicsShapeCount}" );
			Console.WriteLine( $"  Physics joints: {summary.PhysicsJointCount}" );
			Console.WriteLine( $"  Material remaps: {summary.MaterialRemapCount}" );
			return 0;
		}
		catch ( ArgumentException ex )
		{
			Console.Error.WriteLine( ex.Message );
			Console.Error.WriteLine();
			ConverterOptions.PrintUsage();
			return 2;
		}
		catch ( Exception ex )
		{
			Console.Error.WriteLine( "Conversion failed:" );
			Console.Error.WriteLine( ex.ToString() );
			return 1;
		}
	}

	private static bool IsGuiArg( string arg )
	{
		return string.Equals( arg, "--gui", StringComparison.OrdinalIgnoreCase )
			|| string.Equals( arg, "-g", StringComparison.OrdinalIgnoreCase );
	}

	private static void RunGui()
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault( false );
		Application.Run( new MainForm() );
	}
}

internal enum MaterialProfileOverride
{
	Auto,
	SourceEngine,
	ExoPbr,
	Gpbr,
	MwbPbr,
	BftPseudoPbr,
	MadIvan18
}

internal sealed class ConverterOptions
{
	public required string MdlPath { get; init; }
	public string? VvdPath { get; init; }
	public string? VtxPath { get; init; }
	public string? PhyPath { get; init; }
	public required string OutputDirectory { get; init; }
	public string VmdlFileName { get; init; } = string.Empty;
	public string? GmodRootDirectory { get; init; }
	public string? ShaderSourceDirectory { get; init; }
	public bool PreserveModelRelativePath { get; init; } = true;
	public bool ConvertMaterials { get; init; } = true;
	public bool CopyShaders { get; init; } = true;
	public bool Verbose { get; init; }
	public MaterialProfileOverride MaterialProfileOverride { get; init; } = MaterialProfileOverride.Auto;

	public static ConverterOptions Parse( string[] args )
	{
		if ( args.Length == 0 )
		{
			throw new ArgumentException( "Missing arguments." );
		}

		string? mdl = null;
		string? vvd = null;
		string? vtx = null;
		string? phy = null;
		string output = string.Empty;
		string vmdlName = string.Empty;
		string? gmodRoot = null;
		string? shaderSource = null;
		bool preservePath = true;
		bool convertMaterials = true;
		bool copyShaders = true;
		bool verbose = false;
		MaterialProfileOverride profileOverride = MaterialProfileOverride.Auto;

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

			if ( string.Equals( arg, "--preserve-path", StringComparison.OrdinalIgnoreCase ) )
			{
				preservePath = true;
				continue;
			}

			if ( string.Equals( arg, "--no-preserve-path", StringComparison.OrdinalIgnoreCase ) )
			{
				preservePath = false;
				continue;
			}

			if ( string.Equals( arg, "--materials", StringComparison.OrdinalIgnoreCase ) )
			{
				convertMaterials = true;
				continue;
			}

			if ( string.Equals( arg, "--no-materials", StringComparison.OrdinalIgnoreCase ) )
			{
				convertMaterials = false;
				continue;
			}

			if ( string.Equals( arg, "--copy-shaders", StringComparison.OrdinalIgnoreCase ) )
			{
				copyShaders = true;
				continue;
			}

			if ( string.Equals( arg, "--no-copy-shaders", StringComparison.OrdinalIgnoreCase ) )
			{
				copyShaders = false;
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
					case "--gmod-root": gmodRoot = value; break;
					case "--shader-src": shaderSource = value; break;
					case "--profile": profileOverride = ParseProfileOverride( value ); break;
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
			GmodRootDirectory = string.IsNullOrWhiteSpace( gmodRoot ) ? null : Path.GetFullPath( gmodRoot ),
			ShaderSourceDirectory = string.IsNullOrWhiteSpace( shaderSource ) ? null : Path.GetFullPath( shaderSource ),
			PreserveModelRelativePath = preservePath,
			ConvertMaterials = convertMaterials,
			CopyShaders = copyShaders,
			MaterialProfileOverride = profileOverride,
			Verbose = verbose
		};
	}

	private static MaterialProfileOverride ParseProfileOverride( string raw )
	{
		if ( string.IsNullOrWhiteSpace( raw ) )
		{
			return MaterialProfileOverride.Auto;
		}

		string normalized = raw.Trim().ToLowerInvariant();
		return normalized switch
		{
			"auto" => MaterialProfileOverride.Auto,
			"source" or "sourceengine" => MaterialProfileOverride.SourceEngine,
			"exo" or "exopbr" => MaterialProfileOverride.ExoPbr,
			"gpbr" => MaterialProfileOverride.Gpbr,
			"mwb" or "mwbpbr" => MaterialProfileOverride.MwbPbr,
			"bft" or "bftpseudopbr" => MaterialProfileOverride.BftPseudoPbr,
			"madivan18" or "madivan" => MaterialProfileOverride.MadIvan18,
			_ => throw new ArgumentException( $"Unknown profile override: {raw}" )
		};
	}

	public static void PrintUsage()
	{
		Console.WriteLine( "MDL to VMDL converter" );
		Console.WriteLine( "Usage:" );
		Console.WriteLine( "  MdlToVmdlConverter <model.mdl> [--out <dir>] [--vmdl <name>]" );
		Console.WriteLine( "  MdlToVmdlConverter --mdl <model.mdl> [--vvd <file>] [--vtx <file>] [--phy <file>] [--out <dir>]" );
		Console.WriteLine();
		Console.WriteLine( "Options:" );
		Console.WriteLine( "  --gmod-root <dir>      Garry's Mod garrysmod folder (contains models/materials)" );
		Console.WriteLine( "  --preserve-path        Export to models/<relative model path> under --out (default)" );
		Console.WriteLine( "  --no-preserve-path     Export directly into --out" );
		Console.WriteLine( "  --materials            Convert VMT/VTF to VMAT/TGA (default)" );
		Console.WriteLine( "  --no-materials         Skip material conversion" );
		Console.WriteLine( "  --profile <name>       auto|source|exo|gpbr|mwb|bft|madivan18" );
		Console.WriteLine( "  --copy-shaders         Copy custom gmod shaders to output root (default)" );
		Console.WriteLine( "  --no-copy-shaders      Do not copy shaders" );
		Console.WriteLine( "  --shader-src <dir>     Override shader source directory" );
		Console.WriteLine( "  --verbose              Verbose parser logging" );
		Console.WriteLine( "  --gui                  Open GUI" );
	}
}
