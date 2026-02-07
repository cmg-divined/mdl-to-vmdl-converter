namespace GModMount.Source;

/// <summary>
/// Parsed MDL file data.
/// </summary>
public class MdlFile
{
	public StudioHeader Header { get; private set; }
	public StudioHeader2? Header2 { get; private set; }
	public int Version => Header.Version;

	/// <summary>
	/// Model name extracted from header.
	/// </summary>
	public string Name { get; private set; }

	// Bones
	public List<MdlBone> Bones { get; } = new();

	// Body parts and models
	public List<MdlBodyPart> BodyParts { get; } = new();

	// Materials
	public List<string> Materials { get; } = new();
	public List<string> MaterialPaths { get; } = new();

	// Skin families (material replacements)
	public List<short[]> SkinFamilies { get; } = new();

	// Attachments
	public List<MdlAttachment> Attachments { get; } = new();

	// Hitboxes
	public List<MdlHitboxSet> HitboxSets { get; } = new();
	
	// Eyeballs (for eye shader rendering)
	public List<MdlEyeball> Eyeballs { get; } = new();

	// Flex descriptors and mesh flex data
	public List<string> FlexDescriptors { get; } = new();
	public List<MdlFlexController> FlexControllers { get; } = new();
	public List<MdlFlexControllerUI> FlexControllerUIs { get; } = new();
	public List<MdlFlexRule> FlexRules { get; } = new();
	private Dictionary<int, string>? _resolvedFlexDisplayNames;

	/// <summary>
	/// Load an MDL file from a byte array.
	/// </summary>
	public static MdlFile Load( byte[] data )
	{
		using var stream = new MemoryStream( data );
		using var reader = new BinaryReader( stream );
		return Load( reader );
	}

	/// <summary>
	/// Load an MDL file from a stream.
	/// </summary>
	public static MdlFile Load( Stream stream )
	{
		using var reader = new BinaryReader( stream, Encoding.ASCII, leaveOpen: true );
		return Load( reader );
	}

	/// <summary>
	/// Load an MDL file from a binary reader.
	/// </summary>
	public static MdlFile Load( BinaryReader reader )
	{
		var mdl = new MdlFile();
		long fileLength = reader.BaseStream.Length;
		int headerSize = Marshal.SizeOf<StudioHeader>();

		// Check if file is large enough for header
		if ( fileLength < headerSize )
		{
			throw new InvalidDataException( $"MDL file too small: {fileLength} bytes, header needs {headerSize} bytes" );
		}

		// Read main header
		mdl.Header = reader.ReadStruct<StudioHeader>();

		if ( !mdl.Header.IsValid )
		{
			throw new InvalidDataException( $"Invalid MDL file: ID=0x{mdl.Header.Id:X8}" );
		}

		// Support MDL versions 44-49 and 53
		if ( mdl.Header.Version < SourceConstants.MDL_VERSION_44 ||
			 (mdl.Header.Version > SourceConstants.MDL_VERSION_49 && mdl.Header.Version != SourceConstants.MDL_VERSION_53) )
		{
			throw new InvalidDataException( $"Unsupported MDL version: {mdl.Header.Version}" );
		}

		// Read name from the file (at offset 12, after id/version/checksum, 64 bytes)
		reader.BaseStream.Position = 12;
		mdl.Name = ReadFixedString( reader.ReadBytes( 64 ), 64 );
		
		// Read secondary header if present and within bounds
		if ( mdl.Header.StudioHdr2Index > 0 && mdl.Header.StudioHdr2Index + Marshal.SizeOf<StudioHeader2>() <= fileLength )
		{
			reader.BaseStream.Position = mdl.Header.StudioHdr2Index;
			mdl.Header2 = reader.ReadStruct<StudioHeader2>();
		}

		// Read bones (with bounds checking)
		if ( mdl.Header.BoneCount > 0 && mdl.Header.BoneOffset > 0 && mdl.Header.BoneOffset < fileLength )
		{
			mdl.ReadBones( reader );
		}

		// Read flex descriptors (names) before body parts/meshes.
		if ( mdl.Header.FlexDescCount > 0 && mdl.Header.FlexDescIndex > 0 && mdl.Header.FlexDescIndex < fileLength )
		{
			mdl.ReadFlexDescriptors( reader );
		}

		// Read flex controllers/rules/UI names to resolve human-readable morph names.
		if ( mdl.Header.FlexControllerCount > 0 && mdl.Header.FlexControllerIndex > 0 && mdl.Header.FlexControllerIndex < fileLength )
		{
			mdl.ReadFlexControllers( reader );
		}

		if ( mdl.Header.FlexRulesCount > 0 && mdl.Header.FlexRulesIndex > 0 && mdl.Header.FlexRulesIndex < fileLength )
		{
			mdl.ReadFlexRules( reader );
		}

		if ( mdl.Header.FlexControllerUICount > 0 && mdl.Header.FlexControllerUIIndex > 0 && mdl.Header.FlexControllerUIIndex < fileLength )
		{
			mdl.ReadFlexControllerUIs( reader );
		}

		// Read body parts (with bounds checking)
		if ( mdl.Header.BodyPartCount > 0 && mdl.Header.BodyPartOffset > 0 && mdl.Header.BodyPartOffset < fileLength )
		{
			mdl.ReadBodyParts( reader );
		}

		// Read materials (with bounds checking)
		if ( mdl.Header.TextureCount > 0 && mdl.Header.TextureOffset > 0 && mdl.Header.TextureOffset < fileLength )
		{
			mdl.ReadMaterials( reader );
		}

		// Read skin families (with bounds checking)
		if ( mdl.Header.SkinFamilyCount > 0 && mdl.Header.SkinReferenceIndex > 0 && mdl.Header.SkinReferenceIndex < fileLength )
		{
			mdl.ReadSkinFamilies( reader );
		}

		// Read attachments (with bounds checking)
		if ( mdl.Header.AttachmentCount > 0 && mdl.Header.AttachmentOffset > 0 && mdl.Header.AttachmentOffset < fileLength )
		{
			mdl.ReadAttachments( reader );
		}

		// Read hitbox sets (with bounds checking)
		if ( mdl.Header.HitboxSetCount > 0 && mdl.Header.HitboxSetOffset > 0 && mdl.Header.HitboxSetOffset < fileLength )
		{
			mdl.ReadHitboxSets( reader );
		}

		Log.Info( $"MdlFile: Successfully parsed MDL" );
		return mdl;
	}

