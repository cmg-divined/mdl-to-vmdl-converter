namespace GModMount.Source;

/// <summary>
/// VTF (Valve Texture Format) image format enum.
/// </summary>
public enum VtfImageFormat
{
	None = -1,
	RGBA8888 = 0,
	ABGR8888 = 1,
	RGB888 = 2,
	BGR888 = 3,
	RGB565 = 4,
	I8 = 5,
	IA88 = 6,
	P8 = 7,
	A8 = 8,
	RGB888_BlueScreen = 9,
	BGR888_BlueScreen = 10,
	ARGB8888 = 11,
	BGRA8888 = 12,
	DXT1 = 13,
	DXT3 = 14,
	DXT5 = 15,
	BGRX8888 = 16,
	BGR565 = 17,
	BGRX5551 = 18,
	BGRA4444 = 19,
	DXT1_OneBitAlpha = 20,
	BGRA5551 = 21,
	UV88 = 22,
	UVWQ8888 = 23,
	RGBA16161616F = 24,
	RGBA16161616 = 25,
	UVLX8888 = 26
}

/// <summary>
/// VTF texture flags.
/// </summary>
[Flags]
public enum VtfFlags : uint
{
	None = 0,
	PointSample = 0x00000001,
	Trilinear = 0x00000002,
	ClampS = 0x00000004,
	ClampT = 0x00000008,
	Anisotropic = 0x00000010,
	HintDxt5 = 0x00000020,
	Srgb = 0x00000040,
	Normal = 0x00000080,
	NoMip = 0x00000100,
	NoLod = 0x00000200,
	AllMips = 0x00000400,
	Procedural = 0x00000800,
	OneBitAlpha = 0x00001000,
	EightBitAlpha = 0x00002000,
	EnvMap = 0x00004000,
	RenderTarget = 0x00008000,
	DepthRenderTarget = 0x00010000,
	NoDebugOverride = 0x00020000,
	SingleCopy = 0x00040000,
	PreSrgb = 0x00080000,
	NoDepthBuffer = 0x00800000,
	ClampU = 0x02000000,
	VertexTexture = 0x04000000,
	SsBump = 0x08000000,
	Border = 0x20000000
}

/// <summary>
/// VTF file header (version 7.2+).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct VtfHeader
{
	public uint Signature;          // "VTF\0"
	public uint VersionMajor;
	public uint VersionMinor;
	public uint HeaderSize;
	public ushort Width;
	public ushort Height;
	public VtfFlags Flags;
	public ushort Frames;
	public ushort FirstFrame;
	public uint Padding0;
	public float ReflectivityR;
	public float ReflectivityG;
	public float ReflectivityB;
	public uint Padding1;
	public float BumpScale;
	public VtfImageFormat HighResFormat;
	public byte MipCount;
	public VtfImageFormat LowResFormat;
	public byte LowResWidth;
	public byte LowResHeight;
	// 7.2+
	public ushort Depth;
	// 7.3+
	public byte Padding2_0;
	public byte Padding2_1;
	public byte Padding2_2;
	public uint NumResources;

	public readonly bool IsValid => Signature == 0x00465456; // "VTF\0"
	public readonly float Version => VersionMajor + VersionMinor / 10f;
}

/// <summary>
/// Parsed VTF file.
/// </summary>
public class VtfFile
{
	public VtfHeader Header { get; private set; }
	public int Width => Header.Width;
	public int Height => Header.Height;
	public VtfImageFormat Format => Header.HighResFormat;
	public int MipCount => Header.MipCount;
	public int FrameCount => Math.Max( 1, (int)Header.Frames );
	public VtfFlags Flags => Header.Flags;

	/// <summary>
	/// Raw image data for all mip levels (smallest to largest).
	/// </summary>
	public byte[][] MipData { get; private set; }

	/// <summary>
	/// Low-res thumbnail data.
	/// </summary>
	public byte[] ThumbnailData { get; private set; }

	/// <summary>
	/// Load a VTF file from a byte array.
	/// </summary>
	public static VtfFile Load( byte[] data )
	{
		using var stream = new MemoryStream( data );
		using var reader = new BinaryReader( stream );
		return Load( reader, data );
	}

