using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Drawable;
using System;
using Unity.Mathematics;
using Vintagestory.API.Client;

namespace PowerOfMind.Systems.ChunkBatchers
{
	public class VertexPositionOffsetUtil : IDrawableData
	{
		EnumDrawMode IDrawableInfo.DrawMode => original.DrawMode;
		uint IDrawableInfo.IndicesCount => original.IndicesCount;
		uint IDrawableInfo.VerticesCount => verticesCount;
		int IDrawableInfo.VertexBuffersCount => original.VertexBuffersCount;

		private float3 offset;
		private uint verticesCount;
		private IDrawableData original;

		private int posBufferIndex;
		private uint posBufferOffset;

		private byte[] dataBuffer;

		public unsafe VertexPositionOffsetUtil(int bufferCapacity = 1024)
		{
			dataBuffer = new byte[bufferCapacity];
		}

		public void Init(IDrawableData original, float3 offset)
		{
			this.offset = offset;
			this.original = original;
			verticesCount = original.VerticesCount;
			posBufferIndex = -1;
			int buffCount = original.VertexBuffersCount;
			for(int i = 0; i < buffCount; i++)
			{
				var attributes = original.GetVertexBufferMeta(i).Declaration.Attributes;
				for(int j = 0; j < attributes.Length; j++)
				{
					ref readonly var attr = ref attributes[j];
					if(attr.Alias == VertexAttributeAlias.POSITION && attr.Type == EnumShaderPrimitiveType.Float && attr.Size >= 3)
					{
						posBufferIndex = i;
						posBufferOffset = attr.Offset;
						break;
					}
				}
			}
		}

		public void Clear()
		{
			original = null;
		}

		ReadOnlySpan<uint> IDrawableData.GetIndicesData()
		{
			return original.GetIndicesData();
		}

		ReadOnlySpan<byte> IDrawableData.GetVerticesData(int bufferIndex)
		{
			if(bufferIndex == posBufferIndex)
			{
				return ApplyOffset(original.GetVerticesData(bufferIndex), original.GetVertexBufferMeta(bufferIndex).Stride);
			}
			else
			{
				return original.GetVerticesData(bufferIndex);
			}
		}

		private unsafe ReadOnlySpan<byte> ApplyOffset(ReadOnlySpan<byte> data, int stride)
		{
			if(data.IsEmpty) return default;

			uint offset = posBufferOffset;
			if((uint)dataBuffer.Length < verticesCount * (uint)stride)
			{
				dataBuffer = new byte[verticesCount * (uint)stride];
			}

			data.Slice(0, (int)verticesCount * stride).CopyTo(dataBuffer);

			var posOffset = this.offset;
			fixed(byte* ptr = dataBuffer)
			{
				byte* dPtr = ptr + offset;
				for(int i = 0; i < verticesCount; i++)
				{
					*(float3*)dPtr += posOffset;

					dPtr += stride;
				}
			}
			return dataBuffer;
		}

		IndicesMeta IDrawableInfo.GetIndicesMeta()
		{
			return original.GetIndicesMeta();
		}

		VertexBufferMeta IDrawableInfo.GetVertexBufferMeta(int index)
		{
			return original.GetVertexBufferMeta(index);
		}
	}
}