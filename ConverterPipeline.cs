using GModMount.Source;

internal static class ConverterPipeline
{
	public static BuildContext Build( SourceModel sourceModel, string modelBaseName, string outputDir )
	{
		var context = new BuildContext
		{
			SourceModel = sourceModel,
			ModelBaseName = modelBaseName,
			OutputDirectory = outputDir
		};
		context.ExportBoneNames = BoneNameUtil.BuildExportBoneNames( sourceModel.Mdl.Bones );

		BuildRenderMeshesAndBodyGroups( context );
		BuildHitboxes( context );
		BuildPhysics( context );

		return context;
	}

	private static void BuildRenderMeshesAndBodyGroups( BuildContext context )
	{
		MdlFile mdl = context.SourceModel.Mdl;
		VtxFile vtx = context.SourceModel.Vtx;
		VvdVertex[] vertices = context.SourceModel.Vvd.GetVerticesForLod( 0 );
		bool isStaticProp = mdl.Header.IsStaticProp;

		int bodyPartVertexIndexStart = 0;
		for ( int bpIdx = 0; bpIdx < mdl.BodyParts.Count; bpIdx++ )
		{
			MdlBodyPart mdlBodyPart = mdl.BodyParts[bpIdx];
			VtxBodyPartData? vtxBodyPart = bpIdx < vtx.BodyParts.Count ? vtx.BodyParts[bpIdx] : null;
			string bodyGroupName = NameUtil.CleanName( mdlBodyPart.Name, $"bodypart_{bpIdx}" );

			var choices = new List<BodyGroupChoiceExport>();
			int nonEmptyChoiceCount = 0;
			bool hasExplicitEmptyChoice = false;

			for ( int modelIdx = 0; modelIdx < mdlBodyPart.Models.Count; modelIdx++ )
			{
				MdlModel mdlModel = mdlBodyPart.Models[modelIdx];
				VtxModelData? vtxModel = vtxBodyPart is not null && modelIdx < vtxBodyPart.Models.Count ? vtxBodyPart.Models[modelIdx] : null;
				string choiceName = NameUtil.CleanName( mdlModel.Name, $"choice_{modelIdx}" );

				bool isExplicitOff = string.IsNullOrWhiteSpace( mdlModel.Name ) ||
					mdlModel.Name.StartsWith( "blank", StringComparison.OrdinalIgnoreCase ) ||
					mdlModel.Meshes.Count == 0 ||
					vtxModel is null ||
					vtxModel.Lods.Count == 0;

				if ( isExplicitOff )
				{
					hasExplicitEmptyChoice = true;
					choices.Add( new BodyGroupChoiceExport
					{
						Name = choiceName,
						Meshes = new List<string>()
					} );
					bodyPartVertexIndexStart += mdlModel.VertexCount;
					continue;
				}

				VtxLodData lod = vtxModel!.Lods[0];
				List<TriangleRecord> triangles = BuildModelTriangles( mdl, lod, mdlModel, vertices, bodyPartVertexIndexStart, isStaticProp );
				if ( triangles.Count == 0 )
				{
					choices.Add( new BodyGroupChoiceExport
					{
						Name = choiceName,
						Meshes = new List<string>()
					} );
					bodyPartVertexIndexStart += mdlModel.VertexCount;
					continue;
				}

				string meshName = NameUtil.CleanName( $"{bodyGroupName}_{modelIdx}", $"mesh_{bpIdx}_{modelIdx}" );
				string fileName = NameUtil.CleanFileName( $"{context.ModelBaseName}_{bpIdx}_{modelIdx}.smd" );
				context.Meshes.Add( new MeshExport
				{
					Name = meshName,
					FileName = fileName,
					Triangles = triangles
				} );

				nonEmptyChoiceCount++;
				choices.Add( new BodyGroupChoiceExport
				{
					Name = choiceName,
					Meshes = new List<string> { meshName }
				} );

				bodyPartVertexIndexStart += mdlModel.VertexCount;
			}

			if ( nonEmptyChoiceCount == 1 && !hasExplicitEmptyChoice )
			{
				choices.Add( new BodyGroupChoiceExport
				{
					Name = "off",
					Meshes = new List<string>()
				} );
			}

			context.BodyGroups.Add( new BodyGroupExport
			{
				Name = bodyGroupName,
				Choices = choices
			} );
		}

		AddSkeletonAnchorMesh( context );
	}

