using OpenTK;
using PowerOfMind.Collections;
using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Drawable;
using PowerOfMind.Graphics.Shader;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Vintagestory.API.Client;

namespace PowerOfMind.Systems.RenderBatching
{
	public class WorldBatcher<TVertex, TUniform>
		where TVertex : unmanaged, IVertexStruct
		where TUniform : unmanaged, IUniformsData
	{
		[ThreadStatic]
		private static ChunkBatchBuildContext batchBuildContext = null;

		private readonly Dictionary<int3, int> chunkToId = new Dictionary<int3, int>();
		private readonly HeapCollection<ChunkBuilder> chunks = new HeapCollection<ChunkBuilder>();

		private readonly HeapCollection<IBlockDataProvider> providers = new HeapCollection<IBlockDataProvider>();

		private readonly ChainList bitChains = new ChainList();
		private readonly List<uint[]> bitBlocks = new List<uint[]>();

		private readonly TVertex vertexStruct;
		private readonly TUniform uniformStruct;
		private readonly IExtendedShaderProgram shader;

		private readonly int3 chunkSize;
		private readonly int bitBlockSize;

		private bool isInited = false;

		public int AddProvider(IBlockDataProvider provider)
		{
			return providers.Add(provider);
		}

		public void RemoveProvider(int id)
		{
			providers.Remove(id);
		}

		public void AddBlock(int3 pos, int providerId)
		{
			var offset = pos / chunkSize;
			if(!chunkToId.TryGetValue(offset, out var id))
			{
				id = chunks.Add(new ChunkBuilder(this, offset * chunkSize, AddBitBlock(-1), 1));
				chunkToId[offset] = id;
			}
			pos %= chunkSize;
			chunks[id].AddBlock(GetBlockIndex(ref pos), providerId);
		}

		public void RemoveBlock(int3 pos)
		{
			var offset = pos / chunkSize;
			if(chunkToId.TryGetValue(offset, out var id))
			{
				pos %= chunkSize;
				if(chunks[id].RemoveBlock(GetBlockIndex(ref pos)))
				{
					chunkToId.Remove(offset);
					chunks[id].Dispose();
					chunks.Remove(id);
				}
			}
		}

		//TODO: add bulk, remove bulk (int3 from, int3 to, ulong[] fillMap, int providerId)

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
			return pos.x + (pos.y + pos.z * chunkSize.y) * chunkSize.x;
		}

		private class ChunkBuilder : IBatchDataBuilder
		{
			private const int CULL_ALL = 63;

			[ThreadStatic]
			private static uint[][] bitBlocks = null;

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

				int intIndex = bitIndex >> 5;
				bitIndex &= 31;
				foreach(var id in batcher.bitChains.GetEnumerable(bitsChain))
				{
					var block = batcher.bitBlocks[id];
					block[intIndex] = (block[intIndex] & ~(1u << bitIndex)) | (((uint)providerIndex & 1) << bitIndex);
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
					providerIndex |= (block[intIndex] >> bitIndex) & 1;
					block[intIndex] &= ~(1u << bitIndex);
					providerIndex <<= 1;
				}
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