	/// <summary>
	/// Load a VTF file from a binary reader.
	/// </summary>
	public static VtfFile Load( BinaryReader reader, byte[] fullData )
	{
		var vtf = new VtfFile();

		// Read header
		vtf.Header = reader.ReadStruct<VtfHeader>();

		if ( !vtf.Header.IsValid )
		{
			throw new InvalidDataException( $"Invalid VTF file: signature 0x{vtf.Header.Signature:X8}" );
		}

		// Calculate data offset based on version
		int dataOffset;
		if ( vtf.Header.VersionMajor >= 7 && vtf.Header.VersionMinor >= 3 )
		{
			// Version 7.3+: data starts after header
			dataOffset = (int)vtf.Header.HeaderSize;
		}
		else
		{
			// Version 7.2 and earlier
			dataOffset = (int)vtf.Header.HeaderSize;
		}

		// Read low-res thumbnail
		if ( vtf.Header.LowResFormat != VtfImageFormat.None && vtf.Header.LowResWidth > 0 && vtf.Header.LowResHeight > 0 )
		{
			int thumbSize = CalculateImageSize( vtf.Header.LowResFormat, vtf.Header.LowResWidth, vtf.Header.LowResHeight );
			reader.BaseStream.Position = dataOffset;
			vtf.ThumbnailData = reader.ReadBytes( thumbSize );
			dataOffset += thumbSize;
		}

		// Read mip levels (stored smallest to largest)
		vtf.MipData = new byte[vtf.Header.MipCount][];
		int currentOffset = fullData.Length;

		// Calculate offsets from end of file (largest mip is at the end)
		for ( int mip = 0; mip < vtf.Header.MipCount; mip++ )
		{
			int mipWidth = Math.Max( 1, vtf.Width >> mip );
			int mipHeight = Math.Max( 1, vtf.Height >> mip );
			int mipSize = CalculateImageSize( vtf.Header.HighResFormat, mipWidth, mipHeight );
			mipSize *= vtf.FrameCount; // Multiply by frame count for animated textures

			currentOffset -= mipSize;
			vtf.MipData[mip] = new byte[mipSize];
			Array.Copy( fullData, currentOffset, vtf.MipData[mip], 0, mipSize );
		}

		return vtf;
	}

	/// <summary>
	/// Calculate the size in bytes for an image in a given format.
	/// </summary>
	public static int CalculateImageSize( VtfImageFormat format, int width, int height )
	{
		// Ensure minimum dimensions for block-compressed formats
		if ( format == VtfImageFormat.DXT1 || format == VtfImageFormat.DXT1_OneBitAlpha ||
			 format == VtfImageFormat.DXT3 || format == VtfImageFormat.DXT5 )
		{
			width = Math.Max( 4, width );
			height = Math.Max( 4, height );
		}

		return format switch
		{
			VtfImageFormat.RGBA8888 => width * height * 4,
			VtfImageFormat.ABGR8888 => width * height * 4,
			VtfImageFormat.RGB888 => width * height * 3,
			VtfImageFormat.BGR888 => width * height * 3,
			VtfImageFormat.RGB565 => width * height * 2,
			VtfImageFormat.I8 => width * height,
			VtfImageFormat.IA88 => width * height * 2,
			VtfImageFormat.P8 => width * height,
			VtfImageFormat.A8 => width * height,
			VtfImageFormat.ARGB8888 => width * height * 4,
			VtfImageFormat.BGRA8888 => width * height * 4,
			VtfImageFormat.DXT1 => ((width + 3) / 4) * ((height + 3) / 4) * 8,
			VtfImageFormat.DXT1_OneBitAlpha => ((width + 3) / 4) * ((height + 3) / 4) * 8,
			VtfImageFormat.DXT3 => ((width + 3) / 4) * ((height + 3) / 4) * 16,
			VtfImageFormat.DXT5 => ((width + 3) / 4) * ((height + 3) / 4) * 16,
			VtfImageFormat.BGRX8888 => width * height * 4,
			VtfImageFormat.BGR565 => width * height * 2,
			VtfImageFormat.BGRX5551 => width * height * 2,
			VtfImageFormat.BGRA4444 => width * height * 2,
			VtfImageFormat.BGRA5551 => width * height * 2,
			VtfImageFormat.UV88 => width * height * 2,
			VtfImageFormat.UVWQ8888 => width * height * 4,
			VtfImageFormat.RGBA16161616F => width * height * 8,
			VtfImageFormat.RGBA16161616 => width * height * 8,
			VtfImageFormat.UVLX8888 => width * height * 4,
			_ => width * height * 4 // Default to RGBA
		};
	}

