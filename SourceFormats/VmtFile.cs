namespace GModMount.Source;

/// <summary>
/// Parsed VMT (Valve Material Type) file.
/// </summary>
public class VmtFile
{
	/// <summary>
	/// Shader name (e.g., "VertexLitGeneric", "LightmappedGeneric").
	/// </summary>
	public string Shader { get; internal set; }

	/// <summary>
	/// Material parameters as key-value pairs.
	/// </summary>
	public Dictionary<string, string> Parameters { get; } = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>
	/// Proxy definitions.
	/// </summary>
	public List<VmtProxy> Proxies { get; } = new();

	/// <summary>
	/// Get a parameter value as a string.
	/// </summary>
	public string GetString( string key, string defaultValue = "" )
	{
		return Parameters.TryGetValue( key, out var value ) ? value : defaultValue;
	}

	/// <summary>
	/// Get a parameter value as a float.
	/// </summary>
	public float GetFloat( string key, float defaultValue = 0f )
	{
		if ( Parameters.TryGetValue( key, out var value ) && float.TryParse( value, out float result ) )
			return result;
		return defaultValue;
	}

	/// <summary>
	/// Get a parameter value as an int.
	/// </summary>
	public int GetInt( string key, int defaultValue = 0 )
	{
		if ( Parameters.TryGetValue( key, out var value ) && int.TryParse( value, out int result ) )
			return result;
		return defaultValue;
	}

	/// <summary>
	/// Get a parameter value as a bool.
	/// </summary>
	public bool GetBool( string key, bool defaultValue = false )
	{
		if ( Parameters.TryGetValue( key, out var value ) )
		{
			value = value.Trim();
			return value == "1" || value.Equals( "true", StringComparison.OrdinalIgnoreCase );
		}
		return defaultValue;
	}

	/// <summary>
	/// Get a parameter value as a Vector3 (color or vector).
	/// </summary>
	public Vector3 GetVector3( string key, Vector3 defaultValue = default )
	{
		if ( !Parameters.TryGetValue( key, out var value ) )
			return defaultValue;

		// Handle formats: "[1 1 1]", "{ 1 1 1 }", "1 1 1"
		value = value.Trim( '[', ']', '{', '}', ' ' );
		var parts = value.Split( new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries );

		if ( parts.Length >= 3 &&
			 float.TryParse( parts[0], out float x ) &&
			 float.TryParse( parts[1], out float y ) &&
			 float.TryParse( parts[2], out float z ) )
		{
			return new Vector3( x, y, z );
		}

		return defaultValue;
	}

	/// <summary>
	/// Common texture parameters.
	/// </summary>
	public string BaseTexture => GetString( "$basetexture" );
	public string BumpMap => GetString( "$bumpmap" );
	public string NormalMap => GetString( "$normalmap" );
	public string EnvMap => GetString( "$envmap" );
	public string DetailTexture => GetString( "$detail" );
	public string BaseTexture2 => GetString( "$basetexture2" );

	/// <summary>
	/// Common material properties.
	/// </summary>
	public string SurfaceProp => GetString( "$surfaceprop" );
	public bool Translucent => GetBool( "$translucent" );
	public bool AlphaTest => GetBool( "$alphatest" );
	public bool NoCull => GetBool( "$nocull" );
	public bool Additive => GetBool( "$additive" );
	public float Alpha => GetFloat( "$alpha", 1f );

	/// <summary>
	/// Load a VMT file from a string.
	/// </summary>
	public static VmtFile Load( string content )
	{
		var vmt = new VmtFile();
		var parser = new VmtParser( content );
		parser.Parse( vmt );
		return vmt;
	}

	/// <summary>
	/// Load a VMT file from a byte array.
	/// </summary>
	public static VmtFile Load( byte[] data )
	{
		string content = Encoding.UTF8.GetString( data );
		return Load( content );
	}
}

/// <summary>
/// VMT proxy definition.
/// </summary>
public class VmtProxy
{
	public string Name { get; set; }
	public Dictionary<string, string> Parameters { get; } = new( StringComparer.OrdinalIgnoreCase );
}

