namespace GModMount.Source;

/// <summary>
/// VTX (Valve TriangleX) file header.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct VtxHeader
{
	public int Version;                 // Should be 7
	public int VertexCacheSize;
	public ushort MaxBonesPerStrip;
	public ushort MaxBonesPerTriangle;
	public int MaxBonesPerVertex;
	public int Checksum;                // Must match MDL
	public int LodCount;
	public int MaterialReplacementListOffset;
	public int BodyPartCount;
	public int BodyPartOffset;

	public readonly bool IsValid => Version == SourceConstants.VTX_VERSION;
}

/// <summary>
/// VTX body part.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct VtxBodyPart
{
	public int ModelCount;
	public int ModelOffset;
}

/// <summary>
/// VTX model.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct VtxModel
{
	public int LodCount;
	public int LodOffset;
}

/// <summary>
/// VTX model LOD.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct VtxModelLod
{
	public int MeshCount;
	public int MeshOffset;
	public float SwitchPoint;
}

/// <summary>
/// VTX mesh.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct VtxMesh
{
	public int StripGroupCount;
	public int StripGroupOffset;
	public byte Flags;
}

/// <summary>
/// VTX strip group - contains vertices and indices for a portion of a mesh.
/// NOTE: For MDL v44/v45, use VtxStripGroupV44. For MDL v49+, use this struct.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct VtxStripGroup
{
	public int VertexCount;
	public int VertexOffset;
	public int IndexCount;
	public int IndexOffset;
	public int StripCount;
	public int StripOffset;
	public byte Flags;
	// MDL v49+ adds these two fields (except L4D and L4D2)
	public int TopologyIndexCount;
	public int TopologyIndexOffset;
}

/// <summary>
/// VTX strip group for older models (v44/v45) - does NOT have topology fields.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct VtxStripGroupV44
{
	public int VertexCount;
	public int VertexOffset;
	public int IndexCount;
	public int IndexOffset;
	public int StripCount;
	public int StripOffset;
	public byte Flags;
}

/// <summary>
/// VTX strip - a continuous strip of triangles.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct VtxStrip
{
	public int IndexCount;
	public int IndexOffset;
	public int VertexCount;
	public int VertexOffset;
	public short BoneCount;
	public byte Flags;
	public int BoneStateChangeCount;
	public int BoneStateChangeOffset;
}

/// <summary>
/// VTX vertex reference - maps to VVD vertices.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct VtxVertex
{
	public unsafe fixed byte BoneWeightIndex[SourceConstants.MAX_BONES_PER_VERTEX];
	public byte BoneCount;
	public ushort OriginalMeshVertexIndex;
	public unsafe fixed byte BoneIndex[SourceConstants.MAX_BONES_PER_VERTEX];
}
