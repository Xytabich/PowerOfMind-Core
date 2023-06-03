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
					container.drawMode = BeginMode.Lines;
					break;
				case EnumDrawMode.LineStrip:
					container.drawMode = BeginMode.LineStrip;
					break;
				default:
					container.drawMode = BeginMode.Triangles;
					break;
			}

			GL.BindVertexArray(container.vao);

			var attribPointers = new List<int>();
			data.ProvideVertices(new VerticesContext(new VerticesProcessorImpl(container, data.VerticesCount, attribPointers).UploadAndInitContainer, false));
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			container.attribPointers = attribPointers.ToArray();

			data.ProvideIndices(new IndicesContext(new IndicesProcessorImpl(container).Upload, false));

			GL.BindVertexArray(0);
			return container;
		}

		/// <summary>
		/// Recreates the data buffers for the given MeshRef, this may be needed if the number of vertices or indexes has changed.
		/// </summary>
		public static unsafe void ReuploadDrawable(this IRenderAPI rapi, IDrawableHandle handle, IDrawableData data)
		{
			var container = (RefContainer)handle;
			container.indicesCount = data.IndicesCount;

			data.ProvideVertices(new VerticesContext(new VerticesProcessorImpl(container, data.VerticesCount).Upload, false));
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

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
				data.ProvideVertices(new VerticesContext(new VerticesProcessorImpl(container, data.VerticesCount).Update, false));
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
				data.ProvideVertices(new VerticesContext(new VerticesProcessorImpl(container, data.VerticesCount, verticesBufferOffsets).UpdatePartial, false));
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
			GL.DrawElements(container.drawMode, container.indicesCount, DrawElementsType.UnsignedInt, 0);
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
			public BeginMode drawMode;
			public int indicesCount;

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

		private class VerticesProcessorImpl
		{
			private readonly RefContainer container;
			private readonly int verticesCount;
			private readonly int[] offsets = null;

			private readonly List<int> attribPointers;

			public VerticesProcessorImpl(RefContainer container, int verticesCount)
			{
				this.container = container;
				this.verticesCount = verticesCount;
			}

			public VerticesProcessorImpl(RefContainer container, int verticesCount, List<int> outAttribPointers)
			{
				this.container = container;
				this.verticesCount = verticesCount;
				this.attribPointers = outAttribPointers;
			}

			public VerticesProcessorImpl(RefContainer container, int verticesCount, int[] offsets)
			{
				this.container = container;
				this.verticesCount = verticesCount;
				this.offsets = offsets;
			}

			public unsafe void UploadAndInitContainer(int bufferIndex, void* data, int stride, VertexDeclaration declaration, bool isDynamic)
			{
				GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffers[bufferIndex]);
				GL.BufferData(BufferTarget.ArrayBuffer, verticesCount * stride, (IntPtr)data, isDynamic ? BufferUsageHint.DynamicDraw : BufferUsageHint.StaticDraw);
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

			public unsafe void Upload(int bufferIndex, void* data, int stride, VertexDeclaration declaration, bool isDynamic)
			{
				GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffers[bufferIndex]);
				GL.BufferData(BufferTarget.ArrayBuffer, verticesCount * stride, (IntPtr)data, isDynamic ? BufferUsageHint.DynamicDraw : BufferUsageHint.StaticDraw);
			}

			public unsafe void Update(int bufferIndex, void* data, int stride, VertexDeclaration declaration, bool isDynamic)
			{
				GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffers[bufferIndex]);
				GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, verticesCount * stride, (IntPtr)data);
			}

			public unsafe void UpdatePartial(int bufferIndex, void* data, int stride, VertexDeclaration declaration, bool isDynamic)
			{
				GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffers[bufferIndex]);
				GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)(offsets[bufferIndex] * stride), verticesCount * stride, (IntPtr)data);
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

			public unsafe void Upload(int* data, bool isDynamic)
			{
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
				GL.BufferData(BufferTarget.ElementArrayBuffer, 4 * container.indicesCount, (IntPtr)data, isDynamic ? BufferUsageHint.DynamicDraw : BufferUsageHint.StaticDraw);
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			}

			public unsafe void Update(int* data, bool isDynamic)
			{
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
				GL.BufferSubData(BufferTarget.ElementArrayBuffer, (IntPtr)0, 4 * container.indicesCount, (IntPtr)data);
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			}

			public unsafe void UpdatePartial(int* data, bool isDynamic)
			{
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
				GL.BufferSubData(BufferTarget.ElementArrayBuffer, (IntPtr)(4 * offset), 4 * container.indicesCount, (IntPtr)data);
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