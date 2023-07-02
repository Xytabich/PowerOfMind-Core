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

		private readonly RenderBatchingSystem mod;
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
			mod = capi.ModLoader.GetModSystem<RenderBatchingSystem>();
			blockAccessor = capi.World.GetLockFreeBlockAccessor();
			worldAccessor = capi.World;
			batchingSystem = mod.ChunkBatcher;
			this.shader = shader;
			this.vertexStruct = vertexStruct;
			this.uniformStruct = uniformStruct;
		}

		public int AddString(int3 fromBlock, int3 toBlock, IStringBuilder builder)
		{
			int stringChunksChain = -1;
			int chunkSize = blockAccessor.ChunkSize;

			int sid = strings.Add(new StringInfo(stringChunksChain, builder));

			int3 prevChunk = fromBlock / chunkSize;
			StringBatchUtil.EnumerateLine(fromBlock, toBlock, pos => {
				var chunk = pos / chunkSize;
				if(!chunk.Equals(prevChunk))
				{
					AddChunk(ref stringChunksChain, sid, prevChunk);
					prevChunk = chunk;
				}
			});
			AddChunk(ref stringChunksChain, sid, prevChunk);

			return sid;

			void AddChunk(ref int stringChunksChain, int sid, int3 chunk)
			{
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

					mod.RegisterChunkDirtyListener(chunk, UpdateChunk);
				}

				cid = chunkStrings.Add(chunkBatch.stringChain, new ChunkStringSegment(chunkId, sid));
				if(chunkBatch.stringChain < 0) chunkBatch.stringChain = cid;

				stringChunksChain = stringChunks.Append(stringChunksChain, cid);
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
					mod.UnregisterChunkDirtyListener(chunks[chunkId].index, UpdateChunk);

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

		private void UpdateChunk(int3 index)
		{
			if(chunkToId.TryGetValue(index, out var id))
			{
				batchingSystem.MarkBuilderDirty(chunks[id].builderId);
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

				blockLightUtil.Init(batcher.blockAccessor, null, CommonExt.CreateLightUtil(batcher.worldAccessor));//TODO: geometry util

				foreach(var id in batcher.chunkStrings.GetNodeEnumerable(stringChain))
				{
					ref readonly var record = ref batcher.chunkStrings[id];
					batcher.strings[record.stringId].builder.BuildChunk(index, batcher.blockAccessor, blockLightUtil, context);
				}

				blockLightUtil.Clear();
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

			public ChunkStringSegment(int chunkId, int stringId)
			{
				this.chunkId = chunkId;
				this.stringId = stringId;
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

	public static class StringBatchUtil
	{
		public static void EnumerateLine(int3 p0, int3 p1, Action<int3> visitor)
		{
			var sx = p1.x > p0.x ? 1 : (p1.x < p0.x ? -1 : 0);
			var sy = p1.y > p0.y ? 1 : (p1.y < p0.y ? -1 : 0);
			var sz = p1.z > p0.z ? 1 : (p1.z < p0.z ? -1 : 0);

			//Error is normalized to vx * vy * vz so we only have to multiply up
			int3 g;
			g.x = (p1.x == p0.x ? 1 : p1.x - p0.x) * (p1.y == p0.y ? 1 : p1.y - p0.y);
			g.y = (p1.x == p0.x ? 1 : p1.x - p0.x) * (p1.z == p0.z ? 1 : p1.z - p0.z);
			g.z = (p1.y == p0.y ? 1 : p1.y - p0.y) * (p1.z == p0.z ? 1 : p1.z - p0.z);

			//Error from the next plane accumulators, scaled up by vx*vy*vz
			// gx0 + vx * rx == gxp
			// vx * rx == gxp - gx0
			// rx == (gxp - gx0) / vx
			var errx = p1.x > p0.x ? g.z : 0;
			var erry = p1.y > p0.y ? g.y : 0;
			var errz = p1.z > p0.z ? g.x : 0;

			var derrx = sx * g.z;
			var derry = sy * g.y;
			var derrz = sz * g.x;

			g.x = p0.x;
			g.y = p0.y;
			g.z = p0.z;

			while(true)
			{
				visitor(g);

				if(g.x == p1.x && g.y == p1.y && g.z == p1.z) break;

				//Which plane do we cross first?
				var xr = Math.Abs(errx);
				var yr = Math.Abs(erry);
				var zr = Math.Abs(errz);

				if(sx != 0 && (sy == 0 || xr < yr) && (sz == 0 || xr < zr))
				{
					g.x += sx;
					errx += derrx;
				}
				else if(sy != 0 && (sz == 0 || yr < zr))
				{
					g.y += sy;
					erry += derry;
				}
				else if(sz != 0)
				{
					g.z += sz;
					errz += derrz;
				}
			}
		}

		public static void RayCubeIntersection(float3 origin, float3 dir, float size, out float tmin, out float tmax)
		{
			tmin = float.NegativeInfinity;
			tmax = float.PositiveInfinity;

			float t0, t1;

			if(dir.y != 0.0)
			{
				t0 = (-origin.y) / dir.y;
				t1 = (size - origin.y) / dir.y;

				tmin = Math.Max(tmin, Math.Min(t0, t1));
				tmax = Math.Min(tmax, Math.Max(t0, t1));
			}

			if(dir.x != 0.0)
			{
				t0 = (-origin.x) / dir.x;
				t1 = (size - origin.x) / dir.x;

				tmin = Math.Max(tmin, Math.Min(t0, t1));
				tmax = Math.Min(tmax, Math.Max(t0, t1));
			}

			if(dir.z != 0.0)
			{
				t0 = (-origin.z) / dir.z;
				t1 = (size - origin.z) / dir.z;

				tmin = Math.Max(tmin, Math.Min(t0, t1));
				tmax = Math.Min(tmax, Math.Max(t0, t1));
			}
		}
	}

	public interface IStringBuilder
	{
		void BuildChunk(int3 chunkIndex, IBlockAccessor blockAccessor, BlockLightUtil lightUtil, IBatchBuildContext batcher);
	}
}