	private static void AddSkeletonAnchorMesh( BuildContext context )
	{
		List<MdlBone> bones = context.SourceModel.Mdl.Bones;
		if ( bones.Count == 0 )
		{
			return;
		}

		var triangles = new List<TriangleRecord>();
		const float epsilon = 0.0001f;

		for ( int i = 0; i < bones.Count; i += 3 )
		{
			int b0 = bones[i].Index;
			int b1 = bones[Math.Min( i + 1, bones.Count - 1 )].Index;
			int b2 = bones[Math.Min( i + 2, bones.Count - 1 )].Index;

			triangles.Add( new TriangleRecord
			{
				Material = "__skeleton_anchor",
				V0 = CreateAnchorVertex( b0, 0f, 0f, 0f ),
				V1 = CreateAnchorVertex( b1, epsilon, 0f, 0f ),
				V2 = CreateAnchorVertex( b2, 0f, epsilon, 0f )
			} );
		}

		const string anchorMeshName = "__skeleton_anchor";
		context.Meshes.Insert( 0, new MeshExport
		{
			Name = anchorMeshName,
			FileName = NameUtil.CleanFileName( $"{context.ModelBaseName}_skeleton_anchor.smd" ),
			Triangles = triangles
		} );

		context.BodyGroups.Add( new BodyGroupExport
		{
			Name = "__internal",
			HiddenInTools = true,
			Choices = new List<BodyGroupChoiceExport>
			{
				new()
				{
					Name = "off",
					Meshes = new List<string>()
				},
				new()
				{
					Name = "skeleton_anchor",
					Meshes = new List<string> { anchorMeshName }
				}
			}
		} );
	}

	private static VertexRecord CreateAnchorVertex( int boneIndex, float x, float y, float z )
	{
		return new VertexRecord
		{
			PrimaryBone = boneIndex,
			Position = new Vector3( x, y, z ),
			Normal = new Vector3( 0f, 0f, 1f ),
			Uv = new Vector2( 0f, 0f ),
			BoneCount = 1,
			Bones = [boneIndex],
			Weights = [1f]
		};
	}

	private static List<TriangleRecord> BuildModelTriangles(
		MdlFile mdl,
		VtxLodData lod,
		MdlModel mdlModel,
		VvdVertex[] vertices,
		int bodyPartVertexIndexStart,
		bool isStaticProp )
	{
		var triangles = new List<TriangleRecord>();

		for ( int meshIdx = 0; meshIdx < mdlModel.Meshes.Count && meshIdx < lod.Meshes.Count; meshIdx++ )
		{
			MdlMesh mdlMesh = mdlModel.Meshes[meshIdx];
			VtxMeshData vtxMesh = lod.Meshes[meshIdx];
			string materialName = NameUtil.CleanMaterialName( GetMaterialName( mdl, mdlMesh.MaterialIndex ) );

			foreach ( VtxStripGroupData stripGroup in vtxMesh.StripGroups )
			{
				if ( stripGroup.Indices.Length == 0 || stripGroup.Vertices.Length == 0 )
				{
					continue;
				}

				foreach ( VtxStripData strip in stripGroup.Strips )
				{
					if ( strip.IsTriList )
					{
						for ( int i = 0; i + 2 < strip.IndexCount; i += 3 )
						{
							TryAddTriangle(
								triangles,
								stripGroup,
								mdlMesh,
								bodyPartVertexIndexStart,
								vertices,
								strip.IndexOffset + i,
								strip.IndexOffset + i + 2,
								strip.IndexOffset + i + 1,
								materialName,
								isStaticProp
							);
						}
					}
					else if ( strip.IsTriStrip )
					{
						for ( int i = 0; i + 2 < strip.IndexCount; i++ )
						{
							int idx0 = strip.IndexOffset + i;
							int idx1 = strip.IndexOffset + i + 1;
							int idx2 = strip.IndexOffset + i + 2;

							if ( i % 2 == 0 )
							{
								TryAddTriangle( triangles, stripGroup, mdlMesh, bodyPartVertexIndexStart, vertices, idx0, idx2, idx1, materialName, isStaticProp );
							}
							else
							{
								TryAddTriangle( triangles, stripGroup, mdlMesh, bodyPartVertexIndexStart, vertices, idx0, idx1, idx2, materialName, isStaticProp );
							}
						}
					}
				}
			}
		}

		return triangles;
	}

