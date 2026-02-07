namespace GModMount.Source;

/// <summary>
/// Parser for Source Engine PHY (physics) files.
/// PHY files contain collision geometry and ragdoll constraints.
/// </summary>
public class PhyFile
{
	public PhyHeader Header;
	public List<PhyCollisionData> CollisionData = new();
	public List<PhySolid> Solids = new();
	public List<PhyRagdollConstraint> RagdollConstraints = new();
	public List<PhyCollisionPair> CollisionPairs = new();
	public PhyEditParams EditParams = new();
	public bool SelfCollides = true;

	private const int VPHY_ID = 0x59485056; // 'VPHY'
	private const int IVPS_ID = 0x53505649; // 'IVPS'
	private const int SPVI_ID = 0x49565053; // 'SPVI'

	/// <summary>
	/// Load a PHY file from binary data.
	/// </summary>
	public static PhyFile Load( byte[] data )
	{
		if ( data == null || data.Length < 16 )
			return null;

		var phy = new PhyFile();
		
		// Read header
		phy.Header = new PhyHeader
		{
			Size = BitConverter.ToInt32( data, 0 ),
			Id = BitConverter.ToInt32( data, 4 ),
			SolidCount = BitConverter.ToInt32( data, 8 ),
			Checksum = BitConverter.ToInt32( data, 12 )
		};

		Log.Info( $"PhyFile: Header size={phy.Header.Size}, id={phy.Header.Id}, solids={phy.Header.SolidCount}" );

		if ( phy.Header.Size != 16 || phy.Header.SolidCount <= 0 || phy.Header.SolidCount > 128 )
			return phy;

		int offset = 16;

		// Read collision data for each solid
		for ( int i = 0; i < phy.Header.SolidCount && offset + 4 <= data.Length; i++ )
		{
			int solidSize = BitConverter.ToInt32( data, offset );
			offset += 4;

			if ( solidSize <= 0 || offset + solidSize > data.Length )
				break;

			var hulls = ParseSolidCollision( data, offset, solidSize );
			if ( hulls != null && hulls.Count > 0 )
			{
				var collision = new PhyCollisionData { Size = solidSize };
				foreach ( var hull in hulls )
				{
					var mesh = new PhyConvexMesh();
					mesh.Vertices = hull;
					collision.ConvexMeshes.Add( mesh );
				}
				phy.CollisionData.Add( collision );
			}
			else
			{
				// Add empty collision to keep solid indices aligned
				phy.CollisionData.Add( new PhyCollisionData { Size = solidSize } );
			}

			offset += solidSize;
		}

		// Read key-value text section
		ReadKeyValueSection( data, offset, phy );

		Log.Info( $"PhyFile: Loaded {phy.CollisionData.Count} collision solids, {phy.Solids.Count} solid properties, {phy.RagdollConstraints.Count} ragdoll constraints" );

		return phy;
	}

	private static List<List<Vector3>> ParseSolidCollision( byte[] data, int offset, int size )
	{
		if ( size < 8 )
			return null;

		int magic = BitConverter.ToInt32( data, offset );
		if ( magic == VPHY_ID )
		{
			// VPHY format: collideheader_t(8) + compactsurfaceheader_t(20) + IVP_Compact_Surface(48+)
			short modelType = BitConverter.ToInt16( data, offset + 6 );
			if ( modelType != 0 ) // modelType 0 = convex hull
				return null;

			const int CollideHeaderSize = 8;
			const int SurfaceHeaderSize = 20;

			if ( size < CollideHeaderSize + SurfaceHeaderSize + 48 )
				return null;

			int compactSurfaceOffset = offset + CollideHeaderSize + SurfaceHeaderSize;
			return ParseCompactSurface( data, compactSurfaceOffset, size - CollideHeaderSize - SurfaceHeaderSize );
		}
		else
		{
			// Legacy format: raw IVP_Compact_Surface
			if ( size < 48 )
				return null;

			int legacyId = BitConverter.ToInt32( data, offset + 44 );
			return legacyId == 0 || legacyId == IVPS_ID || legacyId == SPVI_ID
				? ParseCompactSurface( data, offset, size )
				: null;
		}
	}

