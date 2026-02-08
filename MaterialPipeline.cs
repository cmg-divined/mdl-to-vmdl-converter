using GModMount.Source;
using System.Collections.Concurrent;

internal sealed class MaterialConversionResult
{
	public List<MaterialRemapExport> Remaps { get; } = new();
	public int ConvertedCount { get; set; }
	public int MissingCount { get; set; }
}

internal static class MaterialPipeline
{
	private static readonly ConcurrentDictionary<string, object> FileWriteLocks = new( StringComparer.OrdinalIgnoreCase );

	private sealed class RgbaImage
	{
		public required int Width { get; init; }
		public required int Height { get; init; }
		public required byte[] Pixels { get; init; }
	}

	public static MaterialConversionResult Convert(
		BuildContext context,
		ConverterOptions options,
		string outputRoot,
		string? gmodRoot,
		string mdlPath,
		Action<string>? info = null,
		Action<string>? warn = null )
	{
		info ??= _ => { };
		warn ??= _ => { };

		var result = new MaterialConversionResult();
		if ( context.SourceMaterials.Count == 0 )
		{
			return result;
		}

		if ( string.IsNullOrWhiteSpace( gmodRoot ) )
		{
			warn( "[warn] GMod root could not be resolved. Material conversion skipped." );
			return result;
		}

		string materialsRoot = Path.Combine( gmodRoot, "materials" );
		if ( !Directory.Exists( materialsRoot ) )
		{
			warn( $"[warn] Materials folder not found: {materialsRoot}. Material conversion skipped." );
			return result;
		}

		string modelRelativeDirectory = string.Empty;
		if ( !ConversionRunner.TryGetModelRelativeDirectory( mdlPath, gmodRoot, out modelRelativeDirectory ) )
		{
			modelRelativeDirectory = string.Empty;
		}

		var seenRemapFrom = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		PbrFormat? forcedFormat = MapProfileOverride( options.MaterialProfileOverride );

		foreach ( string materialToken in context.SourceMaterials.OrderBy( v => v, StringComparer.OrdinalIgnoreCase ) )
		{
			string fromReference = ToMaterialRemapFrom( materialToken );
			if ( !seenRemapFrom.Add( fromReference ) )
			{
				continue;
			}

			string? sourceMaterialPath = ResolveSourceMaterialPath(
				materialToken,
				context.SourceModel.Mdl.MaterialPaths,
				modelRelativeDirectory,
				materialsRoot
			);

			if ( string.IsNullOrWhiteSpace( sourceMaterialPath ) )
			{
				sourceMaterialPath = BuildFallbackMaterialPath( materialToken, modelRelativeDirectory );
				warn( $"[warn] Missing VMT for '{materialToken}', writing fallback VMAT at '{sourceMaterialPath}'" );
				WriteFallbackMaterial( outputRoot, sourceMaterialPath, result, fromReference );
				continue;
			}

			string vmtFilePath = Path.Combine( materialsRoot, sourceMaterialPath.Replace( '/', Path.DirectorySeparatorChar ) + ".vmt" );
			if ( !File.Exists( vmtFilePath ) )
			{
				sourceMaterialPath = BuildFallbackMaterialPath( materialToken, modelRelativeDirectory );
				warn( $"[warn] VMT path resolved but file missing for '{materialToken}', writing fallback VMAT" );
				WriteFallbackMaterial( outputRoot, sourceMaterialPath, result, fromReference );
				continue;
			}

			try
			{
				byte[] vmtBytes = File.ReadAllBytes( vmtFilePath );
				VmtFile vmt = VmtFile.Load( vmtBytes );
				ExtractedPbrProperties props = PseudoPbrFormats.ExtractProperties( vmt, sourceMaterialPath, forcedFormat );

				if ( props.IsEyeShader )
				{
					WriteEyeMaterial( outputRoot, materialsRoot, sourceMaterialPath, props, result, fromReference );
				}
				else
				{
					WritePbrMaterial( outputRoot, materialsRoot, sourceMaterialPath, props, result, fromReference, options, warn );
				}

				result.ConvertedCount++;
				info( $"Material converted: {sourceMaterialPath} ({props.Format})" );
			}
			catch ( Exception ex )
			{
				warn( $"[warn] Material conversion failed for '{materialToken}': {ex.Message}" );
				WriteFallbackMaterial( outputRoot, BuildFallbackMaterialPath( materialToken, modelRelativeDirectory ), result, fromReference );
			}
		}

		info( $"Materials converted: {result.ConvertedCount}, fallbacks: {result.MissingCount}" );
		return result;
	}

	private static void WriteEyeMaterial(
		string outputRoot,
		string materialsRoot,
		string sourceMaterialPath,
		ExtractedPbrProperties props,
		MaterialConversionResult result,
		string fromReference )
	{
		RgbaImage? irisRaw = LoadTexture( materialsRoot, props.IrisTexturePath, forceOpaqueAlpha: false )
			?? LoadTexture( materialsRoot, props.BaseTexturePath, forceOpaqueAlpha: false )
			?? CreateSolidColor( 1, 1, 255, 255, 255, 255 );
		RgbaImage iris = irisRaw;

		RgbaImage? corneaRaw = LoadTexture( materialsRoot, props.CorneaTexturePath, forceOpaqueAlpha: false )
			?? CreateSolidColor( 1, 1, 128, 128, 255, 255 );
		RgbaImage cornea = corneaRaw;

		string irisPath = WriteTextureVariant( outputRoot, sourceMaterialPath, "iris", iris );
		string corneaPath = WriteTextureVariant( outputRoot, sourceMaterialPath, "cornea", cornea );

		string vmatRelativePath = $"materials/{sourceMaterialPath}.vmat".Replace( '\\', '/' );
		string vmatAbsolutePath = Path.Combine( outputRoot, vmatRelativePath.Replace( '/', Path.DirectorySeparatorChar ) );

		WriteEyeVmat( vmatAbsolutePath, irisPath, corneaPath, props );
		result.Remaps.Add( new MaterialRemapExport
		{
			From = fromReference,
			To = vmatRelativePath
		} );
	}

