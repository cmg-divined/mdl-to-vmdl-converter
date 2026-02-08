using GModMount.Source;

internal enum PbrFormat
{
	SourceEngine,
	ExoPBR,
	GPBR,
	MWBPBR,
	BFTPseudoPBR,
	MadIvan18
}

internal sealed class ExtractedPbrProperties
{
	public PbrFormat Format { get; set; } = PbrFormat.SourceEngine;
	public string BaseTexturePath { get; set; } = string.Empty;
	public string BumpMapPath { get; set; } = string.Empty;
	public string EnvMapMaskPath { get; set; } = string.Empty;
	public string PhongExponentTexturePath { get; set; } = string.Empty;

	public string ArmTexturePath { get; set; } = string.Empty;
	public string ExoNormalPath { get; set; } = string.Empty;
	public string EmissionTexturePath { get; set; } = string.Empty;
	public float EmissionScale { get; set; } = 1f;

	public string MraoTexturePath { get; set; } = string.Empty;
	public string GpbrEmissionPath { get; set; } = string.Empty;
	public float GpbrEmissionScale { get; set; } = 1f;

	public float PhongExponent { get; set; } = 10f;
	public float PhongBoost { get; set; } = 1f;
	public bool HasPhong { get; set; }
	public bool HasEnvMap { get; set; }
	public float[]? PhongFresnelRanges { get; set; }
	public float[]? EnvMapTint { get; set; }

	public float Roughness { get; set; } = 1f;
	public float Metallic { get; set; }

	public bool IsTranslucent { get; set; }
	public bool IsSelfIllum { get; set; }
	public bool IsSSBump { get; set; }
	public bool HasAlphaMetallic { get; set; }

	public bool IsAlphaTest { get; set; }
	public float AlphaTestReference { get; set; } = 0.5f;
	public float Alpha { get; set; } = 1f;
	public bool IsAdditive { get; set; }
	public bool IsNoCull { get; set; }

	public bool IsBftMetallicLayer { get; set; }
	public bool IsBftDiffuseLayer { get; set; }
	public float[]? BftColor2 { get; set; }

	public float[]? Color2 { get; set; }
	public bool BlendTintByBaseAlpha { get; set; }

	public bool IsEyeShader { get; set; }
	public string IrisTexturePath { get; set; } = string.Empty;
	public string CorneaTexturePath { get; set; } = string.Empty;
	public string EyeAmbientOcclTexturePath { get; set; } = string.Empty;
	public float[]? EyeAmbientOcclColor { get; set; }
	public float EyeDilation { get; set; } = 0.5f;
	public float EyeParallaxStrength { get; set; } = 0.25f;
	public float EyeCorneaBumpStrength { get; set; } = 1f;
	public float EyeEyeballRadius { get; set; } = 0.5f;
	public float EyeGlossiness { get; set; } = 0.5f;
	public bool EyeRaytraceSphere { get; set; } = true;
	public bool EyeSphereTexKill { get; set; } = true;
}

internal static class PseudoPbrFormats
{
	public static PbrFormat DetectFormat( VmtFile vmt, string? materialPath = null )
	{
		if ( !string.IsNullOrEmpty( materialPath )
			&& materialPath.Contains( "MadIvan18", StringComparison.OrdinalIgnoreCase ) )
		{
			return PbrFormat.MadIvan18;
		}

		string shader = vmt.Shader?.ToLowerInvariant() ?? string.Empty;

		if ( shader == "screenspace_general_8tex" )
		{
			foreach ( VmtProxy proxy in vmt.Proxies )
			{
				if ( proxy.Name.Contains( "exopbr", StringComparison.OrdinalIgnoreCase ) )
				{
					return PbrFormat.ExoPBR;
				}
			}
			if ( vmt.Parameters.Any( p => p.Value.Contains( "exopbr", StringComparison.OrdinalIgnoreCase ) ) )
			{
				return PbrFormat.ExoPBR;
			}
		}

		if ( shader == "pbr" )
		{
			return PbrFormat.GPBR;
		}

		if ( shader == "vertexlitgeneric" || shader == "lightmappedgeneric" )
		{
			string expTex = vmt.GetString( "$phongexponenttexture" );
			if ( !string.IsNullOrWhiteSpace( expTex ) )
			{
				if ( IsMwbFormat( vmt, expTex ) ) return PbrFormat.MWBPBR;
				if ( IsBftFormat( vmt, expTex ) ) return PbrFormat.BFTPseudoPBR;
			}
		}

		return PbrFormat.SourceEngine;
	}

