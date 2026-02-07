// GMod PBR Material Shader - Opaque, Double-sided
// For materials with $nocull (hair, foliage, etc.)

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
	Depth();
}

COMMON
{
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

PS
{
	#define CUSTOM_MATERIAL_INPUTS
	#include "common/pixel.hlsl"
	
	// Disable backface culling (render both sides)
	BoolAttribute( doublesided, true );
	RenderState( CullMode, NONE );
	
	// Texture inputs
	CreateInputTexture2D( Color, Srgb, 8, "", "_color", "Material,10/10", Default3( 1.0, 1.0, 1.0 ) );
	CreateInputTexture2D( Normal, Linear, 8, "NormalizeNormals", "_normal", "Material,10/20", Default3( 0.5, 0.5, 1.0 ) );
	CreateInputTexture2D( Roughness, Linear, 8, "", "_rough", "Material,10/30", Default( 0.5 ) );
	CreateInputTexture2D( Metalness, Linear, 8, "", "_metal", "Material,10/40", Default( 0.0 ) );
	CreateInputTexture2D( AmbientOcclusion, Linear, 8, "", "_ao", "Material,10/50", Default( 1.0 ) );
	
	// Create Texture2D samplers
	Texture2D g_tColor < Channel( RGBA, Box( Color ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	Texture2D g_tNormal < Channel( RGB, Box( Normal ), Linear ); OutputFormat( DXT5 ); SrgbRead( false ); >;
	Texture2D g_tRoughness < Channel( R, Box( Roughness ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tMetalness < Channel( R, Box( Metalness ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	Texture2D g_tAmbientOcclusion < Channel( R, Box( AmbientOcclusion ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	
	// Scalar parameters
	float g_flRoughnessScaleFactor < UiType( Slider ); Default( 1.0 ); Range( 0.0, 2.0 ); UiGroup( "Material,10/31" ); >;
	float g_flMetalnessScale < UiType( Slider ); Default( 1.0 ); Range( 0.0, 1.0 ); UiGroup( "Material,10/41" ); >;
	float g_flAlphaTestReference < UiType( Slider ); Default( 0.0 ); Range( 0.0, 1.0 ); UiGroup( "Material,10/60" ); >;
	
	// Color tinting ($color2 support)
	float3 g_vColorTint < UiType( Color ); Default3( 1.0, 1.0, 1.0 ); UiGroup( "Material,10/15" ); >;
	float g_flBlendTintByBaseAlpha < UiType( Slider ); Default( 0.0 ); Range( 0.0, 1.0 ); UiGroup( "Material,10/16" ); >;

	float4 MainPs( PixelInput i, bool isFrontFace : SV_IsFrontFace ) : SV_Target0
	{
		float2 uv = i.vTextureCoords.xy;
		
		// Sample textures
		float4 colorSample = g_tColor.Sample( g_sAniso, uv );
		float3 albedo = colorSample.rgb;
		float alpha = colorSample.a;
		
		// Apply color tinting
		if ( g_flBlendTintByBaseAlpha > 0.0 )
		{
			float3 tintedColor = albedo * g_vColorTint;
			albedo = lerp( albedo, tintedColor, alpha * g_flBlendTintByBaseAlpha );
		}
		else
		{
			albedo *= g_vColorTint;
		}
		
		// Alpha test - discard pixels below threshold
		if ( g_flAlphaTestReference > 0.0 && alpha < g_flAlphaTestReference )
			discard;
		
		float3 normal = DecodeNormal( g_tNormal.Sample( g_sAniso, uv ).rgb );
		float roughness = g_tRoughness.Sample( g_sAniso, uv ).r * g_flRoughnessScaleFactor;
		float metalness = g_tMetalness.Sample( g_sAniso, uv ).r * g_flMetalnessScale;
		float ao = g_tAmbientOcclusion.Sample( g_sAniso, uv ).r;
		
		// Transform normal from tangent to world space
		float3 normalWs = TransformNormal( normal, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
		
		// Flip normal for back faces so they get lit correctly
		if ( !isFrontFace )
			normalWs = -normalWs;
		
		// Initialize material
		Material mat = Material::Init();
		mat.Albedo = albedo;
		mat.Normal = normalWs;
		mat.Roughness = roughness;
		mat.Metalness = metalness;
		mat.AmbientOcclusion = ao;
		
		return ShadingModelStandard::Shade( i, mat );
	}
}