	private static void WritePbrMaterial(
		string outputRoot,
		string materialsRoot,
		string sourceMaterialPath,
		ExtractedPbrProperties props,
		MaterialConversionResult result,
		string fromReference,
		ConverterOptions options,
		Action<string> warn )
	{
		RgbaImage? baseRaw = LoadTexture( materialsRoot, props.BaseTexturePath, forceOpaqueAlpha: false );
		bool keepColorAlpha = props.IsAlphaTest || props.IsTranslucent || props.IsAdditive;
		RgbaImage color = baseRaw is null
			? CreateSolidColor( 1, 1, 255, 255, 255, 255 )
			: (keepColorAlpha ? CloneImage( baseRaw ) : CloneImageWithAlpha( baseRaw, 255 ));

		RgbaImage? bumpRaw = LoadTexture( materialsRoot, props.BumpMapPath, forceOpaqueAlpha: false );
		RgbaImage normal = bumpRaw is null
			? CreateSolidColor( color.Width, color.Height, 128, 128, 255, 255 )
			: CloneImageWithAlpha( bumpRaw, 255 );
		RgbaImage? armRaw = null;
		RgbaImage? mraoRaw = null;
		RgbaImage? exponentRaw = null;
		RgbaImage? envMaskRaw = null;
		RgbaImage? exoNormalRaw = null;

		byte defaultRough = (byte)Math.Clamp( props.Roughness * 255f, 0f, 255f );
		byte defaultMetal = (byte)Math.Clamp( props.Metallic * 255f, 0f, 255f );
		RgbaImage roughness = CreateGray( color.Width, color.Height, defaultRough );
		RgbaImage metalness = CreateGray( color.Width, color.Height, defaultMetal );
		RgbaImage ao = CreateGray( color.Width, color.Height, 255 );

		switch ( props.Format )
		{
			case PbrFormat.ExoPBR:
				{
					armRaw = LoadTexture( materialsRoot, props.ArmTexturePath, forceOpaqueAlpha: false );
					if ( armRaw is not null )
					{
						roughness = CreateFromRgba( PbrTextureGenerator.ExtractRoughnessFromArm( armRaw.Pixels, armRaw.Width, armRaw.Height ), armRaw.Width, armRaw.Height );
						metalness = CreateFromRgba( PbrTextureGenerator.ExtractMetallicFromArm( armRaw.Pixels, armRaw.Width, armRaw.Height ), armRaw.Width, armRaw.Height );
						ao = CreateFromRgba( PbrTextureGenerator.ExtractAoFromArm( armRaw.Pixels, armRaw.Width, armRaw.Height ), armRaw.Width, armRaw.Height );
					}

					exoNormalRaw = LoadTexture( materialsRoot, props.ExoNormalPath, forceOpaqueAlpha: false );
					if ( exoNormalRaw is not null )
					{
						normal = CreateFromRgba( PbrTextureGenerator.FlipNormalMapGreen( exoNormalRaw.Pixels, exoNormalRaw.Width, exoNormalRaw.Height ), exoNormalRaw.Width, exoNormalRaw.Height );
						normal = CloneImageWithAlpha( normal, 255 );
					}
					break;
				}

			case PbrFormat.GPBR:
				{
					mraoRaw = LoadTexture( materialsRoot, props.MraoTexturePath, forceOpaqueAlpha: false );
					if ( mraoRaw is not null )
					{
						roughness = CreateFromRgba( PbrTextureGenerator.ExtractRoughnessFromMrao( mraoRaw.Pixels, mraoRaw.Width, mraoRaw.Height ), mraoRaw.Width, mraoRaw.Height );
						metalness = CreateFromRgba( PbrTextureGenerator.ExtractMetallicFromMrao( mraoRaw.Pixels, mraoRaw.Width, mraoRaw.Height ), mraoRaw.Width, mraoRaw.Height );
						ao = CreateFromRgba( PbrTextureGenerator.ExtractAoFromMrao( mraoRaw.Pixels, mraoRaw.Width, mraoRaw.Height ), mraoRaw.Width, mraoRaw.Height );
					}
					break;
				}

			case PbrFormat.MWBPBR:
				{
					if ( baseRaw is not null )
					{
						byte[]? metallicFromAlpha = PbrTextureGenerator.ExtractMetallicFromAlpha( baseRaw.Pixels, baseRaw.Width, baseRaw.Height );
						if ( metallicFromAlpha is not null )
						{
							metalness = CreateFromRgba( metallicFromAlpha, baseRaw.Width, baseRaw.Height );
						}
					}

					if ( bumpRaw is not null )
					{
						roughness = ExtractMwbRoughnessFromNormalAlpha( bumpRaw );
						normal = CloneImageWithAlpha( bumpRaw, 255 );
					}
					break;
				}

			case PbrFormat.BFTPseudoPBR:
				{
					if ( baseRaw is not null )
					{
						byte[]? metallicFromAlpha = PbrTextureGenerator.ExtractMetallicFromAlpha( baseRaw.Pixels, baseRaw.Width, baseRaw.Height );
						if ( metallicFromAlpha is not null )
						{
							metalness = CreateFromRgba( metallicFromAlpha, baseRaw.Width, baseRaw.Height );
						}
					}

					RgbaImage? exponent = LoadTexture( materialsRoot, props.PhongExponentTexturePath, forceOpaqueAlpha: false );
					exponentRaw = exponent;
					if ( exponentRaw is not null )
					{
						roughness = CreateFromRgba( PbrTextureGenerator.ConvertBftExponentToRoughness( exponentRaw.Pixels, exponentRaw.Width, exponentRaw.Height ), exponentRaw.Width, exponentRaw.Height );
					}
					break;
				}

			case PbrFormat.MadIvan18:
				{
					if ( bumpRaw is not null )
					{
						roughness = ExtractMadIvanRoughnessFromNormalAlpha( bumpRaw );
						normal = CloneImageWithAlpha( bumpRaw, 255 );
					}

					RgbaImage? exponent = LoadTexture( materialsRoot, props.PhongExponentTexturePath, forceOpaqueAlpha: false );
					exponentRaw = exponent;
					if ( exponentRaw is not null )
					{
						metalness = ExtractMetalnessFromRed( exponentRaw );
					}
					break;
				}

			default:
				{
					exponentRaw = LoadTexture( materialsRoot, props.PhongExponentTexturePath, forceOpaqueAlpha: false );
					if ( exponentRaw is not null )
					{
						roughness = CreateFromRgba( PbrTextureGenerator.ConvertPhongExponentToRoughness( exponentRaw.Pixels, exponentRaw.Width, exponentRaw.Height ), exponentRaw.Width, exponentRaw.Height );
					}
					else
					{
						envMaskRaw = LoadTexture( materialsRoot, props.EnvMapMaskPath, forceOpaqueAlpha: false );
						if ( envMaskRaw is not null )
						{
							roughness = InvertRedToGrayscale( envMaskRaw );
						}
					}
					break;
				}
		}

		var overrideCurves = new MaterialOverrideCurveSettings
		{
			Enabled = options.MaterialOverrideLevelsEnabled,
			InputMin = options.MaterialOverrideInputMin,
			InputMax = options.MaterialOverrideInputMax,
			OutputMin = options.MaterialOverrideOutputMin,
			OutputMax = options.MaterialOverrideOutputMax,
			Gamma = options.MaterialOverrideGamma
		};

		ApplyMaterialMapOverride(
			"roughness",
			options.RoughnessOverrideSource,
			options.RoughnessOverrideChannel,
			options.RoughnessOverrideInvert,
			materialsRoot,
			props,
			ref roughness,
			baseRaw,
			bumpRaw,
			ref armRaw,
			ref mraoRaw,
			ref exponentRaw,
			ref envMaskRaw,
			ref exoNormalRaw,
			overrideCurves,
			warn,
			sourceMaterialPath
		);

		ApplyMaterialMapOverride(
			"metalness",
			options.MetalnessOverrideSource,
			options.MetalnessOverrideChannel,
			options.MetalnessOverrideInvert,
			materialsRoot,
			props,
			ref metalness,
			baseRaw,
			bumpRaw,
			ref armRaw,
			ref mraoRaw,
			ref exponentRaw,
			ref envMaskRaw,
			ref exoNormalRaw,
			overrideCurves,
			warn,
			sourceMaterialPath
		);

		string colorPath = WriteTextureVariant( outputRoot, sourceMaterialPath, "color", color );
		string normalPath = WriteTextureVariant( outputRoot, sourceMaterialPath, "normal", normal );
		string roughnessPath = WriteTextureVariant( outputRoot, sourceMaterialPath, "roughness", roughness );
		string metalnessPath = WriteTextureVariant( outputRoot, sourceMaterialPath, "metalness", metalness );
		string aoPath = WriteTextureVariant( outputRoot, sourceMaterialPath, "ao", ao );

		string shaderPath = GetPbrShaderPath( props );
		string vmatRelativePath = $"materials/{sourceMaterialPath}.vmat".Replace( '\\', '/' );
		string vmatAbsolutePath = Path.Combine( outputRoot, vmatRelativePath.Replace( '/', Path.DirectorySeparatorChar ) );

		WritePbrVmat(
			vmatAbsolutePath,
			shaderPath,
			colorPath,
			normalPath,
			roughnessPath,
			metalnessPath,
			aoPath,
			props
		);

		result.Remaps.Add( new MaterialRemapExport
		{
			From = fromReference,
			To = vmatRelativePath
		} );
	}

