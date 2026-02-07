namespace GModMount.Source;

/// <summary>
/// Extension methods for BinaryReader to read Source Engine structures.
/// </summary>
public static class BinaryReaderExtensions
{
	/// <summary>
	/// Read a struct from the binary reader.
	/// </summary>
	public static unsafe T ReadStruct<T>( this BinaryReader reader ) where T : unmanaged
	{
		int size = sizeof( T );
		long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
		
		if ( remaining < size )
		{
			throw new EndOfStreamException( $"Cannot read {typeof( T ).Name} ({size} bytes): only {remaining} bytes remaining in stream" );
		}
		
		byte[] bytes = reader.ReadBytes( size );

		fixed ( byte* ptr = bytes )
		{
			return *(T*)ptr;
		}
	}

	/// <summary>
	/// Try to read a struct, returning default if not enough bytes.
	/// </summary>
	public static unsafe bool TryReadStruct<T>( this BinaryReader reader, out T result ) where T : unmanaged
	{
		int size = sizeof( T );
		long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
		
		if ( remaining < size )
		{
			result = default;
			return false;
		}
		
		byte[] bytes = reader.ReadBytes( size );

		fixed ( byte* ptr = bytes )
		{
			result = *(T*)ptr;
		}
		return true;
	}

	/// <summary>
	/// Read an array of structs from the binary reader.
	/// </summary>
	public static unsafe T[] ReadStructArray<T>( this BinaryReader reader, int count ) where T : unmanaged
	{
		if ( count <= 0 )
			return Array.Empty<T>();

		int size = sizeof( T ) * count;
		byte[] bytes = reader.ReadBytes( size );
		T[] result = new T[count];

		fixed ( byte* ptr = bytes )
		fixed ( T* dst = result )
		{
			Buffer.MemoryCopy( ptr, dst, size, size );
		}

		return result;
	}

	/// <summary>
	/// Read a null-terminated string at a specific offset.
	/// </summary>
	public static string ReadStringAtOffset( this BinaryReader reader, long baseOffset, int relativeOffset )
	{
		if ( relativeOffset == 0 )
			return string.Empty;

		long targetPos = baseOffset + relativeOffset;
		long fileLength = reader.BaseStream.Length;
		
		// Bounds check
		if ( targetPos < 0 || targetPos >= fileLength )
			return string.Empty;

		long currentPos = reader.BaseStream.Position;
		reader.BaseStream.Position = targetPos;

		var sb = new StringBuilder();
		while ( reader.BaseStream.Position < fileLength )
		{
			byte b = reader.ReadByte();
			if ( b == 0 )
				break;
			sb.Append( (char)b );
		}

		reader.BaseStream.Position = currentPos;
		return sb.ToString();
	}

	/// <summary>
	/// Read a null-terminated string at the current position.
	/// </summary>
	public static string ReadNullTerminatedString( this BinaryReader reader )
	{
		var sb = new StringBuilder();
		byte b;
		while ( (b = reader.ReadByte()) != 0 )
		{
			sb.Append( (char)b );
		}
		return sb.ToString();
	}

	/// <summary>
	/// Read a fixed-length string (null-padded).
	/// </summary>
	public static string ReadFixedString( this BinaryReader reader, int length )
	{
		byte[] bytes = reader.ReadBytes( length );
		int end = Array.IndexOf( bytes, (byte)0 );
		if ( end < 0 ) end = length;
		return Encoding.ASCII.GetString( bytes, 0, end );
	}

	/// <summary>
	/// Read a Vector3.
	/// </summary>
	public static Vector3 ReadVector3( this BinaryReader reader )
	{
		return new Vector3(
			reader.ReadSingle(),
			reader.ReadSingle(),
			reader.ReadSingle()
		);
	}

	/// <summary>
	/// Read a Vector2.
	/// </summary>
	public static Vector2 ReadVector2( this BinaryReader reader )
	{
		return new Vector2(
			reader.ReadSingle(),
			reader.ReadSingle()
		);
	}

	/// <summary>
	/// Read a Quaternion.
	/// </summary>
	public static Quaternion ReadQuaternion( this BinaryReader reader )
	{
		return new Quaternion(
			reader.ReadSingle(),
			reader.ReadSingle(),
			reader.ReadSingle(),
			reader.ReadSingle()
		);
	}

	/// <summary>
	/// Read a 3x4 matrix as an array of 12 floats.
	/// </summary>
	public static float[] ReadMatrix3x4( this BinaryReader reader )
	{
		float[] matrix = new float[12];
		for ( int i = 0; i < 12; i++ )
		{
			matrix[i] = reader.ReadSingle();
		}
		return matrix;
	}
}
