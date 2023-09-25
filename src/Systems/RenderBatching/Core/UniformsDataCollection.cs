using PowerOfMind.Collections;
using PowerOfMind.Utils;
using System;
using System.Collections.Generic;
using XXHash;

namespace PowerOfMind.Systems.RenderBatching.Core
{
	public class UniformsDataCollection
	{
		public readonly RefList<UniformBlock> Blocks;

		private readonly Dictionary<UniformBlockKey, int> blockKeyToIndex;
		private ByteBuffer dataBuffer;

		public UniformsDataCollection(int capacity)
		{
			this.dataBuffer = new ByteBuffer(capacity);
			this.Blocks = Blocks = new RefList<UniformBlock>();
			this.blockKeyToIndex = new Dictionary<UniformBlockKey, int>(new UniformBlockKeyEqualityComparer(this));
		}

		public byte[] GetData()
		{
			return dataBuffer.ToArray();
		}

		/// <summary>
		/// Registers uniform data and returns the identifier associated with this data.
		/// </summary>
		/// <param name="ptr">Pointer to data or null, if it is necessary to apply default data</param>
		/// <param name="size">Data size in bytes</param>
		/// <returns>The block index</returns>
		public unsafe int PutUniformData(byte* ptr, int size)
		{
			UniformBlockKey key;
			int blockIndex;
			if(ptr == null)
			{
				key = new UniformBlockKey(-2, size, -1);
				if(!blockKeyToIndex.TryGetValue(key, out blockIndex))
				{
					blockIndex = Blocks.Count;
					blockKeyToIndex[key] = blockIndex;
					Blocks.Add(new UniformBlock(-1, size));
				}
			}
			else if(MemUtils.MemIsZero(ptr, size))
			{
				key = new UniformBlockKey(-1, size, 0);
				if(!blockKeyToIndex.TryGetValue(key, out blockIndex))
				{
					blockIndex = Blocks.Count;
					blockKeyToIndex[key] = blockIndex;
					Blocks.Add(new UniformBlock(-1, size));
				}
			}
			else
			{
				dataBuffer.EnsureBufferSize(size);
				fixed(byte* buffPtr = dataBuffer.buffer)
				{
					Buffer.MemoryCopy(ptr, buffPtr + dataBuffer.offset, size, size);
				}
				key = new UniformBlockKey(dataBuffer.offset, size, UniformBlockKey.GetHash(ptr, size));
				if(!blockKeyToIndex.TryGetValue(key, out blockIndex))
				{
					blockIndex = Blocks.Count;
					blockKeyToIndex[key] = blockIndex;
					Blocks.Add(new UniformBlock(dataBuffer.offset, size));
					dataBuffer.offset += size;
				}
			}
			return blockIndex;
		}

		public unsafe bool DataEquals(int aOffset, int bOffset, int size)
		{
			fixed(byte* buffPtr = dataBuffer.buffer)
			{
				return MemUtils.MemEquals(buffPtr + aOffset, buffPtr + bOffset, size);
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

		private class UniformBlockKeyEqualityComparer : IEqualityComparer<UniformBlockKey>
		{
			private readonly UniformsDataCollection collection;

			public UniformBlockKeyEqualityComparer(UniformsDataCollection collection)
			{
				this.collection = collection;
			}

			bool IEqualityComparer<UniformBlockKey>.Equals(UniformBlockKey x, UniformBlockKey y)
			{
				if(x.size != y.size) return false;
				if(x.offset == y.offset) return true;
				if((x.offset >= 0) & (y.offset >= 0))
				{
					return x.hash == y.hash && collection.DataEquals(x.offset, y.offset, x.size);
				}
				return false;
			}

			int IEqualityComparer<UniformBlockKey>.GetHashCode(UniformBlockKey key)
			{
				return key.hash;
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

		public struct UniformBlock
		{
			/// <summary>
			/// The offset in the data buffer, or -1 if the block refers to the zero value, or -2 if the block refers to the default value.
			/// </summary>
			public readonly int Offset;
			public readonly int Size;

			public UniformBlock(int offset, int size)
			{
				this.Offset = offset;
				this.Size = size;
			}
		}
	}
}