	/// <summary>
	/// Read a fixed-length string from a byte array.
	/// </summary>
	private static string ReadFixedString( byte[] bytes, int maxLength )
	{
		int length = 0;
		while ( length < maxLength && length < bytes.Length && bytes[length] != 0 )
		{
			length++;
		}
		return Encoding.ASCII.GetString( bytes, 0, length );
	}

	private void ReadBones( BinaryReader reader )
	{
		if ( Header.BoneCount <= 0 || Header.BoneOffset <= 0 )
			return;

		long fileLength = reader.BaseStream.Length;
		reader.BaseStream.Position = Header.BoneOffset;

		// Bone size is 216 bytes for v44+ (includes Unused[8] for all these versions)
		// Based on Crowbar's SourceMdlFile44.vb which reads unused[8] for v44
		int boneSize = 216;

		for ( int i = 0; i < Header.BoneCount; i++ )
		{
			long boneStart = reader.BaseStream.Position;
			
			// Check bounds
			if ( boneStart + boneSize > fileLength )
			{
				Log.Warning( $"MdlFile: Bone {i} would exceed file bounds ({boneStart}+{boneSize}>{fileLength}), stopping" );
				break;
			}

			// Read bone fields manually to handle version differences
			int nameOffset = reader.ReadInt32();
			int parentBone = reader.ReadInt32();
			reader.BaseStream.Position += 24; // Skip BoneController[6]
			var position = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			var quaternion = new Quaternion( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			var rotation = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
			reader.BaseStream.Position += 24; // Skip PositionScale and RotationScale
			reader.BaseStream.Position += 48; // Skip PoseToBone[12]
			reader.BaseStream.Position += 16; // Skip AlignmentQuaternion
			int flags = reader.ReadInt32();
			reader.BaseStream.Position += 20; // Skip ProceduralType, ProceduralIndex, PhysicsBone, SurfacePropIndex, Contents
			
			// Skip Unused[8] only for v47+
			if ( Header.Version >= SourceConstants.MDL_VERSION_47 )
			{
				reader.BaseStream.Position += 32;
			}

			string name = reader.ReadStringAtOffset( boneStart, nameOffset );

			Bones.Add( new MdlBone
			{
				Index = i,
				Name = name,
				ParentIndex = parentBone,
				Position = position,
				Quaternion = quaternion,
				Rotation = rotation,
				Flags = flags
			} );
			
			// Seek to the next bone - IMPORTANT: ReadStringAtOffset changes the stream position
			reader.BaseStream.Position = boneStart + boneSize;
		}
		Log.Info( $"MdlFile: Read {Bones.Count} bones" );
	}

	private void ReadFlexDescriptors( BinaryReader reader )
	{
		if ( Header.FlexDescCount <= 0 || Header.FlexDescIndex <= 0 )
		{
			return;
		}

		long fileLength = reader.BaseStream.Length;
		reader.BaseStream.Position = Header.FlexDescIndex;

		for ( int i = 0; i < Header.FlexDescCount; i++ )
		{
			long descStart = reader.BaseStream.Position;
			if ( descStart + 4 > fileLength )
			{
				Log.Warning( $"MdlFile.ReadFlexDescriptors: descriptor {i} would exceed file bounds" );
				break;
			}

			int nameOffset = reader.ReadInt32();
			string name = $"flex_{i}";

			if ( nameOffset > 0 )
			{
				string readName = reader.ReadStringAtOffset( descStart, nameOffset );
				if ( !string.IsNullOrWhiteSpace( readName ) )
				{
					name = readName;
				}
			}

			FlexDescriptors.Add( name );
			reader.BaseStream.Position = descStart + 4;
		}

		Log.Info( $"MdlFile: Read {FlexDescriptors.Count} flex descriptors" );
	}

	private void ReadFlexControllers( BinaryReader reader )
	{
		if ( Header.FlexControllerCount <= 0 || Header.FlexControllerIndex <= 0 )
		{
			return;
		}

		long fileLength = reader.BaseStream.Length;
		const int flexControllerSize = 20;
		reader.BaseStream.Position = Header.FlexControllerIndex;

		for ( int i = 0; i < Header.FlexControllerCount; i++ )
		{
			long entryStart = reader.BaseStream.Position;
			if ( entryStart + flexControllerSize > fileLength )
			{
				Log.Warning( $"MdlFile.ReadFlexControllers: controller {i} would exceed file bounds" );
				break;
			}

			StudioFlexController controller = reader.ReadStruct<StudioFlexController>();
			string type = controller.TypeOffset != 0
				? reader.ReadStringAtOffset( entryStart, controller.TypeOffset )
				: string.Empty;
			string name = controller.NameOffset != 0
				? reader.ReadStringAtOffset( entryStart, controller.NameOffset )
				: string.Empty;

			if ( string.IsNullOrWhiteSpace( name ) )
			{
				name = $"flex_controller_{i}";
			}

			FlexControllers.Add( new MdlFlexController
			{
				Index = i,
				Type = type ?? string.Empty,
				Name = name,
				LocalToGlobal = controller.LocalToGlobal,
				Min = controller.Min,
				Max = controller.Max
			} );

			reader.BaseStream.Position = entryStart + flexControllerSize;
		}

		Log.Info( $"MdlFile: Read {FlexControllers.Count} flex controllers" );
	}

	private void ReadFlexRules( BinaryReader reader )
	{
		if ( Header.FlexRulesCount <= 0 || Header.FlexRulesIndex <= 0 )
		{
			return;
		}

		long fileLength = reader.BaseStream.Length;
		const int ruleSize = 12;
		const int opSize = 8;
		reader.BaseStream.Position = Header.FlexRulesIndex;

		for ( int i = 0; i < Header.FlexRulesCount; i++ )
		{
			long ruleStart = reader.BaseStream.Position;
			if ( ruleStart + ruleSize > fileLength )
			{
				Log.Warning( $"MdlFile.ReadFlexRules: rule {i} would exceed file bounds" );
				break;
			}

			StudioFlexRule rule = reader.ReadStruct<StudioFlexRule>();
			var parsedRule = new MdlFlexRule
			{
				Index = i,
				FlexDescIndex = rule.FlexIndex
			};

			if ( rule.OpCount > 0 && rule.OpOffset != 0 )
			{
				long opStart = ruleStart + rule.OpOffset;
				if ( opStart > 0 && opStart < fileLength )
				{
					reader.BaseStream.Position = opStart;
					for ( int opIndex = 0; opIndex < rule.OpCount; opIndex++ )
					{
						long currentOpStart = reader.BaseStream.Position;
						if ( currentOpStart + opSize > fileLength )
						{
							Log.Warning( $"MdlFile.ReadFlexRules: op {opIndex} in rule {i} would exceed file bounds" );
							break;
						}

						StudioFlexOp op = reader.ReadStruct<StudioFlexOp>();
						bool isConst = op.Op == 1; // STUDIO_CONST
						parsedRule.Operations.Add( new MdlFlexRuleOp
						{
							Op = op.Op,
							Index = op.Value,
							IsConst = isConst,
							ConstValue = isConst ? BitConverter.Int32BitsToSingle( op.Value ) : 0f
						} );
					}
				}
			}

			FlexRules.Add( parsedRule );
			reader.BaseStream.Position = ruleStart + ruleSize;
		}

		Log.Info( $"MdlFile: Read {FlexRules.Count} flex rules" );
	}

	private void ReadFlexControllerUIs( BinaryReader reader )
	{
		if ( Header.FlexControllerUICount <= 0 || Header.FlexControllerUIIndex <= 0 )
		{
			return;
		}

		long fileLength = reader.BaseStream.Length;
		const int uiSize = 20;
		reader.BaseStream.Position = Header.FlexControllerUIIndex;

		for ( int i = 0; i < Header.FlexControllerUICount; i++ )
		{
			long uiStart = reader.BaseStream.Position;
			if ( uiStart + uiSize > fileLength )
			{
				Log.Warning( $"MdlFile.ReadFlexControllerUIs: ui {i} would exceed file bounds" );
				break;
			}

			StudioFlexControllerUI ui = reader.ReadStruct<StudioFlexControllerUI>();
			string name = ui.NameOffset != 0
				? reader.ReadStringAtOffset( uiStart, ui.NameOffset )
				: string.Empty;
			bool stereo = ui.Stereo != 0;
			byte remapType = ui.RemapType;

			string controller = string.Empty;
			string left = string.Empty;
			string right = string.Empty;
			string nway = string.Empty;

			if ( stereo )
			{
				left = ReadControllerNameFromPointer( reader, uiStart, ui.Index0, fileLength );
				right = ReadControllerNameFromPointer( reader, uiStart, ui.Index1, fileLength );
			}
			else
			{
				controller = ReadControllerNameFromPointer( reader, uiStart, ui.Index0, fileLength );
			}

			// RemapType 2 (NWAY) and 3 (EYELID) use index2 as an additional controller.
			if ( remapType == 2 || remapType == 3 )
			{
				nway = ReadControllerNameFromPointer( reader, uiStart, ui.Index2, fileLength );
			}

			FlexControllerUIs.Add( new MdlFlexControllerUI
			{
				Index = i,
				Name = name ?? string.Empty,
				ControllerName = controller,
				LeftControllerName = left,
				RightControllerName = right,
				NWayControllerName = nway,
				RemapType = remapType,
				Stereo = stereo
			} );

			reader.BaseStream.Position = uiStart + uiSize;
		}

		Log.Info( $"MdlFile: Read {FlexControllerUIs.Count} flex controller UI entries" );
	}

	private static string ReadControllerNameFromPointer( BinaryReader reader, long baseOffset, int relativeOffset, long fileLength )
	{
		if ( relativeOffset == 0 )
		{
			return string.Empty;
		}

		long controllerStart = baseOffset + relativeOffset;
		if ( controllerStart <= 0 || controllerStart + 8 > fileLength )
		{
			return string.Empty;
		}

		long returnPosition = reader.BaseStream.Position;
		try
		{
			reader.BaseStream.Position = controllerStart + 4; // mstudioflexcontroller_t::NameOffset
			int nameOffset = reader.ReadInt32();
			if ( nameOffset == 0 )
			{
				return string.Empty;
			}

			return reader.ReadStringAtOffset( controllerStart, nameOffset );
		}
		catch
		{
			return string.Empty;
		}
		finally
		{
			reader.BaseStream.Position = returnPosition;
		}
	}

	public string ResolveFlexDisplayName( int flexDescIndex, string fallbackName )
	{
		if ( _resolvedFlexDisplayNames is null )
		{
			_resolvedFlexDisplayNames = BuildResolvedFlexDisplayNameMap();
		}

		if ( _resolvedFlexDisplayNames.TryGetValue( flexDescIndex, out string? resolved ) && !string.IsNullOrWhiteSpace( resolved ) )
		{
			return resolved;
		}

		return fallbackName;
	}

	private Dictionary<int, string> BuildResolvedFlexDisplayNameMap()
	{
		var result = new Dictionary<int, string>();
		if ( FlexControllers.Count == 0 || FlexRules.Count == 0 )
		{
			return result;
		}

		Dictionary<string, string> controllerDisplayNames = BuildControllerDisplayNameMap();
		var candidateByDesc = new Dictionary<int, string>();

		foreach ( MdlFlexRule rule in FlexRules )
		{
			if ( rule.FlexDescIndex < 0 || rule.FlexDescIndex >= FlexDescriptors.Count )
			{
				continue;
			}

			string fallbackName = GetFlexName( rule.FlexDescIndex );
			bool fallbackLooksCode = LooksLikeCodeFlexName( fallbackName );
			string? bestCandidate = null;
			int bestScore = int.MinValue;

			foreach ( MdlFlexRuleOp op in rule.Operations )
			{
				if ( op.IsConst || !IsControllerFetchOp( op.Op ) )
				{
					continue;
				}

				if ( op.Index < 0 || op.Index >= FlexControllers.Count )
				{
					continue;
				}

				string controllerName = FlexControllers[op.Index].Name;
				if ( string.IsNullOrWhiteSpace( controllerName ) )
				{
					continue;
				}

				string candidate = controllerDisplayNames.TryGetValue( controllerName, out string? display )
					? display
					: controllerName;
				candidate = ApplySideHintFromFallback( candidate, fallbackName );

				int score = ScoreDisplayNameCandidate( candidate, fallbackName, fallbackLooksCode );
				if ( score > bestScore )
				{
					bestScore = score;
					bestCandidate = candidate;
				}
			}

			if ( !fallbackLooksCode )
			{
				// Preserve authored, descriptive flex descriptor names.
				candidateByDesc[rule.FlexDescIndex] = fallbackName;
				continue;
			}

			if ( string.IsNullOrWhiteSpace( bestCandidate ) )
			{
				candidateByDesc[rule.FlexDescIndex] = fallbackName;
				continue;
			}

			candidateByDesc[rule.FlexDescIndex] = bestCandidate;
		}

		if ( candidateByDesc.Count == 0 )
		{
			return result;
		}

		var usedNames = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( KeyValuePair<int, string> pair in candidateByDesc.OrderBy( p => p.Key ) )
		{
			string finalName = pair.Value;
			if ( !usedNames.Add( finalName ) )
			{
				string fallback = GetFlexName( pair.Key );
				if ( !string.IsNullOrWhiteSpace( fallback ) )
				{
					string merged = $"{finalName}_{fallback}";
					if ( usedNames.Add( merged ) )
					{
						finalName = merged;
					}
				}

				if ( finalName.Equals( pair.Value, StringComparison.OrdinalIgnoreCase ) )
				{
					int suffix = 2;
					string deduped;
					do
					{
						deduped = $"{pair.Value}_{suffix}";
						suffix++;
					}
					while ( !usedNames.Add( deduped ) );

					finalName = deduped;
				}
			}
			result[pair.Key] = finalName;
		}

		return result;
	}

	private Dictionary<string, string> BuildControllerDisplayNameMap()
	{
		var displayNames = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );

		foreach ( MdlFlexController controller in FlexControllers )
		{
			if ( !string.IsNullOrWhiteSpace( controller.Name ) )
			{
				displayNames[controller.Name] = controller.Name;
			}
		}

		foreach ( MdlFlexControllerUI ui in FlexControllerUIs )
		{
			if ( string.IsNullOrWhiteSpace( ui.Name ) )
			{
				continue;
			}

			string uiName = ui.Name.Trim();
			bool uiNameLooksCode = LooksLikeCodeFlexName( uiName );
			if ( !ui.Stereo && !string.IsNullOrWhiteSpace( ui.ControllerName ) )
			{
				displayNames[ui.ControllerName] = uiNameLooksCode ? ui.ControllerName : uiName;
				continue;
			}

			if ( ui.Stereo )
			{
				if ( !string.IsNullOrWhiteSpace( ui.LeftControllerName ) )
				{
					displayNames[ui.LeftControllerName] = uiNameLooksCode
						? ui.LeftControllerName
						: BuildStereoDisplayName( uiName, ui.LeftControllerName, isLeft: true );
				}

				if ( !string.IsNullOrWhiteSpace( ui.RightControllerName ) )
				{
					displayNames[ui.RightControllerName] = uiNameLooksCode
						? ui.RightControllerName
						: BuildStereoDisplayName( uiName, ui.RightControllerName, isLeft: false );
				}
			}
		}

		return displayNames;
	}

