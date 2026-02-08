using GModMount.Source;
using NQuaternion = System.Numerics.Quaternion;

internal static class MdlAnimationPipeline
{
	private const int BoneSize = 216; // mstudiobone_t for v48/v49/v53
	private const int AnimDescSize = 100; // mstudioanimdesc_t
	private const int SeqDescSize = 212; // mstudioseqdesc_t
	private const int AnimSectionSize = 8; // mstudioanimsections_t
	private const int StudioAnimRawPos = 0x01;
	private const int StudioAnimRawRot = 0x02;
	private const int StudioAnimAnimPos = 0x04;
	private const int StudioAnimAnimRot = 0x08;
	private const int StudioAnimDelta = 0x10;
	private const int StudioAnimRawRot2 = 0x20;
	private const int StudioLooping = 0x0001;
	private const int StudioDelta = 0x0004;

	private sealed class BoneAnimBase
	{
		public required Vector3 Position { get; init; }
		public required Vector3 Rotation { get; init; }
		public required Vector3 PositionScale { get; init; }
		public required Vector3 RotationScale { get; init; }
	}

	private sealed class ParsedAnimDesc
	{
		public int Index { get; init; }
		public int StartOffset { get; init; }
		public required string Name { get; init; }
		public float Fps { get; init; }
		public int Flags { get; init; }
		public int NumFrames { get; init; }
		public int AnimBlock { get; init; }
		public int AnimIndex { get; init; }
		public int SectionIndex { get; init; }
		public int SectionFrames { get; init; }
	}

	private sealed class ParsedSeqDesc
	{
		public int Index { get; init; }
		public int StartOffset { get; init; }
		public required string Name { get; init; }
		public int Flags { get; init; }
		public int NumBlends { get; init; }
		public int AnimIndexIndex { get; init; }
		public int GroupSize0 { get; init; }
		public int GroupSize1 { get; init; }
	}

	public static List<AnimationExport> ExportAnimations(
		string mdlPath,
		BuildContext context,
		string modelOutputDirectory,
		Action<string> info,
		Action<string> warn )
	{
		StudioHeader header = context.SourceModel.Mdl.Header;
		if ( header.LocalAnimCount <= 0 || header.LocalSeqCount <= 0 || header.BoneCount <= 0 )
		{
			return new List<AnimationExport>();
		}

		byte[] data = File.ReadAllBytes( mdlPath );
		List<BoneAnimBase> boneBases = ReadBoneAnimationBases( data, header, warn );
		List<ParsedAnimDesc> animDescs = ReadAnimationDescs( data, header, warn );
		List<ParsedSeqDesc> seqDescs = ReadSequenceDescs( data, header, warn );
		if ( boneBases.Count == 0 || animDescs.Count == 0 || seqDescs.Count == 0 )
		{
			return new List<AnimationExport>();
		}

		int boneCount = Math.Min( header.BoneCount, boneBases.Count );
		var exports = new List<AnimationExport>();
		var usedAnimationNames = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		var usedFileNames = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		var warnedExternalAnimBlocks = new HashSet<int>();

		for ( int sequenceIndex = 0; sequenceIndex < seqDescs.Count; sequenceIndex++ )
		{
			ParsedSeqDesc sequence = seqDescs[sequenceIndex];
			if ( !TryResolveSequencePrimaryAnimationIndex( data, sequence, out int animationIndex ) )
			{
				continue;
			}

			if ( animationIndex < 0 || animationIndex >= animDescs.Count )
			{
				continue;
			}

			ParsedAnimDesc animation = animDescs[animationIndex];
			if ( animation.NumFrames <= 0 )
			{
				continue;
			}

			if ( animation.AnimBlock > 0 )
			{
				if ( warnedExternalAnimBlocks.Add( animation.AnimBlock ) )
				{
					warn( $"[warn] Sequence '{sequence.Name}' uses external animblock {animation.AnimBlock}; skipping (ANI blocks are not supported yet)." );
				}
				continue;
			}

			string cleanedName = NameUtil.CleanName( sequence.Name, $"sequence_{sequence.Index}" );
			string animationName = MakeUniqueName( usedAnimationNames, cleanedName, sequence.Index );
			string baseFileName = NameUtil.CleanFileName( $"{context.ModelBaseName}_{animationName}.smd" );
			string fileName = MakeUniqueFileName( usedFileNames, baseFileName, sequence.Index );
			string filePath = Path.Combine( modelOutputDirectory, fileName );

			List<AnimationFramePose> frames = DecodeAnimationFrames( data, animation, boneBases, boneCount, warn );
			if ( frames.Count == 0 )
			{
				continue;
			}

			SmdWriter.WriteAnimation( filePath, context, animationName, frames );

			float frameRate = animation.Fps > 0f ? animation.Fps : 30f;
			bool looping = (sequence.Flags & StudioLooping) != 0;
			exports.Add( new AnimationExport
			{
				Name = animationName,
				FileName = fileName,
				FrameRate = frameRate,
				Looping = looping,
				FrameCount = frames.Count
			} );
		}

		if ( exports.Count > 0 )
		{
			info( $"Animations exported: {exports.Count}" );
		}

		return exports;
	}

