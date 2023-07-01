using PowerOfMind.Collections;
using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Shader;
using PowerOfMind.Systems.RenderBatching;
using PowerOfMind.Utils;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PowerOfMind.Systems.ChunkBatchers
{
	/// <summary>
	/// Batcher for lines, taut ropes, etc.
	/// </summary>
	public class StringBatcher<TVertex, TUniform>
		where TVertex : unmanaged, IVertexStruct
		where TUniform : unmanaged, IUniformsData
	{
		private readonly IBlockAccessor blockAccessor;
		private readonly IClientWorldAccessor worldAccessor;

		private readonly ChunkBatching batchingSystem;
		private readonly TVertex vertexStruct;
		private readonly TUniform uniformStruct;
		private readonly IExtendedShaderProgram shader;

		private readonly Dictionary<int3, int> chunkToId = new Dictionary<int3, int>();
		private readonly HeapCollection<ChunkBatchGroup> chunks = new HeapCollection<ChunkBatchGroup>();
		private readonly ChainList<ChunkStringSegment> chunkStrings = new ChainList<ChunkStringSegment>();
		private readonly ChainList<int> stringChunks = new ChainList<int>();
		private readonly HeapCollection<StringInfo> strings = new HeapCollection<StringInfo>();

		public StringBatcher(ICoreClientAPI capi, IExtendedShaderProgram shader, in TVertex vertexStruct, in TUniform uniformStruct)
		{
			var mod = capi.ModLoader.GetModSystem<RenderBatchingSystem>();
			blockAccessor = capi.World.GetLockFreeBlockAccessor();
			worldAccessor = capi.World;
			batchingSystem = mod.ChunkBatcher;
			this.shader = shader;
			this.vertexStruct = vertexStruct;
			this.uniformStruct = uniformStruct;
		}

		public int AddString(int3 fromBlock, float3 fromOffset, int3 toBlock, float3 toOffset, IStringBuilder builder)
		{
			int stringChunksChain = -1;
			int chunkSize = blockAccessor.ChunkSize;

			int sid = strings.Add(new StringInfo(stringChunksChain, builder));

			int3 prevBlock = fromBlock;
			int3 prevChunk = fromBlock / chunkSize;

			EnumerateLine(fromBlock.x, fromBlock.y, fromBlock.z, toBlock.x, toBlock.y, toBlock.z, (x, y, z) => {
				var chunk = new int3(x / chunkSize, y / chunkSize, z / chunkSize);
				if(chunk.Equals(prevChunk))
				{
					prevBlock = new int3(x, y, z);
				}
				else
				{
					var point = AddChunk(fromBlock, fromOffset, toBlock, toOffset, prevBlock, ref stringChunksChain, chunkSize, sid, prevChunk);

					fromOffset = point - (chunk - prevChunk) * chunkSize;

					prevBlock = fromBlock = new int3(x, y, z);
					prevChunk = chunk;
				}
			});
			AddChunk(fromBlock, fromOffset, toBlock, toOffset, prevBlock, ref stringChunksChain, chunkSize, sid, prevChunk);

			return sid;

			float3 AddChunk(int3 fromBlock, float3 fromOffset, int3 toBlock, float3 toOffset, int3 prevBlock, ref int stringChunksChain, int chunkSize, int sid, int3 chunk)
			{
				var rayOrigin = (fromBlock - chunk * chunkSize) + fromOffset;
				var point = FindRayBoxIntersection(rayOrigin, ((toBlock - chunk * chunkSize) + toOffset) - rayOrigin, chunkSize);

				int cid;
				ChunkBatchGroup chunkBatch;
				if(chunkToId.TryGetValue(chunk, out var chunkId))
				{
					chunkBatch = chunks[chunkId];
					batchingSystem.MarkBuilderDirty(chunkBatch.builderId);
				}
				else
				{
					chunkBatch = new ChunkBatchGroup(chunk, this);
					chunkBatch.builderId = batchingSystem.AddBuilder(chunk, shader, chunkBatch);
					chunkId = chunks.Add(chunkBatch);
					chunkToId[chunk] = chunkId;
				}

				cid = chunkStrings.Add(chunkBatch.stringChain, new ChunkStringSegment(chunkId, sid, rayOrigin, point, fromBlock, prevBlock));
				if(chunkBatch.stringChain < 0) chunkBatch.stringChain = cid;

				stringChunksChain = stringChunks.Append(stringChunksChain, cid);
				return point;
			}
		}

		public void RemoveString(int id)
		{
			foreach(var cid in stringChunks.RemoveEnumerated(strings[id].chainId))
			{
				int chunkId = chunkStrings[cid].chunkId;
				chunks[chunkId].stringChain = chunkStrings.Remove(chunks[chunkId].stringChain, cid);
				if(chunks[chunkId].stringChain < 0)
				{
					batchingSystem.RemoveBuilder(chunks[chunkId].builderId);
					chunkToId.Remove(chunks[chunkId].index);
					chunks.Remove(chunkId);
				}
				else
				{
					batchingSystem.MarkBuilderDirty(chunks[chunkId].builderId);
				}
			}
			strings.Remove(id);
		}

		private static float3 FindRayBoxIntersection(float3 origin, float3 direction, float boxSize)
		{
			float nearestPoint = float.MaxValue;
			for(int i = 0; i < 6; i++)
			{
				var faceNormal = BlockFacing.ALLFACES[i].Normalf;
				float denom = math.dot(direction, new float3(faceNormal.X, faceNormal.Y, faceNormal.Z));
				if(denom > 0f)
				{
					nearestPoint = Math.Min(nearestPoint, -(math.dot(origin, new float3(faceNormal.X, faceNormal.Y, faceNormal.Z)) + boxSize) / denom);
				}
			}
			return origin + direction * nearestPoint;
		}

		private static void EnumerateLine(int gx0, int gy0, int gz0, int gx1, int gy1, int gz1, Action<int, int, int> visitor)
		{
			var sx = gx1 > gx0 ? 1 : (gx1 < gx0 ? -1 : 0);
			var sy = gy1 > gy0 ? 1 : (gy1 < gy0 ? -1 : 0);
			var sz = gz1 > gz0 ? 1 : (gz1 < gz0 ? -1 : 0);

			//Error is normalized to vx * vy * vz so we only have to multiply up
			var gx = (gx1 == gx0 ? 1 : gx1 - gx0) * (gy1 == gy0 ? 1 : gy1 - gy0);
			var gy = (gx1 == gx0 ? 1 : gx1 - gx0) * (gz1 == gz0 ? 1 : gz1 - gz0);
			var gz = (gy1 == gy0 ? 1 : gy1 - gy0) * (gz1 == gz0 ? 1 : gz1 - gz0);

			//Error from the next plane accumulators, scaled up by vx*vy*vz
			// gx0 + vx * rx == gxp
			// vx * rx == gxp - gx0
			// rx == (gxp - gx0) / vx
			var errx = gx1 > gx0 ? gz : 0;
			var erry = gy1 > gy0 ? gy : 0;
			var errz = gz1 > gz0 ? gx : 0;

			var derrx = sx * gz;
			var derry = sy * gy;
			var derrz = sz * gx;

			gx = gx0;
			gy = gy0;
			gz = gz0;

			while(true)
			{
				visitor(gx, gy, gz);

				if(gx == gx1 && gy == gy1 && gz == gz1) break;

				//Which plane do we cross first?
				var xr = Math.Abs(errx);
				var yr = Math.Abs(erry);
				var zr = Math.Abs(errz);

				if(sx != 0 && (sy == 0 || xr < yr) && (sz == 0 || xr < zr))
				{
					gx += sx;
					errx += derrx;
				}
				else if(sy != 0 && (sz == 0 || yr < zr))
				{
					gy += sy;
					erry += derry;
				}
				else if(sz != 0)
				{
					gz += sz;
					errz += derrz;
				}
			}
		}

		private class ChunkBatchGroup : IBatchDataBuilder<TVertex, TUniform>
		{
			[ThreadStatic]
			private static BlockLightUtil blockLightUtil = null;

			public int stringChain = -1;
			public int builderId;
			public readonly int3 index;

			private readonly StringBatcher<TVertex, TUniform> batcher;

			public ChunkBatchGroup(int3 index, StringBatcher<TVertex, TUniform> batcher)
			{
				this.index = index;
				this.batcher = batcher;
			}

			void IBatchDataBuilder<TVertex, TUniform>.Build(IBatchBuildContext context)
			{
				if(blockLightUtil == null) blockLightUtil = new BlockLightUtil();

				blockLightUtil.Init(batcher.blockAccessor, null, CommonExt.CreateLightUtil(batcher.worldAccessor));

				int chunkSize = batcher.blockAccessor.ChunkSize;
				foreach(var id in batcher.chunkStrings.GetNodeEnumerable(stringChain))
				{
					ref readonly var record = ref batcher.chunkStrings[id];
					var builder = batcher.strings[record.stringId].builder;
					var rayOrigin = record.fromOffset;
					var rayDir = record.toOffset - rayOrigin;
					EnumerateLine(record.fromBlock.x, record.fromBlock.y, record.fromBlock.z, record.toBlock.x, record.toBlock.y, record.toBlock.z, (x, y, z) => {
						var localOffset = new int3(x, y, z) - index * chunkSize;
						var p0 = FindRayBoxIntersection(rayOrigin - localOffset, rayDir, 1f);
						var p1 = FindRayBoxIntersection(rayOrigin + rayDir - localOffset, -rayDir, 1f);
						ref var unmanaged = ref blockLightUtil.unmanaged;
						unmanaged.SetUpLightRGBs(blockLightUtil, new int3(x, y, z));
						builder.Build(new int3(x, y, z), localOffset, p0, p1, ref unmanaged, context);
					});
				}
			}

			void IBatchDataBuilder<TVertex, TUniform>.GetDefaultData(out TVertex vertexDefault, out TUniform uniformsDefault)
			{
				vertexDefault = batcher.vertexStruct;
				uniformsDefault = batcher.uniformStruct;
			}
		}

		private readonly struct ChunkStringSegment
		{
			public readonly int chunkId;
			public readonly int stringId;
			// Relative to chunk origin
			public readonly float3 fromOffset;
			public readonly float3 toOffset;
			public readonly int3 fromBlock;
			public readonly int3 toBlock;

			public ChunkStringSegment(int chunkId, int stringId, float3 fromOffset, float3 toOffset, int3 fromBlock, int3 toBlock)
			{
				this.chunkId = chunkId;
				this.stringId = stringId;
				this.fromOffset = fromOffset;
				this.toOffset = toOffset;
				this.fromBlock = fromBlock;
				this.toBlock = toBlock;
			}
		}

		private readonly struct StringInfo
		{
			public readonly int chainId;
			public readonly IStringBuilder builder;

			public StringInfo(int chainId, IStringBuilder builder)
			{
				this.chainId = chainId;
				this.builder = builder;
			}
		}
	}

	public interface IStringBuilder
	{
		void Build(int3 blockPos, int3 chunkLocalPos, float3 fromOffset, float3 toOffset, ref BlockLightUtil.Unmanaged blockLight, IBatchBuildContext context);
	}
}