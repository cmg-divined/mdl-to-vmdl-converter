using System.Windows.Forms;
using System.Globalization;

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
		if ( options.IsBatchMode )
		{
			BatchConversionSummary summary = ConversionRunner.RunBatch(
				options,
				message => Console.WriteLine( message ),
				warning => Console.Error.WriteLine( warning )
			);

			Console.WriteLine( "Batch conversion complete." );
			Console.WriteLine( $"  Output root: {summary.OutputRoot}" );
			Console.WriteLine( $"  Models discovered: {summary.TotalModels}" );
			Console.WriteLine( $"  Succeeded: {summary.Succeeded}" );
			Console.WriteLine( $"  Failed: {summary.Failed}" );
			Console.WriteLine( $"  Total SMD files: {summary.TotalSmdCount}" );
			Console.WriteLine( $"  Total DMX files: {summary.TotalDmxCount}" );
			Console.WriteLine( $"  Total animation files: {summary.TotalAnimationCount}" );
			Console.WriteLine( $"  Total Material remaps: {summary.TotalMaterialRemapCount}" );
			Console.WriteLine( $"  Total Morph channels: {summary.TotalMorphChannelCount}" );

			if ( summary.Failures.Count > 0 )
			{
				Console.WriteLine( "  Failed models:" );
				foreach ( BatchConversionFailure failure in summary.Failures )
				{
					Console.WriteLine( $"    - {failure.MdlPath}" );
					Console.WriteLine( $"      {failure.Error}" );
				}
			}
		}
		else
		{
			ConversionSummary summary = ConversionRunner.Run(
				options,
				message => Console.WriteLine( message ),
				warning => Console.Error.WriteLine( warning )
			);

			Console.WriteLine( "Conversion complete." );
			Console.WriteLine( $"  Model output: {summary.ModelOutputDirectory}" );
			Console.WriteLine( $"  VMDL: {summary.VmdlPath}" );
			Console.WriteLine( $"  SMD files: {summary.SmdCount}" );
			Console.WriteLine( $"  DMX files: {summary.DmxCount}" );
			Console.WriteLine( $"  Anim files: {summary.AnimationCount}" );
			Console.WriteLine( $"  Bodygroups: {summary.BodyGroupCount}" );
			Console.WriteLine( $"  Hitbox sets: {summary.HitboxSetCount}" );
			Console.WriteLine( $"  Physics shapes: {summary.PhysicsShapeCount}" );
			Console.WriteLine( $"  Physics joints: {summary.PhysicsJointCount}" );
			Console.WriteLine( $"  Material remaps: {summary.MaterialRemapCount}" );
			Console.WriteLine( $"  Morph channels: {summary.MorphChannelCount}" );
			}
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

internal enum MaterialOverrideTextureSource
{
	Auto,
	BaseTexture,
	NormalMap,
	ArmTexture,
	MraoTexture,
	PhongExponentTexture,
	EnvMaskTexture,
	ExoNormalTexture
}

internal enum MaterialOverrideChannel
{
	Red,
	Green,
	Blue,
	Alpha
}

internal sealed class ConverterOptions
{
	public string? MdlPath { get; init; }
	public string? BatchRootDirectory { get; init; }
	public bool RecursiveSearch { get; init; } = true;
	public int MaxParallelism { get; init; } = Math.Max( 1, Environment.ProcessorCount );
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
	public bool ExportAnimations { get; init; }
	public bool Verbose { get; init; }
	public MaterialProfileOverride MaterialProfileOverride { get; init; } = MaterialProfileOverride.Auto;
	public MaterialOverrideTextureSource RoughnessOverrideSource { get; init; } = MaterialOverrideTextureSource.Auto;
	public MaterialOverrideChannel RoughnessOverrideChannel { get; init; } = MaterialOverrideChannel.Alpha;
	public bool RoughnessOverrideInvert { get; init; }
	public MaterialOverrideTextureSource MetalnessOverrideSource { get; init; } = MaterialOverrideTextureSource.Auto;
	public MaterialOverrideChannel MetalnessOverrideChannel { get; init; } = MaterialOverrideChannel.Alpha;
	public bool MetalnessOverrideInvert { get; init; }
	public bool MaterialOverrideLevelsEnabled { get; init; }
	public float MaterialOverrideInputMin { get; init; } = 0f;
	public float MaterialOverrideInputMax { get; init; } = 1f;
	public float MaterialOverrideGamma { get; init; } = 1f;
	public float MaterialOverrideOutputMin { get; init; } = 0f;
	public float MaterialOverrideOutputMax { get; init; } = 1f;
	public bool IsBatchMode => !string.IsNullOrWhiteSpace( BatchRootDirectory );