	/// <summary>
	/// Get the largest mip level data (highest resolution).
	/// </summary>
	public byte[] GetLargestMipData()
	{
		if ( MipData == null || MipData.Length == 0 )
			return null;

		return MipData[0]; // Mip 0 is the largest
	}

	/// <summary>
	/// Get the largest mip level data for a specific animation frame.
	/// </summary>
	public byte[] GetLargestMipData( int frameIndex )
	{
		byte[] data = GetLargestMipData();
		if ( data == null || data.Length == 0 )
		{
			return null;
		}

		int frameCount = Math.Max( 1, FrameCount );
		if ( frameCount == 1 )
		{
			return data;
		}

		int frameSize = CalculateImageSize( Format, Width, Height );
		if ( frameSize <= 0 || data.Length < frameSize )
		{
			return data;
		}

		int safeFrame = ((frameIndex % frameCount) + frameCount) % frameCount;
		int frameOffset = safeFrame * frameSize;
		if ( frameOffset + frameSize > data.Length )
		{
			return data;
		}

		var frameData = new byte[frameSize];
		Buffer.BlockCopy( data, frameOffset, frameData, 0, frameSize );
		return frameData;
	}

	/// <summary>
	/// Convert to RGBA8888 format for s&box texture creation.
	/// </summary>
	/// <param name="forceOpaqueAlpha">If true, sets alpha to 255 for all pixels (useful for non-transparent materials that store other data in alpha)</param>
	public byte[] ConvertToRGBA( bool forceOpaqueAlpha = false )
	{
		return ConvertToRGBA( 0, forceOpaqueAlpha );
	}

	/// <summary>
	/// Convert a specific animation frame to RGBA8888 format for s&box texture creation.
	/// </summary>
	/// <param name="frameIndex">Frame index to extract when VTF contains multiple frames.</param>
	/// <param name="forceOpaqueAlpha">If true, sets alpha to 255 for all pixels.</param>
	public byte[] ConvertToRGBA( int frameIndex, bool forceOpaqueAlpha = false )
	{
		var data = GetLargestMipData( frameIndex );
		if ( data == null )
			return null;

		var rgba = ConvertToRGBA( data, Width, Height, Format );
		
		// Force alpha to 255 if requested (for materials that use alpha for non-transparency purposes)
		if ( forceOpaqueAlpha && rgba != null )
		{
			for ( int i = 3; i < rgba.Length; i += 4 )
			{
				rgba[i] = 255;
			}
		}
		
		return rgba;
	}

