namespace GModMount.Source;

/// <summary>
/// Combined Source model data from MDL, VVD, VTX, and PHY files.
/// </summary>
public class SourceModel
{
	/// <summary>
	/// MDL file data (skeleton, bodygroups, materials, etc.)
	/// </summary>
	public MdlFile Mdl { get; private set; }

	/// <summary>
	/// VVD file data (vertices, bone weights)
	/// </summary>
	public VvdFile Vvd { get; private set; }

	/// <summary>
	/// VTX file data (strips, indices)
	/// </summary>
	public VtxFile Vtx { get; private set; }

	/// <summary>
	/// PHY file data (physics collision, ragdoll constraints) - optional
	/// </summary>
	public PhyFile Phy { get; private set; }

	/// <summary>
	/// Whether this model has physics data.
	/// </summary>
	public bool HasPhysics => Phy != null && Phy.CollisionData.Count > 0;

	/// <summary>
	/// Model version (48, 49, or 53)
	/// </summary>
	public int Version => Mdl.Version;

	/// <summary>
	/// Model name from the MDL header.
	/// </summary>
	public string Name { get; private set; }

	/// <summary>
	/// Whether this is a static prop (no bones/animations).
	/// </summary>
	public bool IsStaticProp => Mdl.Header.IsStaticProp;

	private SourceModel() { }

	/// <summary>
	/// Load a Source model from file paths.
	/// </summary>
	public static SourceModel Load( string mdlPath, string vvdPath, string vtxPath, string phyPath = null )
	{
		byte[] mdlData = File.ReadAllBytes( mdlPath );
		byte[] vvdData = File.ReadAllBytes( vvdPath );
		byte[] vtxData = File.ReadAllBytes( vtxPath );
		byte[] phyData = null;
		
		if ( !string.IsNullOrEmpty( phyPath ) && File.Exists( phyPath ) )
		{
			phyData = File.ReadAllBytes( phyPath );
		}

		return Load( mdlData, vvdData, vtxData, phyData );
	}

	/// <summary>
	/// Load a Source model from byte arrays.
	/// </summary>
	public static SourceModel Load( byte[] mdlData, byte[] vvdData, byte[] vtxData, byte[] phyData = null )
	{
		var model = new SourceModel();

		try
		{
			Log.Info( $"SourceModel: Parsing MDL ({mdlData.Length} bytes)..." );
			model.Mdl = MdlFile.Load( mdlData );
			Log.Info( $"SourceModel: MDL parsed - v{model.Mdl.Version}, {model.Mdl.Bones.Count} bones, {model.Mdl.BodyParts.Count} bodyparts, {model.Mdl.Materials.Count} materials, {model.Mdl.FlexDescriptors.Count} flex descriptors" );
		}
		catch ( Exception ex )
		{
			throw new InvalidDataException( $"Failed to parse MDL: {ex.Message}", ex );
		}

		try
		{
			Log.Info( $"SourceModel: Parsing VVD ({vvdData.Length} bytes)..." );
			model.Vvd = VvdFile.Load( vvdData );
			Log.Info( $"SourceModel: VVD parsed - {model.Vvd.Vertices?.Length ?? 0} vertices, {model.Vvd.Fixups?.Count ?? 0} fixups" );
		}
		catch ( Exception ex )
		{
			throw new InvalidDataException( $"Failed to parse VVD: {ex.Message}", ex );
		}

		try
		{
			Log.Info( $"SourceModel: Parsing VTX ({vtxData.Length} bytes)..." );
			// Pass MDL version to VTX parser - v49+ has larger strip group headers
			model.Vtx = VtxFile.Load( vtxData, model.Mdl.Header.Version );
			Log.Info( $"SourceModel: VTX parsed - {model.Vtx.BodyParts.Count} bodyparts" );
		}
		catch ( Exception ex )
		{
			throw new InvalidDataException( $"Failed to parse VTX: {ex.Message}", ex );
		}

		// Get name from MdlFile (it extracts it during load)
		model.Name = model.Mdl.Name;

		// Validate checksums
		if ( model.Mdl.Header.Checksum != model.Vvd.Header.Checksum )
		{
			Log.Warning( $"SourceModel: VVD checksum mismatch (MDL: {model.Mdl.Header.Checksum}, VVD: {model.Vvd.Header.Checksum})" );
		}

		if ( model.Mdl.Header.Checksum != model.Vtx.Header.Checksum )
		{
			Log.Warning( $"SourceModel: VTX checksum mismatch (MDL: {model.Mdl.Header.Checksum}, VTX: {model.Vtx.Header.Checksum})" );
		}

		// Load PHY file if provided
		if ( phyData != null && phyData.Length > 0 )
		{
			try
			{
				Log.Info( $"SourceModel: Parsing PHY ({phyData.Length} bytes)..." );
				model.Phy = PhyFile.Load( phyData );
				if ( model.Phy != null )
				{
					Log.Info( $"SourceModel: PHY parsed - {model.Phy.CollisionData.Count} solids, {model.Phy.RagdollConstraints.Count} constraints" );
					
					// Validate checksum
					if ( model.Mdl.Header.Checksum != model.Phy.Header.Checksum )
					{
						Log.Warning( $"SourceModel: PHY checksum mismatch (MDL: {model.Mdl.Header.Checksum}, PHY: {model.Phy.Header.Checksum})" );
					}
				}
			}
			catch ( Exception ex )
			{
				Log.Warning( $"SourceModel: Failed to parse PHY: {ex.Message}" );
				model.Phy = null;
			}
		}

		return model;
	}

