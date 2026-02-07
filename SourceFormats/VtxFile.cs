namespace GModMount.Source;

/// <summary>
/// Parsed VTX (Valve TriangleX) file.
/// Contains mesh strip/index data for rendering.
/// </summary>
public class VtxFile
{
	public VtxHeader Header { get; private set; }
	public List<VtxBodyPartData> BodyParts { get; } = new();

	/// <summary>
	/// Load a VTX file from a byte array.
	/// </summary>
	/// <param name="data">VTX file data</param>
	/// <param name="mdlVersion">MDL version (affects strip group struct size)</param>
	public static VtxFile Load( byte[] data, int mdlVersion = 49 )
	{
		using var stream = new MemoryStream( data );
		using var reader = new BinaryReader( stream );
		return Load( reader, mdlVersion );
	}

	/// <summary>
	/// Load a VTX file from a stream.
	/// </summary>
	/// <param name="stream">VTX stream</param>
	/// <param name="mdlVersion">MDL version (affects strip group struct size)</param>
	public static VtxFile Load( Stream stream, int mdlVersion = 49 )
	{
		using var reader = new BinaryReader( stream, Encoding.ASCII, leaveOpen: true );
		return Load( reader, mdlVersion );
	}

	/// <summary>
	/// Load a VTX file from a binary reader.
	/// </summary>
	/// <param name="reader">Binary reader</param>
	/// <param name="mdlVersion">MDL version (affects strip group struct size)</param>
	public static VtxFile Load( BinaryReader reader, int mdlVersion = 49 )
	{
		var vtx = new VtxFile();
		
		// MDL v49+ has extra topology fields in strip groups (8 bytes larger)
		bool hasTopologyFields = mdlVersion >= 49;
		int stripGroupSize = hasTopologyFields 
			? Marshal.SizeOf<VtxStripGroup>() 
			: Marshal.SizeOf<VtxStripGroupV44>();

		// Read header
		vtx.Header = reader.ReadStruct<VtxHeader>();

		if ( !vtx.Header.IsValid )
		{
			throw new InvalidDataException( $"Invalid VTX file: Version={vtx.Header.Version}" );
		}

		// Read body parts
		if ( vtx.Header.BodyPartCount > 0 && vtx.Header.BodyPartOffset > 0 )
		{
			reader.BaseStream.Position = vtx.Header.BodyPartOffset;

			for ( int i = 0; i < vtx.Header.BodyPartCount; i++ )
			{
				long bodyPartStart = reader.BaseStream.Position;
				var bodyPart = reader.ReadStruct<VtxBodyPart>();

				var bodyPartData = new VtxBodyPartData { Index = i };

				// Read models
				if ( bodyPart.ModelCount > 0 && bodyPart.ModelOffset != 0 )
				{
					reader.BaseStream.Position = bodyPartStart + bodyPart.ModelOffset;

					for ( int j = 0; j < bodyPart.ModelCount; j++ )
					{
						long modelStart = reader.BaseStream.Position;
						var model = reader.ReadStruct<VtxModel>();

						var modelData = new VtxModelData { Index = j };

						// Read LODs
						if ( model.LodCount > 0 && model.LodOffset != 0 )
						{
							reader.BaseStream.Position = modelStart + model.LodOffset;

							for ( int k = 0; k < model.LodCount; k++ )
							{
								long lodStart = reader.BaseStream.Position;
								var lod = reader.ReadStruct<VtxModelLod>();

								var lodData = new VtxLodData
								{
									Index = k,
									SwitchPoint = lod.SwitchPoint
								};

								// Read meshes
								if ( lod.MeshCount > 0 && lod.MeshOffset != 0 )
								{
									reader.BaseStream.Position = lodStart + lod.MeshOffset;

									for ( int m = 0; m < lod.MeshCount; m++ )
									{
										long meshStart = reader.BaseStream.Position;
										var mesh = reader.ReadStruct<VtxMesh>();

										var meshData = new VtxMeshData
										{
											Index = m,
											Flags = mesh.Flags
										};

										// Read strip groups
										if ( mesh.StripGroupCount > 0 && mesh.StripGroupOffset != 0 )
										{
											reader.BaseStream.Position = meshStart + mesh.StripGroupOffset;

											for ( int sg = 0; sg < mesh.StripGroupCount; sg++ )
											{
												long stripGroupStart = reader.BaseStream.Position;
												
												// Read strip group header based on MDL version
												int vertexCount, vertexOffset, indexCount, indexOffset, stripCount, stripOffset;
												byte flags;
												
												if ( hasTopologyFields )
												{
													var stripGroup = reader.ReadStruct<VtxStripGroup>();
													vertexCount = stripGroup.VertexCount;
													vertexOffset = stripGroup.VertexOffset;
													indexCount = stripGroup.IndexCount;
													indexOffset = stripGroup.IndexOffset;
													stripCount = stripGroup.StripCount;
													stripOffset = stripGroup.StripOffset;
													flags = stripGroup.Flags;
												}
												else
												{
													var stripGroup = reader.ReadStruct<VtxStripGroupV44>();
													vertexCount = stripGroup.VertexCount;
													vertexOffset = stripGroup.VertexOffset;
													indexCount = stripGroup.IndexCount;
													indexOffset = stripGroup.IndexOffset;
													stripCount = stripGroup.StripCount;
													stripOffset = stripGroup.StripOffset;
													flags = stripGroup.Flags;
												}

												var stripGroupData = new VtxStripGroupData
												{
													Index = sg,
													Flags = flags
												};

												// Read vertices
												if ( vertexCount > 0 && vertexOffset != 0 )
												{
													reader.BaseStream.Position = stripGroupStart + vertexOffset;
													stripGroupData.Vertices = reader.ReadStructArray<VtxVertex>( vertexCount );
												}

												// Read indices
												if ( indexCount > 0 && indexOffset != 0 )
												{
													reader.BaseStream.Position = stripGroupStart + indexOffset;
													stripGroupData.Indices = new ushort[indexCount];
													for ( int idx = 0; idx < indexCount; idx++ )
													{
														stripGroupData.Indices[idx] = reader.ReadUInt16();
													}
												}

												// Read strips
												if ( stripCount > 0 && stripOffset != 0 )
												{
													reader.BaseStream.Position = stripGroupStart + stripOffset;

													for ( int s = 0; s < stripCount; s++ )
													{
														var strip = reader.ReadStruct<VtxStrip>();

														stripGroupData.Strips.Add( new VtxStripData
														{
															Index = s,
															IndexCount = strip.IndexCount,
															IndexOffset = strip.IndexOffset,
															VertexCount = strip.VertexCount,
															VertexOffset = strip.VertexOffset,
															BoneCount = strip.BoneCount,
															Flags = strip.Flags
														} );
													}
												}

												meshData.StripGroups.Add( stripGroupData );

												// Move to next strip group using correct size
												reader.BaseStream.Position = stripGroupStart + stripGroupSize;
											}
										}

										lodData.Meshes.Add( meshData );

										// Move to next mesh
										reader.BaseStream.Position = meshStart + Marshal.SizeOf<VtxMesh>();
									}
								}

								modelData.Lods.Add( lodData );

								// Move to next LOD
								reader.BaseStream.Position = lodStart + Marshal.SizeOf<VtxModelLod>();
							}
						}

						bodyPartData.Models.Add( modelData );

						// Move to next model
						reader.BaseStream.Position = modelStart + Marshal.SizeOf<VtxModel>();
					}
				}

				vtx.BodyParts.Add( bodyPartData );

				// Move to next body part
				reader.BaseStream.Position = bodyPartStart + Marshal.SizeOf<VtxBodyPart>();
			}
		}

		return vtx;
	}
}