	private static string MakeUniqueName( HashSet<string> usedNames, string baseName, int fallbackIndex )
	{
		string candidate = string.IsNullOrWhiteSpace( baseName ) ? $"sequence_{fallbackIndex}" : baseName;
		if ( usedNames.Add( candidate ) )
		{
			return candidate;
		}

		for ( int i = 2; ; i++ )
		{
			string deduped = $"{candidate}_{i}";
			if ( usedNames.Add( deduped ) )
			{
				return deduped;
			}
		}
	}

	private static string MakeUniqueFileName( HashSet<string> usedFileNames, string baseFileName, int fallbackIndex )
	{
		string candidate = string.IsNullOrWhiteSpace( baseFileName ) ? $"sequence_{fallbackIndex}.smd" : baseFileName;
		if ( usedFileNames.Add( candidate ) )
		{
			return candidate;
		}

		string stem = Path.GetFileNameWithoutExtension( candidate );
		string extension = Path.GetExtension( candidate );
		if ( string.IsNullOrWhiteSpace( extension ) )
		{
			extension = ".smd";
		}

		for ( int i = 2; ; i++ )
		{
			string deduped = $"{stem}_{i}{extension}";
			if ( usedFileNames.Add( deduped ) )
			{
				return deduped;
			}
		}
	}

	private static List<BoneAnimBase> ReadBoneAnimationBases( byte[] data, StudioHeader header, Action<string> warn )
	{
		var result = new List<BoneAnimBase>( Math.Max( 0, header.BoneCount ) );
		if ( header.BoneCount <= 0 || header.BoneOffset <= 0 )
		{
			return result;
		}

		for ( int boneIndex = 0; boneIndex < header.BoneCount; boneIndex++ )
		{
			int boneStart = header.BoneOffset + (boneIndex * BoneSize);
			if ( !IsRangeValid( data, boneStart, BoneSize ) )
			{
				warn( $"[warn] Bone animation base {boneIndex} exceeds MDL bounds; stopping animation bone parse." );
				break;
			}

			Vector3 position = new Vector3(
				ReadSingle( data, boneStart + 32 ),
				ReadSingle( data, boneStart + 36 ),
				ReadSingle( data, boneStart + 40 ) );
			Vector3 rotation = new Vector3(
				ReadSingle( data, boneStart + 60 ),
				ReadSingle( data, boneStart + 64 ),
				ReadSingle( data, boneStart + 68 ) );
			Vector3 positionScale = new Vector3(
				ReadSingle( data, boneStart + 72 ),
				ReadSingle( data, boneStart + 76 ),
				ReadSingle( data, boneStart + 80 ) );
			Vector3 rotationScale = new Vector3(
				ReadSingle( data, boneStart + 84 ),
				ReadSingle( data, boneStart + 88 ),
				ReadSingle( data, boneStart + 92 ) );

			result.Add( new BoneAnimBase
			{
				Position = position,
				Rotation = rotation,
				PositionScale = positionScale,
				RotationScale = rotationScale
			} );
		}

		return result;
	}

