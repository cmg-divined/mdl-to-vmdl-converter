namespace GModMount.Source;

/// <summary>
/// VVD (Valve Vertex Data) file header.
/// Note: LodVertexCount is read separately due to fixed buffer limitations.
/// </summary>
public struct VvdHeader
{
	public int Id;                      // "IDSV"
	public int Version;                 // Should be 4
	public int Checksum;                // Must match MDL
	public int LodCount;                // Number of LOD levels
	// LodVertexCount[8] is read separately into VvdFile.LodVertexCounts
	public int FixupCount;              // Number of fixup entries
	public int FixupTableOffset;        // Offset to fixup table
	public int VertexDataOffset;        // Offset to vertex data
	public int TangentDataOffset;       // Offset to tangent data

	public readonly bool IsValid => Id == SourceConstants.VVD_ID && Version == SourceConstants.VVD_VERSION;
}

/// <summary>
/// VVD fixup entry for LOD vertex remapping.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct VvdFixup
{
	public int Lod;                     // LOD level
	public int SourceVertexId;          // Original vertex index
	public int NumVertices;             // Number of vertices
}

/// <summary>
/// VVD vertex structure with bone weights.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct VvdVertex
{
	// Bone weights (up to 3 bones)
	public float Weight0;
	public float Weight1;
	public float Weight2;

	// Bone indices
	public byte Bone0;
	public byte Bone1;
	public byte Bone2;
	public byte BoneCount;

	// Position
	public Vector3 Position;

	// Normal
	public Vector3 Normal;

	// Texture coordinates
	public Vector2 TexCoord;
}

/// <summary>
/// VVD tangent data (4D vector).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct VvdTangent
{
	public float X;
	public float Y;
	public float Z;
	public float W; // Handedness (-1 or 1)

	public readonly Vector3 AsVector3 => new( X, Y, Z );
}