	private static string BuildStereoDisplayName( string uiName, string controllerName, bool isLeft )
	{
		if ( string.IsNullOrWhiteSpace( uiName ) )
		{
			return controllerName;
		}

		if ( uiName.IndexOf( "left", StringComparison.OrdinalIgnoreCase ) >= 0 ||
			uiName.IndexOf( "right", StringComparison.OrdinalIgnoreCase ) >= 0 )
		{
			return uiName;
		}

		return $"{uiName}_{(isLeft ? "left" : "right")}";
	}

	private static bool IsControllerFetchOp( int op )
	{
		// Source op codes from studio.h:
		// 2 FETCH1, 15 TWO_WAY_0, 16 TWO_WAY_1, 17 NWAY, 20 DME_LOWER_EYELID, 21 DME_UPPER_EYELID
		return op == 2 || op == 15 || op == 16 || op == 17 || op == 20 || op == 21;
	}

	private static int ScoreDisplayNameCandidate( string candidate, string fallbackName, bool fallbackLooksCode )
	{
		if ( string.IsNullOrWhiteSpace( candidate ) )
		{
			return int.MinValue;
		}

		int score = 0;
		bool candidateLooksCode = LooksLikeCodeFlexName( candidate );
		if ( candidateLooksCode )
		{
			score -= 4;
		}
		else
		{
			score += 4;
		}

		if ( candidate.Contains( "_", StringComparison.Ordinal ) )
		{
			score++;
		}

		bool fallbackLeft = HasLeftHint( fallbackName );
		bool fallbackRight = HasRightHint( fallbackName );
		if ( fallbackLeft || fallbackRight )
		{
			bool candidateLeft = HasLeftHint( candidate );
			bool candidateRight = HasRightHint( candidate );
			if ( (fallbackLeft && candidateLeft) || (fallbackRight && candidateRight) )
			{
				score += 3;
			}
			else if ( (fallbackLeft && candidateRight) || (fallbackRight && candidateLeft) )
			{
				score -= 4;
			}
		}

		if ( !fallbackLooksCode && candidate.Equals( fallbackName, StringComparison.OrdinalIgnoreCase ) )
		{
			score += 2;
		}

		return score;
	}

