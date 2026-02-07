namespace GModMount.Source;

/// <summary>
/// Parsed VVD (Valve Vertex Data) file.
/// </summary>
public class VvdFile
{
	public VvdHeader Header { get; private set; }
	public List<VvdFixup> Fixups { get; } = new();
	public VvdVertex[] Vertices { get; private set; }
	public VvdTangent[] Tangents { get; private set; }

	/// <summary>
	/// LOD vertex counts extracted from header.
	/// </summary>
	public int[] LodVertexCounts { get; private set; }

	/// <summary>
	/// Get vertex count for a specific LOD level.
	/// </summary>
	public int GetLodVertexCount( int lod )
	{
		if ( lod < 0 || lod >= Header.LodCount || LodVertexCounts == null )
			return 0;

		return LodVertexCounts[lod];
	}

	/// <summary>
	/// Load a VVD file from a byte array.
	/// </summary>
	public static VvdFile Load( byte[] data )
	{
		using var stream = new MemoryStream( data );
		using var reader = new BinaryReader( stream );
		return Load( reader );
	}

	/// <summary>
	/// Load a VVD file from a stream.
	/// </summary>
	public static VvdFile Load( Stream stream )
	{
		using var reader = new BinaryReader( stream, Encoding.ASCII, leaveOpen: true );
		return Load( reader );
	}

	/// <summary>
	/// Load a VVD file from a binary reader.
	/// </summary>
	public static VvdFile Load( BinaryReader reader )
	{
		var vvd = new VvdFile();

		// Read header fields manually to extract LOD vertex counts
		long headerStart = reader.BaseStream.Position;

		int id = reader.ReadInt32();
		int version = reader.ReadInt32();
		int checksum = reader.ReadInt32();
		int lodCount = reader.ReadInt32();

		// Read LOD vertex counts (8 ints)
		vvd.LodVertexCounts = new int[SourceConstants.MAX_LOD_COUNT];
		for ( int i = 0; i < SourceConstants.MAX_LOD_COUNT; i++ )
		{
			vvd.LodVertexCounts[i] = reader.ReadInt32();
		}

		int fixupCount = reader.ReadInt32();
		int fixupTableOffset = reader.ReadInt32();
		int vertexDataOffset = reader.ReadInt32();
		int tangentDataOffset = reader.ReadInt32();

		// Reconstruct header struct
		vvd.Header = new VvdHeader
		{
			Id = id,
			Version = version,
			Checksum = checksum,
			LodCount = lodCount,
			FixupCount = fixupCount,
			FixupTableOffset = fixupTableOffset,
			VertexDataOffset = vertexDataOffset,
			TangentDataOffset = tangentDataOffset
		};

		if ( !vvd.Header.IsValid )
		{
			throw new InvalidDataException( $"Invalid VVD file: ID=0x{vvd.Header.Id:X8}, Version={vvd.Header.Version}" );
		}

		// Read fixups
		if ( vvd.Header.FixupCount > 0 && vvd.Header.FixupTableOffset > 0 )
		{
			reader.BaseStream.Position = vvd.Header.FixupTableOffset;
			var fixups = reader.ReadStructArray<VvdFixup>( vvd.Header.FixupCount );
			vvd.Fixups.AddRange( fixups );
		}

		// Read vertices
		int vertexCount = vvd.GetLodVertexCount( 0 );
		if ( vertexCount > 0 && vvd.Header.VertexDataOffset > 0 )
		{
			reader.BaseStream.Position = vvd.Header.VertexDataOffset;
			vvd.Vertices = reader.ReadStructArray<VvdVertex>( vertexCount );
		}
		else
		{
			vvd.Vertices = Array.Empty<VvdVertex>();
		}

		// Read tangents
		if ( vertexCount > 0 && vvd.Header.TangentDataOffset > 0 )
		{
			reader.BaseStream.Position = vvd.Header.TangentDataOffset;
			vvd.Tangents = reader.ReadStructArray<VvdTangent>( vertexCount );
		}
		else
		{
			vvd.Tangents = Array.Empty<VvdTangent>();
		}

		return vvd;
	}

	/// <summary>
	/// Get vertices for a specific LOD level, applying fixups if needed.
	/// </summary>
	public VvdVertex[] GetVerticesForLod( int lod )
	{
		// Only skip fixup processing when there are NO fixups
		// When fixups exist, they MUST be applied even for LOD 0 because
		// the vertex data is stored in a reorganized order
		if ( Fixups.Count == 0 )
		{
			return Vertices;
		}

		// Apply fixups to remap vertices for this LOD
		// Fixups with Lod >= requested lod should be included
		int lodVertexCount = GetLodVertexCount( lod );
		var lodVertices = new List<VvdVertex>( lodVertexCount );

		foreach ( var fixup in Fixups )
		{
			// Include fixups that apply to this LOD level or higher detail
			if ( fixup.Lod >= lod )
			{
				for ( int i = 0; i < fixup.NumVertices; i++ )
				{
					int sourceIndex = fixup.SourceVertexId + i;
					if ( sourceIndex < Vertices.Length )
					{
						lodVertices.Add( Vertices[sourceIndex] );
					}
				}
			}
		}

		return lodVertices.ToArray();
	}
}
