namespace GModMount.Source;

/// <summary>
/// PHY file header structure.
/// </summary>
public struct PhyHeader
{
	/// <summary>Header size (usually 16)</summary>
	public int Size;
	/// <summary>ID (usually 0)</summary>
	public int Id;
	/// <summary>Number of collision solids</summary>
	public int SolidCount;
	/// <summary>Checksum matching MDL file</summary>
	public int Checksum;
}

/// <summary>
/// A single convex mesh within a collision solid.
/// </summary>
public class PhyConvexMesh
{
	/// <summary>Bone index this mesh is attached to</summary>
	public int BoneIndex;
	/// <summary>Flags</summary>
	public int Flags;
	/// <summary>Triangle faces</summary>
	public List<PhyFace> Faces = new();
	/// <summary>Vertices for this mesh (in Source engine coordinates)</summary>
	public List<Vector3> Vertices = new();
}

/// <summary>
/// A triangle face in a convex mesh.
/// </summary>
public struct PhyFace
{
	public ushort V0;
	public ushort V1;
	public ushort V2;
}

/// <summary>
/// Collision data for a single solid (bone).
/// </summary>
public class PhyCollisionData
{
	/// <summary>Size of collision data</summary>
	public int Size;
	/// <summary>Convex meshes making up this solid</summary>
	public List<PhyConvexMesh> ConvexMeshes = new();
}

/// <summary>
/// Physics properties for a solid.
/// </summary>
public class PhySolid
{
	public int Index;
	public string Name = "";
	public string Parent = "";
	public float Mass = 1f;
	public string SurfaceProp = "default";
	public float Damping;
	public float RotDamping;
	public float Inertia = 1f;
	public float Volume;
	public float MassBias = 1f;
	public float DragCoefficient = -1f;
}

/// <summary>
/// Ragdoll joint constraint between two bones.
/// </summary>
public class PhyRagdollConstraint
{
	public int ParentIndex;
	public int ChildIndex;
	
	/// <summary>X axis rotation limits (min, max, friction)</summary>
	public float XMin, XMax, XFriction;
	/// <summary>Y axis rotation limits (min, max, friction)</summary>
	public float YMin, YMax, YFriction;
	/// <summary>Z axis rotation limits (min, max, friction)</summary>
	public float ZMin, ZMax, ZFriction;
}

/// <summary>
/// Collision pair for self-collision rules.
/// </summary>
public struct PhyCollisionPair
{
	public int Object0;
	public int Object1;
}

/// <summary>
/// Edit parameters section.
/// </summary>
public class PhyEditParams
{
	public string RootName = "";
	public float TotalMass;
	public bool Concave;
}
