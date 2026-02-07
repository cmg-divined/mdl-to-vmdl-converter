using GModMount.Source;

internal static class SmdWriter
{
	public static void WriteMesh( string path, BuildContext context, MeshExport mesh )
	{
		using var writer = new StreamWriter( path, false, Encoding.ASCII );
		SourceModel sourceModel = context.SourceModel;
		IReadOnlyList<string> exportBoneNames = context.ExportBoneNames;
		var writtenVertices = new List<VertexRecord>( mesh.Triangles.Count * 3 );

		writer.WriteLine( "version 1" );
		writer.WriteLine( "nodes" );
		foreach ( MdlBone bone in sourceModel.Mdl.Bones )
		{
			string boneName = bone.Index >= 0 && bone.Index < exportBoneNames.Count
				? exportBoneNames[bone.Index]
				: BoneNameUtil.SanitizeBoneName( bone.Name, $"bone_{bone.Index}" );
			writer.WriteLine( $"  {bone.Index} \"{Escape( boneName )}\" {bone.ParentIndex}" );
		}
		writer.WriteLine( "end" );

		writer.WriteLine( "skeleton" );
		writer.WriteLine( "  time 0" );
		foreach ( MdlBone bone in sourceModel.Mdl.Bones )
		{
			writer.WriteLine(
				$"    {bone.Index} {Fmt( bone.Position.x )} {Fmt( bone.Position.y )} {Fmt( bone.Position.z )} {Fmt( bone.Rotation.x )} {Fmt( bone.Rotation.y )} {Fmt( bone.Rotation.z )}"
			);
		}
		writer.WriteLine( "end" );

		writer.WriteLine( "triangles" );
		foreach ( TriangleRecord triangle in mesh.Triangles )
		{
			writer.WriteLine( triangle.Material );
			WriteVertex( writer, triangle.V0 );
			writtenVertices.Add( triangle.V0 );
			WriteVertex( writer, triangle.V1 );
			writtenVertices.Add( triangle.V1 );
			WriteVertex( writer, triangle.V2 );
			writtenVertices.Add( triangle.V2 );
		}
		writer.WriteLine( "end" );

		if ( mesh.Morphs.Count > 0 )
		{
			// Keep a sidecar map for future DMX/blendshape work, but do not write
			// SMD vertexanimation blocks because s&box's SMD parser rejects them.
			WriteMorphMapSidecar( path, mesh.Morphs );
		}
	}

	private static void WriteMorphMapSidecar( string smdPath, List<MeshMorphExport> morphs )
	{
		var ordered = morphs
			.OrderBy( m => m.Name, StringComparer.OrdinalIgnoreCase )
			.ToList();
		if ( ordered.Count == 0 )
		{
			return;
		}

		string mapPath = smdPath + ".morphmap.txt";
		using var writer = new StreamWriter( mapPath, false, Encoding.ASCII );
		writer.WriteLine( "# SMD vertexanimation time -> source flex channel name" );
		writer.WriteLine( "# time 0 is bind pose" );
		int time = 1;
		foreach ( MeshMorphExport morph in ordered )
		{
			writer.WriteLine( $"{time}\t{morph.Name}\t{morph.Deltas.Count}" );
			time++;
		}
	}

	private static void WriteVertex( StreamWriter writer, VertexRecord v )
	{
		var sb = new StringBuilder( 128 );
		sb.Append( "  " );
		sb.Append( v.PrimaryBone.ToString( CultureInfo.InvariantCulture ) );
		sb.Append( ' ' );
		sb.Append( Fmt( v.Position.x ) );
		sb.Append( ' ' );
		sb.Append( Fmt( v.Position.y ) );
		sb.Append( ' ' );
		sb.Append( Fmt( v.Position.z ) );
		sb.Append( ' ' );
		sb.Append( Fmt( v.Normal.x ) );
		sb.Append( ' ' );
		sb.Append( Fmt( v.Normal.y ) );
		sb.Append( ' ' );
		sb.Append( Fmt( v.Normal.z ) );
		sb.Append( ' ' );
		sb.Append( Fmt( v.Uv.x ) );
		sb.Append( ' ' );
		sb.Append( Fmt( 1f - v.Uv.y ) );
		sb.Append( ' ' );
		sb.Append( v.BoneCount.ToString( CultureInfo.InvariantCulture ) );
		for ( int i = 0; i < v.BoneCount; i++ )
		{
			sb.Append( ' ' );
			sb.Append( v.Bones[i].ToString( CultureInfo.InvariantCulture ) );
			sb.Append( ' ' );
			sb.Append( Fmt( v.Weights[i] ) );
		}
		writer.WriteLine( sb.ToString() );
	}

	private static string Escape( string value )
	{
		return (value ?? string.Empty).Replace( "\\", "\\\\" ).Replace( "\"", "\\\"" );
	}

	private static string Fmt( float value )
	{
		if ( float.IsNaN( value ) || float.IsInfinity( value ) )
		{
			return "0.000000";
		}
		return value.ToString( "0.000000", CultureInfo.InvariantCulture );
	}
}