	private static void WriteFallbackMaterial( string outputRoot, string sourceMaterialPath, MaterialConversionResult result, string fromReference )
	{
		RgbaImage color = CreateSolidColor( 1, 1, 255, 255, 255, 255 );
		RgbaImage normal = CreateSolidColor( 1, 1, 128, 128, 255, 255 );
		RgbaImage rough = CreateGray( 1, 1, 255 );
		RgbaImage metal = CreateGray( 1, 1, 0 );
		RgbaImage ao = CreateGray( 1, 1, 255 );

		string colorPath = WriteTextureVariant( outputRoot, sourceMaterialPath, "color", color );
		string normalPath = WriteTextureVariant( outputRoot, sourceMaterialPath, "normal", normal );
		string roughPath = WriteTextureVariant( outputRoot, sourceMaterialPath, "roughness", rough );
		string metalPath = WriteTextureVariant( outputRoot, sourceMaterialPath, "metalness", metal );
		string aoPath = WriteTextureVariant( outputRoot, sourceMaterialPath, "ao", ao );

		string vmatRelativePath = $"materials/{sourceMaterialPath}.vmat".Replace( '\\', '/' );
		string vmatAbsolutePath = Path.Combine( outputRoot, vmatRelativePath.Replace( '/', Path.DirectorySeparatorChar ) );

		WritePbrVmat(
			vmatAbsolutePath,
			"shaders/gmod_pbr.shader",
			colorPath,
			normalPath,
			roughPath,
			metalPath,
			aoPath,
			new ExtractedPbrProperties()
		);

		result.Remaps.Add( new MaterialRemapExport
		{
			From = fromReference,
			To = vmatRelativePath
		} );
		result.MissingCount++;
	}

	private static string? ResolveSourceMaterialPath(
		string materialToken,
		IReadOnlyList<string> modelMaterialPaths,
		string modelRelativeDirectory,
		string materialsRoot )
	{
		string token = NormalizeVirtualMaterialPath( materialToken );
		if ( string.IsNullOrWhiteSpace( token ) )
		{
			return null;
		}

		var candidates = new List<string>();
		void AddCandidate( string value )
		{
			string normalized = NormalizeVirtualMaterialPath( value );
			if ( string.IsNullOrWhiteSpace( normalized ) ) return;
			if ( !candidates.Any( c => string.Equals( c, normalized, StringComparison.OrdinalIgnoreCase ) ) )
			{
				candidates.Add( normalized );
			}
		}

		if ( token.Contains( '/', StringComparison.Ordinal ) )
		{
			AddCandidate( token );
		}

		foreach ( string prefix in modelMaterialPaths )
		{
			string cleanPrefix = NormalizeVirtualMaterialPath( prefix );
			if ( string.IsNullOrWhiteSpace( cleanPrefix ) ) continue;
			AddCandidate( $"{cleanPrefix}/{token}" );
		}

		if ( !string.IsNullOrWhiteSpace( modelRelativeDirectory ) )
		{
			AddCandidate( $"{modelRelativeDirectory.Replace( '\\', '/' )}/{token}" );
		}

		AddCandidate( token );

		foreach ( string candidate in candidates )
		{
			string filePath = Path.Combine( materialsRoot, candidate.Replace( '/', Path.DirectorySeparatorChar ) + ".vmt" );
			if ( File.Exists( filePath ) )
			{
				return candidate;
			}
		}

		return null;
	}