	private static List<List<Vector3>> ParseCompactSurface( byte[] data, int offset, int size )
	{
		// IVP_Compact_Surface: offset_ledgetree_root at byte 32
		const int CompactSurfaceSize = 48;
		if ( size < CompactSurfaceSize )
			return null;

		int ledgetreeOffset = BitConverter.ToInt32( data, offset + 32 );
		if ( ledgetreeOffset <= 0 || ledgetreeOffset >= size )
			return null;

		int nodeOffset = offset + ledgetreeOffset;

		var allLedges = new List<(int offset, int triangleCount)>();
		CollectLedges( data, nodeOffset, allLedges );

		var result = new List<List<Vector3>>();
		foreach ( var (ledgeOffset, _) in allLedges )
		{
			var vertices = ParseCompactLedge( data, ledgeOffset );
			if ( vertices != null && vertices.Count >= 4 )
				result.Add( vertices );
		}

		return result.Count > 0 ? result : null;
	}

	private static void CollectLedges( byte[] data, int nodeOffset, List<(int offset, int triangleCount)> ledges )
	{
		// IVP_Compact_Ledgetree_Node (28 bytes):
		// offset_right_node(4) + offset_compact_ledge(4) + center(12) + radius(4) + box_sizes(3) + free_0(1)
		const int NodeSize = 28;

		var nodeStack = new Stack<int>();
		nodeStack.Push( nodeOffset );

		while ( nodeStack.Count > 0 )
		{
			int currentOffset = nodeStack.Pop();

			if ( currentOffset < 0 || currentOffset + NodeSize > data.Length )
				continue;

			int offsetRightNode = BitConverter.ToInt32( data, currentOffset );
			int offsetCompactLedge = BitConverter.ToInt32( data, currentOffset + 4 );

			if ( offsetRightNode == 0 )
			{
				if ( offsetCompactLedge != 0 )
				{
					int ledgeOffset = currentOffset + offsetCompactLedge;
					if ( ledgeOffset >= 0 && ledgeOffset + 16 <= data.Length )
					{
						short numTriangles = BitConverter.ToInt16( data, ledgeOffset + 12 );
						if ( numTriangles > 0 )
							ledges.Add( (ledgeOffset, numTriangles) );
					}
				}
			}
			else
			{
				int rightOffset = currentOffset + offsetRightNode;
				if ( rightOffset >= 0 && rightOffset + NodeSize <= data.Length )
					nodeStack.Push( rightOffset );

				int leftOffset = currentOffset + NodeSize;
				if ( leftOffset >= 0 && leftOffset + NodeSize <= data.Length )
					nodeStack.Push( leftOffset );
			}
		}
	}

