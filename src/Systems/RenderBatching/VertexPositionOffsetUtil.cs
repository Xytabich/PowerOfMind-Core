using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Drawable;
using System;
using Unity.Mathematics;
using Vintagestory.API.Client;

namespace PowerOfMind.Systems.RenderBatching
{
	public class VertexPositionOffsetUtil : IDrawableData, VerticesContext.IProcessor
	{
		EnumDrawMode IDrawableData.DrawMode => original.DrawMode;
		uint IDrawableData.IndicesCount => original.IndicesCount;
		uint IDrawableData.VerticesCount => verticesCount;
		int IDrawableData.VertexBuffersCount => original.VertexBuffersCount;

		private float3 offset;
		private uint verticesCount;
		private IDrawableData original;
		private VerticesContext.IProcessor targetProcessor;

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
			targetProcessor = context.GetProcessor();
			original.ProvideVertices(new VerticesContext(this, false));
			targetProcessor = null;
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

					targetProcessor.Process(bufferIndex, ptr, declaration, stride, isDynamic);
				}
			}
			else
			{
				targetProcessor.Process(bufferIndex, data, declaration, stride, isDynamic);
			}
		}
	}
}