using Datamodel;
using GModMount.Source;
using DMElement = Datamodel.Element;
using NQuaternion = System.Numerics.Quaternion;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;

internal static class DmxWriter
{
	public static void WriteMesh( string path, BuildContext context, MeshExport mesh )
	{
		using var dmx = new Datamodel.Datamodel( "model", 22 );

		DMElement root = CreateElement( dmx, "root", "DmElement" );
		string modelName = NameUtil.CleanName( context.ModelBaseName, "model" );
		DMElement model = CreateElement( dmx, modelName, "DmeModel" );
		model["transform"] = CreateTransform( dmx, modelName, NVector3.Zero, NQuaternion.Identity );
		model["visible"] = true;

		root["model"] = model;
		root["skeleton"] = model;
		root["exportTags"] = CreateExportTags( dmx );

		(ElementArray modelChildren, ElementArray jointList) = BuildSkeleton( dmx, context );

		MeshDagBuildResult meshDag = BuildMeshDag( dmx, context, mesh );
		modelChildren.Add( meshDag.Dag );
		jointList.Add( meshDag.Dag );

		model["children"] = modelChildren;
		model["jointList"] = jointList;
		model["upAxis"] = "Z";
		model["axisSystem"] = CreateAxisSystem( dmx );

		if ( meshDag.Morphs.Count > 0 )
		{
			root["combinationOperator"] = CreateCombinationOperator( dmx, meshDag.Mesh, meshDag.Morphs );
		}

		dmx.Root = root;

		string? directory = Path.GetDirectoryName( path );
		if ( !string.IsNullOrWhiteSpace( directory ) )
		{
			Directory.CreateDirectory( directory );
		}

		using var stream = File.Create( path );
		dmx.Save( stream, "binary", 9 );
	}

	private static (ElementArray Children, ElementArray JointList) BuildSkeleton( Datamodel.Datamodel dmx, BuildContext context )
	{
		List<MdlBone> bones = context.SourceModel.Mdl.Bones;
		var joints = new DMElement[bones.Count];
		var childrenArrays = new ElementArray[bones.Count];
		var rootChildren = new ElementArray();
		var jointList = new ElementArray();

		for ( int i = 0; i < bones.Count; i++ )
		{
			MdlBone bone = bones[i];
			string boneName = i >= 0 && i < context.ExportBoneNames.Count
				? context.ExportBoneNames[i]
				: BoneNameUtil.SanitizeBoneName( bone.Name, $"bone_{i}" );

			DMElement joint = CreateElement( dmx, boneName, "DmeJoint" );
			joint["transform"] = CreateTransform( dmx, boneName, ToNumerics( bone.Position ), ToNumerics( bone.Quaternion ) );
			joint["visible"] = true;

			var childArray = new ElementArray();
			joint["children"] = childArray;

			joints[i] = joint;
			childrenArrays[i] = childArray;
			jointList.Add( joint );
		}

		for ( int i = 0; i < bones.Count; i++ )
		{
			int parent = bones[i].ParentIndex;
			if ( parent >= 0 && parent < bones.Count )
			{
				childrenArrays[parent].Add( joints[i] );
			}
			else
			{
				rootChildren.Add( joints[i] );
			}
		}

		return (rootChildren, jointList);
	}

