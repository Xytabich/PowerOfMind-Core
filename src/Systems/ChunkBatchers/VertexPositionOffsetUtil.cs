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

		private readonly VerticesContext.ProcessorDelegate Process;

		private float3 offset;
		private uint verticesCount;
		private IDrawableData original;

		private int posBufferIndex;
		private uint posBufferOffset;
		private VerticesContext.ProcessorDelegate targetProcessor;

		private byte[] dataBuffer;

		public unsafe VertexPositionOffsetUtil(int bufferCapacity = 1024)
		{
			dataBuffer = new byte[bufferCapacity];
			Process = ProcessImpl;
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

		void IDrawableData.ProvideIndices(IndicesContext context)
		{
			original.ProvideIndices(context);
		}

		void IDrawableData.ProvideVertices(VerticesContext context)
		{
			if(context.BufferIndex == posBufferIndex)
			{
				targetProcessor = context.GetProcessor();
				original.ProvideVertices(new VerticesContext(Process, context.BufferIndex));
				targetProcessor = null;
			}
			else
			{
				original.ProvideVertices(context);
			}
		}

		private unsafe void ProcessImpl(void* data, int stride)
		{
			uint offset = posBufferOffset;
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

				targetProcessor(ptr, stride);
			}
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