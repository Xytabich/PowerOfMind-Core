using PowerOfMind.Collections;
using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Drawable;
using PowerOfMind.Graphics.Shader;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

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

		private readonly ChunkBatching batchingSystem;
		private readonly TVertex vertexStruct;
		private readonly TUniform uniformStruct;
		private readonly IExtendedShaderProgram shader;

		private readonly int chunkSize;
		private readonly int bitBlockSize;

		public WorldBatcher(ICoreClientAPI capi, IExtendedShaderProgram shader, in TVertex vertexStruct, in TUniform uniformStruct)
		{
			this.batchingSystem = capi.ModLoader.GetModSystem<RenderBatchingSystem>().ChunkBatcher;
			this.shader = shader;
			this.vertexStruct = vertexStruct;
			this.uniformStruct = uniformStruct;

			chunkSize = capi.World.BlockAccessor.ChunkSize;
			bitBlockSize = (chunkSize * chunkSize * chunkSize) >> 5;
		}

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
				var builder = new ChunkBuilder(this, offset * chunkSize, AddBitBlock(-1), 1);
				builder.batchingId = batchingSystem.AddBuilder(offset, shader, builder, vertexStruct, uniformStruct);
				id = chunks.Add(builder);
				chunkToId[offset] = id;
			}
			pos %= chunkSize;
			chunks[id].AddBlock(GetBlockIndex(ref pos), providerId);

			batchingSystem.MarkBuilderDirty(chunks[id].batchingId);
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
					batchingSystem.RemoveBuilder(chunks[id].batchingId);
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
			return pos.x + (pos.y + pos.z * chunkSize) * chunkSize;
		}

		private class ChunkBuilder : IBatchDataBuilder
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
					providerIndex <<= 1;
					providerIndex |= (block[intIndex] >> bitIndex) & 1;
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
			public float3 blockOffset;

			private IBatchBuildContext chunkContext;

			private readonly DrawableDataBuilder buildData = new DrawableDataBuilder();
			private readonly VertsOffsetShell offsetShell = new VertsOffsetShell();

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

				if(buildData.BuildIndices(data, cullSides, cullBackfaces))
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

				if(buildData.BuildIndices(data, cullSides, cullBackfaces))
				{
					offsetShell.Init(buildData, blockOffset);
					chunkContext.AddData(offsetShell, renderPass);
					offsetShell.Clear();
				}
				buildData.Clear();
			}

			private class VertsOffsetShell : IDrawableData, VerticesContext.IProcessor
			{
				EnumDrawMode IDrawableData.DrawMode => original.DrawMode;
				uint IDrawableData.IndicesCount => original.IndicesCount;
				uint IDrawableData.VerticesCount => verticesCount;
				int IDrawableData.VertexBuffersCount => original.VertexBuffersCount;

				private float3 offset;
				private uint verticesCount;
				private IDrawableData original;
				private VerticesContext targetContext;

				private byte[] dataBuffer = new byte[1024];

				public void Init(IDrawableData original, float3 offset)
				{
					this.offset = offset;
					this.original = original;
					verticesCount = original.VerticesCount;
				}

				public void Clear()
				{
					original = null;
				}

				void IDrawableData.ProvideIndices(IndicesContext context)
				{
					original.ProvideIndices(context);
				}

				void IDrawableData.ProvideVertices(VerticesContext context)
				{
					targetContext = context;
					original.ProvideVertices(new VerticesContext(this, false));
					targetContext = default;
				}

				unsafe void VerticesContext.IProcessor.Process<T>(int bufferIndex, T* data, VertexDeclaration declaration, int stride, bool isDynamic)
				{
					uint offset = uint.MaxValue;
					for(int i = 0; i < declaration.Attributes.Length; i++)
					{
						ref readonly var attr = ref declaration.Attributes[i];
						if(attr.Alias == VertexAttributeAlias.POSITION && attr.Type == EnumShaderPrimitiveType.Float && attr.Size >= 3)
						{
							offset = attr.Offset;
							break;
						}
					}
					if(offset < uint.MaxValue)
					{
						if((uint)dataBuffer.Length < verticesCount * (uint)stride)
						{
							dataBuffer = new byte[verticesCount * (uint)stride];
						}
						var posOffset = this.offset;
						fixed(byte* ptr = dataBuffer)
						{
							Buffer.MemoryCopy(data, ptr, verticesCount * stride, verticesCount * stride);

							byte* dPtr = ptr + offset;
							for(int i = 0; i < verticesCount; i++)
							{
								*(float3*)dPtr += posOffset;

								dPtr += stride;
							}

							targetContext.Process(bufferIndex, ptr, declaration, stride, isDynamic);
						}
					}
					else
					{
						targetContext.Process(bufferIndex, data, declaration, stride, isDynamic);
					}
				}
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

				private bool cullBackfaces = false;
				private int cullSides = 0;

				public unsafe DrawableDataBuilder()
				{
					indicesProcessor = IndicesProcessor;
				}

				public bool BuildIndices(IDrawableData original, int cullSides, bool cullBackfaces)
				{
					this.original = original;
					this.cullSides = cullSides;
					this.cullBackfaces = cullBackfaces;
					indicesCount = original.IndicesCount;
					verticesCount = original.VerticesCount;
					if(indices == null) indices = new uint[Math.Max(256, (int)original.IndicesCount)];

					hasIndices = false;
					original.ProvideIndices(new IndicesContext(indicesProcessor, false));
					if(hasIndices)
					{
						original.ProvideVertices(new VerticesContext(this, false));
						return indicesCount > 0;
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
							CullIndices((byte*)data + attr.Offset, (uint)stride);
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

				private unsafe void CullIndices(byte* vertices, uint stride)
				{
					bool cullBackfaces = this.cullBackfaces;
					int cullSides = this.cullSides;
					TriangleCalcUtil util = default;
					fixed(uint* ptr = indices)
					{
						uint* moveTo = ptr;
						//TODO: should be vertices culled too? i.e. add some Dictionary<uint, uint> origVertIndexToOutputIndex; & then only collect vertices from this list
						uint counter = 0;
						for(uint i = 0; i < indicesCount; i += 3)
						{
							uint a = ptr[i];
							uint b = ptr[i];
							uint c = ptr[i];

							util.a = *(float3*)(vertices + a * stride);
							util.b = *(float3*)(vertices + b * stride);
							util.c = *(float3*)(vertices + c * stride);
							float4 plane = new float4(math.cross(util.b - util.a, util.c - util.a), 0);

							if(!cullBackfaces || CubeBoundsHelper.TriInView(plane.xyz, cullSides))
							{
								plane.xyz = math.normalize(plane.xyz);
								plane.w = math.dot(util.a, plane.xyz);

								var p = CubeBoundsHelper.cubeCenter - plane.xyz * math.dot(plane.xyz, CubeBoundsHelper.cubeCenter);

								if(CubeBoundsHelper.PointInView(p, cullSides))
								{
									util.ClosestPointOnTriangleToPoint(ref p);
									if(CubeBoundsHelper.PointInView(p, cullSides))
									{
										*moveTo = a;
										moveTo++;
										*moveTo = b;
										moveTo++;
										*moveTo = c;
										moveTo++;

										counter++;
									}
								}
							}
						}
						indicesCount = counter;
					}
				}

				[StructLayout(LayoutKind.Auto, Pack = 4)]
				private struct TriangleCalcUtil
				{
					public float3 a, b, c;

					private float3 ab, ac, ap, bp, cp;
					private float v, d1, d2, d3, d4, d5, d6;

					public void ClosestPointOnTriangleToPoint(ref float3 p)
					{
						//https://github.com/StudioTechtrics/closestPointOnMesh/blob/master/closestPointOnMesh/Assets/KDTreeData.cs
						//Source: Real-Time Collision Detection by Christer Ericson
						//Reference: Page 136

						//Check if P in vertex region outside A
						ab = b - a;
						ac = c - a;
						ap = p - a;

						d1 = math.dot(ab, ap);
						d2 = math.dot(ac, ap);
						if(d1 <= 0.0f && d2 <= 0.0f)
						{
							p = a; //Barycentric coordinates (1,0,0)
							return;
						}

						//Check if P in vertex region outside B
						float3 bp = p - b;
						d3 = math.dot(ab, bp);
						d4 = math.dot(ac, bp);
						if(d3 >= 0.0f && d4 <= d3)
						{
							p = b; //Barycentric coordinates (1,0,0)
							return;
						}

						//Check if P in edge region of AB, if so return projection of P onto AB
						v = d1 * d4 - d3 * d2;
						if(v <= 0.0f && d1 >= 0.0f && d3 <= 0.0f)
						{
							v = d1 / (d1 - d3);
							p = (1 - v) * a + v * b; //Barycentric coordinates (1-v,v,0)
							return;
						}

						//Check if P in vertex region outside C
						cp = p - c;
						d5 = math.dot(ab, cp);
						d6 = math.dot(ac, cp);
						if(d6 >= 0.0f && d5 <= d6)
						{
							p = c; //Barycentric coordinates (1,0,0)
							return;
						}

						//Check if P in edge region of AC, if so return projection of P onto AC
						v = d5 * d2 - d1 * d6;
						if(v <= 0.0f && d2 >= 0.0f && d6 <= 0.0f)
						{
							v = d2 / (d2 - d6);
							p = (1 - v) * a + v * c; //Barycentric coordinates (1-w,0,w)
							return;
						}

						//Check if P in edge region of BC, if so return projection of P onto BC
						v = d3 * d6 - d5 * d4;
						if(v <= 0.0f && (d4 - d3) >= 0.0f && (d5 - d6) >= 0.0f)
						{
							v = (d4 - d3) / ((d4 - d3) + (d5 - d6));
							p = (1 - v) * b + v * c; //Barycentric coordinates (0,1-w,w)
							return;
						}

						//P inside face region
					}
				}
			}
		}

		private static unsafe class CubeBoundsHelper
		{
			private const float EPSILON_MIN = 0.0001f;
			private const float EPSILON_MAX = 0.9999f;
			private const float CONE_MAX = 0.6f;

			private const int FLAG_NORTH = 1 << BlockFacing.indexNORTH;
			private const int FLAG_EAST = 1 << BlockFacing.indexEAST;
			private const int FLAG_SOUTH = 1 << BlockFacing.indexSOUTH;
			private const int FLAG_WEST = 1 << BlockFacing.indexWEST;
			private const int FLAG_UP = 1 << BlockFacing.indexUP;
			private const int FLAG_DOWN = 1 << BlockFacing.indexDOWN;

			public static readonly float3 cubeCenter = new float3(0.5f, 0.5f, 0.5f);

#pragma warning disable CS0169 // The field is never used
			private static Float6x3 sideNormals;
#pragma warning restore CS0169

			static CubeBoundsHelper()
			{
				fixed(float* sideNormals = CubeBoundsHelper.sideNormals.values)
				{
					foreach(var face in BlockFacing.ALLFACES)
					{
						int i = face.Index * 3;
						sideNormals[i] = face.Normalf.X;
						sideNormals[i + 1] = face.Normalf.Y;
						sideNormals[i + 2] = face.Normalf.Z;
					}
				}
			}

			public static bool PointInView(float3 v, int sidesMask)
			{
				if((v.x > EPSILON_MIN) & (v.x < EPSILON_MAX) & (v.y > EPSILON_MIN) & (v.y < EPSILON_MAX) & (v.z > EPSILON_MIN) & (v.z < EPSILON_MAX)) return true;
				v = math.normalizesafe(v - cubeCenter);
				fixed(float* sideNormals = CubeBoundsHelper.sideNormals.values)
				{
					if((sidesMask & FLAG_NORTH) != 0 && math.dot(v, ((float3*)sideNormals)[BlockFacing.indexNORTH]) > CONE_MAX) return true;
					if((sidesMask & FLAG_EAST) != 0 && math.dot(v, ((float3*)sideNormals)[BlockFacing.indexEAST]) > CONE_MAX) return true;
					if((sidesMask & FLAG_SOUTH) != 0 && math.dot(v, ((float3*)sideNormals)[BlockFacing.indexSOUTH]) > CONE_MAX) return true;
					if((sidesMask & FLAG_WEST) != 0 && math.dot(v, ((float3*)sideNormals)[BlockFacing.indexWEST]) > CONE_MAX) return true;
					if((sidesMask & FLAG_UP) != 0 && math.dot(v, ((float3*)sideNormals)[BlockFacing.indexUP]) > CONE_MAX) return true;
					if((sidesMask & FLAG_DOWN) != 0 && math.dot(v, ((float3*)sideNormals)[BlockFacing.indexDOWN]) > CONE_MAX) return true;
				}
				return false;
			}

			public static bool TriInView(float3 triNormal, int sidesMask)
			{
				fixed(float* sideNormals = CubeBoundsHelper.sideNormals.values)
				{
					if((sidesMask & FLAG_NORTH) != 0 && math.dot(triNormal, ((float3*)sideNormals)[BlockFacing.indexNORTH]) > math.EPSILON) return true;
					if((sidesMask & FLAG_EAST) != 0 && math.dot(triNormal, ((float3*)sideNormals)[BlockFacing.indexEAST]) > math.EPSILON) return true;
					if((sidesMask & FLAG_SOUTH) != 0 && math.dot(triNormal, ((float3*)sideNormals)[BlockFacing.indexSOUTH]) > math.EPSILON) return true;
					if((sidesMask & FLAG_WEST) != 0 && math.dot(triNormal, ((float3*)sideNormals)[BlockFacing.indexWEST]) > math.EPSILON) return true;
					if((sidesMask & FLAG_UP) != 0 && math.dot(triNormal, ((float3*)sideNormals)[BlockFacing.indexUP]) > math.EPSILON) return true;
					if((sidesMask & FLAG_DOWN) != 0 && math.dot(triNormal, ((float3*)sideNormals)[BlockFacing.indexDOWN]) > math.EPSILON) return true;
				}
				return false;
			}

			[StructLayout(LayoutKind.Auto, Pack = 4)]
			private struct Float6x3
			{
				public fixed float values[6 * 3];
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
				value |= (bitBlocks[i] >> bitOffset) & 1;
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

	public interface IBlockDataProvider
	{
		/// <summary>
		/// Provides the block data for the batcher.
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