using OpenTK.Graphics.OpenGL;
using PowerOfMind.Graphics.Drawable;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.Client;

namespace PowerOfMind.Graphics
{
	public static class RenderExtensions
	{
		private static readonly VertexAttribPointerType[] convertComponentType = new VertexAttribPointerType[] {
			VertexAttribPointerType.UnsignedByte,
			VertexAttribPointerType.Byte,
			VertexAttribPointerType.UnsignedShort,
			VertexAttribPointerType.Short,
			VertexAttribPointerType.UnsignedInt,
			VertexAttribPointerType.Int,
			VertexAttribPointerType.HalfFloat,
			VertexAttribPointerType.Float,
			VertexAttribPointerType.UnsignedInt2101010Rev,
			VertexAttribPointerType.Int2101010Rev
		};

		private static readonly VertexAttribIntegerType[] convertIntComponentType = new VertexAttribIntegerType[] {
			VertexAttribIntegerType.UnsignedByte,
			VertexAttribIntegerType.Byte,
			VertexAttribIntegerType.UnsignedShort,
			VertexAttribIntegerType.Short,
			VertexAttribIntegerType.UnsignedInt,
			VertexAttribIntegerType.Int,
			VertexAttribIntegerType.Short,
			VertexAttribIntegerType.Int,
			VertexAttribIntegerType.UnsignedInt,
			VertexAttribIntegerType.Int
		};

		/// <summary>
		/// Uploads the mesh data to the GPU, returns a MeshRef that can be used to update the data or free memory.
		/// </summary>
		public static unsafe IDrawableHandle UploadDrawable(this IRenderAPI rapi, IDrawableData data)
		{
			var container = new RefContainer(data.VertexBuffersCount);
			container.indicesCount = data.IndicesCount;

			switch(data.DrawMode)
			{
				case EnumDrawMode.Lines:
					container.drawMode = PrimitiveType.Lines;
					break;
				case EnumDrawMode.LineStrip:
					container.drawMode = PrimitiveType.LineStrip;
					break;
				default:
					container.drawMode = PrimitiveType.Triangles;
					break;
			}

			GL.BindVertexArray(container.vao);

			var attribPointers = new List<int>();
			data.ProvideVertices(new VerticesContext(new InitVerticesProcessor(container, data.VerticesCount, attribPointers), false));
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			container.attribPointers = attribPointers.ToArray();

			data.ProvideIndices(new IndicesContext(new IndicesProcessorImpl(container).Upload, false));

			GL.BindVertexArray(0);
			return container;
		}

		/// <summary>
		/// Recreates the data buffers for the given MeshRef, this may be needed if the number of vertices or indexes has changed.
		/// </summary>
		public static unsafe void ReuploadDrawable(this IRenderAPI rapi, IDrawableHandle handle, IDrawableData data, bool updateVertexDeclaration = false)
		{
			var container = (RefContainer)handle;
			container.indicesCount = data.IndicesCount;

			if(updateVertexDeclaration)
			{
				GL.BindVertexArray(container.vao);

				//Clear the previous state, nothing else needs to be changed as unused attributes won't be enabled at render time anyway
				foreach(var loc in container.attribPointers)
				{
					GL.VertexAttribDivisor((uint)loc, 0);
				}

				var attribPointers = new List<int>();
				data.ProvideVertices(new VerticesContext(new InitVerticesProcessor(container, data.VerticesCount, attribPointers), false));
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

				container.attribPointers = attribPointers.ToArray();
				GL.BindVertexArray(0);
			}
			else
			{
				data.ProvideVertices(new VerticesContext(new UploadVerticesProcessor(container, data.VerticesCount), false));
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			}

			data.ProvideIndices(new IndicesContext(new IndicesProcessorImpl(container).Upload, false));
		}

		/// <summary>
		/// Updates the vertex and index data for the given MeshRef.
		/// If the number of vertices or indexes has changed since <see cref="UploadDrawable"/>, use <see cref="ReuploadDrawable"/> instead.
		/// </summary>
		public static unsafe void UpdateDrawable(this IRenderAPI rapi, IDrawableHandle handle, IDrawableData data)
		{
			var container = (RefContainer)handle;

			if(data.VerticesCount > 0)
			{
				data.ProvideVertices(new VerticesContext(new UpdateVerticesProcessor(container, data.VerticesCount), false));
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			}

			if(data.IndicesCount > 0)
			{
				data.ProvideIndices(new IndicesContext(new IndicesProcessorImpl(container).Update, false));
			}
		}

