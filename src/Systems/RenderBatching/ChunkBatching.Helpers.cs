using PowerOfMind.Collections;
using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Drawable;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using XXHash;

namespace PowerOfMind.Systems.RenderBatching
{
	public partial class ChunkBatching
	{
		private class UniformsDataCollection
		{
			public ByteBuffer dataBuffer;
			public readonly RefList<UniformBlock> blocks;
			public readonly Dictionary<UniformBlockKey, int> blockKeyToIndex;

			public UniformsDataCollection(int capacity)
			{
				this.dataBuffer = new ByteBuffer(capacity);
				this.blocks = blocks = new RefList<UniformBlock>();
				this.blockKeyToIndex = new Dictionary<UniformBlockKey, int>(new UniformBlockKeyEqualityComparer(this));
			}

			public unsafe int PutUniformData(byte* ptr, int size)
			{
				if(MemIsZero(ptr, size)) return -1;

				dataBuffer.EnsureBufferSize(size);
				fixed(byte* buffPtr = dataBuffer.buffer)
				{
					Buffer.MemoryCopy(ptr, buffPtr + dataBuffer.offset, size, size);
				}
				var key = new UniformBlockKey(dataBuffer.offset, size, UniformBlockKey.GetHash(ptr, size));
				if(!blockKeyToIndex.TryGetValue(key, out var blockIndex))
				{
					blockIndex = blocks.Count;
					blockKeyToIndex[key] = blockIndex;
					blocks.Add(new UniformBlock(dataBuffer.offset, size));
					dataBuffer.offset += size;
				}
				return blockIndex;
			}

			public unsafe bool DataEquals(int aOffset, int bOffset, int size)
			{
				fixed(byte* buffPtr = dataBuffer.buffer)
				{
					return MemEquals(buffPtr + aOffset, buffPtr + bOffset, size);
				}
			}
		}

		private readonly struct UniformBlockKey
		{
			public readonly int offset;
			public readonly int size;
			public readonly int hash;

			public UniformBlockKey(int offset, int size, int hash)
			{
				this.offset = offset;
				this.size = size;
				this.hash = hash;
			}

			public override int GetHashCode()
			{
				return hash;
			}

			public static unsafe int GetHash(byte* ptr, int size)
			{
				return (int)XXHash32.Hash(ptr, size);
			}
		}

		private class UniformBlockKeyEqualityComparer : IEqualityComparer<UniformBlockKey>
		{
			private readonly UniformsDataCollection collection;

			public UniformBlockKeyEqualityComparer(UniformsDataCollection collection)
			{
				this.collection = collection;
			}

			bool IEqualityComparer<UniformBlockKey>.Equals(UniformBlockKey x, UniformBlockKey y)
			{
				return x.hash == y.hash && x.size == y.size && collection.DataEquals(x.offset, y.offset, x.size);
			}

			int IEqualityComparer<UniformBlockKey>.GetHashCode(UniformBlockKey key)
			{
				return key.hash;
			}
		}

		private struct UniformBlock
		{
			public readonly int offset;
			public readonly int size;

			public UniformBlock(int offset, int size)
			{
				this.offset = offset;
				this.size = size;
			}
		}

		private struct ByteBuffer
		{
			public byte[] buffer;
			public int offset;

			public ByteBuffer(int capacity)
			{
				this.buffer = new byte[capacity];
				this.offset = 0;
			}

			public unsafe void EnsureBufferSize(int size)
			{
				if(buffer.Length < offset + size)
				{
					var newBuffer = new byte[buffer.Length * 2];
					fixed(byte* ptrFrom = buffer)
					{
						fixed(byte* ptrTo = newBuffer)
						{
							Buffer.MemoryCopy(ptrFrom, ptrTo, offset, offset);
						}
					}
					buffer = newBuffer;
				}
			}

			public unsafe byte[] ToArray()
			{
				var arr = new byte[offset];
				fixed(byte* ptrFrom = buffer)
				{
					fixed(byte* ptrTo = arr)
					{
						Buffer.MemoryCopy(ptrFrom, ptrTo, offset, offset);
					}
				}
				return arr;
			}
		}

		private class ChunkDrawableData : IDrawableData
		{
			EnumDrawMode IDrawableData.DrawMode => EnumDrawMode.Triangles;
			uint IDrawableData.IndicesCount => indicesCount;
			uint IDrawableData.VerticesCount => verticesCount;
			int IDrawableData.VertexBuffersCount => 1;

			public uint indicesCount, verticesCount;
			public int verticesStride;
			public VertexDeclaration vertexDeclaration;

			public byte[] verticesData;
			public uint[] indicesData;

			public void Clear()
			{
				verticesData = null;
				indicesData = null;
			}

			unsafe void IDrawableData.ProvideIndices(IndicesContext context)
			{
				if(indicesData == null) context.Process(null);
				else
				{
					fixed(uint* ptr = indicesData)
					{
						context.Process(ptr);
					}
				}
			}

			unsafe void IDrawableData.ProvideVertices(VerticesContext context)
			{
				if(verticesData == null)
				{
					context.Process((byte*)null, verticesStride);
				}
				else
				{
					fixed(byte* ptr = verticesData)
					{
						context.Process(ptr, verticesStride);
					}
				}
			}

			IndicesMeta IDrawableData.GetIndicesMeta()
			{
				return new IndicesMeta(false);
			}

			VertexBufferMeta IDrawableData.GetVertexBufferMeta(int index)
			{
				return new VertexBufferMeta(vertexDeclaration, false);
			}
		}
	}
}