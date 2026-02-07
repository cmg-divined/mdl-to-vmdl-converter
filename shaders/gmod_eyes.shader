// Simple Eye Shader
// Projects iris texture onto eyeball mesh using world position

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
	
	// Eye center in world space
	float3 g_vEyeOrigin < UiGroup( "Eye,10/10" ); Default3( 0.0, 0.0, 0.0 ); >;
	
	// Iris projection - projects world position to UV
	// UV.x = dot(ProjectionU.xyz, worldPos) + ProjectionU.w
	// UV.y = dot(ProjectionV.xyz, worldPos) + ProjectionV.w  
	float4 g_vIrisProjectionU < UiGroup( "Eye,10/20" ); Default4( 0.0, 1.0, 0.0, 0.5 ); >;
	float4 g_vIrisProjectionV < UiGroup( "Eye,10/30" ); Default4( 0.0, 0.0, 1.0, 0.5 ); >;
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
		
		// Calculate spherical normal from eye center
		float3 sphereNormal = normalize( o.vPositionWs - g_vEyeOrigin );
		o.vNormalWs = sphereNormal;
		
		return FinalizeVertex( o );
	}
}

PS
{
	#define CUSTOM_MATERIAL_INPUTS
	#include "common/pixel.hlsl"

	// Iris texture
	CreateInputTexture2D( Iris, Srgb, 8, "", "_iris", "Eye,10/50", Default3( 1.0, 1.0, 1.0 ) );
	Texture2D g_tIris < Channel( RGBA, Box( Iris ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); AddressU( CLAMP ); AddressV( CLAMP ); >;

	// Cornea normal map (optional)
	CreateInputTexture2D( Cornea, Linear, 8, "", "_cornea", "Eye,10/60", Default3( 0.5, 0.5, 1.0 ) );
	Texture2D g_tCornea < Channel( RGBA, Box( Cornea ), Linear ); OutputFormat( DXT5 ); SrgbRead( false ); AddressU( CLAMP ); AddressV( CLAMP ); >;

	float g_flGlossiness < UiGroup( "Eye,10/70" ); Default( 0.8 ); Range( 0.0, 1.0 ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		// Get world position
		float3 worldPos = i.vPositionWithOffsetWs.xyz + g_vCameraPositionWs;
		
		// Project world position to iris UV
		float2 irisUv;
		irisUv.x = dot( g_vIrisProjectionU.xyz, worldPos ) + g_vIrisProjectionU.w;
		irisUv.y = dot( g_vIrisProjectionV.xyz, worldPos ) + g_vIrisProjectionV.w;
		
		// Sample iris texture
		float4 irisColor = g_tIris.Sample( g_sAniso, irisUv );
		
		// Use mesh normal (spherical from VS)
		float3 normal = normalize( i.vNormalWs );
		
		// Simple material setup
		Material mat = Material::Init();
		mat.Albedo = irisColor.rgb;
		mat.Normal = normal;
		mat.Roughness = 1.0 - g_flGlossiness;
		mat.Metalness = 0.0;
		mat.AmbientOcclusion = 1.0;

		return ShadingModelStandard::Shade( i, mat );
	}
}