	public static ConverterOptions Parse( string[] args )
	{
		if ( args.Length == 0 )
		{
			throw new ArgumentException( "Missing arguments." );
		}

		string? mdl = null;
		string? batchRoot = null;
		string? vvd = null;
		string? vtx = null;
		string? phy = null;
		string output = string.Empty;
		string vmdlName = string.Empty;
		string? gmodRoot = null;
		string? shaderSource = null;
		bool preservePath = true;
		bool recursiveSearch = true;
		bool convertMaterials = true;
		bool copyShaders = true;
		bool exportAnimations = false;
		bool verbose = false;
		int maxParallelism = Math.Max( 1, Environment.ProcessorCount );
		MaterialProfileOverride profileOverride = MaterialProfileOverride.Auto;
		MaterialOverrideTextureSource roughnessOverrideSource = MaterialOverrideTextureSource.Auto;
		MaterialOverrideChannel roughnessOverrideChannel = MaterialOverrideChannel.Alpha;
		bool roughnessOverrideInvert = false;
		MaterialOverrideTextureSource metalnessOverrideSource = MaterialOverrideTextureSource.Auto;
		MaterialOverrideChannel metalnessOverrideChannel = MaterialOverrideChannel.Alpha;
		bool metalnessOverrideInvert = false;
		bool materialOverrideLevelsEnabled = false;
		float materialOverrideInputMin = 0f;
		float materialOverrideInputMax = 1f;
		float materialOverrideGamma = 1f;
		float materialOverrideOutputMin = 0f;
		float materialOverrideOutputMax = 1f;

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

			if ( string.Equals( arg, "--recursive", StringComparison.OrdinalIgnoreCase ) )
			{
				recursiveSearch = true;
				continue;
			}

			if ( string.Equals( arg, "--no-recursive", StringComparison.OrdinalIgnoreCase ) )
			{
				recursiveSearch = false;
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

			if ( string.Equals( arg, "--animations", StringComparison.OrdinalIgnoreCase ) )
			{
				exportAnimations = true;
				continue;
			}

			if ( string.Equals( arg, "--no-animations", StringComparison.OrdinalIgnoreCase ) )
			{
				exportAnimations = false;
				continue;
			}

			if ( string.Equals( arg, "--override-levels", StringComparison.OrdinalIgnoreCase ) )
			{
				materialOverrideLevelsEnabled = true;
				continue;
			}

			if ( string.Equals( arg, "--no-override-levels", StringComparison.OrdinalIgnoreCase ) )
			{
				materialOverrideLevelsEnabled = false;
				continue;
			}

			if ( string.Equals( arg, "--rough-invert", StringComparison.OrdinalIgnoreCase ) )
			{
				roughnessOverrideInvert = true;
				continue;
			}

			if ( string.Equals( arg, "--no-rough-invert", StringComparison.OrdinalIgnoreCase ) )
			{
				roughnessOverrideInvert = false;
				continue;
			}

			if ( string.Equals( arg, "--metal-invert", StringComparison.OrdinalIgnoreCase ) )
			{
				metalnessOverrideInvert = true;
				continue;
			}

			if ( string.Equals( arg, "--no-metal-invert", StringComparison.OrdinalIgnoreCase ) )
			{
				metalnessOverrideInvert = false;
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
					case "--batch": batchRoot = value; break;
					case "--vvd": vvd = value; break;
					case "--vtx": vtx = value; break;
					case "--phy": phy = value; break;
					case "--out": output = value; break;
					case "--vmdl": vmdlName = value; break;
					case "--gmod-root": gmodRoot = value; break;
					case "--shader-src": shaderSource = value; break;
					case "--threads":
						if ( !int.TryParse( value, out maxParallelism ) || maxParallelism <= 0 )
						{
							throw new ArgumentException( $"Invalid thread count: {value}" );
						}
						break;
					case "--profile": profileOverride = ParseProfileOverride( value ); break;
					case "--rough-source": roughnessOverrideSource = ParseOverrideTextureSource( value ); break;
					case "--rough-channel": roughnessOverrideChannel = ParseOverrideChannel( value ); break;
					case "--metal-source": metalnessOverrideSource = ParseOverrideTextureSource( value ); break;
					case "--metal-channel": metalnessOverrideChannel = ParseOverrideChannel( value ); break;
					case "--override-in-min": materialOverrideInputMin = ParseUnitFloat( value, "--override-in-min" ); break;
					case "--override-in-max": materialOverrideInputMax = ParseUnitFloat( value, "--override-in-max" ); break;
					case "--override-gamma": materialOverrideGamma = ParsePositiveFloat( value, "--override-gamma" ); break;
					case "--override-out-min": materialOverrideOutputMin = ParseUnitFloat( value, "--override-out-min" ); break;
					case "--override-out-max": materialOverrideOutputMax = ParseUnitFloat( value, "--override-out-max" ); break;
					default: throw new ArgumentException( $"Unknown option: {arg}" );
				}
			}
			else if ( mdl is null && batchRoot is null )
			{
				mdl = arg;
			}
			else
			{
				throw new ArgumentException( $"Unexpected argument: {arg}" );
			}
		}

