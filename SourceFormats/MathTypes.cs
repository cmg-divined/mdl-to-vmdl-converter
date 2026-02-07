namespace GModMount.Source;

[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct Vector2
{
	public float x;
	public float y;

	public Vector2( float x, float y )
	{
		this.x = x;
		this.y = y;
	}
}

[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct Vector3
{
	public float x;
	public float y;
	public float z;

	public Vector3( float x, float y, float z )
	{
		this.x = x;
		this.y = y;
		this.z = z;
	}

	public readonly float Length => MathF.Sqrt( (x * x) + (y * y) + (z * z) );

	public readonly Vector3 Normal
	{
		get
		{
			float len = Length;
			return len > 1e-6f ? this / len : new Vector3( 0f, 0f, 0f );
		}
	}

	public static Vector3 Cross( Vector3 a, Vector3 b )
	{
		return new Vector3(
			(a.y * b.z) - (a.z * b.y),
			(a.z * b.x) - (a.x * b.z),
			(a.x * b.y) - (a.y * b.x)
		);
	}

	public static float Dot( Vector3 a, Vector3 b )
	{
		return (a.x * b.x) + (a.y * b.y) + (a.z * b.z);
	}

	public static Vector3 operator +( Vector3 a, Vector3 b ) => new( a.x + b.x, a.y + b.y, a.z + b.z );
	public static Vector3 operator -( Vector3 a, Vector3 b ) => new( a.x - b.x, a.y - b.y, a.z - b.z );
	public static Vector3 operator *( Vector3 a, float scalar ) => new( a.x * scalar, a.y * scalar, a.z * scalar );
	public static Vector3 operator /( Vector3 a, float scalar ) => new( a.x / scalar, a.y / scalar, a.z / scalar );
}

[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct Vector4
{
	public float x;
	public float y;
	public float z;
	public float w;

	public Vector4( float x, float y, float z, float w )
	{
		this.x = x;
		this.y = y;
		this.z = z;
		this.w = w;
	}
}

[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public struct Quaternion
{
	public float x;
	public float y;
	public float z;
	public float w;

	public Quaternion( float x, float y, float z, float w )
	{
		this.x = x;
		this.y = y;
		this.z = z;
		this.w = w;
	}
}