	private static List<ParsedAnimDesc> ReadAnimationDescs( byte[] data, StudioHeader header, Action<string> warn )
	{
		var result = new List<ParsedAnimDesc>( Math.Max( 0, header.LocalAnimCount ) );
		if ( header.LocalAnimCount <= 0 || header.LocalAnimOffset <= 0 )
		{
			return result;
		}

		for ( int animIndex = 0; animIndex < header.LocalAnimCount; animIndex++ )
		{
			int start = header.LocalAnimOffset + (animIndex * AnimDescSize);
			if ( !IsRangeValid( data, start, AnimDescSize ) )
			{
				warn( $"[warn] Animation descriptor {animIndex} exceeds MDL bounds; stopping descriptor parse." );
				break;
			}

			int nameOffset = ReadInt32( data, start + 4 );
			string name = ReadStringAtRelativeOffset( data, start, nameOffset );
			if ( string.IsNullOrWhiteSpace( name ) )
			{
				name = $"anim_{animIndex}";
			}

			result.Add( new ParsedAnimDesc
			{
				Index = animIndex,
				StartOffset = start,
				Name = name,
				Fps = ReadSingle( data, start + 8 ),
				Flags = ReadInt32( data, start + 12 ),
				NumFrames = ReadInt32( data, start + 16 ),
				AnimBlock = ReadInt32( data, start + 52 ),
				AnimIndex = ReadInt32( data, start + 56 ),
				SectionIndex = ReadInt32( data, start + 80 ),
				SectionFrames = ReadInt32( data, start + 84 )
			} );
		}

		return result;
	}

	private static List<ParsedSeqDesc> ReadSequenceDescs( byte[] data, StudioHeader header, Action<string> warn )
	{
		var result = new List<ParsedSeqDesc>( Math.Max( 0, header.LocalSeqCount ) );
		if ( header.LocalSeqCount <= 0 || header.LocalSeqOffset <= 0 )
		{
			return result;
		}

		for ( int sequenceIndex = 0; sequenceIndex < header.LocalSeqCount; sequenceIndex++ )
		{
			int start = header.LocalSeqOffset + (sequenceIndex * SeqDescSize);
			if ( !IsRangeValid( data, start, SeqDescSize ) )
			{
				warn( $"[warn] Sequence descriptor {sequenceIndex} exceeds MDL bounds; stopping descriptor parse." );
				break;
			}

			int labelOffset = ReadInt32( data, start + 4 );
			string name = ReadStringAtRelativeOffset( data, start, labelOffset );
			if ( string.IsNullOrWhiteSpace( name ) )
			{
				name = $"sequence_{sequenceIndex}";
			}

			result.Add( new ParsedSeqDesc
			{
				Index = sequenceIndex,
				StartOffset = start,
				Name = name,
				Flags = ReadInt32( data, start + 12 ),
				// mstudioseqdesc_t layout (v48/v49/v53):
				// numblends @ +56, animindexindex @ +60, groupsize[0..1] @ +68/+72
				NumBlends = ReadInt32( data, start + 56 ),
				AnimIndexIndex = ReadInt32( data, start + 60 ),
				GroupSize0 = ReadInt32( data, start + 68 ),
				GroupSize1 = ReadInt32( data, start + 72 )
			} );
		}

		return result;
	}

	private static bool TryResolveSequencePrimaryAnimationIndex( byte[] data, ParsedSeqDesc sequence, out int animationIndex )
	{
		animationIndex = -1;
		if ( sequence.AnimIndexIndex <= 0 )
		{
			return false;
		}

		int blendWidth = sequence.GroupSize0 > 0 ? sequence.GroupSize0 : 1;
		int blendHeight = sequence.GroupSize1 > 0 ? sequence.GroupSize1 : 1;
		int blendCount = sequence.NumBlends > 0 ? sequence.NumBlends : (blendWidth * blendHeight);
		blendCount = Math.Max( 1, blendCount );

		int tableStart = sequence.StartOffset + sequence.AnimIndexIndex;
		if ( !IsRangeValid( data, tableStart, blendCount * 2 ) )
		{
			if ( !IsRangeValid( data, tableStart, 2 ) )
			{
				return false;
			}
			blendCount = 1;
		}

		for ( int i = 0; i < blendCount; i++ )
		{
			short candidate = ReadInt16( data, tableStart + (i * 2) );
			if ( candidate >= 0 )
			{
				animationIndex = candidate;
				return true;
			}
		}

		return false;
	}