/// <summary>
/// Parsed VTX body part data.
/// </summary>
public class VtxBodyPartData
{
	public int Index { get; set; }
	public List<VtxModelData> Models { get; } = new();
}

/// <summary>
/// Parsed VTX model data.
/// </summary>
public class VtxModelData
{
	public int Index { get; set; }
	public List<VtxLodData> Lods { get; } = new();
}

/// <summary>
/// Parsed VTX LOD data.
/// </summary>
public class VtxLodData
{
	public int Index { get; set; }
	public float SwitchPoint { get; set; }
	public List<VtxMeshData> Meshes { get; } = new();
}

/// <summary>
/// Parsed VTX mesh data.
/// </summary>
public class VtxMeshData
{
	public int Index { get; set; }
	public byte Flags { get; set; }
	public List<VtxStripGroupData> StripGroups { get; } = new();
}

/// <summary>
/// Parsed VTX strip group data.
/// </summary>
public class VtxStripGroupData
{
	public int Index { get; set; }
	public byte Flags { get; set; }
	public VtxVertex[] Vertices { get; set; } = Array.Empty<VtxVertex>();
	public ushort[] Indices { get; set; } = Array.Empty<ushort>();
	public List<VtxStripData> Strips { get; } = new();

	public bool IsHardwareSkinned => (Flags & SourceConstants.STRIPGROUP_IS_HWSKINNED) != 0;
}

/// <summary>
/// Parsed VTX strip data.
/// </summary>
public class VtxStripData
{
	public int Index { get; set; }
	public int IndexCount { get; set; }
	public int IndexOffset { get; set; }
	public int VertexCount { get; set; }
	public int VertexOffset { get; set; }
	public short BoneCount { get; set; }
	public byte Flags { get; set; }

	public bool IsTriList => (Flags & SourceConstants.STRIP_IS_TRILIST) != 0;
	public bool IsTriStrip => (Flags & SourceConstants.STRIP_IS_TRISTRIP) != 0;
}
