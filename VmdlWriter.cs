using GModMount.Source;

internal static class VmdlWriter
{
	private const string Kv3Header = "<!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:modeldoc29:version{3cec427c-1b0e-4d48-a90a-0436f33a6041} -->";

	public static void Write( string path, BuildContext context )
	{
		using var writer = new StreamWriter( path, false, new UTF8Encoding( false ) );
		writer.WriteLine( Kv3Header );
		writer.WriteLine( "{" );
		WriteLine( writer, 1, "rootNode =" );
		WriteLine( writer, 1, "{" );
		WriteLine( writer, 2, "_class = \"RootNode\"" );
		WriteLine( writer, 2, "children =" );
		WriteLine( writer, 2, "[" );

		WriteRenderMeshList( writer, context, 3 );
		WriteBodyGroupList( writer, context, 3 );
		WriteMaterialGroupList( writer, context, 3 );
		WriteAnimationList( writer, context, 3 );
		WriteHitboxSetList( writer, context, 3 );
		WritePhysicsShapeList( writer, context, 3 );
		WritePhysicsBodyMarkupList( writer, context, 3 );
		WritePhysicsJointList( writer, context, 3 );

		WriteLine( writer, 2, "]" );
		WriteLine( writer, 2, "model_archetype = \"\"" );
		WriteLine( writer, 2, "primary_associated_entity = \"\"" );
		WriteLine( writer, 2, "anim_graph_name = \"\"" );
		WriteLine( writer, 2, "base_model_name = \"\"" );
		WriteLine( writer, 1, "}" );
		writer.WriteLine( "}" );
	}

	private static void WriteRenderMeshList( StreamWriter writer, BuildContext context, int indent )
	{
		WriteLine( writer, indent, "{" );
		WriteLine( writer, indent + 1, "_class = \"RenderMeshList\"" );
		WriteLine( writer, indent + 1, "children =" );
		WriteLine( writer, indent + 1, "[" );
		foreach ( MeshExport mesh in context.Meshes )
		{
			string meshFilePath = string.IsNullOrWhiteSpace( context.ModelAssetDirectory )
				? mesh.FileName
				: $"{context.ModelAssetDirectory.TrimEnd( '/' )}/{mesh.FileName}";

			WriteLine( writer, indent + 2, "{" );
			WriteLine( writer, indent + 3, "_class = \"RenderMeshFile\"" );
			WriteLine( writer, indent + 3, $"name = \"{Escape( mesh.Name )}\"" );
			WriteLine( writer, indent + 3, $"filename = \"{Escape( meshFilePath.Replace( '\\', '/' ) )}\"" );
			WriteLine( writer, indent + 3, "import_translation = [ 0.0, 0.0, 0.0 ]" );
			WriteLine( writer, indent + 3, "import_rotation = [ 0.0, 0.0, 0.0 ]" );
			WriteLine( writer, indent + 3, "import_scale = 1.0" );
			WriteLine( writer, indent + 3, "align_origin_x_type = \"None\"" );
			WriteLine( writer, indent + 3, "align_origin_y_type = \"None\"" );
			WriteLine( writer, indent + 3, "align_origin_z_type = \"None\"" );
			WriteLine( writer, indent + 3, "parent_bone = \"\"" );
			WriteLine( writer, indent + 3, "import_filter =" );
			WriteLine( writer, indent + 3, "{" );
			WriteLine( writer, indent + 4, "exclude_by_default = false" );
			WriteLine( writer, indent + 4, "exception_list = [ ]" );
			WriteLine( writer, indent + 3, "}" );
			WriteLine( writer, indent + 2, "}," );
		}
		WriteLine( writer, indent + 1, "]" );
		WriteLine( writer, indent, "}," );
	}

	private static void WriteBodyGroupList( StreamWriter writer, BuildContext context, int indent )
	{
		WriteLine( writer, indent, "{" );
		WriteLine( writer, indent + 1, "_class = \"BodyGroupList\"" );
		WriteLine( writer, indent + 1, "children =" );
		WriteLine( writer, indent + 1, "[" );
		foreach ( BodyGroupExport group in context.BodyGroups )
		{
			WriteLine( writer, indent + 2, "{" );
			WriteLine( writer, indent + 3, "_class = \"BodyGroup\"" );
			WriteLine( writer, indent + 3, $"name = \"{Escape( group.Name )}\"" );
			WriteLine( writer, indent + 3, "children =" );
			WriteLine( writer, indent + 3, "[" );
			foreach ( BodyGroupChoiceExport choice in group.Choices )
			{
				WriteLine( writer, indent + 4, "{" );
				WriteLine( writer, indent + 5, "_class = \"BodyGroupChoice\"" );
				WriteLine( writer, indent + 5, $"name = \"{Escape( choice.Name )}\"" );
				WriteLine( writer, indent + 5, "meshes =" );
				WriteLine( writer, indent + 5, "[" );
				foreach ( string mesh in choice.Meshes )
				{
					WriteLine( writer, indent + 6, $"\"{Escape( mesh )}\"," );
				}
				WriteLine( writer, indent + 5, "]" );
				WriteLine( writer, indent + 4, "}," );
			}
			WriteLine( writer, indent + 3, "]" );
			WriteLine( writer, indent + 3, $"hidden_in_tools = {(group.HiddenInTools ? "true" : "false")}" );
			WriteLine( writer, indent + 2, "}," );
		}
		WriteLine( writer, indent + 1, "]" );
		WriteLine( writer, indent, "}," );
	}

