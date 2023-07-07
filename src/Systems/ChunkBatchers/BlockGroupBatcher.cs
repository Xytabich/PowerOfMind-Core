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
	/// Batcher for groups of blocks to be split into chunks
	/// </summary>
	public class BlockGroupBatcher<TVertex, TUniform>
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
		private readonly ChainList<ChunkGroupSegment> chunkGroups = new ChainList<ChunkGroupSegment>();
		private readonly ChainList<int> groupChunks = new ChainList<int>();
		private readonly HeapCollection<GroupInfo> groups = new HeapCollection<GroupInfo>();

		public BlockGroupBatcher(ICoreClientAPI capi, IExtendedShaderProgram shader, in TVertex vertexStruct, in TUniform uniformStruct)
		{
			mod = capi.ModLoader.GetModSystem<RenderBatchingSystem>();
			blockAccessor = capi.World.GetLockFreeBlockAccessor();
			worldAccessor = capi.World;
			batchingSystem = mod.ChunkBatcher;
			this.shader = shader;
			this.vertexStruct = vertexStruct;
			this.uniformStruct = uniformStruct;
		}

		public int AddGroup<TEnumerator>(ref TEnumerator blockEnumerator, IBlockGroupBuilder builder) where TEnumerator : IEnumerator<int3>
		{
			int groupChunksChain = -1;
			int chunkSize = blockAccessor.ChunkSize;

			int gid = groups.Add(new GroupInfo(groupChunksChain, builder));

			var addedChunks = new HashSet<int3>();
			while(blockEnumerator.MoveNext())
			{
				var chunk = blockEnumerator.Current / chunkSize;
				if(addedChunks.Add(chunk))
				{
					AddChunk(ref groupChunksChain, gid, chunk);
				}
			}

			return gid;

			void AddChunk(ref int groupChunksChain, int sid, int3 chunk)
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

				cid = chunkGroups.Add(chunkBatch.groupChain, new ChunkGroupSegment(chunkId, sid));
				if(chunkBatch.groupChain < 0) chunkBatch.groupChain = cid;

				groupChunksChain = groupChunks.Append(groupChunksChain, cid);
			}
		}

		public void RemoveGroup(int id)
		{
			foreach(var cid in groupChunks.RemoveEnumerated(groups[id].chainId))
			{
				int chunkId = chunkGroups[cid].chunkId;
				chunks[chunkId].groupChain = chunkGroups.Remove(chunks[chunkId].groupChain, cid);
				if(chunks[chunkId].groupChain < 0)
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
			groups.Remove(id);
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

			public int groupChain = -1;
			public int builderId;
			public readonly int3 index;

			private readonly BlockGroupBatcher<TVertex, TUniform> batcher;

			public ChunkBatchGroup(int3 index, BlockGroupBatcher<TVertex, TUniform> batcher)
			{
				this.index = index;
				this.batcher = batcher;
			}

			void IBatchDataBuilder<TVertex, TUniform>.Build(IBatchBuildContext context)
			{
				if(blockLightUtil == null) blockLightUtil = new BlockLightUtil();

				blockLightUtil.Init(batcher.blockAccessor, null, CommonExt.CreateLightUtil(batcher.worldAccessor));//TODO: geometry util

				foreach(var id in batcher.chunkGroups.GetNodeEnumerable(groupChain))
				{
					ref readonly var record = ref batcher.chunkGroups[id];
					batcher.groups[record.groupId].builder.BuildChunk(index, batcher.blockAccessor, blockLightUtil, context);
				}

				blockLightUtil.Clear();
			}

			void IBatchDataBuilder<TVertex, TUniform>.GetDefaultData(out TVertex vertexDefault, out TUniform uniformsDefault)
			{
				vertexDefault = batcher.vertexStruct;
				uniformsDefault = batcher.uniformStruct;
			}
		}

		private readonly struct ChunkGroupSegment
		{
			public readonly int chunkId;
			public readonly int groupId;

			public ChunkGroupSegment(int chunkId, int groupId)
			{
				this.chunkId = chunkId;
				this.groupId = groupId;
			}
		}

		private readonly struct GroupInfo
		{
			public readonly int chainId;
			public readonly IBlockGroupBuilder builder;

			public GroupInfo(int chainId, IBlockGroupBuilder builder)
			{
				this.chainId = chainId;
				this.builder = builder;
			}
		}
	}

	public interface IBlockGroupBuilder
	{
		void BuildChunk(int3 chunkIndex, IBlockAccessor blockAccessor, BlockLightUtil lightUtil, IBatchBuildContext batcher);
	}
}