	private static string BuildFallbackMaterialPath( string materialToken, string modelRelativeDirectory )
	{
		string token = NormalizeVirtualMaterialPath( materialToken );
		token = token.Replace( '/', '_' );
		token = BoneNameUtil.SanitizeBoneName( token, "material" ).ToLowerInvariant();

		if ( string.IsNullOrWhiteSpace( modelRelativeDirectory ) )
		{
			return token;
		}

		string relative = modelRelativeDirectory.Replace( '\\', '/' ).Trim( '/' );
		if ( string.IsNullOrWhiteSpace( relative ) )
		{
			return token;
		}

		return $"{relative}/{token}";
	}

	private static string ToMaterialRemapFrom( string materialToken )
	{
		string normalized = NormalizeVirtualMaterialPath( materialToken );
		if ( string.IsNullOrWhiteSpace( normalized ) )
		{
			normalized = "default";
		}

		if ( normalized.EndsWith( ".vmat", StringComparison.OrdinalIgnoreCase ) )
		{
			return normalized;
		}

		if ( normalized.EndsWith( ".vmt", StringComparison.OrdinalIgnoreCase ) )
		{
			return normalized[..^4] + ".vmat";
		}

		return normalized + ".vmat";
	}

	private static string NormalizeVirtualMaterialPath( string raw )
	{
		if ( string.IsNullOrWhiteSpace( raw ) )
		{
			return string.Empty;
		}

		string value = raw.Trim().Replace( '\\', '/' );
		while ( value.Contains( "//", StringComparison.Ordinal ) )
		{
			value = value.Replace( "//", "/", StringComparison.Ordinal );
		}

		value = value.Trim( '/' );
		if ( value.EndsWith( ".vmt", StringComparison.OrdinalIgnoreCase ) )
		{
			value = value[..^4];
		}
		if ( value.EndsWith( ".vmat", StringComparison.OrdinalIgnoreCase ) )
		{
			value = value[..^5];
		}

		return value;
	}

	private static PbrFormat? MapProfileOverride( MaterialProfileOverride profileOverride )
	{
		return profileOverride switch
		{
			MaterialProfileOverride.Auto => null,
			MaterialProfileOverride.SourceEngine => PbrFormat.SourceEngine,
			MaterialProfileOverride.ExoPbr => PbrFormat.ExoPBR,
			MaterialProfileOverride.Gpbr => PbrFormat.GPBR,
			MaterialProfileOverride.MwbPbr => PbrFormat.MWBPBR,
			MaterialProfileOverride.BftPseudoPbr => PbrFormat.BFTPseudoPBR,
			MaterialProfileOverride.MadIvan18 => PbrFormat.MadIvan18,
			_ => null
		};
	}

	private static RgbaImage? LoadTexture( string materialsRoot, string texturePath, bool forceOpaqueAlpha )
	{
		string normalized = NormalizeVirtualMaterialPath( texturePath );
		if ( string.IsNullOrWhiteSpace( normalized ) )
		{
			return null;
		}

		string filePath = Path.Combine( materialsRoot, normalized.Replace( '/', Path.DirectorySeparatorChar ) + ".vtf" );
		if ( !File.Exists( filePath ) )
		{
			return null;
		}

		try
		{
			byte[] bytes = File.ReadAllBytes( filePath );
			VtfFile vtf = VtfFile.Load( bytes );
			byte[]? rgba = vtf.ConvertToRGBA( forceOpaqueAlpha );
			if ( rgba is null )
			{
				return null;
			}

			return CreateFromRgba( rgba, vtf.Width, vtf.Height );
		}
		catch
		{
			return null;
		}
	}

	private readonly struct MaterialOverrideCurveSettings
	{
		public bool Enabled { get; init; }
		public float InputMin { get; init; }
		public float InputMax { get; init; }
		public float OutputMin { get; init; }
		public float OutputMax { get; init; }
		public float Gamma { get; init; }
	}

	private static void ApplyMaterialMapOverride(
		string mapName,
		MaterialOverrideTextureSource source,
		MaterialOverrideChannel channel,
		bool invert,
		string materialsRoot,
		ExtractedPbrProperties props,
		ref RgbaImage mapImage,
		RgbaImage? baseRaw,
		RgbaImage? bumpRaw,
		ref RgbaImage? armRaw,
		ref RgbaImage? mraoRaw,
		ref RgbaImage? exponentRaw,
		ref RgbaImage? envMaskRaw,
		ref RgbaImage? exoNormalRaw,
		MaterialOverrideCurveSettings curves,
		Action<string> warn,
		string materialPath )
	{
		if ( source == MaterialOverrideTextureSource.Auto )
		{
			return;
		}

		RgbaImage? sourceImage = ResolveOverrideSourceTexture(
			source,
			materialsRoot,
			props,
			baseRaw,
			bumpRaw,
			ref armRaw,
			ref mraoRaw,
			ref exponentRaw,
			ref envMaskRaw,
			ref exoNormalRaw
		);

		if ( sourceImage is null )
		{
			warn( $"[warn] {materialPath}: {mapName} override requested ({source}/{channel}) but source texture was missing. Keeping auto pipeline output." );
			return;
		}

		mapImage = ExtractChannelToGrayscale( sourceImage, channel, invert, curves );
	}

	private static RgbaImage? ResolveOverrideSourceTexture(
		MaterialOverrideTextureSource source,
		string materialsRoot,
		ExtractedPbrProperties props,
		RgbaImage? baseRaw,
		RgbaImage? bumpRaw,
		ref RgbaImage? armRaw,
		ref RgbaImage? mraoRaw,
		ref RgbaImage? exponentRaw,
		ref RgbaImage? envMaskRaw,
		ref RgbaImage? exoNormalRaw )
	{
		return source switch
		{
			MaterialOverrideTextureSource.Auto => null,
			MaterialOverrideTextureSource.BaseTexture => baseRaw,
			MaterialOverrideTextureSource.NormalMap => bumpRaw,
			MaterialOverrideTextureSource.ArmTexture => armRaw ??= LoadTexture( materialsRoot, props.ArmTexturePath, forceOpaqueAlpha: false ),
			MaterialOverrideTextureSource.MraoTexture => mraoRaw ??= LoadTexture( materialsRoot, props.MraoTexturePath, forceOpaqueAlpha: false ),
			MaterialOverrideTextureSource.PhongExponentTexture => exponentRaw ??= LoadTexture( materialsRoot, props.PhongExponentTexturePath, forceOpaqueAlpha: false ),
			MaterialOverrideTextureSource.EnvMaskTexture => envMaskRaw ??= LoadTexture( materialsRoot, props.EnvMapMaskPath, forceOpaqueAlpha: false ),
			MaterialOverrideTextureSource.ExoNormalTexture => exoNormalRaw ??= LoadTexture( materialsRoot, props.ExoNormalPath, forceOpaqueAlpha: false ),
			_ => null
		};
	}