	private static List<Vector3> ParseCompactLedge( byte[] data, int offset )
	{
		// IVP_Compact_Ledge (16 bytes): c_point_offset(4) + client_data(4) + flags:size_div_16(4) + n_triangles(2) + reserved(2)
		if ( offset + 16 > data.Length )
			return null;

		int pointOffset = BitConverter.ToInt32( data, offset );
		short numTriangles = BitConverter.ToInt16( data, offset + 12 );

		if ( numTriangles <= 0 || pointOffset == 0 )
			return null;

		int pointArrayOffset = offset + pointOffset;
		if ( pointArrayOffset < 0 || pointArrayOffset >= data.Length )
			return null;

		int trianglesOffset = offset + 16;

		// IVP_Compact_Triangle (16 bytes): indices(4) + c_three_edges[3](12)
		// IVP_Compact_Edge (4 bytes): start_point_index:16 + opposite_index:15 + is_virtual:1
		const int TriangleSize = 16;

		if ( trianglesOffset + numTriangles * TriangleSize > data.Length )
			return null;

		var vertexSet = new HashSet<int>();
		var vertices = new List<Vector3>();

		const float MetersToInches = 39.3701f;

		for ( int i = 0; i < numTriangles; i++ )
		{
			int triOffset = trianglesOffset + i * TriangleSize;

			for ( int j = 0; j < 3; j++ )
			{
				int edgeOffset = triOffset + 4 + j * 4;
				if ( edgeOffset + 4 > data.Length )
					continue;

				uint edgeData = BitConverter.ToUInt32( data, edgeOffset );
				int pointIndex = (int)(edgeData & 0xFFFF);

				if ( !vertexSet.Add( pointIndex ) )
					continue;

				// IVP_Compact_Poly_Point (16 bytes): x,y,z floats + hesse_val
				int ptOffset = pointArrayOffset + pointIndex * 16;
				if ( ptOffset + 12 > data.Length || ptOffset < 0 )
					continue;

				float ivpX = BitConverter.ToSingle( data, ptOffset );
				float ivpY = BitConverter.ToSingle( data, ptOffset + 4 );
				float ivpZ = BitConverter.ToSingle( data, ptOffset + 8 );

				// IVP to Source: (X, Z, -Y) * MetersToInches
				vertices.Add( new Vector3( ivpX * MetersToInches, ivpZ * MetersToInches, -ivpY * MetersToInches ) );
			}
		}

		return vertices.Count > 0 ? vertices : null;
	}

	private static void ReadKeyValueSection( byte[] data, int offset, PhyFile phy )
	{
		if ( offset >= data.Length )
			return;

		// Try to read as text
		try
		{
			string text = System.Text.Encoding.ASCII.GetString( data, offset, data.Length - offset );
			
			// Find the start of the text (skip any binary data)
			int textStart = text.IndexOf( "solid" );
			if ( textStart < 0 )
				textStart = text.IndexOf( "ragdollconstraint" );
			if ( textStart < 0 )
				return;

			text = text.Substring( textStart );

			// Parse key-value pairs
			ParseKeyValues( text, phy );
		}
		catch
		{
			// Ignore parsing errors
		}
	}

