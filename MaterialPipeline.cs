using GModMount.Source;

internal sealed class MaterialConversionResult
{
	public List<MaterialRemapExport> Remaps { get; } = new();
	public int ConvertedCount { get; set; }
	public int MissingCount { get; set; }
}

internal static class MaterialPipeline
{
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
					WritePbrMaterial( outputRoot, materialsRoot, sourceMaterialPath, props, result, fromReference );
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
		RgbaImage iris = LoadTexture( materialsRoot, props.IrisTexturePath, forceOpaqueAlpha: true )
			?? LoadTexture( materialsRoot, props.BaseTexturePath, forceOpaqueAlpha: true )
			?? CreateSolidColor( 1, 1, 255, 255, 255, 255 );
		RgbaImage cornea = LoadTexture( materialsRoot, props.CorneaTexturePath, forceOpaqueAlpha: true )
			?? CreateSolidColor( 1, 1, 128, 128, 255, 255 );

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
		string fromReference )
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

		byte defaultRough = (byte)Math.Clamp( props.Roughness * 255f, 0f, 255f );
		byte defaultMetal = (byte)Math.Clamp( props.Metallic * 255f, 0f, 255f );
		RgbaImage roughness = CreateGray( color.Width, color.Height, defaultRough );
		RgbaImage metalness = CreateGray( color.Width, color.Height, defaultMetal );
		RgbaImage ao = CreateGray( color.Width, color.Height, 255 );

		switch ( props.Format )
		{
			case PbrFormat.ExoPBR:
				{
					RgbaImage? arm = LoadTexture( materialsRoot, props.ArmTexturePath, forceOpaqueAlpha: false );
					if ( arm is not null )
					{
						roughness = CreateFromRgba( PbrTextureGenerator.ExtractRoughnessFromArm( arm.Pixels, arm.Width, arm.Height ), arm.Width, arm.Height );
						metalness = CreateFromRgba( PbrTextureGenerator.ExtractMetallicFromArm( arm.Pixels, arm.Width, arm.Height ), arm.Width, arm.Height );
						ao = CreateFromRgba( PbrTextureGenerator.ExtractAoFromArm( arm.Pixels, arm.Width, arm.Height ), arm.Width, arm.Height );
					}

					RgbaImage? exoNormal = LoadTexture( materialsRoot, props.ExoNormalPath, forceOpaqueAlpha: false );
					if ( exoNormal is not null )
					{
						normal = CreateFromRgba( PbrTextureGenerator.FlipNormalMapGreen( exoNormal.Pixels, exoNormal.Width, exoNormal.Height ), exoNormal.Width, exoNormal.Height );
						normal = CloneImageWithAlpha( normal, 255 );
					}
					break;
				}

			case PbrFormat.GPBR:
				{
					RgbaImage? mrao = LoadTexture( materialsRoot, props.MraoTexturePath, forceOpaqueAlpha: false );
					if ( mrao is not null )
					{
						roughness = CreateFromRgba( PbrTextureGenerator.ExtractRoughnessFromMrao( mrao.Pixels, mrao.Width, mrao.Height ), mrao.Width, mrao.Height );
						metalness = CreateFromRgba( PbrTextureGenerator.ExtractMetallicFromMrao( mrao.Pixels, mrao.Width, mrao.Height ), mrao.Width, mrao.Height );
						ao = CreateFromRgba( PbrTextureGenerator.ExtractAoFromMrao( mrao.Pixels, mrao.Width, mrao.Height ), mrao.Width, mrao.Height );
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
					if ( exponent is not null )
					{
						roughness = CreateFromRgba( PbrTextureGenerator.ConvertBftExponentToRoughness( exponent.Pixels, exponent.Width, exponent.Height ), exponent.Width, exponent.Height );
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
					if ( exponent is not null )
					{
						metalness = ExtractMetalnessFromRed( exponent );
					}
					break;
				}

			default:
				{
					RgbaImage? exponent = LoadTexture( materialsRoot, props.PhongExponentTexturePath, forceOpaqueAlpha: false );
					if ( exponent is not null )
					{
						roughness = CreateFromRgba( PbrTextureGenerator.ConvertPhongExponentToRoughness( exponent.Pixels, exponent.Width, exponent.Height ), exponent.Width, exponent.Height );
					}
					else
					{
						RgbaImage? envMask = LoadTexture( materialsRoot, props.EnvMapMaskPath, forceOpaqueAlpha: false );
						if ( envMask is not null )
						{
							roughness = InvertRedToGrayscale( envMask );
						}
					}
					break;
				}
		}

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
		TgaWriter.WriteRgba32( fullPath, image.Width, image.Height, image.Pixels );
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
		File.WriteAllText( vmatAbsolutePath, sb.ToString(), new UTF8Encoding( false ) );
	}

	private static void WriteEyeVmat( string vmatAbsolutePath, string irisPath, string corneaPath, ExtractedPbrProperties props )
	{
		Directory.CreateDirectory( Path.GetDirectoryName( vmatAbsolutePath ) ?? "." );

		var sb = new StringBuilder( 384 );
		sb.AppendLine( "\"Layer0\"" );
		sb.AppendLine( "{" );
		AppendKeyValue( sb, "shader", "shaders/gmod_eyes.shader" );
		AppendKeyValue( sb, "Iris", irisPath );
		AppendKeyValue( sb, "Cornea", corneaPath );
		AppendKeyValue( sb, "g_flGlossiness", FmtFloat( props.EyeGlossiness ) );
		sb.AppendLine( "}" );
		File.WriteAllText( vmatAbsolutePath, sb.ToString(), new UTF8Encoding( false ) );
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