	public static ExtractedPbrProperties ExtractProperties( VmtFile vmt, string? materialPath = null, PbrFormat? forcedFormat = null )
	{
		var props = new ExtractedPbrProperties
		{
			Format = forcedFormat ?? DetectFormat( vmt, materialPath ),
			BaseTexturePath = vmt.BaseTexture,
			BumpMapPath = vmt.BumpMap,
			EnvMapMaskPath = vmt.GetString( "$envmapmask" ),
			PhongExponentTexturePath = vmt.GetString( "$phongexponenttexture" ),
			IsTranslucent = vmt.Translucent,
			IsSelfIllum = vmt.GetBool( "$selfillum" ),
			IsSSBump = vmt.GetBool( "$ssbump" ),
			IsAlphaTest = vmt.GetBool( "$alphatest" ),
			AlphaTestReference = vmt.GetFloat( "$alphatestreference", 0.5f ),
			Alpha = vmt.GetFloat( "$alpha", 1f ),
			IsAdditive = vmt.GetBool( "$additive" ),
			IsNoCull = vmt.GetBool( "$nocull" ),
			HasPhong = vmt.GetBool( "$phong" ),
			HasEnvMap = !string.IsNullOrEmpty( vmt.EnvMap ),
			PhongExponent = vmt.GetFloat( "$phongexponent", 10f ),
			PhongBoost = vmt.GetFloat( "$phongboost", 1f ),
			BlendTintByBaseAlpha = vmt.GetBool( "$blendtintbybasealpha" )
		};

		Vector3 fresnelVec = vmt.GetVector3( "$phongfresnelranges" );
		if ( !IsDefaultVector( fresnelVec ) )
		{
			props.PhongFresnelRanges = [fresnelVec.x, fresnelVec.y, fresnelVec.z];
		}

		Vector3 envTintVec = vmt.GetVector3( "$envmaptint" );
		if ( !IsDefaultVector( envTintVec ) )
		{
			props.EnvMapTint = [envTintVec.x, envTintVec.y, envTintVec.z];
		}

		Vector3 color2Vec = vmt.GetVector3( "$color2" );
		if ( !IsDefaultVector( color2Vec ) )
		{
			props.Color2 = [color2Vec.x, color2Vec.y, color2Vec.z];
		}

		string shaderLower = vmt.Shader?.ToLowerInvariant() ?? string.Empty;
		if ( shaderLower == "eyes" || shaderLower == "eyes_dx8" || shaderLower == "eyerefract" )
		{
			props.IsEyeShader = true;
			props.IrisTexturePath = vmt.GetString( "$iris" );
			props.CorneaTexturePath = vmt.GetString( "$corneatexture" );
			props.EyeAmbientOcclTexturePath = vmt.GetString( "$ambientoccltexture" );
			props.EyeDilation = vmt.GetFloat( "$dilation", 0.5f );
			props.EyeParallaxStrength = vmt.GetFloat( "$parallaxstrength", 0.25f );
			props.EyeCorneaBumpStrength = vmt.GetFloat( "$corneabumpstrength", 1f );
			props.EyeGlossiness = vmt.GetFloat( "$glossiness", 0.5f );
			props.EyeEyeballRadius = vmt.GetFloat( "$eyeballradius", 0.5f );
			props.EyeRaytraceSphere = vmt.GetBool( "$raytracesphere", true );
			props.EyeSphereTexKill = vmt.GetBool( "$spheretexkillcombo", true ) || vmt.GetBool( "$spheretexkill", false );

			Vector3 aoColor = vmt.GetVector3( "$ambientocclcolor" );
			props.EyeAmbientOcclColor = !IsDefaultVector( aoColor )
				? [aoColor.x, aoColor.y, aoColor.z]
				: [0.33f, 0.33f, 0.33f];
		}

		switch ( props.Format )
		{
			case PbrFormat.ExoPBR:
				ExtractExoPbrProperties( vmt, props );
				break;
			case PbrFormat.GPBR:
				ExtractGpbrProperties( vmt, props );
				break;
			case PbrFormat.MWBPBR:
				ExtractMwbProperties( props );
				break;
			case PbrFormat.BFTPseudoPBR:
				ExtractBftProperties( vmt, props );
				break;
			case PbrFormat.MadIvan18:
				ExtractMadIvanProperties( props );
				break;
			default:
				ExtractSourceProperties( props );
				break;
		}

		return props;
	}