			void IBatchDataBuilder.Build(IBatchBuildContext context)
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
				batchBuildContext.Init(batcher, origin, context);
				for(pos.z = 0; pos.z < size.z; pos.z++)
				{
					for(pos.y = 0; pos.y < size.y; pos.y++)
					{
						for(pos.x = 0; pos.x < size.x; pos.x++)
						{
							int index = batcher.GetBlockIndex(ref pos);
							bit = index & 31;
							index >>= 5;
							if(index != prevIndex) helper.FillFrom(bitBlocks, index >> 5, bitCount);
							index = (int)helper.GetValue(bit, bitCount);
							if(index <= 0 || index > providers.Length) continue;

							var provider = batcher.providers[providers[index].providerId];
							if(provider != null)
							{
								int cullSides = provider.GetCullSides(pos + origin);
								if((cullSides & CULL_ALL) == CULL_ALL) continue;
								batchBuildContext.cullSides = cullSides;
								provider.ProvideData(pos, batchBuildContext);
							}
						}
					}
				}
				batchBuildContext?.Clear();
			}
		}

		private class ChunkBatchBuildContext : IBatchBuildContext
		{
			private const int CULL_MASK = 63;

			public int cullSides;

			private int3 chunkOrigin;
			private IBatchBuildContext chunkContext;
			private WorldBatcher<TVertex, TUniform> batcher;

			private readonly DrawableDataBuilder buildData = new DrawableDataBuilder();

			public void Init(WorldBatcher<TVertex, TUniform> batcher, int3 chunkOrigin, IBatchBuildContext chunkContext)
			{
				this.batcher = batcher;
				this.chunkOrigin = chunkOrigin;
				this.chunkContext = chunkContext;
			}

			public void Clear()
			{
				batcher = null;
				chunkContext = null;
			}

			void IBatchBuildContext.AddData<T>(IDrawableData data, in T uniformsData, EnumChunkRenderPass renderPass)
			{
				if(data.DrawMode != EnumDrawMode.Triangles) return;
				if((cullSides & CULL_MASK) == 0)
				{
					chunkContext.AddData(data, uniformsData, renderPass);
					return;
				}

				if(buildData.BuildIndices(data, cullSides))
				{
					chunkContext.AddData(buildData, uniformsData, renderPass);
				}
				buildData.Clear();
			}

			void IBatchBuildContext.AddData(IDrawableData data, EnumChunkRenderPass renderPass)
			{
				if(data.DrawMode != EnumDrawMode.Triangles) return;
				if((cullSides & CULL_MASK) == 0)
				{
					chunkContext.AddData(data, renderPass);
					return;
				}

				if(buildData.BuildIndices(data, cullSides))
				{
					chunkContext.AddData(buildData, renderPass);
				}
				buildData.Clear();
			}

			private class DrawableDataBuilder : IDrawableData, VerticesContext.IProcessor
			{
				EnumDrawMode IDrawableData.DrawMode => EnumDrawMode.Triangles;
				uint IDrawableData.IndicesCount => indicesCount;
				uint IDrawableData.VerticesCount => verticesCount;
				int IDrawableData.VertexBuffersCount => original.VertexBuffersCount;

				private readonly IndicesContext.ProcessorDelegate indicesProcessor;

				private IDrawableData original;
				private uint indicesCount;
				private uint verticesCount;
				private uint[] indices = null;

				private bool hasIndices = false;
				private bool vertsProcessed = false;

				public unsafe DrawableDataBuilder()
				{
					indicesProcessor = IndicesProcessor;
				}

				public bool BuildIndices(IDrawableData original, int cullSides)
				{
					this.original = original;
					indicesCount = original.IndicesCount;
					verticesCount = original.VerticesCount;
					if(indices == null) indices = new uint[Math.Max(256, (int)original.IndicesCount)];

					hasIndices = false;
					original.ProvideIndices(new IndicesContext(indicesProcessor, false));
					if(hasIndices)
					{
						vertsProcessed = false;
						original.ProvideVertices(new VerticesContext(this, false));
						return vertsProcessed;
					}
					return false;
				}

				public void Clear()
				{
					original = null;
				}

				unsafe void IDrawableData.ProvideIndices(IndicesContext context)
				{
					fixed(uint* ptr = indices)
					{
						context.Process(ptr, false);
					}
				}

				void IDrawableData.ProvideVertices(VerticesContext context)
				{
					original.ProvideVertices(context);
				}

				unsafe void VerticesContext.IProcessor.Process<T>(int bufferIndex, T* data, VertexDeclaration declaration, int stride, bool isDynamic)
				{
					if(isDynamic) return;

					for(int i = 0; i < declaration.Attributes.Length; i++)
					{
						ref readonly var attr = ref declaration.Attributes[i];
						if(attr.Alias == VertexAttributeAlias.POSITION && attr.Type == EnumShaderPrimitiveType.Float && attr.Size >= 3)
						{
							CullIndices((byte*)data + attr.Offset, stride);
							vertsProcessed = true;
							break;
						}
					}
				}

				private unsafe void IndicesProcessor(uint* indices, bool isDynamic)
				{
					if(isDynamic) return;
					hasIndices = true;

					fixed(uint* ptr = this.indices)
					{
						Buffer.MemoryCopy(indices, ptr, indicesCount * 4, indicesCount * 4);
					}
				}

				private unsafe void CullIndices(byte* vertices, int stride)
				{
					fixed(uint* ptr = indices)
					{
						for(uint i = 0; i < verticesCount; i += 3)
						{
							//TODO: cull tris
						}
					}
				}
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
				this.usageCounter = 0;
			}
		}

		private unsafe struct BitHelper
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
					value |= (bitBlocks[i] >> bitOffset) & 1;
					i--;
				}
				return value;
			}
		}
	}

	public interface IBlockDataProvider
	{
		/// <summary>
		/// Builds the drawable data for the batcher.
		/// Sometimes can be called for invalid coordinates, as it is processed in a different thread.
		/// </summary>
		void ProvideData(int3 pos, IBatchBuildContext context);

		/// <summary>
		/// Returns mask of block sides that should be culled.
		/// Sometimes can be called for invalid coordinates, as it is processed in a different thread.
		/// </summary>
		int GetCullSides(int3 pos);
	}
}