	private static string ApplySideHintFromFallback( string candidate, string fallbackName )
	{
		if ( string.IsNullOrWhiteSpace( candidate ) )
		{
			return candidate;
		}

		bool fallbackLeft = HasLeftHint( fallbackName );
		bool fallbackRight = HasRightHint( fallbackName );
		if ( !fallbackLeft && !fallbackRight )
		{
			return candidate;
		}

		if ( HasLeftHint( candidate ) || HasRightHint( candidate ) )
		{
			return candidate;
		}

		return $"{candidate}_{(fallbackLeft ? "left" : "right")}";
	}

	private static bool LooksLikeCodeFlexName( string? value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
		{
			return false;
		}

		string name = value.Trim();
		if ( name.Length >= 3 )
		{
			string upper = name.ToUpperInvariant();
			if ( (upper.StartsWith( "AU", StringComparison.Ordinal ) || upper.StartsWith( "AD", StringComparison.Ordinal )) &&
				char.IsDigit( upper[2] ) )
			{
				return true;
			}
		}

		if ( (name.StartsWith( "f", StringComparison.OrdinalIgnoreCase ) || name.StartsWith( "F", StringComparison.OrdinalIgnoreCase )) &&
			name.Length >= 2 && char.IsDigit( name[1] ) )
		{
			return true;
		}

		return false;
	}

	private static bool HasLeftHint( string? value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
		{
			return false;
		}

		string name = value.Trim().ToLowerInvariant();
		return name.Contains( "left", StringComparison.Ordinal ) || name.EndsWith( "_l", StringComparison.Ordinal ) ||
			(name.EndsWith( "l", StringComparison.Ordinal ) && name.Length > 1 && char.IsDigit( name[^2] ));
	}