	/// <summary>
	/// Convert image data to RGBA8888 format.
	/// </summary>
	public static byte[] ConvertToRGBA( byte[] data, int width, int height, VtfImageFormat format )
	{
		byte[] rgba = new byte[width * height * 4];

		switch ( format )
		{
			case VtfImageFormat.RGBA8888:
				Array.Copy( data, rgba, Math.Min( data.Length, rgba.Length ) );
				break;

			case VtfImageFormat.BGRA8888:
				for ( int i = 0; i < width * height && i * 4 + 3 < data.Length; i++ )
				{
					rgba[i * 4 + 0] = data[i * 4 + 2]; // R
					rgba[i * 4 + 1] = data[i * 4 + 1]; // G
					rgba[i * 4 + 2] = data[i * 4 + 0]; // B
					rgba[i * 4 + 3] = data[i * 4 + 3]; // A
				}
				break;

			case VtfImageFormat.RGB888:
				for ( int i = 0; i < width * height && i * 3 + 2 < data.Length; i++ )
				{
					rgba[i * 4 + 0] = data[i * 3 + 0]; // R
					rgba[i * 4 + 1] = data[i * 3 + 1]; // G
					rgba[i * 4 + 2] = data[i * 3 + 2]; // B
					rgba[i * 4 + 3] = 255;             // A
				}
				break;

			case VtfImageFormat.BGR888:
				for ( int i = 0; i < width * height && i * 3 + 2 < data.Length; i++ )
				{
					rgba[i * 4 + 0] = data[i * 3 + 2]; // R
					rgba[i * 4 + 1] = data[i * 3 + 1]; // G
					rgba[i * 4 + 2] = data[i * 3 + 0]; // B
					rgba[i * 4 + 3] = 255;             // A
				}
				break;

			case VtfImageFormat.DXT1:
			case VtfImageFormat.DXT1_OneBitAlpha:
				DecompressDXT1( data, rgba, width, height );
				break;

			case VtfImageFormat.DXT3:
				DecompressDXT3( data, rgba, width, height );
				break;

			case VtfImageFormat.DXT5:
				DecompressDXT5( data, rgba, width, height );
				break;

			case VtfImageFormat.I8:
				for ( int i = 0; i < width * height && i < data.Length; i++ )
				{
					rgba[i * 4 + 0] = data[i];
					rgba[i * 4 + 1] = data[i];
					rgba[i * 4 + 2] = data[i];
					rgba[i * 4 + 3] = 255;
				}
				break;

			case VtfImageFormat.A8:
				for ( int i = 0; i < width * height && i < data.Length; i++ )
				{
					rgba[i * 4 + 0] = 255;
					rgba[i * 4 + 1] = 255;
					rgba[i * 4 + 2] = 255;
					rgba[i * 4 + 3] = data[i];
				}
				break;

			default:
				Log.Warning( $"VTF: Unsupported format {format}, returning empty texture" );
				Array.Fill( rgba, (byte)255 );
				break;
		}

		return rgba;
	}

	/// <summary>
	/// Decompress DXT1 block-compressed data.
	/// </summary>
	private static void DecompressDXT1( byte[] compressed, byte[] output, int width, int height )
	{
		int blockWidth = (width + 3) / 4;
		int blockHeight = (height + 3) / 4;
		int blockIndex = 0;

		for ( int by = 0; by < blockHeight; by++ )
		{
			for ( int bx = 0; bx < blockWidth; bx++ )
			{
				int blockOffset = blockIndex * 8;
				if ( blockOffset + 8 > compressed.Length )
					break;

				// Read color endpoints
				ushort c0 = (ushort)(compressed[blockOffset] | (compressed[blockOffset + 1] << 8));
				ushort c1 = (ushort)(compressed[blockOffset + 2] | (compressed[blockOffset + 3] << 8));

				// Decode colors
				byte[] colors = new byte[16];
				DecodeRGB565( c0, out colors[0], out colors[1], out colors[2] );
				colors[3] = 255;
				DecodeRGB565( c1, out colors[4], out colors[5], out colors[6] );
				colors[7] = 255;

				if ( c0 > c1 )
				{
					colors[8] = (byte)((2 * colors[0] + colors[4]) / 3);
					colors[9] = (byte)((2 * colors[1] + colors[5]) / 3);
					colors[10] = (byte)((2 * colors[2] + colors[6]) / 3);
					colors[11] = 255;
					colors[12] = (byte)((colors[0] + 2 * colors[4]) / 3);
					colors[13] = (byte)((colors[1] + 2 * colors[5]) / 3);
					colors[14] = (byte)((colors[2] + 2 * colors[6]) / 3);
					colors[15] = 255;
				}
				else
				{
					colors[8] = (byte)((colors[0] + colors[4]) / 2);
					colors[9] = (byte)((colors[1] + colors[5]) / 2);
					colors[10] = (byte)((colors[2] + colors[6]) / 2);
					colors[11] = 255;
					colors[12] = 0;
					colors[13] = 0;
					colors[14] = 0;
					colors[15] = 0; // Transparent
				}

				// Read indices
				uint indices = (uint)(compressed[blockOffset + 4] |
									  (compressed[blockOffset + 5] << 8) |
									  (compressed[blockOffset + 6] << 16) |
									  (compressed[blockOffset + 7] << 24));

				// Write pixels
				for ( int py = 0; py < 4; py++ )
				{
					for ( int px = 0; px < 4; px++ )
					{
						int x = bx * 4 + px;
						int y = by * 4 + py;
						if ( x >= width || y >= height )
							continue;

						int colorIndex = (int)((indices >> ((py * 4 + px) * 2)) & 0x3);
						int outOffset = (y * width + x) * 4;
						output[outOffset + 0] = colors[colorIndex * 4 + 0];
						output[outOffset + 1] = colors[colorIndex * 4 + 1];
						output[outOffset + 2] = colors[colorIndex * 4 + 2];
						output[outOffset + 3] = colors[colorIndex * 4 + 3];
					}
				}

				blockIndex++;
			}
		}
	}