	private static void WriteMaterialGroupList( StreamWriter writer, BuildContext context, int indent )
	{
		if ( context.MaterialRemaps.Count == 0 && context.MaterialGroups.Count == 0 )
		{
			return;
		}

		WriteLine( writer, indent, "{" );
		WriteLine( writer, indent + 1, "_class = \"MaterialGroupList\"" );
		WriteLine( writer, indent + 1, "children =" );
		WriteLine( writer, indent + 1, "[" );

		WriteLine( writer, indent + 2, "{" );
		WriteLine( writer, indent + 3, "_class = \"DefaultMaterialGroup\"" );
		WriteLine( writer, indent + 3, "remaps =" );
		WriteLine( writer, indent + 3, "[" );
		foreach ( MaterialRemapExport remap in context.MaterialRemaps )
		{
			WriteLine( writer, indent + 4, "{" );
			WriteLine( writer, indent + 5, $"from = \"{Escape( remap.From )}\"" );
			WriteLine( writer, indent + 5, $"to = \"{Escape( remap.To )}\"" );
			WriteLine( writer, indent + 4, "}," );
		}
		WriteLine( writer, indent + 3, "]" );
		WriteLine( writer, indent + 3, "use_global_default = false" );
		WriteLine( writer, indent + 3, "global_default_material = \"\"" );
		WriteLine( writer, indent + 2, "}," );

		foreach ( MaterialGroupExport materialGroup in context.MaterialGroups )
		{
			WriteLine( writer, indent + 2, "{" );
			WriteLine( writer, indent + 3, "_class = \"MaterialGroup\"" );
			WriteLine( writer, indent + 3, $"name = \"{Escape( materialGroup.Name )}\"" );
			WriteLine( writer, indent + 3, "remaps =" );
			WriteLine( writer, indent + 3, "[" );
			foreach ( MaterialRemapExport remap in materialGroup.Remaps )
			{
				WriteLine( writer, indent + 4, "{" );
				WriteLine( writer, indent + 5, $"from = \"{Escape( remap.From )}\"" );
				WriteLine( writer, indent + 5, $"to = \"{Escape( remap.To )}\"" );
				WriteLine( writer, indent + 4, "}," );
			}
			WriteLine( writer, indent + 3, "]" );
			WriteLine( writer, indent + 2, "}," );
		}

		WriteLine( writer, indent + 1, "]" );
		WriteLine( writer, indent, "}," );
	}

	private static void WriteAnimationList( StreamWriter writer, BuildContext context, int indent )
	{
		if ( context.Animations.Count == 0 )
		{
			return;
		}

		WriteLine( writer, indent, "{" );
		WriteLine( writer, indent + 1, "_class = \"AnimationList\"" );
		WriteLine( writer, indent + 1, "children =" );
		WriteLine( writer, indent + 1, "[" );
		foreach ( AnimationExport animation in context.Animations )
		{
			string sourceFilePath = string.IsNullOrWhiteSpace( context.ModelAssetDirectory )
				? animation.FileName
				: $"{context.ModelAssetDirectory.TrimEnd( '/' )}/{animation.FileName}";

			WriteLine( writer, indent + 2, "{" );
			WriteLine( writer, indent + 3, "_class = \"AnimFile\"" );
			WriteLine( writer, indent + 3, $"name = \"{Escape( animation.Name )}\"" );
			WriteLine( writer, indent + 3, "activity_name = \"\"" );
			WriteLine( writer, indent + 3, "activity_weight = 1" );
			WriteLine( writer, indent + 3, "weight_list_name = \"\"" );
			WriteLine( writer, indent + 3, "fade_in_time = 0.2" );
			WriteLine( writer, indent + 3, "fade_out_time = 0.2" );
			WriteLine( writer, indent + 3, $"looping = {(animation.Looping ? "true" : "false")}" );
			WriteLine( writer, indent + 3, "delta = false" );
			WriteLine( writer, indent + 3, "worldSpace = false" );
			WriteLine( writer, indent + 3, "hidden = false" );
			WriteLine( writer, indent + 3, "anim_markup_ordered = false" );
			WriteLine( writer, indent + 3, "disable_compression = false" );
			WriteLine( writer, indent + 3, "enable_scale = false" );
			WriteLine( writer, indent + 3, $"source_filename = \"{Escape( sourceFilePath.Replace( '\\', '/' ) )}\"" );
			WriteLine( writer, indent + 3, "start_frame = -1" );
			WriteLine( writer, indent + 3, "end_frame = -1" );
			WriteLine( writer, indent + 3, $"framerate = {Fmt( animation.FrameRate > 0.0f ? animation.FrameRate : -1.0f )}" );
			WriteLine( writer, indent + 3, "take = 0" );
			WriteLine( writer, indent + 3, "reverse = false" );
			WriteLine( writer, indent + 2, "}," );
		}
		WriteLine( writer, indent + 1, "]" );
		WriteLine( writer, indent, "}," );
	}