	private static bool HasRightHint( string? value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
		{
			return false;
		}

		string name = value.Trim().ToLowerInvariant();
		return name.Contains( "right", StringComparison.Ordinal ) || name.EndsWith( "_r", StringComparison.Ordinal ) ||
			(name.EndsWith( "r", StringComparison.Ordinal ) && name.Length > 1 && char.IsDigit( name[^2] ));
	}

	private void ReadBodyParts( BinaryReader reader )
	{
		if ( Header.BodyPartCount <= 0 || Header.BodyPartOffset <= 0 )
			return;

		long fileLength = reader.BaseStream.Length;
		reader.BaseStream.Position = Header.BodyPartOffset;

		for ( int i = 0; i < Header.BodyPartCount; i++ )
		{
			long bodyPartStart = reader.BaseStream.Position;
			
			if ( bodyPartStart + 16 > fileLength )
			{
				Log.Warning( $"MdlFile.ReadBodyParts: bodyPart {i} would exceed file bounds" );
				break;
			}
			
			var bodyPart = reader.ReadStruct<StudioBodyPart>();

			string name = reader.ReadStringAtOffset( bodyPartStart, bodyPart.NameOffset );

			var mdlBodyPart = new MdlBodyPart
			{
				Index = i,
				Name = name,
				Base = bodyPart.Base
			};

			// Read models within this body part
			if ( bodyPart.ModelCount > 0 && bodyPart.ModelOffset != 0 )
			{
				long modelsStart = bodyPartStart + bodyPart.ModelOffset;
				reader.BaseStream.Position = modelsStart;

				for ( int j = 0; j < bodyPart.ModelCount; j++ )
				{
					long modelStart = reader.BaseStream.Position;
					
					if ( modelStart + 148 > fileLength )
					{
						Log.Warning( $"MdlFile.ReadBodyParts: model {j} would exceed file bounds" );
						break;
					}
					
					// Read model name first (64 bytes at start of struct)
					string modelName = ReadFixedString( reader.ReadBytes( 64 ), 64 );
					
					// Read rest of StudioModel struct manually
					// mstudiomodel_t layout (148 bytes total):
					// 0-63: name (64 bytes) - already read
					// 64: type (int)
					// 68: boundingradius (float)
					// 72: nummeshes (int)
					// 76: meshindex (int)
					// 80: numvertices (int)
					// 84: vertexindex (int)
					// 88: tangentsindex (int)
					// 92: numattachments (int)
					// 96: attachmentindex (int)
					// 100: numeyeballs (int)
					// 104: eyeballindex (int)
					// 108: vertexdata pointer (int)
					// 112: tangentdata pointer (int)
					// 116-147: unused (32 bytes = 8 ints)
					int modelType = reader.ReadInt32();
					float boundingRadius = reader.ReadSingle();
					int meshCount = reader.ReadInt32();
					int meshOffset = reader.ReadInt32();
					int vertexCount = reader.ReadInt32();
					int vertexIndex = reader.ReadInt32();
					int tangentIndex = reader.ReadInt32();
					int attachmentCount = reader.ReadInt32();
					int attachmentOffset = reader.ReadInt32();
					int eyeballCount = reader.ReadInt32();
					int eyeballOffset = reader.ReadInt32();
					reader.ReadInt32(); // VertexDataPointer
					reader.ReadInt32(); // TangentDataPointer
					for ( int u = 0; u < 8; u++ ) reader.ReadInt32(); // Unused
					
					Log.Info( $"MdlFile: Model '{modelName}': meshes={meshCount}, eyeballs={eyeballCount}, eyeballOffset={eyeballOffset}" );

					var mdlModel = new MdlModel
					{
						Index = j,
						Name = modelName,
						BoundingRadius = boundingRadius,
						VertexCount = vertexCount,
						VertexIndex = vertexIndex,
						TangentIndex = tangentIndex
					};

					// Read meshes within this model
					if ( meshCount > 0 && meshOffset != 0 )
					{
						long meshesStart = modelStart + meshOffset;
						reader.BaseStream.Position = meshesStart;

						for ( int k = 0; k < meshCount; k++ )
						{
							long meshStart = reader.BaseStream.Position;
							
							if ( meshStart + Marshal.SizeOf<StudioMesh>() > fileLength )
							{
								Log.Warning( $"MdlFile.ReadBodyParts: mesh {k} would exceed file bounds" );
								break;
							}
							
							var mesh = reader.ReadStruct<StudioMesh>();
							var mdlMesh = new MdlMesh
							{
								Index = k,
								MaterialIndex = mesh.Material,
								MaterialType = mesh.MaterialType,
								MaterialParam = mesh.MaterialParam,
								VertexCount = mesh.VertexCount,
								VertexOffset = mesh.VertexOffset,
								Center = mesh.Center
							};

							if ( mesh.FlexCount > 0 && mesh.FlexOffset > 0 )
							{
								mdlMesh.Flexes.AddRange( ReadMeshFlexes( reader, meshStart, mesh, fileLength ) );
							}

							mdlModel.Meshes.Add( mdlMesh );
						}
					}
					
					// Read eyeballs within this model (NOTE: read eyeballs BEFORE we fill in their texture indices from meshes)
					if ( eyeballCount > 0 && eyeballOffset != 0 )
					{
						long eyeballsStart = modelStart + eyeballOffset;
						reader.BaseStream.Position = eyeballsStart;
						int eyeballSize = Marshal.SizeOf<StudioEyeball>();
						
						Log.Info( $"MdlFile: Reading {eyeballCount} eyeballs from model '{modelName}' (modelOffset={eyeballOffset}, structSize={eyeballSize}, expected=172)" );
						
						for ( int k = 0; k < eyeballCount; k++ )
						{
							long eyeballStart = reader.BaseStream.Position;
							
							if ( eyeballStart + eyeballSize > fileLength )
							{
								Log.Warning( $"MdlFile.ReadBodyParts: eyeball {k} would exceed file bounds" );
								break;
							}
							
							var eyeball = reader.ReadStruct<StudioEyeball>();
							string eyeballName = reader.ReadStringAtOffset( eyeballStart, eyeball.NameOffset );
							
							Log.Info( $"  Eyeball[{k}]: name='{eyeballName}', bone={eyeball.Bone}, texture={eyeball.Texture}" );
							Log.Info( $"    Origin=({eyeball.Org.x:F3}, {eyeball.Org.y:F3}, {eyeball.Org.z:F3})" );
							Log.Info( $"    Radius={eyeball.Radius:F3}, IrisScale={eyeball.IrisScale:F3}" );
							Log.Info( $"    Up=({eyeball.Up.x:F3}, {eyeball.Up.y:F3}, {eyeball.Up.z:F3})" );
							Log.Info( $"    Forward=({eyeball.Forward.x:F3}, {eyeball.Forward.y:F3}, {eyeball.Forward.z:F3})" );
							
							Eyeballs.Add( new MdlEyeball
							{
								Name = eyeballName,
								BoneIndex = eyeball.Bone,
								Origin = eyeball.Org,
								ZOffset = eyeball.ZOffset,
								Radius = eyeball.Radius,
								Up = eyeball.Up,
								Forward = eyeball.Forward,
								TextureIndex = eyeball.Texture,
								IrisScale = eyeball.IrisScale
							} );
							
							reader.BaseStream.Position = eyeballStart + eyeballSize;
						}
					}

					mdlBodyPart.Models.Add( mdlModel );

					// Move to next model (148 bytes per model)
					reader.BaseStream.Position = modelStart + 148;
				}
			}

			BodyParts.Add( mdlBodyPart );

			// Move to next body part (16 bytes per body part)
			reader.BaseStream.Position = bodyPartStart + 16;
		}
		
		// Fill in eyeball texture indices from mesh materialParam
		// According to Crowbar: aModel.theEyeballs(aMesh.materialParam).theTextureIndex = aMesh.materialIndex
		// MaterialType 1 indicates an eyeball mesh
		foreach ( var bodyPart in BodyParts )
		{
			foreach ( var model in bodyPart.Models )
			{
				foreach ( var mesh in model.Meshes )
				{
					// MaterialType 1 = eyeball mesh
					if ( mesh.MaterialType == 1 && mesh.MaterialParam >= 0 && mesh.MaterialParam < Eyeballs.Count )
					{
						var eyeball = Eyeballs[mesh.MaterialParam];
						eyeball.TextureIndex = mesh.MaterialIndex;
						Log.Info( $"MdlFile: Eyeball[{mesh.MaterialParam}] texture index set to {mesh.MaterialIndex} from mesh" );
					}
				}
			}
		}
		
		Log.Info( $"MdlFile: Read {BodyParts.Count} body parts, {Eyeballs.Count} eyeballs" );
	}

