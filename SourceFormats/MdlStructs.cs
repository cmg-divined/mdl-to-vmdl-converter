namespace GModMount.Source;

/// <summary>
/// Main MDL file header structure (studiohdr_t).
/// Used by MDL versions 48, 49, and 53.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioHeader
{
	public int Id;                      // "IDST"
	public int Version;                 // 48, 49, or 53
	public int Checksum;                // Must match VTX/VVD
	public unsafe fixed byte Name[64];  // Internal model name

	public int DataLength;              // Total file size

	// Bounding info
	public Vector3 EyePosition;
	public Vector3 IllumPosition;
	public Vector3 HullMin;
	public Vector3 HullMax;
	public Vector3 ViewBBMin;
	public Vector3 ViewBBMax;

	public int Flags;

	// Bones
	public int BoneCount;
	public int BoneOffset;

	// Bone controllers
	public int BoneControllerCount;
	public int BoneControllerOffset;

	// Hitboxes
	public int HitboxSetCount;
	public int HitboxSetOffset;

	// Animations
	public int LocalAnimCount;
	public int LocalAnimOffset;

	// Sequences
	public int LocalSeqCount;
	public int LocalSeqOffset;

	public int ActivityListVersion;
	public int EventsIndexed;

	// Textures/Materials
	public int TextureCount;
	public int TextureOffset;

	// Texture directories
	public int TextureDirCount;
	public int TextureDirOffset;

	// Skin references (for skin families)
	public int SkinReferenceCount;
	public int SkinFamilyCount;
	public int SkinReferenceIndex;

	// Body parts
	public int BodyPartCount;
	public int BodyPartOffset;

	// Attachments
	public int AttachmentCount;
	public int AttachmentOffset;

	// Nodes
	public int LocalNodeCount;
	public int LocalNodeIndex;
	public int LocalNodeNameIndex;

	// Flex descriptors
	public int FlexDescCount;
	public int FlexDescIndex;

	// Flex controllers
	public int FlexControllerCount;
	public int FlexControllerIndex;

	// Flex rules
	public int FlexRulesCount;
	public int FlexRulesIndex;

	// IK chains
	public int IKChainCount;
	public int IKChainIndex;

	// Mouths
	public int MouthsCount;
	public int MouthsIndex;

	// Pose parameters
	public int LocalPoseParamCount;
	public int LocalPoseParamIndex;

	public int SurfacePropIndex;

	// Key-value data
	public int KeyValueIndex;
	public int KeyValueCount;

	// IK locks
	public int IKLockCount;
	public int IKLockIndex;

	public float Mass;
	public int Contents;

	// Included models
	public int IncludeModelCount;
	public int IncludeModelIndex;

	public int VirtualModel;

	// Animation blocks
	public int AnimBlockNameIndex;
	public int AnimBlockCount;
	public int AnimBlockIndex;

	public int AnimBlockModel;

	public int BoneTableNameIndex;

	public int VertexBase;
	public int OffsetBase;

	public byte DirectionalDotProduct;
	public byte RootLod;
	public byte NumAllowedRootLods;

	public byte Unused0;
	public int Unused1;

	// Flex controller UI
	public int FlexControllerUICount;
	public int FlexControllerUIIndex;

	public float VertAnimFixedPointScale;
	public int Unused2;

	// Secondary header offset
	public int StudioHdr2Index;

	public int Unused3;

	public readonly bool IsValid => Id == SourceConstants.MDL_ID;
	public readonly bool IsStaticProp => (Flags & SourceConstants.STUDIOHDR_FLAGS_STATIC_PROP) != 0;
}

/// <summary>
/// Secondary MDL header (studiohdr2_t).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioHeader2
{
	public int SrcBoneTransformCount;
	public int SrcBoneTransformIndex;
	public int IllumPositionAttachmentIndex;
	public float MaxEyeDeflection;
	public int LinearBoneIndex;
	public unsafe fixed int Unknown[64];
}

/// <summary>
/// Bone structure (mstudiobone_t).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioBone
{
	public int NameOffset;
	public int ParentBone;
	public unsafe fixed int BoneController[6];
	public Vector3 Position;
	public Quaternion Quaternion;
	public Vector3 Rotation;
	public Vector3 PositionScale;
	public Vector3 RotationScale;
	public unsafe fixed float PoseToBone[12]; // 3x4 matrix
	public Quaternion AlignmentQuaternion;
	public int Flags;
	public int ProceduralType;
	public int ProceduralIndex;
	public int PhysicsBone;
	public int SurfacePropIndex;
	public int Contents;
	public unsafe fixed int Unused[8];
}

/// <summary>
/// Body part structure (mstudiobodyparts_t).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioBodyPart
{
	public int NameOffset;
	public int ModelCount;
	public int Base;
	public int ModelOffset;
}

/// <summary>
/// Model within a body part (mstudiomodel_t).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioModel
{
	public unsafe fixed byte Name[64];
	public int Type;
	public float BoundingRadius;
	public int MeshCount;
	public int MeshOffset;

	// Vertex data
	public int VertexCount;
	public int VertexIndex;
	public int TangentIndex;

	public int AttachmentCount;
	public int AttachmentOffset;

	public int EyeballCount;
	public int EyeballOffset;

	public StudioModelVertexData VertexData;

	public unsafe fixed int Unused[8];
}

/// <summary>
/// Vertex data info within a model.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioModelVertexData
{
	public int VertexDataPointer;
	public int TangentDataPointer;
}

/// <summary>
/// Mesh within a model (mstudiomesh_t).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioMesh
{
	public int Material;
	public int ModelOffset;
	public int VertexCount;
	public int VertexOffset;
	public int FlexCount;
	public int FlexOffset;
	public int MaterialType;
	public int MaterialParam;
	public int MeshId;
	public Vector3 Center;
	public StudioMeshVertexData VertexData;
	public unsafe fixed int Unused[8];
}

