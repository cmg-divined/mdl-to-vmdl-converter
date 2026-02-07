using GModMount.Source;

internal static class GeometryUtil
{
	public static Capsule CapsuleFromBounds( Vector3 min, Vector3 max )
	{
		Vector3 center = new Vector3(
			(min.x + max.x) * 0.5f,
			(min.y + max.y) * 0.5f,
			(min.z + max.z) * 0.5f
		);

		float sx = MathF.Max( 0.001f, max.x - min.x );
		float sy = MathF.Max( 0.001f, max.y - min.y );
		float sz = MathF.Max( 0.001f, max.z - min.z );

		int axis = 0;
		float axisSize = sx;
		if ( sy > axisSize ) { axis = 1; axisSize = sy; }
		if ( sz > axisSize ) { axis = 2; axisSize = sz; }

		float radius = axis switch
		{
			0 => MathF.Max( 0.05f, MathF.Min( sy, sz ) * 0.5f ),
			1 => MathF.Max( 0.05f, MathF.Min( sx, sz ) * 0.5f ),
			_ => MathF.Max( 0.05f, MathF.Min( sx, sy ) * 0.5f )
		};

		float halfAxis = axisSize * 0.5f;
		float lineHalf = MathF.Max( 0f, halfAxis - radius );
		Vector3 p0 = center;
		Vector3 p1 = center;
		switch ( axis )
		{
			case 0:
				p0 = new Vector3( center.x - lineHalf, center.y, center.z );
				p1 = new Vector3( center.x + lineHalf, center.y, center.z );
				break;
			case 1:
				p0 = new Vector3( center.x, center.y - lineHalf, center.z );
				p1 = new Vector3( center.x, center.y + lineHalf, center.z );
				break;
			default:
				p0 = new Vector3( center.x, center.y, center.z - lineHalf );
				p1 = new Vector3( center.x, center.y, center.z + lineHalf );
				break;
		}

		return new Capsule( p0, p1, radius );
	}

	public static Aabb ComputeAabb( List<Vector3> points )
	{
		if ( points.Count == 0 )
		{
			return new Aabb( new Vector3( 0f, 0f, 0f ), new Vector3( 1f, 1f, 1f ) );
		}

		Vector3 min = points[0];
		Vector3 max = points[0];
		for ( int i = 1; i < points.Count; i++ )
		{
			Vector3 p = points[i];
			if ( p.x < min.x ) min.x = p.x;
			if ( p.y < min.y ) min.y = p.y;
			if ( p.z < min.z ) min.z = p.z;
			if ( p.x > max.x ) max.x = p.x;
			if ( p.y > max.y ) max.y = p.y;
			if ( p.z > max.z ) max.z = p.z;
		}

		Vector3 center = new Vector3(
			(min.x + max.x) * 0.5f,
			(min.y + max.y) * 0.5f,
			(min.z + max.z) * 0.5f
		);
		Vector3 size = new Vector3(
			MathF.Max( 0.1f, max.x - min.x ),
			MathF.Max( 0.1f, max.y - min.y ),
			MathF.Max( 0.1f, max.z - min.z )
		);
		return new Aabb( center, size );
	}
}