/// <summary>
/// Simple VMT file parser.
/// </summary>
internal class VmtParser
{
	private readonly string _content;
	private int _position;

	public VmtParser( string content )
	{
		_content = content;
		_position = 0;
	}

	public void Parse( VmtFile vmt )
	{
		SkipWhitespaceAndComments();

		// Read shader name (first token)
		vmt.Shader = ReadToken().Trim( '"' );

		SkipWhitespaceAndComments();

		// Expect opening brace
		if ( Peek() != '{' )
		{
			Log.Warning( $"VMT: Expected '{{' after shader name, got '{Peek()}'" );
			return;
		}
		_position++;

		// Read parameters
		ParseBlock( vmt.Parameters, vmt.Proxies );
	}

	private void ParseBlock( Dictionary<string, string> parameters, List<VmtProxy> proxies )
	{
		while ( _position < _content.Length )
		{
			SkipWhitespaceAndComments();

			if ( _position >= _content.Length )
				break;

			char c = Peek();

			if ( c == '}' )
			{
				_position++;
				return;
			}

			// Read key
			string key = ReadToken().Trim( '"' );
			if ( string.IsNullOrEmpty( key ) )
				continue;

			SkipWhitespaceAndComments();

			// Check for nested block (e.g., Proxies, $fallback)
			if ( Peek() == '{' )
			{
				_position++;

				if ( key.Equals( "Proxies", StringComparison.OrdinalIgnoreCase ) )
				{
					// Parse proxies block
					ParseProxies( proxies );
				}
				else
				{
					// Skip unknown nested block
					SkipBlock();
				}
			}
			else
			{
				// Read value
				string value = ReadToken().Trim( '"' );
				parameters[key] = value;
			}
		}
	}

	private void ParseProxies( List<VmtProxy> proxies )
	{
		while ( _position < _content.Length )
		{
			SkipWhitespaceAndComments();

			if ( Peek() == '}' )
			{
				_position++;
				return;
			}

			string proxyName = ReadToken().Trim( '"' );
			if ( string.IsNullOrEmpty( proxyName ) )
				continue;

			SkipWhitespaceAndComments();

			if ( Peek() == '{' )
			{
				_position++;

				var proxy = new VmtProxy { Name = proxyName };
				ParseBlock( proxy.Parameters, new List<VmtProxy>() );
				proxies.Add( proxy );
			}
		}
	}

	private void SkipBlock()
	{
		int depth = 1;
		while ( _position < _content.Length && depth > 0 )
		{
			char c = _content[_position++];
			if ( c == '{' ) depth++;
			else if ( c == '}' ) depth--;
		}
	}

	private string ReadToken()
	{
		SkipWhitespaceAndComments();

		if ( _position >= _content.Length )
			return string.Empty;

		var sb = new StringBuilder();

		// Check for quoted string
		if ( Peek() == '"' )
		{
			_position++;
			while ( _position < _content.Length && _content[_position] != '"' )
			{
				sb.Append( _content[_position++] );
			}
			if ( _position < _content.Length )
				_position++; // Skip closing quote
		}
		else
		{
			// Read unquoted token
			while ( _position < _content.Length )
			{
				char c = _content[_position];
				if ( char.IsWhiteSpace( c ) || c == '{' || c == '}' || c == '"' )
					break;
				sb.Append( c );
				_position++;
			}
		}

		return sb.ToString();
	}

	private void SkipWhitespaceAndComments()
	{
		while ( _position < _content.Length )
		{
			char c = _content[_position];

			if ( char.IsWhiteSpace( c ) )
			{
				_position++;
				continue;
			}

			// Check for comment
			if ( c == '/' && _position + 1 < _content.Length && _content[_position + 1] == '/' )
			{
				// Skip to end of line
				while ( _position < _content.Length && _content[_position] != '\n' )
					_position++;
				continue;
			}

			break;
		}
	}

	private char Peek()
	{
		return _position < _content.Length ? _content[_position] : '\0';
	}
}