	private static MeshDagBuildResult BuildMeshDag( Datamodel.Datamodel dmx, BuildContext context, MeshExport mesh )
	{
		var positions = new Vector3Array();
		var normals = new Vector3Array();
		var texcoords = new Vector2Array();
		var cornerIndices = new IntArray();
		var corners = new List<VertexRecord>( mesh.Triangles.Count * 3 );
		var sourceVertexToCorners = new Dictionary<int, List<int>>();
		var materialFaces = new Dictionary<string, IntArray>( StringComparer.OrdinalIgnoreCase );

		void AddCorner( in VertexRecord vertex )
		{
			int cornerIndex = positions.Count;
			positions.Add( ToNumerics( vertex.Position ) );
			normals.Add( ToNumerics( vertex.Normal ) );
			texcoords.Add( ToNumerics( vertex.Uv ) );
			cornerIndices.Add( cornerIndex );
			corners.Add( vertex );

			if ( vertex.SourceVertexIndex >= 0 )
			{
				if ( !sourceVertexToCorners.TryGetValue( vertex.SourceVertexIndex, out List<int>? mappedCorners ) )
				{
					mappedCorners = new List<int>();
					sourceVertexToCorners[vertex.SourceVertexIndex] = mappedCorners;
				}

				mappedCorners.Add( cornerIndex );
			}
		}

		foreach ( TriangleRecord tri in mesh.Triangles )
		{
			string materialName = NormalizeMaterialName( tri.Material );
			if ( !materialFaces.TryGetValue( materialName, out IntArray? faces ) )
			{
				faces = new IntArray();
				materialFaces[materialName] = faces;
			}

			int startCorner = positions.Count;
			AddCorner( tri.V0 );
			AddCorner( tri.V1 );
			AddCorner( tri.V2 );

			faces.Add( startCorner );
			faces.Add( startCorner + 1 );
			faces.Add( startCorner + 2 );
			faces.Add( -1 );
		}

		int boneCount = Math.Max( 1, context.SourceModel.Mdl.Bones.Count );
		int jointCount = Math.Clamp( corners.Count == 0 ? 1 : corners.Max( c => Math.Clamp( c.BoneCount, 1, 3 ) ), 1, 3 );
		var blendWeights = new FloatArray();
		var blendIndices = new IntArray();
		foreach ( VertexRecord corner in corners )
		{
			int fallbackBone = ClampBoneIndex( corner.PrimaryBone, boneCount );
			for ( int i = 0; i < jointCount; i++ )
			{
				bool hasWeight = i < corner.BoneCount && corner.Bones is { Length: > 0 } && corner.Weights is { Length: > 0 } &&
					i < corner.Bones.Length && i < corner.Weights.Length;
				if ( hasWeight )
				{
					blendIndices.Add( ClampBoneIndex( corner.Bones[i], boneCount ) );
					blendWeights.Add( corner.Weights[i] );
				}
				else
				{
					blendIndices.Add( fallbackBone );
					blendWeights.Add( 0f );
				}
			}
		}

		DMElement vertexData = CreateElement( dmx, "bind", "DmeVertexData" );
		var vertexFormat = new StringArray
		{
			"position$0",
			"normal$0",
			"texcoord$0",
			"blendweights$0",
			"blendindices$0"
		};

		vertexData["vertexFormat"] = vertexFormat;
		vertexData["jointCount"] = jointCount;
		vertexData["flipVCoordinates"] = false;
		vertexData["position$0"] = positions;
		vertexData["position$0Indices"] = cornerIndices;
		vertexData["normal$0"] = normals;
		vertexData["normal$0Indices"] = cornerIndices;
		vertexData["texcoord$0"] = texcoords;
		vertexData["texcoord$0Indices"] = cornerIndices;
		vertexData["blendweights$0"] = blendWeights;
		vertexData["blendindices$0"] = blendIndices;

		var faceSets = new ElementArray();
		int faceSetIndex = 0;
		foreach ( KeyValuePair<string, IntArray> pair in materialFaces )
		{
			string faceSetName = NameUtil.CleanName( pair.Key, $"faceset_{faceSetIndex}" );
			DMElement faceSet = CreateElement( dmx, faceSetName, "DmeFaceSet" );
			faceSet["faces"] = pair.Value;
			DMElement material = CreateElement( dmx, NameUtil.CleanName( pair.Key, "material" ), "DmeMaterial" );
			material["mtlName"] = pair.Key;
			faceSet["material"] = material;
			faceSets.Add( faceSet );
			faceSetIndex++;
		}

		DMElement dmeMesh = CreateElement( dmx, mesh.Name, "DmeMesh" );
		dmeMesh["visible"] = true;
		dmeMesh["currentState"] = vertexData;
		var baseStates = new ElementArray();
		baseStates.Add( vertexData );
		dmeMesh["baseStates"] = baseStates;
		dmeMesh["faceSets"] = faceSets;

		List<MeshMorphExport> writtenMorphs = AddMorphDeltaStates( dmx, dmeMesh, mesh.Morphs, sourceVertexToCorners );

		DMElement dag = CreateElement( dmx, mesh.Name, "DmeDag" );
		dag["transform"] = CreateTransform( dmx, mesh.Name, NVector3.Zero, NQuaternion.Identity );
		dag["shape"] = dmeMesh;
		dag["visible"] = true;

		return new MeshDagBuildResult
		{
			Dag = dag,
			Mesh = dmeMesh,
			Morphs = writtenMorphs
		};
	}