	private static void WriteHitboxSetList( StreamWriter writer, BuildContext context, int indent )
	{
		WriteLine( writer, indent, "{" );
		WriteLine( writer, indent + 1, "_class = \"HitboxSetList\"" );
		WriteLine( writer, indent + 1, "children =" );
		WriteLine( writer, indent + 1, "[" );
		foreach ( HitboxSetExport set in context.HitboxSets )
		{
			WriteLine( writer, indent + 2, "{" );
			WriteLine( writer, indent + 3, "_class = \"HitboxSet\"" );
			WriteLine( writer, indent + 3, $"name = \"{Escape( set.Name )}\"" );
			WriteLine( writer, indent + 3, "children =" );
			WriteLine( writer, indent + 3, "[" );
			foreach ( HitboxExport hitbox in set.Hitboxes )
			{
				WriteLine( writer, indent + 4, "{" );
				WriteLine( writer, indent + 5, "_class = \"HitboxCapsule\"" );
				WriteLine( writer, indent + 5, $"name = \"{Escape( hitbox.Name )}\"" );
				WriteLine( writer, indent + 5, $"parent_bone = \"{Escape( hitbox.Bone )}\"" );
				WriteLine( writer, indent + 5, "surface_property = \"flesh\"" );
				WriteLine( writer, indent + 5, "translation_only = false" );
				WriteLine( writer, indent + 5, $"tags = \"{Escape( hitbox.Tags )}\"" );
				WriteLine( writer, indent + 5, $"radius = {Fmt( hitbox.Radius )}" );
				WriteLine( writer, indent + 5, $"point0 = {FmtVec3( hitbox.Point0 )}" );
				WriteLine( writer, indent + 5, $"point1 = {FmtVec3( hitbox.Point1 )}" );
				WriteLine( writer, indent + 4, "}," );
			}
			WriteLine( writer, indent + 3, "]" );
			WriteLine( writer, indent + 2, "}," );
		}
		WriteLine( writer, indent + 1, "]" );
		WriteLine( writer, indent, "}," );
	}

	private static void WritePhysicsShapeList( StreamWriter writer, BuildContext context, int indent )
	{
		WriteLine( writer, indent, "{" );
		WriteLine( writer, indent + 1, "_class = \"PhysicsShapeList\"" );
		WriteLine( writer, indent + 1, "children =" );
		WriteLine( writer, indent + 1, "[" );
		foreach ( PhysicsShapeExport shape in context.PhysicsShapes )
		{
			string shapeClass = string.IsNullOrWhiteSpace( shape.ShapeClassName ) ? "PhysicsShapeBox" : shape.ShapeClassName;
			WriteLine( writer, indent + 2, "{" );
			WriteLine( writer, indent + 3, $"_class = \"{Escape( shapeClass )}\"" );
			WriteLine( writer, indent + 3, $"parent_bone = \"{Escape( shape.ParentBone )}\"" );
			WriteLine( writer, indent + 3, $"surface_prop = \"{Escape( shape.SurfaceProp )}\"" );
			WriteLine( writer, indent + 3, "collision_tags = \"solid\"" );
			if ( string.Equals( shapeClass, "PhysicsShapeSphere", StringComparison.Ordinal ) )
			{
				WriteLine( writer, indent + 3, $"radius = {Fmt( shape.Radius )}" );
				WriteLine( writer, indent + 3, $"center = {FmtVec3( shape.Center )}" );
			}
			else
			{
				WriteLine( writer, indent + 3, $"origin = {FmtVec3( shape.Origin )}" );
				WriteLine( writer, indent + 3, "angles = [ 0.0, 0.0, 0.0 ]" );
				WriteLine( writer, indent + 3, $"dimensions = {FmtVec3( shape.Dimensions )}" );
			}
			WriteLine( writer, indent + 2, "}," );
		}
		WriteLine( writer, indent + 1, "]" );
		WriteLine( writer, indent, "}," );
	}