	private static void TryAddTriangle(
		List<TriangleRecord> triangles,
		VtxStripGroupData stripGroup,
		MdlMesh mdlMesh,
		int bodyPartVertexIndexStart,
		VvdVertex[] vertices,
		int index0,
		int index1,
		int index2,
		string materialName,
		bool isStaticProp )
	{
		if ( !TryResolveVertex( stripGroup, mdlMesh, bodyPartVertexIndexStart, vertices, index0, isStaticProp, out VertexRecord v0 ) ) return;
		if ( !TryResolveVertex( stripGroup, mdlMesh, bodyPartVertexIndexStart, vertices, index1, isStaticProp, out VertexRecord v1 ) ) return;
		if ( !TryResolveVertex( stripGroup, mdlMesh, bodyPartVertexIndexStart, vertices, index2, isStaticProp, out VertexRecord v2 ) ) return;

		triangles.Add( new TriangleRecord
		{
			Material = materialName,
			V0 = v0,
			V1 = v1,
			V2 = v2
		} );
	}

	private static bool TryResolveVertex(
		VtxStripGroupData stripGroup,
		MdlMesh mdlMesh,
		int bodyPartVertexIndexStart,
		VvdVertex[] vertices,
		int stripGroupIndex,
		bool isStaticProp,
		out VertexRecord vertex )
	{
		vertex = default;

		if ( stripGroupIndex < 0 || stripGroupIndex >= stripGroup.Indices.Length )
		{
			return false;
		}

		int stripVertexIndex = stripGroup.Indices[stripGroupIndex];
		if ( stripVertexIndex < 0 || stripVertexIndex >= stripGroup.Vertices.Length )
		{
			return false;
		}

		VtxVertex vtxVertex = stripGroup.Vertices[stripVertexIndex];
		int vertexIndex = vtxVertex.OriginalMeshVertexIndex + bodyPartVertexIndexStart + mdlMesh.VertexOffset;
		if ( vertexIndex < 0 || vertexIndex >= vertices.Length )
		{
			return false;
		}

		vertex = VertexRecord.FromVvd( vertices[vertexIndex], isStaticProp );
		return true;
	}

	private static string GetMaterialName( MdlFile mdl, int materialIndex )
	{
		if ( materialIndex < 0 || materialIndex >= mdl.Materials.Count )
		{
			return "default";
		}

		string material = mdl.Materials[materialIndex];
		return string.IsNullOrWhiteSpace( material ) ? "default" : material;
	}

	private static void BuildHitboxes( BuildContext context )
	{
		foreach ( MdlHitboxSet hitboxSet in context.SourceModel.Mdl.HitboxSets )
		{
			var exportedSet = new HitboxSetExport
			{
				Name = NameUtil.CleanName( hitboxSet.Name, $"hitboxset_{hitboxSet.Index}" )
			};

			foreach ( MdlHitbox hitbox in hitboxSet.Hitboxes )
			{
				Capsule capsule = GeometryUtil.CapsuleFromBounds( hitbox.Min, hitbox.Max );
				exportedSet.Hitboxes.Add( new HitboxExport
				{
					Name = NameUtil.CleanName( hitbox.Name, $"hitbox_{hitbox.Index}" ),
					Bone = GetExportBoneName( context, hitbox.BoneIndex ),
					Radius = capsule.Radius,
					Point0 = capsule.Point0,
					Point1 = capsule.Point1,
					Tags = $"group_{hitbox.Group}"
				} );
			}

			context.HitboxSets.Add( exportedSet );
		}
	}