		/// <summary>
		/// Updates part of the mesh's data.
		/// </summary>
		/// <param name="indicesBufferOffset">From which buffer element to start the update</param>
		/// <param name="verticesBufferOffsets">From which buffer element to start the update</param>
		public static unsafe void UpdateDrawablePart(this IRenderAPI rapi, IDrawableHandle handle, IDrawableData data, int indicesBufferOffset, params int[] verticesBufferOffsets)
		{
			var container = (RefContainer)handle;

			if(data.VerticesCount > 0)
			{
				data.ProvideVertices(new VerticesContext(new UpdatePartialVerticesProcessor(container, data.VerticesCount, verticesBufferOffsets), false));
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			}

			if(data.IndicesCount > 0)
			{
				data.ProvideIndices(new IndicesContext(new IndicesProcessorImpl(container, indicesBufferOffset).UpdatePartial, false));
			}
		}

		public static void RenderDrawable(this IRenderAPI rapi, IDrawableHandle handle)
		{
			RuntimeStats.drawCallsCount++;
			if(!handle.Initialized)
			{
				throw new ArgumentException("Fatal: Trying to render an uninitialized drawable");
			}
			if(handle.Disposed)
			{
				throw new ArgumentException("Fatal: Trying to render a disposed drawable");
			}

			var container = (RefContainer)handle;
			GL.BindVertexArray(container.vao);

			var attribPointers = container.attribPointers;
			int len = attribPointers.Length;
			for(int i = 0; i < len; i++)
			{
				GL.EnableVertexAttribArray(attribPointers[i]);
			}
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
			GL.DrawElements(container.drawMode, (int)container.indicesCount, DrawElementsType.UnsignedInt, 0);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			for(int i = 0; i < len; i++)
			{
				GL.DisableVertexAttribArray(attribPointers[i]);
			}
			GL.BindVertexArray(0);
		}

		public static void RenderDrawable(this IRenderAPI rapi, IDrawableHandle handle, uint indicesOffset, int indicesCount)
		{
			RuntimeStats.drawCallsCount++;
			if(!handle.Initialized)
			{
				throw new ArgumentException("Fatal: Trying to render an uninitialized drawable");
			}
			if(handle.Disposed)
			{
				throw new ArgumentException("Fatal: Trying to render a disposed drawable");
			}

			var container = (RefContainer)handle;
			GL.BindVertexArray(container.vao);

			var attribPointers = container.attribPointers;
			int len = attribPointers.Length;
			for(int i = 0; i < len; i++)
			{
				GL.EnableVertexAttribArray(attribPointers[i]);
			}
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
			GL.DrawElements(container.drawMode, indicesCount, DrawElementsType.UnsignedInt, (IntPtr)indicesOffset);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			for(int i = 0; i < len; i++)
			{
				GL.DisableVertexAttribArray(attribPointers[i]);
			}
			GL.BindVertexArray(0);
		}

		private class RefContainer : IDrawableHandle
		{
			public int vao, indexBuffer;
			public int[] vertexBuffers;
			public int[] attribPointers;
			public PrimitiveType drawMode;
			public uint indicesCount;

			public bool Initialized => vao != 0;
			public bool Disposed => disposed;

			private bool disposed = false;

			public RefContainer(int buffersCount)
			{
				indexBuffer = GL.GenBuffer();
				vertexBuffers = new int[buffersCount];
				for(int i = 0; i < buffersCount; i++)
				{
					vertexBuffers[i] = GL.GenBuffer();
				}
				vao = GL.GenVertexArray();
			}

			public void Dispose()
			{
				if(!disposed)
				{
					GL.DeleteVertexArray(vao);
					for(int i = vertexBuffers.Length - 1; i >= 0; i--)
					{
						GL.DeleteBuffer(vertexBuffers[i]);
					}
					GL.DeleteBuffer(indexBuffer);

					vao = 0;
					indexBuffer = 0;
					vertexBuffers = null;

					disposed = true;
				}
			}
		}

		private class InitVerticesProcessor : VerticesContext.IProcessor
		{
			private readonly RefContainer container;
			private readonly uint verticesCount;

			private readonly List<int> attribPointers;

			public InitVerticesProcessor(RefContainer container, uint verticesCount, List<int> attribPointers)
			{
				this.container = container;
				this.verticesCount = verticesCount;
				this.attribPointers = attribPointers;
			}