		if ( string.IsNullOrWhiteSpace( mdl ) && string.IsNullOrWhiteSpace( batchRoot ) )
		{
			throw new ArgumentException( "You must pass an MDL file path or a batch folder path." );
		}

		if ( !string.IsNullOrWhiteSpace( mdl ) && string.IsNullOrWhiteSpace( batchRoot ) && Directory.Exists( mdl ) )
		{
			batchRoot = mdl;
			mdl = null;
		}

		if ( !string.IsNullOrWhiteSpace( mdl ) && !string.IsNullOrWhiteSpace( batchRoot ) )
		{
			throw new ArgumentException( "Use either --mdl or --batch, not both." );
		}

		if ( !string.IsNullOrWhiteSpace( batchRoot ) )
		{
			if ( !string.IsNullOrWhiteSpace( vvd ) || !string.IsNullOrWhiteSpace( vtx ) || !string.IsNullOrWhiteSpace( phy ) )
			{
				throw new ArgumentException( "--vvd/--vtx/--phy are only valid for single-model conversion." );
			}
		}

		if ( materialOverrideInputMin > materialOverrideInputMax )
		{
			throw new ArgumentException( "--override-in-min cannot be greater than --override-in-max." );
		}

		if ( materialOverrideOutputMin > materialOverrideOutputMax )
		{
			throw new ArgumentException( "--override-out-min cannot be greater than --override-out-max." );
		}