	private static RgbaImage ExtractChannelToGrayscale( RgbaImage source, MaterialOverrideChannel channel, bool invert, MaterialOverrideCurveSettings curves )
	{
		var output = new byte[source.Width * source.Height * 4];
		int channelIndex = channel switch
		{
			MaterialOverrideChannel.Red => 0,
			MaterialOverrideChannel.Green => 1,
			MaterialOverrideChannel.Blue => 2,
			MaterialOverrideChannel.Alpha => 3,
			_ => 0
		};

		int pixels = source.Width * source.Height;
		for ( int i = 0; i < pixels; i++ )
		{
			byte value = source.Pixels[i * 4 + channelIndex];
			float normalized = value / 255f;
			if ( invert )
			{
				normalized = 1f - normalized;
			}
			if ( curves.Enabled )
			{
				normalized = ApplyLevelsCurve( normalized, curves );
			}

			byte mapped = (byte)Math.Clamp( (int)MathF.Round( normalized * 255f ), 0, 255 );
			output[i * 4 + 0] = mapped;
			output[i * 4 + 1] = mapped;
			output[i * 4 + 2] = mapped;
			output[i * 4 + 3] = 255;
		}

		return CreateFromRgba( output, source.Width, source.Height );
	}

	private static float ApplyLevelsCurve( float value, MaterialOverrideCurveSettings curves )
	{
		float inMin = Math.Clamp( curves.InputMin, 0f, 1f );
		float inMax = Math.Clamp( curves.InputMax, 0f, 1f );
		float outMin = Math.Clamp( curves.OutputMin, 0f, 1f );
		float outMax = Math.Clamp( curves.OutputMax, 0f, 1f );
		float gamma = curves.Gamma <= 0f ? 1f : curves.Gamma;

		float inputRange = MathF.Max( inMax - inMin, 1e-6f );
		float t = Math.Clamp( (value - inMin) / inputRange, 0f, 1f );
		t = MathF.Pow( t, 1f / gamma );

		float outputRange = outMax - outMin;
		return Math.Clamp( outMin + (t * outputRange), 0f, 1f );
	}

	private static RgbaImage ExtractMwbRoughnessFromNormalAlpha( RgbaImage bumpRaw )
	{
		var roughData = new byte[bumpRaw.Width * bumpRaw.Height * 4];
		int pixels = bumpRaw.Width * bumpRaw.Height;
		for ( int i = 0; i < pixels; i++ )
		{
			byte encoded = bumpRaw.Pixels[i * 4 + 3];
			float linear = encoded / 255f;
			float srgb = MathF.Pow( linear, 0.4545f );
			float gloss = MathF.Pow( srgb, 0.4f );
			float roughness = 1f - gloss;
			byte roughByte = (byte)Math.Clamp( roughness * 255f, 0f, 255f );
			roughData[i * 4 + 0] = roughByte;
			roughData[i * 4 + 1] = roughByte;
			roughData[i * 4 + 2] = roughByte;
			roughData[i * 4 + 3] = 255;
		}

		return CreateFromRgba( roughData, bumpRaw.Width, bumpRaw.Height );
	}

	private static RgbaImage ExtractMadIvanRoughnessFromNormalAlpha( RgbaImage bumpRaw )
	{
		var roughData = new byte[bumpRaw.Width * bumpRaw.Height * 4];
		int pixels = bumpRaw.Width * bumpRaw.Height;
		for ( int i = 0; i < pixels; i++ )
		{
			byte gloss = bumpRaw.Pixels[i * 4 + 3];
			byte roughness = (byte)(255 - gloss);
			roughData[i * 4 + 0] = roughness;
			roughData[i * 4 + 1] = roughness;
			roughData[i * 4 + 2] = roughness;
			roughData[i * 4 + 3] = 255;
		}

		return CreateFromRgba( roughData, bumpRaw.Width, bumpRaw.Height );
	}

	private static RgbaImage ExtractMetalnessFromRed( RgbaImage source )
	{
		var data = new byte[source.Width * source.Height * 4];
		int pixels = source.Width * source.Height;
		for ( int i = 0; i < pixels; i++ )
		{
			byte value = source.Pixels[i * 4 + 0];
			data[i * 4 + 0] = value;
			data[i * 4 + 1] = value;
			data[i * 4 + 2] = value;
			data[i * 4 + 3] = 255;
		}

		return CreateFromRgba( data, source.Width, source.Height );
	}

	private static RgbaImage InvertRedToGrayscale( RgbaImage source )
	{
		var data = new byte[source.Width * source.Height * 4];
		int pixels = source.Width * source.Height;
		for ( int i = 0; i < pixels; i++ )
		{
			byte inverted = (byte)(255 - source.Pixels[i * 4 + 0]);
			data[i * 4 + 0] = inverted;
			data[i * 4 + 1] = inverted;
			data[i * 4 + 2] = inverted;
			data[i * 4 + 3] = 255;
		}

		return CreateFromRgba( data, source.Width, source.Height );
	}

	private static string WriteTextureVariant( string outputRoot, string sourceMaterialPath, string suffix, RgbaImage image )
	{
		string relativePath = $"materials/{sourceMaterialPath}_{suffix}.tga".Replace( '\\', '/' );
		string fullPath = Path.Combine( outputRoot, relativePath.Replace( '/', Path.DirectorySeparatorChar ) );
		WriteWithFileLock( fullPath, () => TgaWriter.WriteRgba32( fullPath, image.Width, image.Height, image.Pixels ) );
		return relativePath;
	}