			unsafe void VerticesContext.IProcessor.Process<T>(int bufferIndex, T* data, VertexDeclaration declaration, int stride, bool isDynamic)
			{
				GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffers[bufferIndex]);
				GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(verticesCount * (uint)stride), (IntPtr)data, isDynamic ? BufferUsageHint.DynamicDraw : BufferUsageHint.StaticDraw);
				var attributes = declaration.Attributes;
				for(int i = attributes.Length - 1; i >= 0; i--)
				{
					ref readonly var attrib = ref attributes[i];
					attribPointers.Add(attrib.Location);
					if(attrib.IntegerTarget)
					{
						GL.VertexAttribIPointer(
							attrib.Location,
							(int)attrib.Size,
							convertIntComponentType[(int)attrib.Type - 1],
							(int)attrib.Stride,
							(IntPtr)attrib.Offset
						);
					}
					else
					{
						GL.VertexAttribPointer(
							attrib.Location,
							(int)attrib.Size,
							convertComponentType[(int)attrib.Type - 1],
							attrib.Normalized,
							(int)attrib.Stride,
							(int)attrib.Offset
						);
					}
					if(attrib.InstanceDivisor > 0)
					{
						GL.VertexAttribDivisor((uint)attrib.Location, attrib.InstanceDivisor);
					}
				}
			}
		}

		private class UploadVerticesProcessor : VerticesContext.IProcessor
		{
			private readonly RefContainer container;
			private readonly uint verticesCount;

			public UploadVerticesProcessor(RefContainer container, uint verticesCount)
			{
				this.container = container;
				this.verticesCount = verticesCount;
			}

			unsafe void VerticesContext.IProcessor.Process<T>(int bufferIndex, T* data, VertexDeclaration declaration, int stride, bool isDynamic)
			{
				GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffers[bufferIndex]);
				GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(verticesCount * (uint)stride), (IntPtr)data, isDynamic ? BufferUsageHint.DynamicDraw : BufferUsageHint.StaticDraw);
			}
		}

		private class UpdateVerticesProcessor : VerticesContext.IProcessor
		{
			private readonly RefContainer container;
			private readonly uint verticesCount;

			public UpdateVerticesProcessor(RefContainer container, uint verticesCount)
			{
				this.container = container;
				this.verticesCount = verticesCount;
			}

			unsafe void VerticesContext.IProcessor.Process<T>(int bufferIndex, T* data, VertexDeclaration declaration, int stride, bool isDynamic)
			{
				if(data == null) return;
				GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffers[bufferIndex]);
				GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)(verticesCount * (uint)stride), (IntPtr)data);
			}
		}

		private class UpdatePartialVerticesProcessor : VerticesContext.IProcessor
		{
			private readonly RefContainer container;
			private readonly uint verticesCount;
			private readonly int[] offsets;

			public UpdatePartialVerticesProcessor(RefContainer container, uint verticesCount, int[] offsets)
			{
				this.container = container;
				this.verticesCount = verticesCount;
				this.offsets = offsets;
			}

			unsafe void VerticesContext.IProcessor.Process<T>(int bufferIndex, T* data, VertexDeclaration declaration, int stride, bool isDynamic)
			{
				if(data == null) return;
				GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffers[bufferIndex]);
				GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)(offsets[bufferIndex] * stride), (IntPtr)(verticesCount * (uint)stride), (IntPtr)data);
			}
		}

		private class IndicesProcessorImpl
		{
			private readonly RefContainer container;
			private readonly int offset;

			public IndicesProcessorImpl(RefContainer container, int offset = 0)
			{
				this.container = container;
				this.offset = offset;
			}

			public unsafe void Upload(uint* data, bool isDynamic)
			{
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
				GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(4 * container.indicesCount), (IntPtr)data, isDynamic ? BufferUsageHint.DynamicDraw : BufferUsageHint.StaticDraw);
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			}

			public unsafe void Update(uint* data, bool isDynamic)
			{
				if(data == null) return;
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
				GL.BufferSubData(BufferTarget.ElementArrayBuffer, (IntPtr)0, (IntPtr)(4 * container.indicesCount), (IntPtr)data);
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			}

			public unsafe void UpdatePartial(uint* data, bool isDynamic)
			{
				if(data == null) return;
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
				GL.BufferSubData(BufferTarget.ElementArrayBuffer, (IntPtr)(4 * offset), (IntPtr)(4 * container.indicesCount), (IntPtr)data);
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			}
		}
	}

	public interface IDrawableHandle : IDisposable
	{
		bool Initialized { get; }

		bool Disposed { get; }
	}
}