	private static void BuildPhysics( BuildContext context )
	{
		PhyFile? phy = context.SourceModel.Phy;
		if ( phy is null ) return;

		List<MdlBone> bones = context.SourceModel.Mdl.Bones;
		IReadOnlyList<string> exportBoneNames = context.ExportBoneNames;
		var sourceBoneLookup = bones.ToDictionary( b => b.Name, b => b.Index, StringComparer.OrdinalIgnoreCase );
		var exportBoneLookup = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
		for ( int i = 0; i < exportBoneNames.Count; i++ )
		{
			exportBoneLookup[exportBoneNames[i]] = i;
		}

		BuildBoneWorldTransforms( bones, out System.Numerics.Vector3[] worldPositions, out System.Numerics.Quaternion[] worldRotations );

		int maxConstraintIndex = -1;
		foreach ( PhyRagdollConstraint c in phy.RagdollConstraints )
		{
			if ( c.ParentIndex > maxConstraintIndex ) maxConstraintIndex = c.ParentIndex;
			if ( c.ChildIndex > maxConstraintIndex ) maxConstraintIndex = c.ChildIndex;
		}

		int bodyCount = Math.Max( Math.Max( phy.CollisionData.Count, phy.Solids.Count ), maxConstraintIndex + 1 );
		var bodyNames = new string[bodyCount];
		var bodyBoneIndices = new int[bodyCount];
		var bodySurfaceProps = new string[bodyCount];
		var bodyHasShape = new bool[bodyCount];
		Array.Fill( bodyBoneIndices, -1 );

		var solidsByIndex = new Dictionary<int, PhySolid>();
		foreach ( PhySolid solid in phy.Solids )
		{
			if ( solid.Index >= 0 && !solidsByIndex.ContainsKey( solid.Index ) )
			{
				solidsByIndex[solid.Index] = solid;
			}
		}

		for ( int solidIdx = 0; solidIdx < bodyCount; solidIdx++ )
		{
			PhySolid? solid = null;
			if ( !solidsByIndex.TryGetValue( solidIdx, out solid ) && solidIdx < phy.Solids.Count )
			{
				solid = phy.Solids[solidIdx];
			}

			string rawName = solid?.Name ?? string.Empty;

			int boneIndex = -1;
			if ( !string.IsNullOrWhiteSpace( rawName ) )
			{
				sourceBoneLookup.TryGetValue( rawName, out boneIndex );
			}

			if ( boneIndex < 0 && !string.IsNullOrWhiteSpace( rawName ) )
			{
				string sanitizedRaw = BoneNameUtil.CanonicalizeForModelDoc( BoneNameUtil.SanitizeBoneName( rawName, $"solid_{solidIdx}" ) );
				exportBoneLookup.TryGetValue( sanitizedRaw, out boneIndex );
			}
			bodyBoneIndices[solidIdx] = boneIndex;
			bodyNames[solidIdx] = boneIndex >= 0 && boneIndex < exportBoneNames.Count
				? exportBoneNames[boneIndex]
				: NormalizeBodyName( rawName, $"solid_{solidIdx}" );
			bodySurfaceProps[solidIdx] = solid is null || string.IsNullOrWhiteSpace( solid.SurfaceProp )
				? "default"
				: solid.SurfaceProp;

			context.PhysicsBodies.Add( new PhysicsBodyMarkupExport
			{
				TargetBody = bodyNames[solidIdx],
				MassOverride = 0f,
				InertiaScale = 1f,
				LinearDamping = 0f,
				AngularDamping = 0f,
				UseMassCenterOverride = false,
				MassCenterOverride = new Vector3( 0f, 0f, 0f )
			} );

			if ( solidIdx >= phy.CollisionData.Count )
			{
				continue;
			}

			PhyCollisionData collision = phy.CollisionData[solidIdx];
			foreach ( PhyConvexMesh mesh in collision.ConvexMeshes )
			{
				if ( mesh.Vertices is null || mesh.Vertices.Count < 4 )
				{
					continue;
				}

				if ( boneIndex < 0 || boneIndex >= bones.Count )
				{
					continue;
				}

				Aabb aabb = GeometryUtil.ComputeAabb( mesh.Vertices );
				context.PhysicsShapes.Add( new PhysicsShapeExport
				{
					ShapeClassName = "PhysicsShapeBox",
					ParentBone = exportBoneNames[boneIndex],
					SurfaceProp = bodySurfaceProps[solidIdx],
					Origin = aabb.Center,
					Dimensions = aabb.Size
				} );
				bodyHasShape[solidIdx] = true;
			}
		}

		for ( int solidIdx = 0; solidIdx < bodyCount; solidIdx++ )
		{
			if ( bodyHasShape[solidIdx] )
			{
				continue;
			}

			int boneIndex = bodyBoneIndices[solidIdx];
			if ( boneIndex < 0 || boneIndex >= bones.Count )
			{
				continue;
			}

			context.PhysicsShapes.Add( new PhysicsShapeExport
			{
				ShapeClassName = "PhysicsShapeSphere",
				ParentBone = exportBoneNames[boneIndex],
				SurfaceProp = bodySurfaceProps[solidIdx],
				Center = new Vector3( 0f, 0f, 0f ),
				Radius = 0.1f
			} );
		}

		var uniqueBodies = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		context.PhysicsBodies.RemoveAll( b => !uniqueBodies.Add( b.TargetBody ) );

		for ( int i = 0; i < phy.RagdollConstraints.Count; i++ )
		{
			PhyRagdollConstraint c = phy.RagdollConstraints[i];
			if ( c.ParentIndex < 0 || c.ParentIndex >= bodyNames.Length || c.ChildIndex < 0 || c.ChildIndex >= bodyNames.Length )
			{
				continue;
			}

			string parentBody = bodyNames[c.ParentIndex];
			string childBody = bodyNames[c.ChildIndex];
			if ( string.IsNullOrWhiteSpace( parentBody ) || string.IsNullOrWhiteSpace( childBody ) )
			{
				continue;
			}
			if ( string.Equals( parentBody, childBody, StringComparison.OrdinalIgnoreCase ) )
			{
				continue;
			}

			Vector3 anchor = new Vector3( 0f, 0f, 0f );
			int parentBoneIndex = bodyBoneIndices[c.ParentIndex];
			int childBoneIndex = bodyBoneIndices[c.ChildIndex];
			if ( parentBoneIndex >= 0 && parentBoneIndex < worldPositions.Length &&
				 childBoneIndex >= 0 && childBoneIndex < worldPositions.Length )
			{
				System.Numerics.Vector3 delta = worldPositions[childBoneIndex] - worldPositions[parentBoneIndex];
				System.Numerics.Quaternion invParent = System.Numerics.Quaternion.Inverse( worldRotations[parentBoneIndex] );
				System.Numerics.Vector3 localAnchor = System.Numerics.Vector3.Transform( delta, invParent );
				anchor = new Vector3( localAnchor.X, localAnchor.Y, localAnchor.Z );
			}
			else if ( childBoneIndex >= 0 && childBoneIndex < bones.Count )
			{
				anchor = bones[childBoneIndex].Position;
			}

			float friction = MathF.Max( c.XFriction, MathF.Max( c.YFriction, c.ZFriction ) );
			float xRange = MathF.Abs( c.XMax - c.XMin );
			float yRange = MathF.Abs( c.YMax - c.YMin );
			float zRange = MathF.Abs( c.ZMax - c.ZMin );
			int activeAxes = (xRange > 5f ? 1 : 0) + (yRange > 5f ? 1 : 0) + (zRange > 5f ? 1 : 0);

			if ( activeAxes <= 1 )
			{
				(float min, float max) = SelectSingleAxisLimits( c );
				context.PhysicsJoints.Add( PhysicsJointExport.CreateRevolute( parentBody, childBody, anchor, friction, min, max ) );
			}
			else
			{
				float swing = MathF.Max(
					MathF.Max( MathF.Abs( c.YMin ), MathF.Abs( c.YMax ) ),
					MathF.Max( MathF.Abs( c.ZMin ), MathF.Abs( c.ZMax ) )
				);
				context.PhysicsJoints.Add( PhysicsJointExport.CreateConical( parentBody, childBody, anchor, friction, swing, c.XMin, c.XMax ) );
			}
		}
	}