	/// <summary>
	/// Get all body part configurations (for bodygroup selection).
	/// Each configuration is a selection of one model from each body part.
	/// </summary>
	public IEnumerable<BodyGroupConfiguration> GetBodyGroupConfigurations()
	{
		var configurations = new List<BodyGroupConfiguration>();

		// Start with the base configuration (model 0 from each body part)
		var baseConfig = new BodyGroupConfiguration();
		foreach ( var bodyPart in Mdl.BodyParts )
		{
			baseConfig.Selections[bodyPart.Index] = 0;
		}
		configurations.Add( baseConfig );

		// Generate all combinations
		GenerateConfigurations( configurations, 0, new Dictionary<int, int>() );

		return configurations.Distinct( new BodyGroupConfigurationComparer() );
	}

	private void GenerateConfigurations( List<BodyGroupConfiguration> configs, int bodyPartIndex, Dictionary<int, int> current )
	{
		if ( bodyPartIndex >= Mdl.BodyParts.Count )
		{
			var config = new BodyGroupConfiguration();
			foreach ( var kvp in current )
			{
				config.Selections[kvp.Key] = kvp.Value;
			}
			configs.Add( config );
			return;
		}

		var bodyPart = Mdl.BodyParts[bodyPartIndex];
		for ( int modelIndex = 0; modelIndex < bodyPart.Models.Count; modelIndex++ )
		{
			current[bodyPartIndex] = modelIndex;
			GenerateConfigurations( configs, bodyPartIndex + 1, current );
		}
	}
}

/// <summary>
/// Represents a specific bodygroup configuration.
/// </summary>
public class BodyGroupConfiguration
{
	/// <summary>
	/// Maps body part index to selected model index.
	/// </summary>
	public Dictionary<int, int> Selections { get; } = new();

	/// <summary>
	/// Calculate the bodygroup value for this configuration.
	/// </summary>
	public int CalculateBodyValue( MdlFile mdl )
	{
		int value = 0;
		int multiplier = 1;

		foreach ( var bodyPart in mdl.BodyParts )
		{
			if ( Selections.TryGetValue( bodyPart.Index, out int modelIndex ) )
			{
				value += modelIndex * multiplier;
			}
			multiplier *= Math.Max( 1, bodyPart.Models.Count );
		}

		return value;
	}
}

/// <summary>
/// Comparer for BodyGroupConfiguration.
/// </summary>
public class BodyGroupConfigurationComparer : IEqualityComparer<BodyGroupConfiguration>
{
	public bool Equals( BodyGroupConfiguration x, BodyGroupConfiguration y )
	{
		if ( x == null && y == null ) return true;
		if ( x == null || y == null ) return false;
		if ( x.Selections.Count != y.Selections.Count ) return false;

		foreach ( var kvp in x.Selections )
		{
			if ( !y.Selections.TryGetValue( kvp.Key, out int value ) || value != kvp.Value )
				return false;
		}
		return true;
	}

	public int GetHashCode( BodyGroupConfiguration obj )
	{
		int hash = 17;
		foreach ( var kvp in obj.Selections.OrderBy( k => k.Key ) )
		{
			hash = hash * 31 + kvp.Key;
			hash = hash * 31 + kvp.Value;
		}
		return hash;
	}
}
