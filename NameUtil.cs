using System.Text;

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

	public static string CleanNodeName( string? value, string fallback )
	{
		string cleaned = CleanName( value, fallback );
		var sb = new StringBuilder( cleaned.Length );
		foreach ( char c in cleaned )
		{
			bool isAlphaNum = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
			sb.Append( isAlphaNum || c == '_' ? c : '_' );
		}

		cleaned = sb.ToString().Trim( '_' );
		while ( cleaned.Contains( "__", StringComparison.Ordinal ) )
		{
			cleaned = cleaned.Replace( "__", "_", StringComparison.Ordinal );
		}

		if ( string.IsNullOrWhiteSpace( cleaned ) )
		{
			cleaned = fallback;
		}

		if ( cleaned.Length > 0 && cleaned[0] >= '0' && cleaned[0] <= '9' )
		{
			cleaned = "_" + cleaned;
		}

		return cleaned;
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