	private void ReadMaterials( BinaryReader reader )
	{
		long fileLength = reader.BaseStream.Length;
		
		// Read texture names
		if ( Header.TextureCount > 0 && Header.TextureOffset > 0 )
		{
			reader.BaseStream.Position = Header.TextureOffset;
			int textureSize = Marshal.SizeOf<StudioTexture>();

			for ( int i = 0; i < Header.TextureCount; i++ )
			{
				long textureStart = reader.BaseStream.Position;
				
				if ( textureStart + textureSize > fileLength )
				{
					Log.Warning( $"MdlFile: texture {i} would exceed file bounds" );
					break;
				}
				
				var texture = reader.ReadStruct<StudioTexture>();

				string name = reader.ReadStringAtOffset( textureStart, texture.NameOffset );
				Materials.Add( name );
			}
		}

		// Read texture directories (paths)
		if ( Header.TextureDirCount > 0 && Header.TextureDirOffset > 0 )
		{
			reader.BaseStream.Position = Header.TextureDirOffset;

			for ( int i = 0; i < Header.TextureDirCount; i++ )
			{
				if ( reader.BaseStream.Position + 4 > fileLength )
				{
					Log.Warning( $"MdlFile: textureDir {i} would exceed file bounds" );
					break;
				}
				
				int offset = reader.ReadInt32();
				string path = reader.ReadStringAtOffset( 0, offset );
				MaterialPaths.Add( path );
			}
		}
		Log.Info( $"MdlFile: Read {Materials.Count} materials, {MaterialPaths.Count} paths" );
	}

	private void ReadSkinFamilies( BinaryReader reader )
	{
		if ( Header.SkinFamilyCount <= 0 || Header.SkinReferenceCount <= 0 || Header.SkinReferenceIndex <= 0 )
			return;

		long fileLength = reader.BaseStream.Length;
		reader.BaseStream.Position = Header.SkinReferenceIndex;
		
		int expectedSize = Header.SkinFamilyCount * Header.SkinReferenceCount * 2;
		
		if ( Header.SkinReferenceIndex + expectedSize > fileLength )
		{
			Log.Warning( $"MdlFile.ReadSkinFamilies: would exceed file bounds, skipping" );
			return;
		}

		for ( int i = 0; i < Header.SkinFamilyCount; i++ )
		{
			short[] family = new short[Header.SkinReferenceCount];
			for ( int j = 0; j < Header.SkinReferenceCount; j++ )
			{
				family[j] = reader.ReadInt16();
			}
			SkinFamilies.Add( family );
		}
		Log.Info( $"MdlFile: Read {SkinFamilies.Count} skin families" );
	}

	private void ReadAttachments( BinaryReader reader )
	{
		if ( Header.AttachmentCount <= 0 || Header.AttachmentOffset <= 0 )
			return;

		long fileLength = reader.BaseStream.Length;
		reader.BaseStream.Position = Header.AttachmentOffset;
		
		// Attachment size is 92 bytes for v44+ (includes Unused[8] for all these versions)
		// Based on Crowbar's SourceMdlFile44.vb which reads unused[8] for v44 attachments
		int attachmentSize = 92;

		for ( int i = 0; i < Header.AttachmentCount; i++ )
		{
			long attachStart = reader.BaseStream.Position;
			
			if ( attachStart + attachmentSize > fileLength )
			{
				Log.Warning( $"MdlFile.ReadAttachments: attachment {i} exceeds file bounds" );
				break;
			}

			// Read attachment fields manually
			int nameOffset = reader.ReadInt32();
			uint flags = reader.ReadUInt32();
			int localBone = reader.ReadInt32();
			
			// Read 3x4 matrix (12 floats)
			float[] matrix = new float[12];
			for ( int j = 0; j < 12; j++ )
			{
				matrix[j] = reader.ReadSingle();
			}
			
			// Skip Unused[8] for v47+
			if ( Header.Version >= SourceConstants.MDL_VERSION_47 )
			{
				reader.BaseStream.Position += 32;
			}

			string name = reader.ReadStringAtOffset( attachStart, nameOffset );

			Attachments.Add( new MdlAttachment
			{
				Index = i,
				Name = name,
				BoneIndex = localBone,
				Matrix = matrix
			} );
			
			// Seek to the next attachment - IMPORTANT: ReadStringAtOffset changes the stream position
			reader.BaseStream.Position = attachStart + attachmentSize;
		}
	}