	private static string GetPbrShaderPath( ExtractedPbrProperties props )
	{
		bool translucent = props.IsTranslucent || props.IsAdditive;
		if ( translucent && props.IsNoCull ) return "shaders/gmod_pbr_translucent_twosided.shader";
		if ( translucent ) return "shaders/gmod_pbr_translucent.shader";
		if ( props.IsNoCull ) return "shaders/gmod_pbr_twosided.shader";
		return "shaders/gmod_pbr.shader";
	}

	private static void WritePbrVmat(
		string vmatAbsolutePath,
		string shaderPath,
		string colorPath,
		string normalPath,
		string roughnessPath,
		string metalnessPath,
		string aoPath,
		ExtractedPbrProperties props )
	{
		Directory.CreateDirectory( Path.GetDirectoryName( vmatAbsolutePath ) ?? "." );

		var sb = new StringBuilder( 768 );
		sb.AppendLine( "\"Layer0\"" );
		sb.AppendLine( "{" );
		AppendKeyValue( sb, "shader", shaderPath );
		AppendKeyValue( sb, "Color", colorPath );
		AppendKeyValue( sb, "Normal", normalPath );
		AppendKeyValue( sb, "Roughness", roughnessPath );
		AppendKeyValue( sb, "Metalness", metalnessPath );
		AppendKeyValue( sb, "AmbientOcclusion", aoPath );
		AppendKeyValue( sb, "g_flRoughnessScaleFactor", "1.000000" );
		AppendKeyValue( sb, "g_flMetalnessScale", "1.000000" );

		if ( props.IsAlphaTest && !(props.IsTranslucent || props.IsAdditive) )
		{
			AppendKeyValue( sb, "g_flAlphaTestReference", FmtFloat( props.AlphaTestReference ) );
		}

		if ( props.IsTranslucent || props.IsAdditive )
		{
			AppendKeyValue( sb, "g_flOpacity", FmtFloat( props.Alpha ) );
		}

		if ( props.Color2 is { Length: >= 3 } )
		{
			AppendKeyValue( sb, "g_vColorTint", $"[{FmtFloat( props.Color2[0] )} {FmtFloat( props.Color2[1] )} {FmtFloat( props.Color2[2] )} 0.000000]" );
		}

		if ( props.BlendTintByBaseAlpha )
		{
			AppendKeyValue( sb, "g_flBlendTintByBaseAlpha", "1.000000" );
		}

		sb.AppendLine( "}" );
		string contents = sb.ToString();
		WriteWithFileLock( vmatAbsolutePath, () => File.WriteAllText( vmatAbsolutePath, contents, new UTF8Encoding( false ) ) );
	}

	private sealed class EyeShaderProjectionData
	{
		public required Vector3 EyeOrigin { get; init; }
		public required Vector4 IrisProjectionU { get; init; }
		public required Vector4 IrisProjectionV { get; init; }
		public required float EyeballRadius { get; init; }
	}

	private static void WriteEyeVmat(
		string vmatAbsolutePath,
		string irisPath,
		string corneaPath,
		ExtractedPbrProperties props )
	{
		Directory.CreateDirectory( Path.GetDirectoryName( vmatAbsolutePath ) ?? "." );

		var sb = new StringBuilder( 1024 );
		sb.AppendLine( "\"Layer0\"" );
		sb.AppendLine( "{" );
		AppendKeyValue( sb, "shader", "shaders/eyeball.shader" );
		sb.AppendLine();
		sb.AppendLine( "\t//---- Animation ----" );
		sb.AppendLine( "\tF_MORPH_SUPPORTED 1" );
		sb.AppendLine();
		sb.AppendLine( "\t//---- Specular ----" );
		sb.AppendLine( "\tF_SPECULAR_CUBE_MAP 1" );
		sb.AppendLine();
		sb.AppendLine( "\t//---- Color ----" );
		AppendKeyValue( sb, "g_flModelTintAmount", "1.000" );
		AppendKeyValue( sb, "g_vColorTint", "[1.000000 1.000000 1.000000 0.000000]" );
		AppendKeyValue( sb, "TextureColor", irisPath );
		sb.AppendLine();
		sb.AppendLine( "\t//---- Fade ----" );
		AppendKeyValue( sb, "g_flFadeExponent", "1.000" );
		sb.AppendLine();
		sb.AppendLine( "\t//---- Fog ----" );
		AppendKeyValue( sb, "g_bFogEnabled", "1" );
		sb.AppendLine();
		sb.AppendLine( "\t//---- Iris ----" );
		AppendKeyValue( sb, "g_flIrisBumpStrength", FmtFloat( props.EyeCorneaBumpStrength ) );
		AppendKeyValue( sb, "g_flParallaxStrength", "0.000" );
		AppendKeyValue( sb, "g_vIrisAutoexposureRange", "[0.000 0.000]" );
		AppendKeyValue( sb, "IrisNormal", corneaPath );
		AppendKeyValue( sb, "IrisRoughness", "[1.000000 1.000000 1.000000 0.000000]" );
		AppendKeyValue( sb, "TextureIrisMask", irisPath );
		sb.AppendLine();
		sb.AppendLine( "\t//---- Lighting ----" );
		AppendKeyValue( sb, "g_vReflectanceRange", "[1.000 1.000]" );
		AppendKeyValue( sb, "TextureReflectance", "materials/default/default_refl.tga" );
		sb.AppendLine();
		sb.AppendLine( "\t//---- Normal ----" );
		AppendKeyValue( sb, "g_flCorneaScleraBumpStrength", "0.000" );
		AppendKeyValue( sb, "g_flScleraDiffuseExponent", "1.000" );
		AppendKeyValue( sb, "g_flScleraDiffuseWrap", "1.000" );
		AppendKeyValue( sb, "TextureNormal", "[0.501961 0.501961 1.000000 0.000000]" );
		sb.AppendLine();
		sb.AppendLine( "\t//---- Occlusion ----" );
		AppendKeyValue( sb, "g_flOcclusionStrength", "0.000" );
		AppendKeyValue( sb, "g_flOcclusionWidth", "0.100" );
		AppendKeyValue( sb, "TextureOcclusion", "[1.000000 1.000000 1.000000 0.000000]" );
		sb.AppendLine();
		sb.AppendLine( "\t//---- Texture Coordinates ----" );
		AppendKeyValue( sb, "g_nScaleTexCoordUByModelScaleAxis", "0" );
		AppendKeyValue( sb, "g_nScaleTexCoordVByModelScaleAxis", "0" );
		AppendKeyValue( sb, "g_vTexCoordOffset", "[0.000 0.000]" );
		AppendKeyValue( sb, "g_vTexCoordScale", "[1.000 1.000]" );
		AppendKeyValue( sb, "g_vTexCoordScrollSpeed", "[0.000 0.000]" );
		sb.AppendLine();
		sb.AppendLine( "\tAttributes" );
		sb.AppendLine( "\t{" );
		AppendKeyValue( sb, "eyes", "1" );
		sb.AppendLine( "\t}" );

		sb.AppendLine( "}" );
		string contents = sb.ToString();
		WriteWithFileLock( vmatAbsolutePath, () => File.WriteAllText( vmatAbsolutePath, contents, new UTF8Encoding( false ) ) );
	}