/// <summary>
/// Flex descriptor entry (mstudioflexdesc_t).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioFlexDesc
{
	public int NameOffset;
}

/// <summary>
/// Flex controller entry (mstudioflexcontroller_t).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioFlexController
{
	public int TypeOffset;
	public int NameOffset;
	public int LocalToGlobal;
	public float Min;
	public float Max;
}

/// <summary>
/// Flex rule entry (mstudioflexrule_t).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioFlexRule
{
	public int FlexIndex;
	public int OpCount;
	public int OpOffset;
}

/// <summary>
/// Flex op entry (mstudioflexop_t).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioFlexOp
{
	public int Op;
	public int Value;
}

/// <summary>
/// Flex controller UI entry (mstudioflexcontrollerui_t).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioFlexControllerUI
{
	public int NameOffset;
	public int Index0;
	public int Index1;
	public int Index2;
	public byte RemapType;
	public byte Stereo;
	public ushort Unused;
}

/// <summary>
/// Flex entry on a mesh (mstudioflex_t) for MDL v44+.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioFlex
{
	public int FlexDescIndex;
	public float Target0;
	public float Target1;
	public float Target2;
	public float Target3;
	public int VertCount;
	public int VertOffset;
	public int PartnerIndex;
	public byte VertAnimType;
	public unsafe fixed byte UnusedChar[3];
	public unsafe fixed int Unused[6];
}

/// <summary>
/// Flex vertex animation entry (mstudiovertanim_t) for normal flex data.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioVertAnim
{
	public ushort VertexIndex;
	public byte Speed;
	public byte Side;
	public ushort DeltaX;
	public ushort DeltaY;
	public ushort DeltaZ;
	public ushort NDeltaX;
	public ushort NDeltaY;
	public ushort NDeltaZ;
}

/// <summary>
/// Flex vertex animation entry (mstudiovertanim_wrinkle_t).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioVertAnimWrinkle
{
	public ushort VertexIndex;
	public byte Speed;
	public byte Side;
	public ushort DeltaX;
	public ushort DeltaY;
	public ushort DeltaZ;
	public ushort NDeltaX;
	public ushort NDeltaY;
	public ushort NDeltaZ;
	public ushort WrinkleDelta;
}

/// <summary>
/// Vertex data info within a mesh.
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioMeshVertexData
{
	public int ModelVertexDataPointer;
	public unsafe fixed int NumLodVertices[SourceConstants.MAX_LOD_COUNT];
}

/// <summary>
/// Texture/Material reference (mstudiotexture_t).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioTexture
{
	public int NameOffset;
	public int Flags;
	public int Used;
	public int Unused;
	public int Material;
	public int ClientMaterial;
	public unsafe fixed int Unused2[10];
}

/// <summary>
/// Attachment point (mstudioattachment_t).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioAttachment
{
	public int NameOffset;
	public uint Flags;
	public int LocalBone;
	public unsafe fixed float Local[12]; // 3x4 matrix
	public unsafe fixed int Unused[8];
}

/// <summary>
/// Hitbox set (mstudiohitboxset_t).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioHitboxSet
{
	public int NameOffset;
	public int HitboxCount;
	public int HitboxOffset;
}

/// <summary>
/// Hitbox (mstudiobbox_t).
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioBBox
{
	public int Bone;
	public int Group;
	public Vector3 BBMin;
	public Vector3 BBMax;
	public int HitboxNameOffset;
	public unsafe fixed int Unused[8];
}

/// <summary>
/// Eyeball data (mstudioeyeball_t).
/// Contains the positioning and projection data for eye rendering.
/// Total size: 172 bytes
/// </summary>
[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct StudioEyeball
{
	public int NameOffset;       // 0: Offset to name string
	public int Bone;             // 4: Bone the eye is attached to
	public Vector3 Org;          // 8: Origin position in local bone space (12 bytes)
	public float ZOffset;        // 20: Z offset for iris
	public float Radius;         // 24: Eyeball radius (diameter / 2)
	public Vector3 Up;           // 28: Up vector (12 bytes)
	public Vector3 Forward;      // 40: Forward/look direction (12 bytes)
	public int Texture;          // 52: Texture/material index
	public int Unused1;          // 56
	public float IrisScale;      // 60: Iris scale (1.0 / iris_scale from QC)
	public int Unused2;          // 64
	// upperflexdesc[3] - raiser, neutral, lowerer flex descriptors
	public int UpperFlexDesc0;   // 68
	public int UpperFlexDesc1;   // 72
	public int UpperFlexDesc2;   // 76
	// lowerflexdesc[3]
	public int LowerFlexDesc0;   // 80
	public int LowerFlexDesc1;   // 84
	public int LowerFlexDesc2;   // 88
	// uppertarget[3] - angles in radians
	public float UpperTarget0;   // 92
	public float UpperTarget1;   // 96
	public float UpperTarget2;   // 100
	// lowertarget[3]
	public float LowerTarget0;   // 104
	public float LowerTarget1;   // 108
	public float LowerTarget2;   // 112
	public int UpperLidFlexDesc; // 116: Upper lid flex desc index
	public int LowerLidFlexDesc; // 120: Lower lid flex desc index
	public unsafe fixed int Unused3[4]; // 124-139: (16 bytes)
	public byte NonFACS;         // 140
	public unsafe fixed byte Unused4[3]; // 141-143: padding (3 bytes)
	public unsafe fixed int Unused5[7];  // 144-171: Final padding (28 bytes)
}