	private void ReadHitboxSets( BinaryReader reader )
	{
		if ( Header.HitboxSetCount <= 0 || Header.HitboxSetOffset <= 0 )
			return;

		long fileLength = reader.BaseStream.Length;
		reader.BaseStream.Position = Header.HitboxSetOffset;

		// Hitbox size is 68 bytes for all versions (including v44 based on Crowbar)
		int hitboxSize = 68;

		for ( int i = 0; i < Header.HitboxSetCount; i++ )
		{
			long setStart = reader.BaseStream.Position;
			
			// Check if we can read the hitbox set header
			if ( setStart + 12 > fileLength )
			{
				Log.Warning( $"MdlFile: hitbox set {i} header would exceed file bounds" );
				break;
			}
				
			var set = reader.ReadStruct<StudioHitboxSet>();

			string name = reader.ReadStringAtOffset( setStart, set.NameOffset );

			var hitboxSet = new MdlHitboxSet
			{
				Index = i,
				Name = name
			};

			// Read hitboxes
			if ( set.HitboxCount > 0 && set.HitboxOffset != 0 )
			{
				long hitboxArrayStart = setStart + set.HitboxOffset;
				long hitboxArrayEnd = hitboxArrayStart + (set.HitboxCount * hitboxSize);
				
				// Check if hitbox array is within bounds
				if ( hitboxArrayEnd > fileLength )
				{
					Log.Warning( $"MdlFile: hitbox array would exceed file bounds, skipping hitboxes" );
				}
				else
				{
					reader.BaseStream.Position = hitboxArrayStart;

					for ( int j = 0; j < set.HitboxCount; j++ )
					{
						long hitboxStart = reader.BaseStream.Position;
						
						// Read hitbox fields manually 
						int bone = reader.ReadInt32();
						int group = reader.ReadInt32();
						var bbMin = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
						var bbMax = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
						int hitboxNameOffset = reader.ReadInt32();
						
						// Skip unused[8] - 32 bytes - present in all versions
						reader.BaseStream.Position += 32;

						string hitboxName;
						if ( hitboxNameOffset != 0 && hitboxNameOffset < 10000 ) // sanity check
						{
							hitboxName = reader.ReadStringAtOffset( hitboxStart, hitboxNameOffset );
						}
						else
						{
							hitboxName = $"hitbox_{j}";
						}

						hitboxSet.Hitboxes.Add( new MdlHitbox
						{
							Index = j,
							Name = hitboxName,
							BoneIndex = bone,
							Group = group,
							Min = bbMin,
							Max = bbMax
						} );
						
						// Seek to the next hitbox - IMPORTANT: ReadStringAtOffset changes the stream position
						reader.BaseStream.Position = hitboxStart + hitboxSize;
					}
				}
			}

			HitboxSets.Add( hitboxSet );

			// Move to next set
			reader.BaseStream.Position = setStart + 12; // StudioHitboxSet is always 12 bytes
		}
		Log.Info( $"MdlFile: Read {HitboxSets.Count} hitbox sets" );
	}

	private List<MdlFlex> ReadMeshFlexes( BinaryReader reader, long meshStart, StudioMesh mesh, long fileLength )
	{
		var result = new List<MdlFlex>();
		long returnPosition = reader.BaseStream.Position;
		const int studioFlexSize = 60;

		try
		{
			long flexArrayStart = meshStart + mesh.FlexOffset;
			if ( flexArrayStart <= 0 || flexArrayStart >= fileLength )
			{
				return result;
			}

			reader.BaseStream.Position = flexArrayStart;
			for ( int i = 0; i < mesh.FlexCount; i++ )
			{
				long flexStart = reader.BaseStream.Position;
				if ( flexStart + studioFlexSize > fileLength )
				{
					Log.Warning( $"MdlFile.ReadMeshFlexes: flex {i} would exceed file bounds" );
					break;
				}

				var flex = reader.ReadStruct<StudioFlex>();
				var mdlFlex = new MdlFlex
				{
					FlexDescIndex = flex.FlexDescIndex,
					Name = GetFlexName( flex.FlexDescIndex ),
					PartnerIndex = flex.PartnerIndex,
					VertexAnimType = flex.VertAnimType
				};

				if ( flex.VertCount > 0 && flex.VertOffset > 0 )
				{
					long vertAnimStart = flexStart + flex.VertOffset;
					if ( vertAnimStart > 0 && vertAnimStart < fileLength )
					{
						reader.BaseStream.Position = vertAnimStart;
						bool hasWrinkle = flex.VertAnimType == 1;
						int vertAnimSize = hasWrinkle ? 18 : 16;

						for ( int v = 0; v < flex.VertCount; v++ )
						{
							long animStart = reader.BaseStream.Position;
							if ( animStart + vertAnimSize > fileLength )
							{
								Log.Warning( $"MdlFile.ReadMeshFlexes: vertex animation {v} would exceed file bounds" );
								break;
							}

							ushort index = reader.ReadUInt16();
							reader.ReadByte(); // speed
							byte side = reader.ReadByte();

							float deltaX = HalfToFloat( reader.ReadUInt16() );
							float deltaY = HalfToFloat( reader.ReadUInt16() );
							float deltaZ = HalfToFloat( reader.ReadUInt16() );
							float normalX = HalfToFloat( reader.ReadUInt16() );
							float normalY = HalfToFloat( reader.ReadUInt16() );
							float normalZ = HalfToFloat( reader.ReadUInt16() );

							float wrinkle = 0f;
							bool hasWrinkleDelta = false;
							if ( hasWrinkle )
							{
								wrinkle = HalfToFloat( reader.ReadUInt16() );
								hasWrinkleDelta = true;
							}

							mdlFlex.VertexAnimations.Add( new MdlFlexVertexAnimation
							{
								VertexIndex = index,
								Side = side,
								VertexDelta = new Vector3( deltaX, deltaY, deltaZ ),
								NormalDelta = new Vector3( normalX, normalY, normalZ ),
								WrinkleDelta = wrinkle,
								HasWrinkleDelta = hasWrinkleDelta
							} );
						}
					}
				}

				result.Add( mdlFlex );
				reader.BaseStream.Position = flexStart + studioFlexSize;
			}
		}
		finally
		{
			reader.BaseStream.Position = returnPosition;
		}

		return result;
	}

	private string GetFlexName( int flexDescIndex )
	{
		if ( flexDescIndex >= 0 && flexDescIndex < FlexDescriptors.Count )
		{
			string name = FlexDescriptors[flexDescIndex];
			if ( !string.IsNullOrWhiteSpace( name ) )
			{
				return name;
			}
		}

		return $"flex_{flexDescIndex}";
	}

