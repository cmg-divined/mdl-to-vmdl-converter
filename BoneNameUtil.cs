using System.Text;
using GModMount.Source;

internal static class BoneNameUtil
{
	public static IReadOnlyList<string> BuildExportBoneNames( IReadOnlyList<MdlBone> bones )
	{
		var result = new string[bones.Count];
		var used = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		for ( int i = 0; i < bones.Count; i++ )
		{
			string baseName = CanonicalizeForModelDoc( SanitizeBoneName( bones[i].Name, $"bone_{i}" ) );
			string unique = baseName;
			int suffix = 1;
			while ( !used.Add( unique ) )
			{
				suffix++;
				unique = $"{baseName}_{suffix}";
			}

			result[i] = unique;
		}

		return result;
	}

	public static string CanonicalizeForModelDoc( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
		{
			return value;
		}

		string normalized = value;
		const string valveBipedPrefix = "valvebiped_";
		if ( normalized.StartsWith( valveBipedPrefix, StringComparison.OrdinalIgnoreCase ) )
		{
			normalized = normalized.Substring( valveBipedPrefix.Length );
		}

		return normalized.ToLowerInvariant();
	}

	public static string SanitizeBoneName( string? value, string fallback )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
		{
			return fallback;
		}

		var sb = new StringBuilder( value.Length );
		bool lastUnderscore = false;
		foreach ( char ch in value.Trim() )
		{
			char outChar = char.IsLetterOrDigit( ch ) || ch == '_' ? ch : '_';
			if ( outChar == '_' )
			{
				if ( lastUnderscore )
				{
					continue;
				}
				lastUnderscore = true;
			}
			else
			{
				lastUnderscore = false;
			}

			sb.Append( outChar );
		}

		string cleaned = sb.ToString().Trim( '_' );
		if ( string.IsNullOrWhiteSpace( cleaned ) )
		{
			return fallback;
		}

		if ( char.IsDigit( cleaned[0] ) )
		{
			cleaned = "b_" + cleaned;
		}

		return cleaned;
	}
}