	/// <summary>
	/// Decompress DXT3 block-compressed data.
	/// </summary>
	private static void DecompressDXT3( byte[] compressed, byte[] output, int width, int height )
	{
		int blockWidth = (width + 3) / 4;
		int blockHeight = (height + 3) / 4;
		int blockIndex = 0;

		for ( int by = 0; by < blockHeight; by++ )
		{
			for ( int bx = 0; bx < blockWidth; bx++ )
			{
				int blockOffset = blockIndex * 16;
				if ( blockOffset + 16 > compressed.Length )
					break;

				// Read alpha values (first 8 bytes)
				ulong alphas = 0;
				for ( int i = 0; i < 8; i++ )
					alphas |= (ulong)compressed[blockOffset + i] << (i * 8);

				// Read color data (same as DXT1)
				ushort c0 = (ushort)(compressed[blockOffset + 8] | (compressed[blockOffset + 9] << 8));
				ushort c1 = (ushort)(compressed[blockOffset + 10] | (compressed[blockOffset + 11] << 8));

				byte[] colors = new byte[12];
				DecodeRGB565( c0, out colors[0], out colors[1], out colors[2] );
				DecodeRGB565( c1, out colors[3], out colors[4], out colors[5] );
				colors[6] = (byte)((2 * colors[0] + colors[3]) / 3);
				colors[7] = (byte)((2 * colors[1] + colors[4]) / 3);
				colors[8] = (byte)((2 * colors[2] + colors[5]) / 3);
				colors[9] = (byte)((colors[0] + 2 * colors[3]) / 3);
				colors[10] = (byte)((colors[1] + 2 * colors[4]) / 3);
				colors[11] = (byte)((colors[2] + 2 * colors[5]) / 3);

				uint indices = (uint)(compressed[blockOffset + 12] |
									  (compressed[blockOffset + 13] << 8) |
									  (compressed[blockOffset + 14] << 16) |
									  (compressed[blockOffset + 15] << 24));

				for ( int py = 0; py < 4; py++ )
				{
					for ( int px = 0; px < 4; px++ )
					{
						int x = bx * 4 + px;
						int y = by * 4 + py;
						if ( x >= width || y >= height )
							continue;

						int colorIndex = (int)((indices >> ((py * 4 + px) * 2)) & 0x3);
						int alphaIndex = py * 4 + px;
						byte alpha = (byte)(((alphas >> (alphaIndex * 4)) & 0xF) * 17);

						int outOffset = (y * width + x) * 4;
						output[outOffset + 0] = colors[colorIndex * 3 + 0];
						output[outOffset + 1] = colors[colorIndex * 3 + 1];
						output[outOffset + 2] = colors[colorIndex * 3 + 2];
						output[outOffset + 3] = alpha;
					}
				}

				blockIndex++;
			}
		}
	}