	private static List<AnimationFramePose> DecodeAnimationFrames(
		byte[] data,
		ParsedAnimDesc animation,
		IReadOnlyList<BoneAnimBase> boneBases,
		int boneCount,
		Action<string> warn )
	{
		var frames = new List<AnimationFramePose>( animation.NumFrames );
		bool isDeltaAnimation = (animation.Flags & StudioDelta) != 0;

		for ( int frameIndex = 0; frameIndex < animation.NumFrames; frameIndex++ )
		{
			var positions = new Vector3[boneCount];
			var rotations = new Vector3[boneCount];
			for ( int boneIndex = 0; boneIndex < boneCount; boneIndex++ )
			{
				if ( isDeltaAnimation )
				{
					positions[boneIndex] = new Vector3( 0f, 0f, 0f );
					rotations[boneIndex] = new Vector3( 0f, 0f, 0f );
				}
				else
				{
					positions[boneIndex] = boneBases[boneIndex].Position;
					rotations[boneIndex] = boneBases[boneIndex].Rotation;
				}
			}

			if ( TryResolveAnimDataOffsetForFrame( data, animation, frameIndex, out int localFrame, out int animDataOffset ) )
			{
				int currentOffset = animDataOffset;
				int guard = 0;
				while ( IsRangeValid( data, currentOffset, 4 ) && guard < boneCount * 8 )
				{
					int boneIndex = data[currentOffset];
					int flags = data[currentOffset + 1];
					short nextOffset = ReadInt16( data, currentOffset + 2 );
					int dataOffset = currentOffset + 4;

					if ( boneIndex >= 0 && boneIndex < boneCount )
					{
						DecodeBoneFrame(
							data,
							boneBases[boneIndex],
							flags,
							dataOffset,
							localFrame,
							out Vector3 position,
							out Vector3 rotation );

						positions[boneIndex] = position;
						rotations[boneIndex] = rotation;
					}

					if ( nextOffset <= 0 )
					{
						break;
					}

					int next = currentOffset + nextOffset;
					if ( next <= currentOffset )
					{
						break;
					}

					currentOffset = next;
					guard++;
				}
			}
			else if ( frameIndex == 0 )
			{
				warn( $"[warn] Animation '{animation.Name}' has missing frame data; using base pose where needed." );
			}

			frames.Add( new AnimationFramePose
			{
				Positions = positions,
				Rotations = rotations
			} );
		}

		return frames;
	}

	private static bool TryResolveAnimDataOffsetForFrame(
		byte[] data,
		ParsedAnimDesc animation,
		int frameIndex,
		out int localFrame,
		out int animDataOffset )
	{
		localFrame = frameIndex;
		animDataOffset = -1;
		int block = animation.AnimBlock;
		int index = animation.AnimIndex;

		if ( animation.SectionFrames != 0 )
		{
			int section;
			if ( animation.NumFrames > animation.SectionFrames && frameIndex == animation.NumFrames - 1 )
			{
				localFrame = 0;
				section = (animation.NumFrames / animation.SectionFrames) + 1;
			}
			else
			{
				section = frameIndex / animation.SectionFrames;
				localFrame = frameIndex - (section * animation.SectionFrames);
			}

			if ( animation.SectionIndex <= 0 )
			{
				return false;
			}

			int sectionOffset = animation.StartOffset + animation.SectionIndex + (section * AnimSectionSize);
			if ( !IsRangeValid( data, sectionOffset, AnimSectionSize ) )
			{
				return false;
			}

			block = ReadInt32( data, sectionOffset );
			index = ReadInt32( data, sectionOffset + 4 );
		}

		if ( block == -1 || block != 0 )
		{
			return false;
		}

		if ( index <= 0 )
		{
			return false;
		}

		animDataOffset = animation.StartOffset + index;
		return IsRangeValid( data, animDataOffset, 4 );
	}