	private static List<MeshMorphExport> AddMorphDeltaStates(
		Datamodel.Datamodel dmx,
		DMElement dmeMesh,
		List<MeshMorphExport> morphs,
		Dictionary<int, List<int>> sourceVertexToCorners )
	{
		var writtenMorphs = new List<MeshMorphExport>( morphs.Count );
		var deltaStates = new ElementArray();

		foreach ( MeshMorphExport morph in morphs.OrderBy( m => m.Name, StringComparer.OrdinalIgnoreCase ) )
		{
			var cornerDeltaMap = new Dictionary<int, (NVector3 Position, NVector3 Normal)>();

			foreach ( MeshMorphDeltaExport delta in morph.Deltas )
			{
				if ( !sourceVertexToCorners.TryGetValue( delta.SourceVertexIndex, out List<int>? cornerIndices ) || cornerIndices.Count == 0 )
				{
					continue;
				}

				NVector3 posDelta = ToNumerics( delta.PositionDelta );
				NVector3 nrmDelta = ToNumerics( delta.NormalDelta );

				foreach ( int cornerIndex in cornerIndices )
				{
					if ( cornerDeltaMap.TryGetValue( cornerIndex, out (NVector3 Position, NVector3 Normal) existing ) )
					{
						cornerDeltaMap[cornerIndex] = (existing.Position + posDelta, existing.Normal + nrmDelta);
					}
					else
					{
						cornerDeltaMap[cornerIndex] = (posDelta, nrmDelta);
					}
				}
			}

			if ( cornerDeltaMap.Count == 0 )
			{
				continue;
			}

			var orderedCornerIndices = cornerDeltaMap.Keys.OrderBy( i => i ).ToList();
			var positionDeltas = new Vector3Array();
			var positionDeltaIndices = new IntArray();
			var normalDeltas = new Vector3Array();
			var normalDeltaIndices = new IntArray();
			bool hasPosition = false;
			bool hasNormal = false;

			foreach ( int cornerIndex in orderedCornerIndices )
			{
				(NVector3 Position, NVector3 Normal) values = cornerDeltaMap[cornerIndex];
				if ( !IsNearlyZero( values.Position ) )
				{
					positionDeltas.Add( values.Position );
					positionDeltaIndices.Add( cornerIndex );
					hasPosition = true;
				}

				if ( !IsNearlyZero( values.Normal ) )
				{
					normalDeltas.Add( values.Normal );
					normalDeltaIndices.Add( cornerIndex );
					hasNormal = true;
				}
			}

			if ( !hasPosition && !hasNormal )
			{
				continue;
			}

			DMElement deltaState = CreateElement( dmx, morph.Name, "DmeVertexDeltaData" );
			var vertexFormat = new StringArray();
			if ( hasPosition ) vertexFormat.Add( "position$0" );
			if ( hasNormal ) vertexFormat.Add( "normal$0" );
			deltaState["vertexFormat"] = vertexFormat;
			deltaState["jointCount"] = 0;
			deltaState["flipVCoordinates"] = false;
			deltaState["corrected"] = true;

			if ( hasPosition )
			{
				deltaState["position$0"] = positionDeltas;
				deltaState["position$0Indices"] = positionDeltaIndices;
			}

			if ( hasNormal )
			{
				deltaState["normal$0"] = normalDeltas;
				deltaState["normal$0Indices"] = normalDeltaIndices;
			}

			deltaStates.Add( deltaState );
			writtenMorphs.Add( morph );
		}

		if ( writtenMorphs.Count > 0 )
		{
			dmeMesh["deltaStates"] = deltaStates;

			var weights = new Vector2Array();
			var laggedWeights = new Vector2Array();
			for ( int i = 0; i < writtenMorphs.Count; i++ )
			{
				weights.Add( new NVector2( 0f, 0f ) );
				laggedWeights.Add( new NVector2( 0f, 0f ) );
			}

			dmeMesh["deltaStateWeights"] = weights;
			dmeMesh["deltaStateWeightsLagged"] = laggedWeights;
		}

		return writtenMorphs;
	}

