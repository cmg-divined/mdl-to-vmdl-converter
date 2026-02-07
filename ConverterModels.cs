using GModMount.Source;

internal sealed class BuildContext
{
	public required SourceModel SourceModel { get; init; }
	public required string ModelBaseName { get; init; }
	public required string OutputDirectory { get; init; }
	public string ModelAssetDirectory { get; set; } = string.Empty;
	public IReadOnlyList<string> ExportBoneNames { get; set; } = Array.Empty<string>();
	public HashSet<string> SourceMaterials { get; } = new( StringComparer.OrdinalIgnoreCase );
	public List<MaterialRemapExport> MaterialRemaps { get; } = new();
	public List<MeshExport> Meshes { get; } = new();
	public List<BodyGroupExport> BodyGroups { get; } = new();
	public List<HitboxSetExport> HitboxSets { get; } = new();
	public List<PhysicsShapeExport> PhysicsShapes { get; } = new();
	public List<PhysicsBodyMarkupExport> PhysicsBodies { get; } = new();
	public List<PhysicsJointExport> PhysicsJoints { get; } = new();
	public List<string> MorphChannels { get; } = new();
	public int MorphChannelCount { get; set; }
}

internal sealed class MeshExport
{
	public required string Name { get; init; }
	public required string FileName { get; set; }
	public required List<TriangleRecord> Triangles { get; init; }
	public required List<MeshMorphExport> Morphs { get; init; }
}

internal sealed class MeshMorphExport
{
	public required string Name { get; init; }
	public required List<MeshMorphDeltaExport> Deltas { get; init; }
}

internal sealed class MeshMorphDeltaExport
{
	public int SourceVertexIndex { get; init; }
	public Vector3 PositionDelta { get; init; }
	public Vector3 NormalDelta { get; init; }
}

internal sealed class BodyGroupExport
{
	public required string Name { get; init; }
	public required List<BodyGroupChoiceExport> Choices { get; init; }
	public bool HiddenInTools { get; init; }
}

internal sealed class BodyGroupChoiceExport
{
	public required string Name { get; init; }
	public required List<string> Meshes { get; init; }
}

internal sealed class MaterialRemapExport
{
	public required string From { get; init; }
	public required string To { get; init; }
}

internal sealed class HitboxSetExport
{
	public required string Name { get; init; }
	public List<HitboxExport> Hitboxes { get; } = new();
}

internal sealed class HitboxExport
{
	public required string Name { get; init; }
	public required string Bone { get; init; }
	public required float Radius { get; init; }
	public required Vector3 Point0 { get; init; }
	public required Vector3 Point1 { get; init; }
	public required string Tags { get; init; }
}

internal sealed class PhysicsShapeExport
{
	public required string ShapeClassName { get; init; }
	public required string ParentBone { get; init; }
	public required string SurfaceProp { get; init; }
	public Vector3 Origin { get; init; }
	public Vector3 Dimensions { get; init; }
	public Vector3 Center { get; init; }
	public float Radius { get; init; }
}

internal sealed class PhysicsBodyMarkupExport
{
	public required string TargetBody { get; init; }
	public float MassOverride { get; init; }
	public float InertiaScale { get; init; } = 1f;
	public float LinearDamping { get; init; }
	public float AngularDamping { get; init; }
	public bool UseMassCenterOverride { get; init; }
	public Vector3 MassCenterOverride { get; init; }
}

internal sealed class PhysicsJointExport
{
	public required string ClassName { get; init; }
	public required string ParentBody { get; init; }
	public required string ChildBody { get; init; }
	public required Vector3 AnchorOrigin { get; init; }
	public required float Friction { get; init; }
	public required float MinAngle { get; init; }
	public required float MaxAngle { get; init; }
	public required float SwingLimit { get; init; }

	public bool IsRevolute => string.Equals( ClassName, "PhysicsJointRevolute", StringComparison.Ordinal );

	public static PhysicsJointExport CreateRevolute( string parent, string child, Vector3 anchor, float friction, float min, float max )
	{
		return new PhysicsJointExport
		{
			ClassName = "PhysicsJointRevolute",
			ParentBody = parent,
			ChildBody = child,
			AnchorOrigin = anchor,
			Friction = MathF.Max( 0f, friction ),
			MinAngle = min,
			MaxAngle = max,
			SwingLimit = 0f
		};
	}

	public static PhysicsJointExport CreateConical( string parent, string child, Vector3 anchor, float friction, float swingLimit, float minTwist, float maxTwist )
	{
		return new PhysicsJointExport
		{
			ClassName = "PhysicsJointConical",
			ParentBody = parent,
			ChildBody = child,
			AnchorOrigin = anchor,
			Friction = MathF.Max( 0f, friction ),
			MinAngle = minTwist,
			MaxAngle = maxTwist,
			SwingLimit = MathF.Max( 0f, swingLimit )
		};
	}
}

internal readonly record struct Aabb( Vector3 Center, Vector3 Size );
internal readonly record struct Capsule( Vector3 Point0, Vector3 Point1, float Radius );

internal struct TriangleRecord
{
	public required string Material;
	public required VertexRecord V0;
	public required VertexRecord V1;
	public required VertexRecord V2;
}

internal struct VertexRecord
{
	public int PrimaryBone;
	public int SourceVertexIndex;
	public Vector3 Position;
	public Vector3 Normal;
	public Vector2 Uv;
	public int BoneCount;
	public int[] Bones;
	public float[] Weights;

	public static VertexRecord FromVvd( VvdVertex vertex, bool isStaticProp )
	{
		Vector3 position;
		Vector3 normal;

		if ( isStaticProp )
		{
			position = new Vector3( vertex.Position.y, -vertex.Position.x, vertex.Position.z );
			normal = new Vector3( vertex.Normal.y, -vertex.Normal.x, vertex.Normal.z );
		}
		else
		{
			position = vertex.Position;
			normal = vertex.Normal;
		}

		int count = vertex.BoneCount;
		if ( count <= 0 ) count = 1;
		if ( count > 3 ) count = 3;

		var bones = new int[count];
		var weights = new float[count];

		if ( count >= 1 )
		{
			bones[0] = vertex.Bone0;
			weights[0] = count == 1 ? 1f : vertex.Weight0;
		}
		if ( count >= 2 )
		{
			bones[1] = vertex.Bone1;
			weights[1] = vertex.Weight1;
		}
		if ( count >= 3 )
		{
			bones[2] = vertex.Bone2;
			weights[2] = vertex.Weight2;
		}

		float sum = 0f;
		for ( int i = 0; i < count; i++ ) sum += weights[i];
		if ( sum <= 0f )
		{
			weights[0] = 1f;
			sum = 1f;
		}
		if ( MathF.Abs( sum - 1f ) > 0.001f )
		{
			for ( int i = 0; i < count; i++ ) weights[i] /= sum;
		}

		return new VertexRecord
		{
			PrimaryBone = bones[0],
			SourceVertexIndex = -1,
			Position = position,
			Normal = normal,
			Uv = vertex.TexCoord,
			BoneCount = count,
			Bones = bones,
			Weights = weights
		};
	}
}