	private static float HalfToFloat( ushort bits )
	{
		return (float)BitConverter.UInt16BitsToHalf( bits );
	}
}

/// <summary>
/// Parsed bone data.
/// </summary>
public class MdlBone
{
	public int Index { get; set; }
	public string Name { get; set; }
	public int ParentIndex { get; set; }
	public Vector3 Position { get; set; }
	public Quaternion Quaternion { get; set; }
	public Vector3 Rotation { get; set; }
	public int Flags { get; set; }
}

/// <summary>
/// Parsed body part data.
/// </summary>
public class MdlBodyPart
{
	public int Index { get; set; }
	public string Name { get; set; }
	public int Base { get; set; }
	public List<MdlModel> Models { get; } = new();
}

/// <summary>
/// Parsed model data (within a body part).
/// </summary>
public class MdlModel
{
	public int Index { get; set; }
	public string Name { get; set; }
	public float BoundingRadius { get; set; }
	public int VertexCount { get; set; }
	public int VertexIndex { get; set; }
	public int TangentIndex { get; set; }
	public List<MdlMesh> Meshes { get; } = new();
}

/// <summary>
/// Parsed mesh data (within a model).
/// </summary>
public class MdlMesh
{
	public int Index { get; set; }
	public int MaterialIndex { get; set; }
	public int MaterialType { get; set; }
	public int MaterialParam { get; set; }
	public int VertexCount { get; set; }
	public int VertexOffset { get; set; }
	public Vector3 Center { get; set; }
	public List<MdlFlex> Flexes { get; } = new();
}

/// <summary>
/// Parsed flex controller data.
/// </summary>
public class MdlFlexController
{
	public int Index { get; set; }
	public string Type { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public int LocalToGlobal { get; set; }
	public float Min { get; set; }
	public float Max { get; set; }
}

/// <summary>
/// Parsed flex controller UI mapping data.
/// </summary>
public class MdlFlexControllerUI
{
	public int Index { get; set; }
	public string Name { get; set; } = string.Empty;
	public string ControllerName { get; set; } = string.Empty;
	public string LeftControllerName { get; set; } = string.Empty;
	public string RightControllerName { get; set; } = string.Empty;
	public string NWayControllerName { get; set; } = string.Empty;
	public byte RemapType { get; set; }
	public bool Stereo { get; set; }
}

/// <summary>
/// Parsed flex rule data.
/// </summary>
public class MdlFlexRule
{
	public int Index { get; set; }
	public int FlexDescIndex { get; set; }
	public List<MdlFlexRuleOp> Operations { get; } = new();
}

/// <summary>
/// Parsed flex rule operation.
/// </summary>
public class MdlFlexRuleOp
{
	public int Op { get; set; }
	public int Index { get; set; }
	public bool IsConst { get; set; }
	public float ConstValue { get; set; }
}

/// <summary>
/// Parsed flex channel data attached to a mesh.
/// </summary>
public class MdlFlex
{
	public int FlexDescIndex { get; set; }
	public string Name { get; set; } = string.Empty;
	public int PartnerIndex { get; set; }
	public byte VertexAnimType { get; set; }
	public List<MdlFlexVertexAnimation> VertexAnimations { get; } = new();
}

/// <summary>
/// Parsed flex vertex delta.
/// </summary>
public class MdlFlexVertexAnimation
{
	public int VertexIndex { get; set; }
	public byte Side { get; set; }
	public Vector3 VertexDelta { get; set; }
	public Vector3 NormalDelta { get; set; }
	public float WrinkleDelta { get; set; }
	public bool HasWrinkleDelta { get; set; }
}

/// <summary>
/// Parsed attachment data.
/// </summary>
public class MdlAttachment
{
	public int Index { get; set; }
	public string Name { get; set; }
	public int BoneIndex { get; set; }
	public float[] Matrix { get; set; }
}

/// <summary>
/// Parsed hitbox set.
/// </summary>
public class MdlHitboxSet
{
	public int Index { get; set; }
	public string Name { get; set; }
	public List<MdlHitbox> Hitboxes { get; } = new();
}

/// <summary>
/// Parsed hitbox.
/// </summary>
public class MdlHitbox
{
	public int Index { get; set; }
	public string Name { get; set; }
	public int BoneIndex { get; set; }
	public int Group { get; set; }
	public Vector3 Min { get; set; }
	public Vector3 Max { get; set; }
}

/// <summary>
/// Parsed eyeball data for eye shader rendering.
/// Contains the positioning and projection vectors for proper iris mapping.
/// </summary>
public class MdlEyeball
{
	public string Name { get; set; }
	public int BoneIndex { get; set; }
	public Vector3 Origin { get; set; }      // Eye center in bone local space
	public float ZOffset { get; set; }       // Z offset for iris depth
	public float Radius { get; set; }        // Eyeball radius
	public Vector3 Up { get; set; }          // Up vector for projection
	public Vector3 Forward { get; set; }     // Forward/look direction
	public int TextureIndex { get; set; }    // Material index
	public float IrisScale { get; set; }     // Iris texture scale
	
	/// <summary>
	/// Compute iris projection U vector for the eye shader.
	/// The projection maps world positions to iris UV coordinates.
	/// </summary>
	public Vector4 ComputeIrisProjectionU()
	{
		// Left vector is perpendicular to forward and up
		var left = Vector3.Cross( Up, Forward ).Normal;
		
		// Scale based on iris scale and radius
		// iris_scale typically around 0.5-1.0, radius around 0.5-1.0
		float scale = 1.0f / ( Radius * IrisScale * 2.0f );
		
		// The W component is the offset: -dot(left, origin) * scale + 0.5
		// This centers the UV at 0.5 when world pos equals origin
		float offset = -Vector3.Dot( left, Origin ) * scale + 0.5f;
		
		return new Vector4( left.x * scale, left.y * scale, left.z * scale, offset );
	}
	
	/// <summary>
	/// Compute iris projection V vector for the eye shader.
	/// </summary>
	public Vector4 ComputeIrisProjectionV()
	{
		// Up vector scaled for projection
		float scale = 1.0f / ( Radius * IrisScale * 2.0f );
		
		// The W component centers the UV
		float offset = -Vector3.Dot( Up, Origin ) * scale + 0.5f;
		
		return new Vector4( Up.x * scale, Up.y * scale, Up.z * scale, offset );
	}
}