	private static DMElement CreateCombinationOperator( Datamodel.Datamodel dmx, DMElement targetMesh, IReadOnlyList<MeshMorphExport> morphs )
	{
		DMElement combo = CreateElement( dmx, "combinationOperator", "DmeCombinationOperator" );
		var controls = new ElementArray();
		var controlValues = new Vector3Array();
		var controlValuesLagged = new Vector3Array();

		foreach ( MeshMorphExport morph in morphs.OrderBy( n => n.Name, StringComparer.OrdinalIgnoreCase ) )
		{
			string controlName = string.IsNullOrWhiteSpace( morph.DisplayName ) ? morph.Name : morph.DisplayName;
			DMElement control = CreateElement( dmx, controlName, "DmeCombinationInputControl" );
			control["rawControlNames"] = new StringArray { morph.Name };
			control["stereo"] = false;
			control["eyelid"] = false;
			control["wrinkleScales"] = new FloatArray { 0f };
			controls.Add( control );

			controlValues.Add( new NVector3( 0f, 0.5f, 0.5f ) );
			controlValuesLagged.Add( new NVector3( 0f, 0.5f, 0.5f ) );
		}

		var targets = new ElementArray();
		targets.Add( targetMesh );

		combo["controls"] = controls;
		combo["controlValues"] = controlValues;
		combo["controlValuesLagged"] = controlValuesLagged;
		combo["useLaggedValues"] = false;
		combo["targets"] = targets;
		return combo;
	}

	private static DMElement CreateTransform( Datamodel.Datamodel dmx, string name, NVector3 position, NQuaternion orientation )
	{
		DMElement transform = CreateElement( dmx, name, "DmeTransform" );
		transform["position"] = position;
		transform["orientation"] = NormalizeQuaternion( orientation );
		transform["scale"] = 1f;
		return transform;
	}

	private static DMElement CreateAxisSystem( Datamodel.Datamodel dmx )
	{
		DMElement axisSystem = CreateElement( dmx, "axisSystem", "DmeAxisSystem" );
		axisSystem["upAxis"] = 3;
		// Match known-good citizen DMX orientation metadata used by s&box.
		axisSystem["forwardParity"] = -2;
		axisSystem["coordSys"] = 0;
		return axisSystem;
	}

	private static DMElement CreateExportTags( Datamodel.Datamodel dmx )
	{
		DMElement exportTags = CreateElement( dmx, "exportTags", "DmeExportTags" );
		exportTags["source"] = "MdlToVmdlConverter";
		exportTags["date"] = DateTime.UtcNow.ToString( "yyyy/MM/dd", CultureInfo.InvariantCulture );
		exportTags["time"] = DateTime.UtcNow.ToString( "HH:mm:ss", CultureInfo.InvariantCulture );
		return exportTags;
	}

	private static DMElement CreateElement( Datamodel.Datamodel dmx, string name, string className )
	{
		return new DMElement( dmx, string.IsNullOrWhiteSpace( name ) ? className : name, null, className );
	}

	private static NVector3 ToNumerics( Vector3 value ) => new( value.x, value.y, value.z );
	private static NVector2 ToNumerics( Vector2 value ) => new( value.x, value.y );

	private static NQuaternion ToNumerics( Quaternion value ) => new( value.x, value.y, value.z, value.w );

	private static NQuaternion NormalizeQuaternion( NQuaternion quaternion )
	{
		float lengthSq = quaternion.LengthSquared();
		if ( lengthSq <= 1e-8f )
		{
			return NQuaternion.Identity;
		}

		return NQuaternion.Normalize( quaternion );
	}

	private static bool IsNearlyZero( NVector3 value )
	{
		return MathF.Abs( value.X ) <= 1e-6f &&
			MathF.Abs( value.Y ) <= 1e-6f &&
			MathF.Abs( value.Z ) <= 1e-6f;
	}

	private static string NormalizeMaterialName( string? material )
	{
		if ( string.IsNullOrWhiteSpace( material ) )
		{
			return "default";
		}

		return material.Trim();
	}

	private static int ClampBoneIndex( int value, int boneCount )
	{
		if ( boneCount <= 0 )
		{
			return 0;
		}

		if ( value < 0 ) return 0;
		if ( value >= boneCount ) return boneCount - 1;
		return value;
	}

	private sealed class MeshDagBuildResult
	{
		public required DMElement Dag { get; init; }
		public required DMElement Mesh { get; init; }
		public required List<MeshMorphExport> Morphs { get; init; }
	}
}