	private static EyeShaderProjectionData? TryResolveEyeProjectionData( MdlFile mdl, string sourceMaterialPath )
	{
		if ( mdl.Eyeballs.Count == 0 || mdl.Materials.Count == 0 || string.IsNullOrWhiteSpace( sourceMaterialPath ) )
		{
			return null;
		}

		string normalizedMaterialPath = NormalizeMaterialPath( sourceMaterialPath );
		string materialLeafName = normalizedMaterialPath;
		int slashIndex = materialLeafName.LastIndexOf( '/' );
		if ( slashIndex >= 0 && slashIndex + 1 < materialLeafName.Length )
		{
			materialLeafName = materialLeafName[(slashIndex + 1)..];
		}

		int materialIndex = -1;
		for ( int i = 0; i < mdl.Materials.Count; i++ )
		{
			string normalizedCandidate = NormalizeMaterialPath( mdl.Materials[i] );
			if ( string.Equals( normalizedCandidate, normalizedMaterialPath, StringComparison.OrdinalIgnoreCase )
				|| string.Equals( normalizedCandidate, materialLeafName, StringComparison.OrdinalIgnoreCase ) )
			{
				materialIndex = i;
				break;
			}
		}

		if ( materialIndex < 0 )
		{
			return null;
		}

		List<MdlEyeball> matches = mdl.Eyeballs
			.Where( e => e.TextureIndex == materialIndex )
			.ToList();

		if ( matches.Count == 0 )
		{
			return null;
		}

		MdlEyeball eyeball = PickEyeballForMaterial( matches, normalizedMaterialPath );
		BuildBoneWorldTransforms( mdl.Bones, out System.Numerics.Vector3[] worldPositions, out System.Numerics.Quaternion[] worldRotations );

		System.Numerics.Vector3 localOrigin = new( eyeball.Origin.x, eyeball.Origin.y, eyeball.Origin.z );
		System.Numerics.Vector3 localUp = new( eyeball.Up.x, eyeball.Up.y, eyeball.Up.z );
		System.Numerics.Vector3 localForward = new( eyeball.Forward.x, eyeball.Forward.y, eyeball.Forward.z );

		System.Numerics.Vector3 worldOrigin = localOrigin;
		System.Numerics.Vector3 worldUp = localUp;
		System.Numerics.Vector3 worldForward = localForward;

		if ( eyeball.BoneIndex >= 0 && eyeball.BoneIndex < worldPositions.Length )
		{
			System.Numerics.Quaternion boneRotation = worldRotations[eyeball.BoneIndex];
			worldOrigin = worldPositions[eyeball.BoneIndex] + System.Numerics.Vector3.Transform( localOrigin, boneRotation );
			worldUp = System.Numerics.Vector3.Transform( localUp, boneRotation );
			worldForward = System.Numerics.Vector3.Transform( localForward, boneRotation );
		}

		if ( worldUp.LengthSquared() < 1e-8f )
		{
			worldUp = new System.Numerics.Vector3( 0f, 0f, 1f );
		}
		else
		{
			worldUp = System.Numerics.Vector3.Normalize( worldUp );
		}

		if ( worldForward.LengthSquared() < 1e-8f )
		{
			worldForward = new System.Numerics.Vector3( 1f, 0f, 0f );
		}
		else
		{
			worldForward = System.Numerics.Vector3.Normalize( worldForward );
		}

		System.Numerics.Vector3 left = System.Numerics.Vector3.Cross( worldUp, worldForward );
		if ( left.LengthSquared() < 1e-8f )
		{
			left = System.Numerics.Vector3.Cross( worldUp, new System.Numerics.Vector3( 0f, 1f, 0f ) );
		}
		if ( left.LengthSquared() < 1e-8f )
		{
			left = new System.Numerics.Vector3( 0f, 1f, 0f );
		}
		left = System.Numerics.Vector3.Normalize( left );

		float radius = eyeball.Radius > 1e-4f ? eyeball.Radius : 0.5f;
		// MDL stores mstudioeyeball_t::iris_scale as reciprocal QC pupil scale.
		// QC: iris_scale ~= 0.52, MDL IrisScale ~= 1.923 (1 / 0.52).
		// EyeRefract projection expects the QC-side value, so invert first.
		float irisScaleReciprocal = eyeball.IrisScale > 1e-4f ? eyeball.IrisScale : 1.0f;
		float irisScale = 1.0f / irisScaleReciprocal;
		float scale = irisScale / (radius * 2.0f);

		float uOffset = -(System.Numerics.Vector3.Dot( left, worldOrigin ) * scale) + 0.5f;
		float vOffset = -(System.Numerics.Vector3.Dot( worldUp, worldOrigin ) * scale) + 0.5f;

		return new EyeShaderProjectionData
		{
			EyeOrigin = new Vector3( worldOrigin.X, worldOrigin.Y, worldOrigin.Z ),
			IrisProjectionU = new Vector4( left.X * scale, left.Y * scale, left.Z * scale, uOffset ),
			IrisProjectionV = new Vector4( worldUp.X * scale, worldUp.Y * scale, worldUp.Z * scale, vOffset ),
			EyeballRadius = radius
		};
	}