	private static string NormalizeBodyName( string rawName, string fallback )
	{
		if ( string.IsNullOrWhiteSpace( rawName ) )
		{
			return fallback;
		}

		string trimmed = rawName.Trim();
		string sanitized = BoneNameUtil.SanitizeBoneName( trimmed, fallback );
		return BoneNameUtil.CanonicalizeForModelDoc( sanitized );
	}

	private static string GetExportBoneName( BuildContext context, int boneIndex )
	{
		if ( boneIndex < 0 || boneIndex >= context.ExportBoneNames.Count )
		{
			return string.Empty;
		}

		return context.ExportBoneNames[boneIndex];
	}

	private static void BuildBoneWorldTransforms(
		List<MdlBone> bones,
		out System.Numerics.Vector3[] worldPositions,
		out System.Numerics.Quaternion[] worldRotations )
	{
		worldPositions = new System.Numerics.Vector3[bones.Count];
		worldRotations = new System.Numerics.Quaternion[bones.Count];
		var computed = new bool[bones.Count];

		for ( int i = 0; i < bones.Count; i++ )
		{
			EnsureBoneWorldTransform( i, bones, worldPositions, worldRotations, computed );
		}
	}

	private static void EnsureBoneWorldTransform(
		int boneIndex,
		List<MdlBone> bones,
		System.Numerics.Vector3[] worldPositions,
		System.Numerics.Quaternion[] worldRotations,
		bool[] computed )
	{
		if ( computed[boneIndex] )
		{
			return;
		}

		MdlBone bone = bones[boneIndex];
		System.Numerics.Vector3 localPos = new System.Numerics.Vector3( bone.Position.x, bone.Position.y, bone.Position.z );
		System.Numerics.Quaternion localRot = new System.Numerics.Quaternion( bone.Quaternion.x, bone.Quaternion.y, bone.Quaternion.z, bone.Quaternion.w );
		if ( localRot.LengthSquared() < 1e-8f )
		{
			localRot = System.Numerics.Quaternion.Identity;
		}
		else
		{
			localRot = System.Numerics.Quaternion.Normalize( localRot );
		}

		if ( bone.ParentIndex >= 0 && bone.ParentIndex < bones.Count )
		{
			EnsureBoneWorldTransform( bone.ParentIndex, bones, worldPositions, worldRotations, computed );
			System.Numerics.Quaternion parentRot = worldRotations[bone.ParentIndex];
			System.Numerics.Vector3 parentPos = worldPositions[bone.ParentIndex];
			System.Numerics.Vector3 rotatedLocal = System.Numerics.Vector3.Transform( localPos, parentRot );
			worldPositions[boneIndex] = parentPos + rotatedLocal;
			worldRotations[boneIndex] = System.Numerics.Quaternion.Normalize( localRot * parentRot );
		}
		else
		{
			worldPositions[boneIndex] = localPos;
			worldRotations[boneIndex] = localRot;
		}

		computed[boneIndex] = true;
	}

	private static (float min, float max) SelectSingleAxisLimits( PhyRagdollConstraint c )
	{
		float xRange = MathF.Abs( c.XMax - c.XMin );
		float yRange = MathF.Abs( c.YMax - c.YMin );
		float zRange = MathF.Abs( c.ZMax - c.ZMin );
		if ( xRange >= yRange && xRange >= zRange ) return (c.XMin, c.XMax);
		if ( yRange >= zRange ) return (c.YMin, c.YMax);
		return (c.ZMin, c.ZMax);
	}
}