		return new ConverterOptions
		{
			MdlPath = string.IsNullOrWhiteSpace( mdl ) ? null : Path.GetFullPath( mdl ),
			BatchRootDirectory = string.IsNullOrWhiteSpace( batchRoot ) ? null : Path.GetFullPath( batchRoot ),
			RecursiveSearch = recursiveSearch,
			MaxParallelism = maxParallelism,
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
			ExportAnimations = exportAnimations,
			MaterialProfileOverride = profileOverride,
			Verbose = verbose,
			RoughnessOverrideSource = roughnessOverrideSource,
			RoughnessOverrideChannel = roughnessOverrideChannel,
			RoughnessOverrideInvert = roughnessOverrideInvert,
			MetalnessOverrideSource = metalnessOverrideSource,
			MetalnessOverrideChannel = metalnessOverrideChannel,
			MetalnessOverrideInvert = metalnessOverrideInvert,
			MaterialOverrideLevelsEnabled = materialOverrideLevelsEnabled,
			MaterialOverrideInputMin = materialOverrideInputMin,
			MaterialOverrideInputMax = materialOverrideInputMax,
			MaterialOverrideGamma = materialOverrideGamma,
			MaterialOverrideOutputMin = materialOverrideOutputMin,
			MaterialOverrideOutputMax = materialOverrideOutputMax
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

	private static MaterialOverrideTextureSource ParseOverrideTextureSource( string raw )
	{
		if ( string.IsNullOrWhiteSpace( raw ) )
		{
			return MaterialOverrideTextureSource.Auto;
		}

		string normalized = raw.Trim().Replace( "-", string.Empty, StringComparison.Ordinal ).Replace( "_", string.Empty, StringComparison.Ordinal ).ToLowerInvariant();
		return normalized switch
		{
			"auto" => MaterialOverrideTextureSource.Auto,
			"base" or "basetexture" => MaterialOverrideTextureSource.BaseTexture,
			"normal" or "normalmap" or "bump" or "bumpmap" => MaterialOverrideTextureSource.NormalMap,
			"arm" or "armtexture" => MaterialOverrideTextureSource.ArmTexture,
			"mrao" or "mraotexture" => MaterialOverrideTextureSource.MraoTexture,
			"exponent" or "phongexponent" or "phongexponenttexture" => MaterialOverrideTextureSource.PhongExponentTexture,
			"envmask" or "envmapmask" => MaterialOverrideTextureSource.EnvMaskTexture,
			"exonormal" or "exonormaltexture" => MaterialOverrideTextureSource.ExoNormalTexture,
			_ => throw new ArgumentException( $"Unknown override source: {raw}" )
		};
	}

	private static MaterialOverrideChannel ParseOverrideChannel( string raw )
	{
		if ( string.IsNullOrWhiteSpace( raw ) )
		{
			return MaterialOverrideChannel.Alpha;
		}

		string normalized = raw.Trim().ToLowerInvariant();
		return normalized switch
		{
			"r" or "red" => MaterialOverrideChannel.Red,
			"g" or "green" => MaterialOverrideChannel.Green,
			"b" or "blue" => MaterialOverrideChannel.Blue,
			"a" or "alpha" => MaterialOverrideChannel.Alpha,
			_ => throw new ArgumentException( $"Unknown override channel: {raw}" )
		};
	}

	private static float ParseUnitFloat( string raw, string optionName )
	{
		if ( !float.TryParse( raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed ) )
		{
			throw new ArgumentException( $"Invalid value for {optionName}: {raw}" );
		}

		if ( parsed < 0f || parsed > 1f )
		{
			throw new ArgumentException( $"{optionName} must be in range [0,1]." );
		}

		return parsed;
	}

	private static float ParsePositiveFloat( string raw, string optionName )
	{
		if ( !float.TryParse( raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed ) )
		{
			throw new ArgumentException( $"Invalid value for {optionName}: {raw}" );
		}

		if ( parsed <= 0f )
		{
			throw new ArgumentException( $"{optionName} must be > 0." );
		}

		return parsed;
	}

	public static void PrintUsage()
	{
		Console.WriteLine( "MDL to VMDL converter" );
		Console.WriteLine( "Usage:" );
		Console.WriteLine( "  MdlToVmdlConverter <model.mdl> [--out <dir>] [--vmdl <name>]" );
		Console.WriteLine( "  MdlToVmdlConverter --mdl <model.mdl> [--vvd <file>] [--vtx <file>] [--phy <file>] [--out <dir>]" );
		Console.WriteLine( "  MdlToVmdlConverter --batch <modelsDir> [--recursive] [--threads <n>] [--out <dir>]" );
		Console.WriteLine();
		Console.WriteLine( "Options:" );
		Console.WriteLine( "  --gmod-root <dir>      Garry's Mod garrysmod folder (contains models/materials)" );
		Console.WriteLine( "  --batch <dir>          Convert all .mdl files under this folder" );
		Console.WriteLine( "  --recursive            Include subfolders in batch mode (default)" );
		Console.WriteLine( "  --no-recursive         Only convert .mdl files in the batch root folder" );
		Console.WriteLine( "  --threads <n>          Parallel workers in batch mode (default: CPU count)" );
		Console.WriteLine( "  --preserve-path        Export to models/<relative model path> under --out (default)" );
		Console.WriteLine( "  --no-preserve-path     Export directly into --out" );
		Console.WriteLine( "  --materials            Convert VMT/VTF to VMAT/TGA (default)" );
		Console.WriteLine( "  --no-materials         Skip material conversion" );
		Console.WriteLine( "  --profile <name>       auto|source|exo|gpbr|mwb|bft|madivan18" );
		Console.WriteLine( "  --copy-shaders         Copy custom gmod shaders to output root (default)" );
		Console.WriteLine( "  --no-copy-shaders      Do not copy shaders" );
		Console.WriteLine( "  --animations           Export sequence animations as SMD + add AnimationList" );
		Console.WriteLine( "  --no-animations        Skip animation export (default)" );
		Console.WriteLine( "  --shader-src <dir>     Override shader source directory" );
		Console.WriteLine( "  --rough-source <name>  auto|base|normal|arm|mrao|phongexponent|envmask|exonormal" );
		Console.WriteLine( "  --rough-channel <c>    r|g|b|a (used when rough-source != auto)" );
		Console.WriteLine( "  --rough-invert         Invert roughness override channel" );
		Console.WriteLine( "  --no-rough-invert      Do not invert roughness override channel (default)" );
		Console.WriteLine( "  --metal-source <name>  auto|base|normal|arm|mrao|phongexponent|envmask|exonormal" );
		Console.WriteLine( "  --metal-channel <c>    r|g|b|a (used when metal-source != auto)" );
		Console.WriteLine( "  --metal-invert         Invert metalness override channel" );
		Console.WriteLine( "  --no-metal-invert      Do not invert metalness override channel (default)" );
		Console.WriteLine( "  --override-levels      Enable override levels/curves remap" );
		Console.WriteLine( "  --no-override-levels   Disable override levels/curves remap (default)" );
		Console.WriteLine( "  --override-in-min <v>  Levels input min [0..1], default 0" );
		Console.WriteLine( "  --override-in-max <v>  Levels input max [0..1], default 1" );
		Console.WriteLine( "  --override-gamma <v>   Levels gamma > 0, default 1" );
		Console.WriteLine( "  --override-out-min <v> Levels output min [0..1], default 0" );
		Console.WriteLine( "  --override-out-max <v> Levels output max [0..1], default 1" );
		Console.WriteLine( "  --verbose              Verbose parser logging" );
		Console.WriteLine( "  --gui                  Open GUI" );
	}
}
