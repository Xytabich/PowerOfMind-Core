using PowerOfMind.Collections;
using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Drawable;
using PowerOfMind.Graphics.Shader;
using PowerOfMind.Systems.RenderBatching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Vintagestory.API.Client;

namespace PowerOfMind.Systems.ChunkBatchers
{
	public class WorldBatcher<TVertex, TUniform> : WorldBatcher
		where TVertex : unmanaged, IVertexStruct
		where TUniform : unmanaged, IUniformsData
	{
		[ThreadStatic]
		private static ChunkBatchBuildContext batchBuildContext = null;

		private readonly RenderBatchingSystem mod;

		private readonly Dictionary<int3, int> chunkToId = new Dictionary<int3, int>();
		private readonly HeapCollection<ChunkBuilder> chunks = new HeapCollection<ChunkBuilder>();

		private readonly HeapCollection<IBlockDataProvider> providers = new HeapCollection<IBlockDataProvider>();

		private readonly ChainList bitChains = new ChainList();
		private readonly List<uint[]> bitBlocks = new List<uint[]>();

		private readonly ChunkBatching batchingSystem;
		private readonly TVertex vertexStruct;
		private readonly TUniform uniformStruct;
		private readonly IExtendedShaderProgram shader;

		private readonly int chunkSize;
		private readonly int bitBlockSize;

		public WorldBatcher(ICoreClientAPI capi, IExtendedShaderProgram shader, in TVertex vertexStruct, in TUniform uniformStruct)
		{
			mod = capi.ModLoader.GetModSystem<RenderBatchingSystem>();
			batchingSystem = mod.ChunkBatcher;
			this.shader = shader;
			this.vertexStruct = vertexStruct;
			this.uniformStruct = uniformStruct;

			chunkSize = capi.World.BlockAccessor.ChunkSize;
			bitBlockSize = chunkSize * chunkSize * chunkSize >> 5;
		}

		public override int AddProvider(IBlockDataProvider provider)
		{
			return providers.Add(provider);
		}

		public override void RemoveProvider(int id)
		{
			providers.Remove(id);
		}

		public override void AddBlock(int3 pos, int providerId)
		{
			var offset = pos / chunkSize;
			if(!chunkToId.TryGetValue(offset, out var id))
			{
				var builder = new ChunkBuilder(this, offset * chunkSize, AddBitBlock(-1), 1);
				builder.batchingId = batchingSystem.AddBuilder(offset, shader, builder);
				id = chunks.Add(builder);
				chunkToId[offset] = id;

				mod.RegisterChunkDirtyListener(offset, UpdateChunk);
			}
			pos %= chunkSize;
			chunks[id].AddBlock(GetBlockIndex(ref pos), providerId);

			batchingSystem.MarkBuilderDirty(chunks[id].batchingId);
		}

		public override void RemoveBlock(int3 pos)
		{
			var offset = pos / chunkSize;
			if(chunkToId.TryGetValue(offset, out var id))
			{
				pos %= chunkSize;
				if(chunks[id].RemoveBlock(GetBlockIndex(ref pos)))
				{
					chunkToId.Remove(offset);
					batchingSystem.RemoveBuilder(chunks[id].batchingId);
					chunks[id].Dispose();
					chunks.Remove(id);

					mod.UnregisterChunkDirtyListener(offset, UpdateChunk);
				}
				else
				{
					batchingSystem.MarkBuilderDirty(chunks[id].batchingId);
				}
			}
		}

		//TODO: add bulk, remove bulk (int3 from, int3 to, ulong[] fillMap, int providerId)

		private void UpdateChunk(int3 chunkCoord)
		{
			if(chunkToId.TryGetValue(chunkCoord, out var id))
			{
				batchingSystem.MarkBuilderDirty(chunks[id].batchingId);
			}
		}

		private int AddBitBlock(int bitsChain)
		{
			int id = bitChains.Add(bitsChain);
			if(id >= bitBlocks.Count)
			{
				bitBlocks.Insert(id, new uint[bitBlockSize]);
			}
			return id;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int GetBlockIndex(ref int3 pos)
		{
			return pos.x + (pos.y + pos.z * chunkSize) * chunkSize;
		}

		private class ChunkBuilder : IBatchDataBuilder<TVertex, TUniform>
		{
			private const int CULL_ALL = 63;

			[ThreadStatic]
			private static uint[][] bitBlocks = null;

			public int batchingId;

			private readonly WorldBatcher<TVertex, TUniform> batcher;
			private readonly int3 origin;
			private readonly int bitsChain;
			private int bitsDepth;
			private int numCount;
			private int providersCount;
			private ChunkProviderInfo[] providers;

			private bool isDisposed = false;

			public ChunkBuilder(WorldBatcher<TVertex, TUniform> batcher, int3 origin, int bitsChain, int initialDepth)
			{
				this.batcher = batcher;
				this.origin = origin;
				this.bitsChain = bitsChain;
				bitsDepth = initialDepth;
				numCount = 1 << initialDepth;
				providers = new ChunkProviderInfo[numCount - 1];
				providersCount = 0;
			}

			public void AddBlock(int bitIndex, int providerId)
			{
				int providerIndex = -1;
				for(int i = 0; i < providersCount; i++)
				{
					if(providers[i].usageCounter <= 0)
					{
						providers[i] = new ChunkProviderInfo(providerId);
						providerIndex = i;
						break;
					}
					if(providers[i].providerId == providerId)
					{
						providerIndex = i;
						break;
					}
				}
				if(providerIndex < 0)
				{
					providerIndex = providersCount;
					providersCount++;
					if(providersCount >= numCount)
					{
						bitsDepth++;
						numCount = 1 << bitsDepth;
						Array.Resize(ref providers, numCount - 1);
						batcher.AddBitBlock(bitsChain);
					}
				}
				providers[providerIndex].usageCounter++;

				providerIndex++;
				int intIndex = bitIndex >> 5;
				bitIndex &= 31;
				foreach(var id in batcher.bitChains.GetEnumerable(bitsChain))
				{
					var block = batcher.bitBlocks[id];
					block[intIndex] = block[intIndex] & ~(1u << bitIndex) | ((uint)providerIndex & 1) << bitIndex;
					providerIndex >>= 1;
				}
			}

			public bool RemoveBlock(int bitIndex)
			{
				int intIndex = bitIndex >> 5;
				bitIndex &= 31;
				uint providerIndex = 0;
				foreach(var id in batcher.bitChains.GetEnumerable(bitsChain))
				{
					var block = batcher.bitBlocks[id];
					providerIndex <<= 1;
					providerIndex |= block[intIndex] >> bitIndex & 1;
					block[intIndex] &= ~(1u << bitIndex);
				}
				if(providerIndex == 0) return false;

				providerIndex--;
				if(--providers[(int)providerIndex].usageCounter == 0)
				{
					if(providerIndex == (uint)providersCount - 1)
					{
						providersCount--;
						while(providersCount > 0)
						{
							if(providers[providersCount - 1].usageCounter > 0)
							{
								break;
							}
							providersCount--;
						}
						return providersCount == 0;
					}
				}
				return false;
			}

			public void Dispose()
			{
				isDisposed = true;
				foreach(var id in batcher.bitChains.RemoveEnumerated(bitsChain))
				{
					Array.Clear(batcher.bitBlocks[id], 0, batcher.bitBlockSize);
				}
			}

			void IBatchDataBuilder<TVertex, TUniform>.Build(IBatchBuildContext context)
			{
				if(isDisposed) return;
				var providers = this.providers;
				if(providers == null) return;

				if(bitBlocks == null) bitBlocks = new uint[32][];
				int bitCount = bitsDepth;
				int bit = 0;
				foreach(var id in batcher.bitChains.GetEnumerable(bitsChain))
				{
					if(id < batcher.bitBlocks.Count && bit < bitCount)
					{
						var bitBlock = batcher.bitBlocks[id];
						if(bitBlock == null) break;

						bitBlocks[bit] = bitBlock;
					}
					else break;
					bit++;
				}
				if(bit == 0) return;
				bitCount = bit;

				BitHelper helper = default;
				var size = batcher.chunkSize;
				int3 pos;
				int prevIndex = -1;
				if(batchBuildContext == null) batchBuildContext = new ChunkBatchBuildContext();
				batchBuildContext.Init(context);
				for(pos.z = 0; pos.z < size; pos.z++)
				{
					for(pos.y = 0; pos.y < size; pos.y++)
					{
						for(pos.x = 0; pos.x < size; pos.x++)
						{
							int index = batcher.GetBlockIndex(ref pos);
							bit = index & 31;
							index >>= 5;
							if(index != prevIndex)
							{
								helper.FillFrom(bitBlocks, index, bitCount);
								prevIndex = index;
							}
							index = (int)helper.GetValue(bit, bitCount) - 1;
							if(index < 0 || index >= providers.Length) continue;

							var provider = batcher.providers[providers[index].providerId];
							if(provider != null)
							{
								int cullSides = provider.GetCullSides(pos + origin);
								if((cullSides & CULL_ALL) == CULL_ALL) continue;
								batchBuildContext.cullSides = cullSides;
								batchBuildContext.blockOffset = pos;
								provider.ProvideData(pos + origin, batchBuildContext);
							}
						}
					}
				}
				batchBuildContext?.Clear();
			}

			void IBatchDataBuilder<TVertex, TUniform>.GetDefaultData(out TVertex vertexDefault, out TUniform uniformsDefault)
			{
				vertexDefault = batcher.vertexStruct;
				uniformsDefault = batcher.uniformStruct;
			}
		}

		private class ChunkBatchBuildContext : IBatchBuildContext
		{
			private const int CULL_MASK = 63;

			public int cullSides;
			public float3 blockOffset;

			private IBatchBuildContext chunkContext;

			private readonly DrawableUnitCubeCuller buildData = new DrawableUnitCubeCuller();
			private readonly VertexPositionOffsetUtil offsetShell = new VertexPositionOffsetUtil();

			public void Init(IBatchBuildContext chunkContext)
			{
				this.chunkContext = chunkContext;
			}

			public void Clear()
			{
				chunkContext = null;
			}

			void IBatchBuildContext.AddData<T>(IDrawableData data, in T uniformsData, EnumChunkRenderPass renderPass)
			{
				if(data.DrawMode != EnumDrawMode.Triangles) return;
				if((cullSides & CULL_MASK) == 0)
				{
					offsetShell.Init(data, blockOffset);
					chunkContext.AddData(offsetShell, uniformsData, renderPass);
					offsetShell.Clear();
					return;
				}

				bool cullBackfaces = true;
				switch(renderPass)
				{
					case EnumChunkRenderPass.OpaqueNoCull:
					case EnumChunkRenderPass.BlendNoCull:
					case EnumChunkRenderPass.Liquid:
						cullBackfaces = false;
						break;
				}

				if(buildData.BuildData(data, cullSides, cullBackfaces))
				{
					offsetShell.Init(buildData, blockOffset);
					chunkContext.AddData(offsetShell, uniformsData, renderPass);
					offsetShell.Clear();
				}
				buildData.Clear();
			}

			void IBatchBuildContext.AddData(IDrawableData data, EnumChunkRenderPass renderPass)
			{
				if(data.DrawMode != EnumDrawMode.Triangles) return;
				if((cullSides & CULL_MASK) == 0)
				{
					offsetShell.Init(data, blockOffset);
					chunkContext.AddData(offsetShell, renderPass);
					offsetShell.Clear();
					return;
				}

				bool cullBackfaces = true;
				switch(renderPass)
				{
					case EnumChunkRenderPass.OpaqueNoCull:
					case EnumChunkRenderPass.BlendNoCull:
					case EnumChunkRenderPass.Liquid:
						cullBackfaces = false;
						break;
				}

				if(buildData.BuildData(data, cullSides, cullBackfaces))
				{
					offsetShell.Init(buildData, blockOffset);
					chunkContext.AddData(offsetShell, renderPass);
					offsetShell.Clear();
				}
				buildData.Clear();
			}
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		private struct ChunkProviderInfo
		{
			public readonly int providerId;
			public int usageCounter;

			public ChunkProviderInfo(int providerId)
			{
				this.providerId = providerId;
				usageCounter = 0;
			}
		}
	}

	public abstract class WorldBatcher
	{
		public abstract int AddProvider(IBlockDataProvider provider);
		public abstract void RemoveProvider(int id);
		public abstract void AddBlock(int3 pos, int providerId);
		public abstract void RemoveBlock(int3 pos);
	}

	[DebuggerTypeProxy(type: typeof(BitHelperDebugView))]
	internal unsafe struct BitHelper
	{
		public fixed uint bitBlocks[32];

		public void FillFrom(uint[][] bitBlocks, int index, int bitCount)
		{
			int i = 0;
			while(i < bitCount)
			{
				this.bitBlocks[i] = bitBlocks[i][index];
				i++;
			}
		}

		public uint GetValue(int bitOffset, int bitCount)
		{
			uint value = 0;
			int i = bitCount - 1;
			while(i >= 0)
			{
				value <<= 1;
				value |= bitBlocks[i] >> bitOffset & 1;
				i--;
			}
			return value;
		}

		internal class BitHelperDebugView
		{
			private readonly BitHelper bitHelper;

			public BitHelperDebugView(BitHelper bitHelper)
			{
				this.bitHelper = bitHelper;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public uint[] Values
			{
				get
				{
					var values = new uint[32];
					for(int i = 0; i < 32; i++)
					{
						values[i] = bitHelper.bitBlocks[i];
					}
					return values;
				}
			}
		}
	}
}