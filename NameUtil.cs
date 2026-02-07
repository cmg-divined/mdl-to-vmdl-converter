internal static class NameUtil
{
	public static string CleanName( string? value, string fallback )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
		{
			return fallback;
		}

		string cleaned = value.Trim();
		cleaned = cleaned.Replace( "\\", "_" ).Replace( "/", "_" ).Replace( '"', '_' );
		cleaned = cleaned.Replace( ".smd", string.Empty, StringComparison.OrdinalIgnoreCase );
		cleaned = cleaned.Replace( ".dmx", string.Empty, StringComparison.OrdinalIgnoreCase );
		cleaned = cleaned.Replace( ".fbx", string.Empty, StringComparison.OrdinalIgnoreCase );
		while ( cleaned.Contains( "__", StringComparison.Ordinal ) )
		{
			cleaned = cleaned.Replace( "__", "_", StringComparison.Ordinal );
		}
		return string.IsNullOrWhiteSpace( cleaned ) ? fallback : cleaned;
	}

	public static string CleanFileName( string fileName )
	{
		string cleaned = fileName;
		foreach ( char invalid in Path.GetInvalidFileNameChars() )
		{
			cleaned = cleaned.Replace( invalid, '_' );
		}
		return cleaned;
	}

	public static string CleanMaterialName( string material )
	{
		string result = (material ?? string.Empty).Trim().Replace( '\\', '/' );
		if ( string.IsNullOrWhiteSpace( result ) )
		{
			return "default";
		}
		return result;
	}
}