	private static bool IsMwbFormat( VmtFile vmt, string expTexture )
	{
		string baseTex = vmt.BaseTexture?.ToLowerInvariant() ?? string.Empty;
		if ( baseTex.EndsWith( "_rgb", StringComparison.Ordinal ) )
		{
			return true;
		}

		string expLower = expTexture.ToLowerInvariant();
		if ( expLower.EndsWith( "_e", StringComparison.Ordinal ) && baseTex.EndsWith( "_rgb", StringComparison.Ordinal ) )
		{
			return true;
		}

		if ( baseTex.Contains( "pbr\\output", StringComparison.Ordinal ) || baseTex.Contains( "pbr/output", StringComparison.Ordinal )
			|| expLower.Contains( "pbr\\output", StringComparison.Ordinal ) || expLower.Contains( "pbr/output", StringComparison.Ordinal ) )
		{
			return true;
		}

		foreach ( VmtProxy proxy in vmt.Proxies )
		{
			string proxyName = proxy.Name.ToLowerInvariant();
			if ( proxyName.Contains( "mwenvmaptint", StringComparison.Ordinal )
				|| proxyName.Contains( "arc9envmaptint", StringComparison.Ordinal ) )
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsBftFormat( VmtFile vmt, string expTexture )
	{
		string expLower = expTexture.ToLowerInvariant();
		if ( expLower.EndsWith( "_e", StringComparison.Ordinal ) )
		{
			return true;
		}

		if ( vmt.GetBool( "$blendtintbybasealpha" ) )
		{
			Vector3 color2 = vmt.GetVector3( "$color2" );
			float brightness = color2.x + color2.y + color2.z;
			if ( brightness < 0.3f )
			{
				return true;
			}
		}

		float boost = vmt.GetFloat( "$phongboost" );
		if ( boost is >= 3f and <= 25f )
		{
			return true;
		}

		Vector3 fresnel = vmt.GetVector3( "$phongfresnelranges" );
		if ( !IsDefaultVector( fresnel ) )
		{
			bool metallic = ApproxEqual( fresnel.x, 0.87f, 0.1f )
				&& ApproxEqual( fresnel.y, 0.9f, 0.1f )
				&& ApproxEqual( fresnel.z, 1.0f, 0.1f );
			bool dielectric = fresnel.x < 0.2f && fresnel.y < 0.3f && fresnel.z > 0.8f;
			if ( metallic || dielectric )
			{
				return true;
			}
		}

		return false;
	}

	private static void ExtractExoPbrProperties( VmtFile vmt, ExtractedPbrProperties props )
	{
		props.ArmTexturePath = vmt.GetString( "$texture1" );
		props.ExoNormalPath = vmt.GetString( "$texture2" );
		props.EmissionTexturePath = vmt.GetString( "$texture3" );
		props.EmissionScale = vmt.GetFloat( "$emissionscale", 1f );
		props.Roughness = 0.5f;
		props.Metallic = 0f;
	}

	private static void ExtractGpbrProperties( VmtFile vmt, ExtractedPbrProperties props )
	{
		props.MraoTexturePath = vmt.GetString( "$mraotexture" );
		props.GpbrEmissionPath = vmt.GetString( "$emissiontexture" );
		props.GpbrEmissionScale = vmt.GetFloat( "$emissionscale", 1f );
		props.Roughness = 0.5f;
		props.Metallic = 0f;
	}

	private static void ExtractMwbProperties( ExtractedPbrProperties props )
	{
		props.Roughness = 0.5f;
		props.Metallic = 0f;
	}

	private static void ExtractBftProperties( VmtFile vmt, ExtractedPbrProperties props )
	{
		bool isTranslucent = vmt.Translucent;
		bool hasAlbedoTint = vmt.GetBool( "$phongalbedotint" );
		props.IsBftMetallicLayer = isTranslucent && hasAlbedoTint;

		bool hasBlendTint = vmt.GetBool( "$blendtintbybasealpha" );
		Vector3 color2 = vmt.GetVector3( "$color2" );
		if ( !IsDefaultVector( color2 ) )
		{
			props.BftColor2 = [color2.x, color2.y, color2.z];
			float brightness = color2.x + color2.y + color2.z;
			if ( hasBlendTint && brightness < 0.3f )
			{
				props.IsBftDiffuseLayer = true;
				props.HasAlphaMetallic = true;
			}
		}

		if ( props.IsBftMetallicLayer ) props.Metallic = 0.9f;
		else if ( props.IsBftDiffuseLayer ) props.Metallic = 0.5f;
		else props.Metallic = 0f;

		props.Roughness = 0.5f;
	}

	private static void ExtractMadIvanProperties( ExtractedPbrProperties props )
	{
		props.Roughness = 0.5f;
		props.Metallic = 0f;
	}

	private static void ExtractSourceProperties( ExtractedPbrProperties props )
	{
		props.Roughness = CalculateRoughness( props );
		props.Metallic = EstimateMetallic( props );
	}

	public static float CalculateRoughness( ExtractedPbrProperties props )
	{
		if ( !props.HasPhong )
		{
			return 1f;
		}

		float roughness = PhongExponentToRoughness( props.PhongExponent );
		if ( props.PhongBoost > 1f )
		{
			roughness *= 1f / MathF.Sqrt( props.PhongBoost );
		}

		if ( props.PhongFresnelRanges is { Length: >= 3 } )
		{
			float fresnelMax = props.PhongFresnelRanges[2];
			if ( fresnelMax > 0.5f )
			{
				roughness *= 1f - (fresnelMax - 0.5f) * 0.5f;
			}
		}

		return Math.Clamp( roughness, 0.04f, 1f );
	}

	public static float EstimateMetallic( ExtractedPbrProperties props )
	{
		if ( !props.HasEnvMap )
		{
			return 0f;
		}

		if ( props.EnvMapTint is { Length: >= 3 } )
		{
			float tintBrightness = (props.EnvMapTint[0] + props.EnvMapTint[1] + props.EnvMapTint[2]) / 3f;
			if ( tintBrightness > 0.5f )
			{
				return Math.Min( tintBrightness, 0.9f );
			}
		}

		if ( props.PhongFresnelRanges is { Length: >= 1 } && props.PhongFresnelRanges[0] > 0.5f )
		{
			return 0.8f;
		}

		return 0f;
	}

	public static float PhongExponentToRoughness( float phongExponent )
	{
		if ( phongExponent <= 0f )
		{
			return 1f;
		}

		float roughness = MathF.Sqrt( 2f / (phongExponent + 2f) );
		return Math.Clamp( roughness, 0.04f, 1f );
	}

	public static float BftExponentToRoughness( byte expValue )
	{
		float normalized = expValue / 255f;
		float gloss = MathF.Pow( normalized, 0.28f );
		float roughness = 1f - gloss;
		return Math.Clamp( roughness, 0.04f, 1f );
	}

	public static float MwbExponentToRoughness( byte expValue )
	{
		float normalized = expValue / 255f;
		float gloss = MathF.Pow( normalized, 0.25f );
		float roughness = 1f - gloss;
		return Math.Clamp( roughness, 0.04f, 1f );
	}

	private static bool ApproxEqual( float a, float b, float epsilon = 0.1f )
	{
		return MathF.Abs( a - b ) < epsilon;
	}

	private static bool IsDefaultVector( Vector3 value )
	{
		return MathF.Abs( value.x ) < 0.000001f
			&& MathF.Abs( value.y ) < 0.000001f
			&& MathF.Abs( value.z ) < 0.000001f;
	}
}

internal static class PbrTextureGenerator
{
	public static byte[] ExtractRoughnessFromArm( byte[] rgba, int width, int height )
	{
		var output = new byte[width * height * 4];
		int pixels = width * height;
		for ( int i = 0; i < pixels; i++ )
		{
			byte roughness = rgba[i * 4 + 1];
			output[i * 4 + 0] = roughness;
			output[i * 4 + 1] = roughness;
			output[i * 4 + 2] = roughness;
			output[i * 4 + 3] = 255;
		}
		return output;
	}

	public static byte[] ExtractMetallicFromArm( byte[] rgba, int width, int height )
	{
		var output = new byte[width * height * 4];
		int pixels = width * height;
		for ( int i = 0; i < pixels; i++ )
		{
			byte metallic = rgba[i * 4 + 2];
			output[i * 4 + 0] = metallic;
			output[i * 4 + 1] = metallic;
			output[i * 4 + 2] = metallic;
			output[i * 4 + 3] = 255;
		}
		return output;
	}

	public static byte[] ExtractAoFromArm( byte[] rgba, int width, int height )
	{
		var output = new byte[width * height * 4];
		int pixels = width * height;
		for ( int i = 0; i < pixels; i++ )
		{
			byte ao = rgba[i * 4 + 0];
			output[i * 4 + 0] = ao;
			output[i * 4 + 1] = ao;
			output[i * 4 + 2] = ao;
			output[i * 4 + 3] = 255;
		}
		return output;
	}

	public static byte[] ExtractRoughnessFromMrao( byte[] rgba, int width, int height )
	{
		return ExtractRoughnessFromArm( rgba, width, height );
	}

	public static byte[] ExtractMetallicFromMrao( byte[] rgba, int width, int height )
	{
		var output = new byte[width * height * 4];
		int pixels = width * height;
		for ( int i = 0; i < pixels; i++ )
		{
			byte metallic = rgba[i * 4 + 0];
			output[i * 4 + 0] = metallic;
			output[i * 4 + 1] = metallic;
			output[i * 4 + 2] = metallic;
			output[i * 4 + 3] = 255;
		}
		return output;
	}

	public static byte[] ExtractAoFromMrao( byte[] rgba, int width, int height )
	{
		var output = new byte[width * height * 4];
		int pixels = width * height;
		for ( int i = 0; i < pixels; i++ )
		{
			byte ao = rgba[i * 4 + 2];
			output[i * 4 + 0] = ao;
			output[i * 4 + 1] = ao;
			output[i * 4 + 2] = ao;
			output[i * 4 + 3] = 255;
		}
		return output;
	}

	public static byte[]? ExtractMetallicFromAlpha( byte[] rgba, int width, int height )
	{
		var output = new byte[width * height * 4];
		int pixels = width * height;

		byte minAlpha = 255;
		byte maxAlpha = 0;
		for ( int i = 0; i < pixels; i += 8 )
		{
			byte a = rgba[i * 4 + 3];
			if ( a < minAlpha ) minAlpha = a;
			if ( a > maxAlpha ) maxAlpha = a;
		}

		if ( maxAlpha - minAlpha < 10 )
		{
			return null;
		}

		for ( int i = 0; i < pixels; i++ )
		{
			byte metallic = rgba[i * 4 + 3];
			output[i * 4 + 0] = metallic;
			output[i * 4 + 1] = metallic;
			output[i * 4 + 2] = metallic;
			output[i * 4 + 3] = 255;
		}

		return output;
	}

	public static byte[] ConvertBftExponentToRoughness( byte[] rgba, int width, int height )
	{
		var output = new byte[width * height * 4];
		int pixels = width * height;
		for ( int i = 0; i < pixels; i++ )
		{
			byte expValue = rgba[i * 4 + 0];
			float roughness = PseudoPbrFormats.BftExponentToRoughness( expValue );
			byte roughByte = (byte)(roughness * 255f);
			output[i * 4 + 0] = roughByte;
			output[i * 4 + 1] = roughByte;
			output[i * 4 + 2] = roughByte;
			output[i * 4 + 3] = 255;
		}
		return output;
	}

	public static byte[] ConvertMwbExponentToRoughness( byte[] rgba, int width, int height )
	{
		var output = new byte[width * height * 4];
		int pixels = width * height;
		for ( int i = 0; i < pixels; i++ )
		{
			byte expValue = rgba[i * 4 + 0];
			float roughness = PseudoPbrFormats.MwbExponentToRoughness( expValue );
			byte roughByte = (byte)(roughness * 255f);
			output[i * 4 + 0] = roughByte;
			output[i * 4 + 1] = roughByte;
			output[i * 4 + 2] = roughByte;
			output[i * 4 + 3] = 255;
		}
		return output;
	}

	public static byte[] ConvertPhongExponentToRoughness( byte[] rgba, int width, int height )
	{
		var output = new byte[width * height * 4];
		int pixels = width * height;
		for ( int i = 0; i < pixels; i++ )
		{
			byte expValue = rgba[i * 4 + 0];
			float phongExp = 1f + (expValue / 255f) * 149f;
			float roughness = PseudoPbrFormats.PhongExponentToRoughness( phongExp );
			byte roughByte = (byte)(roughness * 255f);
			output[i * 4 + 0] = roughByte;
			output[i * 4 + 1] = roughByte;
			output[i * 4 + 2] = roughByte;
			output[i * 4 + 3] = 255;
		}
		return output;
	}

	public static byte[] FlipNormalMapGreen( byte[] rgba, int width, int height )
	{
		var output = new byte[rgba.Length];
		Array.Copy( rgba, output, rgba.Length );
		int pixels = width * height;
		for ( int i = 0; i < pixels; i++ )
		{
			output[i * 4 + 1] = (byte)(255 - output[i * 4 + 1]);
		}
		return output;
	}

	public static byte[] GenerateConstant( int width, int height, byte value )
	{
		var output = new byte[width * height * 4];
		for ( int i = 0; i < width * height; i++ )
		{
			output[i * 4 + 0] = value;
			output[i * 4 + 1] = value;
			output[i * 4 + 2] = value;
			output[i * 4 + 3] = 255;
		}
		return output;
	}
}