	/// <summary>
	/// Decompress DXT5 block-compressed data.
	/// </summary>
	private static void DecompressDXT5( byte[] compressed, byte[] output, int width, int height )
	{
		int blockWidth = (width + 3) / 4;
		int blockHeight = (height + 3) / 4;
		int blockIndex = 0;

		for ( int by = 0; by < blockHeight; by++ )
		{
			for ( int bx = 0; bx < blockWidth; bx++ )
			{
				int blockOffset = blockIndex * 16;
				if ( blockOffset + 16 > compressed.Length )
					break;

				// Read alpha endpoints
				byte a0 = compressed[blockOffset];
				byte a1 = compressed[blockOffset + 1];

				// Read alpha indices
				ulong alphaIndices = 0;
				for ( int i = 0; i < 6; i++ )
					alphaIndices |= (ulong)compressed[blockOffset + 2 + i] << (i * 8);

				// Calculate alpha palette
				byte[] alphaPalette = new byte[8];
				alphaPalette[0] = a0;
				alphaPalette[1] = a1;
				if ( a0 > a1 )
				{
					alphaPalette[2] = (byte)((6 * a0 + 1 * a1) / 7);
					alphaPalette[3] = (byte)((5 * a0 + 2 * a1) / 7);
					alphaPalette[4] = (byte)((4 * a0 + 3 * a1) / 7);
					alphaPalette[5] = (byte)((3 * a0 + 4 * a1) / 7);
					alphaPalette[6] = (byte)((2 * a0 + 5 * a1) / 7);
					alphaPalette[7] = (byte)((1 * a0 + 6 * a1) / 7);
				}
				else
				{
					alphaPalette[2] = (byte)((4 * a0 + 1 * a1) / 5);
					alphaPalette[3] = (byte)((3 * a0 + 2 * a1) / 5);
					alphaPalette[4] = (byte)((2 * a0 + 3 * a1) / 5);
					alphaPalette[5] = (byte)((1 * a0 + 4 * a1) / 5);
					alphaPalette[6] = 0;
					alphaPalette[7] = 255;
				}

				// Read color data
				ushort c0 = (ushort)(compressed[blockOffset + 8] | (compressed[blockOffset + 9] << 8));
				ushort c1 = (ushort)(compressed[blockOffset + 10] | (compressed[blockOffset + 11] << 8));

				byte[] colors = new byte[12];
				DecodeRGB565( c0, out colors[0], out colors[1], out colors[2] );
				DecodeRGB565( c1, out colors[3], out colors[4], out colors[5] );
				colors[6] = (byte)((2 * colors[0] + colors[3]) / 3);
				colors[7] = (byte)((2 * colors[1] + colors[4]) / 3);
				colors[8] = (byte)((2 * colors[2] + colors[5]) / 3);
				colors[9] = (byte)((colors[0] + 2 * colors[3]) / 3);
				colors[10] = (byte)((colors[1] + 2 * colors[4]) / 3);
				colors[11] = (byte)((colors[2] + 2 * colors[5]) / 3);

				uint indices = (uint)(compressed[blockOffset + 12] |
									  (compressed[blockOffset + 13] << 8) |
									  (compressed[blockOffset + 14] << 16) |
									  (compressed[blockOffset + 15] << 24));

				for ( int py = 0; py < 4; py++ )
				{
					for ( int px = 0; px < 4; px++ )
					{
						int x = bx * 4 + px;
						int y = by * 4 + py;
						if ( x >= width || y >= height )
							continue;

						int colorIndex = (int)((indices >> ((py * 4 + px) * 2)) & 0x3);
						int alphaIndex = (int)((alphaIndices >> ((py * 4 + px) * 3)) & 0x7);

						int outOffset = (y * width + x) * 4;
						output[outOffset + 0] = colors[colorIndex * 3 + 0];
						output[outOffset + 1] = colors[colorIndex * 3 + 1];
						output[outOffset + 2] = colors[colorIndex * 3 + 2];
						output[outOffset + 3] = alphaPalette[alphaIndex];
					}
				}

				blockIndex++;
			}
		}
	}

	/// <summary>
	/// Decode RGB565 color to RGB components.
	/// </summary>
	private static void DecodeRGB565( ushort color, out byte r, out byte g, out byte b )
	{
		r = (byte)((color >> 11) * 255 / 31);
		g = (byte)(((color >> 5) & 0x3F) * 255 / 63);
		b = (byte)((color & 0x1F) * 255 / 31);
	}
}