	private static void DecodeBoneFrame(
		byte[] data,
		BoneAnimBase boneBase,
		int flags,
		int dataOffset,
		int frame,
		out Vector3 position,
		out Vector3 rotation )
	{
		bool isDelta = (flags & StudioAnimDelta) != 0;

		if ( (flags & StudioAnimRawPos) != 0 )
		{
			int rawPositionOffset = dataOffset;
			if ( (flags & StudioAnimRawRot) != 0 )
			{
				rawPositionOffset += 6;
			}
			if ( (flags & StudioAnimRawRot2) != 0 )
			{
				rawPositionOffset += 8;
			}
			position = ReadVector48( data, rawPositionOffset );
		}
		else if ( (flags & StudioAnimAnimPos) != 0 )
		{
			int posValuePtrOffset = dataOffset + (((flags & StudioAnimAnimRot) != 0) ? 6 : 0);
			position = ReadAnimatedVector(
				data,
				posValuePtrOffset,
				frame,
				boneBase.PositionScale );

			if ( !isDelta )
			{
				position = new Vector3(
					position.x + boneBase.Position.x,
					position.y + boneBase.Position.y,
					position.z + boneBase.Position.z );
			}
		}
		else
		{
			position = isDelta ? new Vector3( 0f, 0f, 0f ) : boneBase.Position;
		}

		if ( (flags & StudioAnimRawRot) != 0 )
		{
			rotation = QuaternionToEulerRadians( ReadQuaternion48( data, dataOffset ) );
		}
		else if ( (flags & StudioAnimRawRot2) != 0 )
		{
			rotation = QuaternionToEulerRadians( ReadQuaternion64( data, dataOffset ) );
		}
		else if ( (flags & StudioAnimAnimRot) != 0 )
		{
			rotation = ReadAnimatedVector(
				data,
				dataOffset,
				frame,
				boneBase.RotationScale );

			if ( !isDelta )
			{
				rotation = new Vector3(
					rotation.x + boneBase.Rotation.x,
					rotation.y + boneBase.Rotation.y,
					rotation.z + boneBase.Rotation.z );
			}
		}
		else
		{
			rotation = isDelta ? new Vector3( 0f, 0f, 0f ) : boneBase.Rotation;
		}
	}

	private static Vector3 ReadAnimatedVector( byte[] data, int valuePtrOffset, int frame, Vector3 scale )
	{
		float x = ReadAnimatedChannel( data, valuePtrOffset, 0, frame, scale.x );
		float y = ReadAnimatedChannel( data, valuePtrOffset, 1, frame, scale.y );
		float z = ReadAnimatedChannel( data, valuePtrOffset, 2, frame, scale.z );
		return new Vector3( x, y, z );
	}

	private static float ReadAnimatedChannel( byte[] data, int valuePtrOffset, int channelIndex, int frame, float scale )
	{
		if ( !IsRangeValid( data, valuePtrOffset, 6 ) )
		{
			return 0f;
		}

		int componentOffsetOffset = valuePtrOffset + (channelIndex * 2);
		short relativeOffset = ReadInt16( data, componentOffsetOffset );
		if ( relativeOffset <= 0 )
		{
			return 0f;
		}

		int animValueOffset = valuePtrOffset + relativeOffset;
		return ExtractAnimValue( data, animValueOffset, frame, scale );
	}

	private static float ExtractAnimValue( byte[] data, int animValueOffset, int frame, float scale )
	{
		if ( !IsRangeValid( data, animValueOffset, 2 ) )
		{
			return 0f;
		}

		int ptr = animValueOffset;
		int k = frame;
		while ( true )
		{
			if ( !IsRangeValid( data, ptr, 2 ) )
			{
				return 0f;
			}

			byte valid = data[ptr];
			byte total = data[ptr + 1];
			if ( total == 0 )
			{
				return 0f;
			}

			if ( total <= k )
			{
				k -= total;
				ptr += (valid + 1) * 2;
				continue;
			}

			int sampleOffset;
			if ( valid > k )
			{
				sampleOffset = ptr + ((k + 1) * 2);
			}
			else
			{
				sampleOffset = ptr + (valid * 2);
			}

			if ( !IsRangeValid( data, sampleOffset, 2 ) )
			{
				return 0f;
			}

			short value = ReadInt16( data, sampleOffset );
			return value * scale;
		}
	}

	private static Vector3 ReadVector48( byte[] data, int offset )
	{
		float x = ReadHalfFloat( data, offset );
		float y = ReadHalfFloat( data, offset + 2 );
		float z = ReadHalfFloat( data, offset + 4 );
		return new Vector3( x, y, z );
	}

	private static Quaternion ReadQuaternion48( byte[] data, int offset )
	{
		ushort xBits = ReadUInt16( data, offset );
		ushort yBits = ReadUInt16( data, offset + 2 );
		ushort zwBits = ReadUInt16( data, offset + 4 );

		float x = ((int)xBits - 32768) * (1f / 32768f);
		float y = ((int)yBits - 32768) * (1f / 32768f);
		float z = ((int)(zwBits & 0x7FFF) - 16384) * (1f / 16384f);
		float t = 1f - (x * x) - (y * y) - (z * z);
		float w = t > 0f ? MathF.Sqrt( t ) : 0f;
		if ( (zwBits & 0x8000) != 0 )
		{
			w = -w;
		}

		return new Quaternion( x, y, z, w );
	}