	private static void WritePhysicsBodyMarkupList( StreamWriter writer, BuildContext context, int indent )
	{
		WriteLine( writer, indent, "{" );
		WriteLine( writer, indent + 1, "_class = \"PhysicsBodyMarkupList\"" );
		WriteLine( writer, indent + 1, "children =" );
		WriteLine( writer, indent + 1, "[" );
		foreach ( PhysicsBodyMarkupExport body in context.PhysicsBodies )
		{
			WriteLine( writer, indent + 2, "{" );
			WriteLine( writer, indent + 3, "_class = \"PhysicsBodyMarkup\"" );
			WriteLine( writer, indent + 3, $"target_body = \"{Escape( body.TargetBody )}\"" );
			WriteLine( writer, indent + 3, $"mass_override = {Fmt( body.MassOverride )}" );
			WriteLine( writer, indent + 3, $"inertia_scale = {Fmt( body.InertiaScale )}" );
			WriteLine( writer, indent + 3, $"linear_damping = {Fmt( body.LinearDamping )}" );
			WriteLine( writer, indent + 3, $"angular_damping = {Fmt( body.AngularDamping )}" );
			WriteLine( writer, indent + 3, $"use_mass_center_override = {(body.UseMassCenterOverride ? "true" : "false")}" );
			WriteLine( writer, indent + 3, $"mass_center_override = {FmtVec3( body.MassCenterOverride )}" );
			WriteLine( writer, indent + 2, "}," );
		}
		WriteLine( writer, indent + 1, "]" );
		WriteLine( writer, indent, "}," );
	}

	private static void WritePhysicsJointList( StreamWriter writer, BuildContext context, int indent )
	{
		WriteLine( writer, indent, "{" );
		WriteLine( writer, indent + 1, "_class = \"PhysicsJointList\"" );
		WriteLine( writer, indent + 1, "children =" );
		WriteLine( writer, indent + 1, "[" );
		foreach ( PhysicsJointExport joint in context.PhysicsJoints )
		{
			WriteLine( writer, indent + 2, "{" );
			WriteLine( writer, indent + 3, $"_class = \"{joint.ClassName}\"" );
			WriteLine( writer, indent + 3, $"parent_body = \"{Escape( joint.ParentBody )}\"" );
			WriteLine( writer, indent + 3, $"child_body = \"{Escape( joint.ChildBody )}\"" );
			WriteLine( writer, indent + 3, $"anchor_origin = {FmtVec3( joint.AnchorOrigin )}" );
			WriteLine( writer, indent + 3, "anchor_angles = [ 0.0, 0.0, 0.0 ]" );
			WriteLine( writer, indent + 3, "collision_enabled = false" );
			WriteLine( writer, indent + 3, "linear_strength = 0.0" );
			WriteLine( writer, indent + 3, "angular_strength = 0.0" );
			WriteLine( writer, indent + 3, $"friction = {Fmt( joint.Friction )}" );
			if ( joint.IsRevolute )
			{
				WriteLine( writer, indent + 3, "enable_limit = true" );
				WriteLine( writer, indent + 3, $"min_angle = {Fmt( joint.MinAngle )}" );
				WriteLine( writer, indent + 3, $"max_angle = {Fmt( joint.MaxAngle )}" );
			}
			else
			{
				WriteLine( writer, indent + 3, "enable_swing_limit = true" );
				WriteLine( writer, indent + 3, $"swing_limit = {Fmt( joint.SwingLimit )}" );
				WriteLine( writer, indent + 3, "swing_offset_angle = [ 0.0, 0.0, 0.0 ]" );
				WriteLine( writer, indent + 3, "enable_twist_limit = true" );
				WriteLine( writer, indent + 3, $"min_twist_angle = {Fmt( joint.MinAngle )}" );
				WriteLine( writer, indent + 3, $"max_twist_angle = {Fmt( joint.MaxAngle )}" );
			}
			WriteLine( writer, indent + 2, "}," );
		}
		WriteLine( writer, indent + 1, "]" );
		WriteLine( writer, indent, "}," );
	}

	private static void WriteLine( StreamWriter writer, int indent, string text )
	{
		writer.WriteLine( new string( '\t', indent ) + text );
	}

	private static string Escape( string text )
	{
		return (text ?? string.Empty).Replace( "\\", "\\\\" ).Replace( "\"", "\\\"" );
	}

	private static string FmtVec3( Vector3 v ) => $"[ {Fmt( v.x )}, {Fmt( v.y )}, {Fmt( v.z )} ]";

	private static string Fmt( float value )
	{
		if ( float.IsNaN( value ) || float.IsInfinity( value ) ) return "0.0";
		return value.ToString( "0.######", CultureInfo.InvariantCulture );
	}
}