	private static MdlEyeball PickEyeballForMaterial( List<MdlEyeball> matches, string normalizedMaterialPath )
	{
		if ( matches.Count == 1 )
		{
			return matches[0];
		}

		bool wantsLeft = normalizedMaterialPath.Contains( "left", StringComparison.OrdinalIgnoreCase )
			|| normalizedMaterialPath.EndsWith( "_l", StringComparison.OrdinalIgnoreCase )
			|| normalizedMaterialPath.Contains( "eyeball_l", StringComparison.OrdinalIgnoreCase );
		bool wantsRight = normalizedMaterialPath.Contains( "right", StringComparison.OrdinalIgnoreCase )
			|| normalizedMaterialPath.EndsWith( "_r", StringComparison.OrdinalIgnoreCase )
			|| normalizedMaterialPath.Contains( "eyeball_r", StringComparison.OrdinalIgnoreCase );

		if ( wantsLeft )
		{
			MdlEyeball? left = matches.FirstOrDefault( m =>
				m.Name.Contains( "left", StringComparison.OrdinalIgnoreCase )
				|| m.Name.EndsWith( "_l", StringComparison.OrdinalIgnoreCase )
				|| m.Name.Contains( "eyeball_l", StringComparison.OrdinalIgnoreCase ) );
			if ( left is not null )
			{
				return left;
			}
		}

		if ( wantsRight )
		{
			MdlEyeball? right = matches.FirstOrDefault( m =>
				m.Name.Contains( "right", StringComparison.OrdinalIgnoreCase )
				|| m.Name.EndsWith( "_r", StringComparison.OrdinalIgnoreCase )
				|| m.Name.Contains( "eyeball_r", StringComparison.OrdinalIgnoreCase ) );
			if ( right is not null )
			{
				return right;
			}
		}

		return matches[0];
	}

	private static string NormalizeMaterialPath( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
		{
			return string.Empty;
		}

		string normalized = value.Replace( '\\', '/' ).Trim().TrimStart( '/' );
		if ( normalized.EndsWith( ".vmt", StringComparison.OrdinalIgnoreCase ) )
		{
			normalized = normalized[..^4];
		}

		return normalized.ToLowerInvariant();
	}

	private static void BuildBoneWorldTransforms(
		List<MdlBone> bones,
		out System.Numerics.Vector3[] worldPositions,
		out System.Numerics.Quaternion[] worldRotations )
	{
		worldPositions = new System.Numerics.Vector3[bones.Count];
		worldRotations = new System.Numerics.Quaternion[bones.Count];
		var computed = new bool[bones.Count];

		for ( int i = 0; i < bones.Count; i++ )
		{
			EnsureBoneWorldTransform( i, bones, worldPositions, worldRotations, computed );
		}
	}

	private static void EnsureBoneWorldTransform(
		int boneIndex,
		List<MdlBone> bones,
		System.Numerics.Vector3[] worldPositions,
		System.Numerics.Quaternion[] worldRotations,
		bool[] computed )
	{
		if ( computed[boneIndex] )
		{
			return;
		}

		MdlBone bone = bones[boneIndex];
		System.Numerics.Vector3 localPos = new( bone.Position.x, bone.Position.y, bone.Position.z );
		System.Numerics.Quaternion localRot = new( bone.Quaternion.x, bone.Quaternion.y, bone.Quaternion.z, bone.Quaternion.w );
		if ( localRot.LengthSquared() < 1e-8f )
		{
			localRot = System.Numerics.Quaternion.Identity;
		}
		else
		{
			localRot = System.Numerics.Quaternion.Normalize( localRot );
		}

		if ( bone.ParentIndex >= 0 && bone.ParentIndex < bones.Count )
		{
			EnsureBoneWorldTransform( bone.ParentIndex, bones, worldPositions, worldRotations, computed );
			System.Numerics.Quaternion parentRot = worldRotations[bone.ParentIndex];
			System.Numerics.Vector3 parentPos = worldPositions[bone.ParentIndex];
			System.Numerics.Vector3 rotatedLocal = System.Numerics.Vector3.Transform( localPos, parentRot );
			worldPositions[boneIndex] = parentPos + rotatedLocal;
			worldRotations[boneIndex] = System.Numerics.Quaternion.Normalize( localRot * parentRot );
		}
		else
		{
			worldPositions[boneIndex] = localPos;
			worldRotations[boneIndex] = localRot;
		}

		computed[boneIndex] = true;
	}

	private static void WriteWithFileLock( string path, Action writer )
	{
		string fullPath = Path.GetFullPath( path );
		object gate = FileWriteLocks.GetOrAdd( fullPath, _ => new object() );
		lock ( gate )
		{
			writer();
		}
	}

	private static void AppendKeyValue( StringBuilder sb, string key, string value )
	{
		sb.Append( '\t' );
		sb.Append( '"' );
		sb.Append( key );
		sb.Append( "\"\t\t\"" );
		sb.Append( value.Replace( "\"", string.Empty, StringComparison.Ordinal ) );
		sb.AppendLine( "\"" );
	}

	private static string FmtFloat( float value )
	{
		if ( float.IsNaN( value ) || float.IsInfinity( value ) )
		{
			return "0.000000";
		}

		return value.ToString( "0.000000", CultureInfo.InvariantCulture );
	}

	private static RgbaImage CloneImage( RgbaImage source )
	{
		var pixels = new byte[source.Pixels.Length];
		Array.Copy( source.Pixels, pixels, pixels.Length );
		return CreateFromRgba( pixels, source.Width, source.Height );
	}

	private static RgbaImage CloneImageWithAlpha( RgbaImage source, byte alpha )
	{
		var pixels = new byte[source.Pixels.Length];
		Array.Copy( source.Pixels, pixels, pixels.Length );
		for ( int i = 3; i < pixels.Length; i += 4 )
		{
			pixels[i] = alpha;
		}
		return CreateFromRgba( pixels, source.Width, source.Height );
	}

	private static RgbaImage CreateGray( int width, int height, byte value )
	{
		return CreateFromRgba( PbrTextureGenerator.GenerateConstant( width, height, value ), width, height );
	}

	private static RgbaImage CreateSolidColor( int width, int height, byte r, byte g, byte b, byte a )
	{
		var pixels = new byte[width * height * 4];
		for ( int i = 0; i < width * height; i++ )
		{
			int offset = i * 4;
			pixels[offset + 0] = r;
			pixels[offset + 1] = g;
			pixels[offset + 2] = b;
			pixels[offset + 3] = a;
		}
		return CreateFromRgba( pixels, width, height );
	}

	private static RgbaImage CreateFromRgba( byte[] rgba, int width, int height )
	{
		return new RgbaImage
		{
			Width = width,
			Height = height,
			Pixels = rgba
		};
	}
}