	private static void ParseKeyValues( string text, PhyFile phy )
	{
		// Simple key-value parser for PHY text section
		var lines = text.Split( new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries );
		
		PhySolid currentSolid = null;
		PhyRagdollConstraint currentConstraint = null;
		bool inSolid = false;
		bool inConstraint = false;
		bool inCollisionRules = false;
		bool inEditParams = false;

		foreach ( var line in lines )
		{
			string trimmed = line.Trim();
			if ( string.IsNullOrEmpty( trimmed ) )
				continue;

			// Check for section starts
			if ( trimmed.StartsWith( "solid", StringComparison.OrdinalIgnoreCase ) && !trimmed.Contains( "\"" ) )
			{
				currentSolid = new PhySolid();
				inSolid = true;
				inConstraint = false;
				inCollisionRules = false;
				inEditParams = false;
				continue;
			}
			
			if ( trimmed.StartsWith( "ragdollconstraint", StringComparison.OrdinalIgnoreCase ) )
			{
				currentConstraint = new PhyRagdollConstraint();
				inConstraint = true;
				inSolid = false;
				inCollisionRules = false;
				inEditParams = false;
				continue;
			}

			if ( trimmed.StartsWith( "collisionrules", StringComparison.OrdinalIgnoreCase ) )
			{
				inCollisionRules = true;
				inSolid = false;
				inConstraint = false;
				inEditParams = false;
				continue;
			}

			if ( trimmed.StartsWith( "editparams", StringComparison.OrdinalIgnoreCase ) )
			{
				inEditParams = true;
				inSolid = false;
				inConstraint = false;
				inCollisionRules = false;
				continue;
			}

			// Handle braces
			if ( trimmed == "{" )
				continue;
			
			if ( trimmed == "}" )
			{
				if ( inSolid && currentSolid != null )
				{
					phy.Solids.Add( currentSolid );
					currentSolid = null;
				}
				if ( inConstraint && currentConstraint != null )
				{
					phy.RagdollConstraints.Add( currentConstraint );
					currentConstraint = null;
				}
				inSolid = false;
				inConstraint = false;
				inCollisionRules = false;
				inEditParams = false;
				continue;
			}

			// Parse key-value pairs
			var parts = ParseKeyValuePair( trimmed );
			if ( parts == null )
				continue;

			string key = parts.Value.key;
			string value = parts.Value.value;

			if ( inSolid && currentSolid != null )
			{
				switch ( key.ToLowerInvariant() )
				{
					case "index": currentSolid.Index = int.TryParse( value, out int idx ) ? idx : 0; break;
					case "name": currentSolid.Name = value; break;
					case "parent": currentSolid.Parent = value; break;
					case "mass": currentSolid.Mass = float.TryParse( value, out float m ) ? m : 1f; break;
					case "surfaceprop": currentSolid.SurfaceProp = value; break;
					case "damping": currentSolid.Damping = float.TryParse( value, out float d ) ? d : 0f; break;
					case "rotdamping": currentSolid.RotDamping = float.TryParse( value, out float rd ) ? rd : 0f; break;
					case "inertia": currentSolid.Inertia = float.TryParse( value, out float i ) ? i : 1f; break;
					case "volume": currentSolid.Volume = float.TryParse( value, out float v ) ? v : 0f; break;
				}
			}
			else if ( inConstraint && currentConstraint != null )
			{
				switch ( key.ToLowerInvariant() )
				{
					case "parent": currentConstraint.ParentIndex = int.TryParse( value, out int p ) ? p : 0; break;
					case "child": currentConstraint.ChildIndex = int.TryParse( value, out int c ) ? c : 0; break;
					case "xmin": currentConstraint.XMin = float.TryParse( value, out float xmin ) ? xmin : 0f; break;
					case "xmax": currentConstraint.XMax = float.TryParse( value, out float xmax ) ? xmax : 0f; break;
					case "xfriction": currentConstraint.XFriction = float.TryParse( value, out float xf ) ? xf : 0f; break;
					case "ymin": currentConstraint.YMin = float.TryParse( value, out float ymin ) ? ymin : 0f; break;
					case "ymax": currentConstraint.YMax = float.TryParse( value, out float ymax ) ? ymax : 0f; break;
					case "yfriction": currentConstraint.YFriction = float.TryParse( value, out float yf ) ? yf : 0f; break;
					case "zmin": currentConstraint.ZMin = float.TryParse( value, out float zmin ) ? zmin : 0f; break;
					case "zmax": currentConstraint.ZMax = float.TryParse( value, out float zmax ) ? zmax : 0f; break;
					case "zfriction": currentConstraint.ZFriction = float.TryParse( value, out float zf ) ? zf : 0f; break;
				}
			}
			else if ( inCollisionRules )
			{
				if ( key.ToLowerInvariant() == "selfcollide" )
				{
					phy.SelfCollides = value != "0";
				}
			}
		}
	}

	private static (string key, string value)? ParseKeyValuePair( string line )
	{
		// Format: "key" "value" or key value
		int firstQuote = line.IndexOf( '"' );
		if ( firstQuote >= 0 )
		{
			int secondQuote = line.IndexOf( '"', firstQuote + 1 );
			if ( secondQuote < 0 ) return null;
			
			string key = line.Substring( firstQuote + 1, secondQuote - firstQuote - 1 );
			
			int thirdQuote = line.IndexOf( '"', secondQuote + 1 );
			if ( thirdQuote < 0 ) return (key, "");
			
			int fourthQuote = line.IndexOf( '"', thirdQuote + 1 );
			if ( fourthQuote < 0 ) return (key, "");
			
			string value = line.Substring( thirdQuote + 1, fourthQuote - thirdQuote - 1 );
			return (key, value);
		}
		
		// Unquoted format
		var parts = line.Split( new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length >= 2 )
			return (parts[0], parts[1]);
		
		return null;
	}
}
