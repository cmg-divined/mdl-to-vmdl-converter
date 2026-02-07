internal static class TgaWriter
{
	public static void WriteRgba32( string path, int width, int height, byte[] rgba )
	{
		if ( width <= 0 || height <= 0 )
		{
			throw new ArgumentOutOfRangeException( nameof( width ), "Image dimensions must be positive." );
		}

		int expected = width * height * 4;
		if ( rgba.Length < expected )
		{
			throw new ArgumentException( $"RGBA data too small ({rgba.Length} < {expected})" );
		}

		Directory.CreateDirectory( Path.GetDirectoryName( path ) ?? "." );

		using var stream = new FileStream( path, FileMode.Create, FileAccess.Write, FileShare.None );
		using var writer = new BinaryWriter( stream, Encoding.ASCII, leaveOpen: false );

		writer.Write( (byte)0 );
		writer.Write( (byte)0 );
		writer.Write( (byte)2 );
		writer.Write( (ushort)0 );
		writer.Write( (ushort)0 );
		writer.Write( (byte)0 );
		writer.Write( (ushort)0 );
		writer.Write( (ushort)0 );
		writer.Write( (ushort)width );
		writer.Write( (ushort)height );
		writer.Write( (byte)32 );
		writer.Write( (byte)0x28 );

		for ( int i = 0; i < width * height; i++ )
		{
			int offset = i * 4;
			byte r = rgba[offset + 0];
			byte g = rgba[offset + 1];
			byte b = rgba[offset + 2];
			byte a = rgba[offset + 3];
			writer.Write( b );
			writer.Write( g );
			writer.Write( r );
			writer.Write( a );
		}
	}
}