	private static Quaternion ReadQuaternion64( byte[] data, int offset )
	{
		if ( !IsRangeValid( data, offset, 8 ) )
		{
			return new Quaternion( 0f, 0f, 0f, 1f );
		}

		ulong packed = BitConverter.ToUInt64( data, offset );
		uint xBits = (uint)(packed & 0x1FFFFF);
		uint yBits = (uint)((packed >> 21) & 0x1FFFFF);
		uint zBits = (uint)((packed >> 42) & 0x1FFFFF);
		bool wNeg = ((packed >> 63) & 1UL) != 0;

		float x = ((int)xBits - 1048576) * (1f / 1048576.5f);
		float y = ((int)yBits - 1048576) * (1f / 1048576.5f);
		float z = ((int)zBits - 1048576) * (1f / 1048576.5f);
		float t = 1f - (x * x) - (y * y) - (z * z);
		float w = t > 0f ? MathF.Sqrt( t ) : 0f;
		if ( wNeg )
		{
			w = -w;
		}

		return new Quaternion( x, y, z, w );
	}

	private static Vector3 QuaternionToEulerRadians( Quaternion quaternion )
	{
		NQuaternion q = new NQuaternion( quaternion.x, quaternion.y, quaternion.z, quaternion.w );
		if ( q.LengthSquared() <= 1e-8f )
		{
			return new Vector3( 0f, 0f, 0f );
		}

		q = NQuaternion.Normalize( q );

		float sinX = 2f * ((q.W * q.X) + (q.Y * q.Z));
		float cosX = 1f - (2f * ((q.X * q.X) + (q.Y * q.Y)));
		float x = MathF.Atan2( sinX, cosX );

		float sinY = 2f * ((q.W * q.Y) - (q.Z * q.X));
		float y = MathF.Abs( sinY ) >= 1f
			? MathF.CopySign( MathF.PI * 0.5f, sinY )
			: MathF.Asin( sinY );

		float sinZ = 2f * ((q.W * q.Z) + (q.X * q.Y));
		float cosZ = 1f - (2f * ((q.Y * q.Y) + (q.Z * q.Z)));
		float z = MathF.Atan2( sinZ, cosZ );

		return new Vector3( x, y, z );
	}

	private static string ReadStringAtRelativeOffset( byte[] data, int baseOffset, int relativeOffset )
	{
		if ( relativeOffset <= 0 )
		{
			return string.Empty;
		}

		int start = baseOffset + relativeOffset;
		if ( start < 0 || start >= data.Length )
		{
			return string.Empty;
		}

		int end = start;
		while ( end < data.Length && data[end] != 0 )
		{
			end++;
		}

		int length = end - start;
		return length > 0 ? Encoding.ASCII.GetString( data, start, length ) : string.Empty;
	}

	private static bool IsRangeValid( byte[] data, int offset, int length )
	{
		if ( offset < 0 || length < 0 )
		{
			return false;
		}

		long end = (long)offset + length;
		return end <= data.Length;
	}

	private static int ReadInt32( byte[] data, int offset )
	{
		if ( !IsRangeValid( data, offset, 4 ) )
		{
			return 0;
		}
		return BitConverter.ToInt32( data, offset );
	}

	private static short ReadInt16( byte[] data, int offset )
	{
		if ( !IsRangeValid( data, offset, 2 ) )
		{
			return 0;
		}
		return BitConverter.ToInt16( data, offset );
	}

	private static ushort ReadUInt16( byte[] data, int offset )
	{
		if ( !IsRangeValid( data, offset, 2 ) )
		{
			return 0;
		}
		return BitConverter.ToUInt16( data, offset );
	}

	private static float ReadSingle( byte[] data, int offset )
	{
		if ( !IsRangeValid( data, offset, 4 ) )
		{
			return 0f;
		}
		return BitConverter.ToSingle( data, offset );
	}

	private static float ReadHalfFloat( byte[] data, int offset )
	{
		ushort bits = ReadUInt16( data, offset );
		return (float)BitConverter.UInt16BitsToHalf( bits );
	}
}
