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
		/// Uploads the mesh data to the GPU, returns a handle that can be used to update the data or free memory.
		/// </summary>
		/// <param name="initEmptyBuffer">Affects the behavior if <see cref="IDrawableData.ProvideVertices"/> provides a <see langword="null"/> pointer. If set to <see langword="false"/>, then the buffer will not be initialized. If <see langword="true"/>, memory will be allocated for the buffer without filling with data.</param>
		public static unsafe IDrawableHandle UploadDrawable(this IRenderAPI rapi, IDrawableData data, bool initEmptyBuffer = true)
		{
			int vBuffersCount = data.VertexBuffersCount;
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
			var uploadVerts = new UploadVerticesProcessor(container, data.VerticesCount);
			uploadVerts.ignoreNull = !initEmptyBuffer;
			for(int i = 0; i < vBuffersCount; i++)
			{
				var meta = data.GetVertexBufferMeta(i);
				uploadVerts.isDynamic = meta.IsDynamic;
				uploadVerts.bufferIndex = i;
				data.ProvideVertices(new VerticesContext(uploadVerts.Process, i));
				InitDeclaration(meta.Declaration, attribPointers);
			}
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			container.attribPointers = attribPointers.ToArray();

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
			data.ProvideIndices(new IndicesContext(new IndicesProcessorImpl(container, data.GetIndicesMeta().IsDynamic).Upload));

			GL.BindVertexArray(0);
			return container;
		}

		/// <summary>
		/// Creates a proxy for the reference handle data. Proxy allows to use a different set of vertex attributers for the same data buffers.
		/// </summary>
		public static IDrawableHandle CreateDrawableProxy(this IRenderAPI rapi, IDrawableHandle refHandle, IDrawableInfo info)
		{
			var refContainer = (RefContainer)refHandle;
			var container = new RefContainer(refContainer);
			container.indicesCount = info.IndicesCount;

			switch(info.DrawMode)
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
			int vBuffersCount = info.VertexBuffersCount;
			for(int i = 0; i < vBuffersCount; i++)
			{
				var meta = info.GetVertexBufferMeta(i);
				if(!meta.Declaration.IsEmpty)
				{
					GL.BindBuffer(BufferTarget.ArrayBuffer, refContainer.vertexBuffers[i]);
					InitDeclaration(meta.Declaration, attribPointers);
				}
			}
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			container.attribPointers = attribPointers.ToArray();

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
			GL.BindVertexArray(0);
			return container;
		}

		/// <summary>
		/// Updates proxy vertex attributes.
		/// </summary>
		public static void UpdateDrawableProxy(this IRenderAPI rapi, IDrawableHandle proxyHandle, IDrawableInfo info)
		{
			var container = (RefContainer)proxyHandle;
			container.indicesCount = info.IndicesCount;

			GL.BindVertexArray(container.vao);

			foreach(var loc in container.attribPointers)
			{
				GL.VertexAttribDivisor((uint)loc, 0);
			}

			var refContainer = container.proxyFor;
			var attribPointers = new List<int>();
			int vBuffersCount = info.VertexBuffersCount;
			for(int i = 0; i < vBuffersCount; i++)
			{
				var meta = info.GetVertexBufferMeta(i);
				if(!meta.Declaration.IsEmpty)
				{
					GL.BindBuffer(BufferTarget.ArrayBuffer, refContainer.vertexBuffers[i]);
					InitDeclaration(meta.Declaration, attribPointers);
				}
			}
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			container.attribPointers = attribPointers.ToArray();

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
			GL.BindVertexArray(0);
		}

		/// <summary>
		/// Resizes the data buffers for the given handle, this may be needed if the number of vertices or indexes has changed.
		/// </summary>
		/// <param name="initEmptyBuffer">Affects the behavior if <see cref="IDrawableData.ProvideVertices"/> provides a <see langword="null"/> pointer. If set to <see langword="false"/>, it will not modify the buffer. If <see langword="true"/>, memory will be allocated for the buffer without filling with data.</param>
		public static unsafe void ReuploadDrawable(this IRenderAPI rapi, IDrawableHandle handle, IDrawableData data, bool updateVertexDeclaration = false, bool initEmptyBuffer = true)
		{
			var container = (RefContainer)handle;
			container.indicesCount = data.IndicesCount;

			int vBuffersCount = data.VertexBuffersCount;
			if(updateVertexDeclaration)
			{
				GL.BindVertexArray(container.vao);

				//Clear the previous state, nothing else needs to be changed as unused attributes won't be enabled at render time anyway
				foreach(var loc in container.attribPointers)
				{
					GL.VertexAttribDivisor((uint)loc, 0);
				}

				var attribPointers = new List<int>();
				var uploadVerts = new UploadVerticesProcessor(container, data.VerticesCount);
				uploadVerts.ignoreNull = !initEmptyBuffer;
				for(int i = 0; i < vBuffersCount; i++)
				{
					var meta = data.GetVertexBufferMeta(i);
					uploadVerts.isDynamic = meta.IsDynamic;
					uploadVerts.bufferIndex = i;
					data.ProvideVertices(new VerticesContext(uploadVerts.Process, i));
					InitDeclaration(meta.Declaration, attribPointers);
				}
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

				container.attribPointers = attribPointers.ToArray();
				GL.BindVertexArray(0);
			}
			else
			{
				var uploadVerts = new UploadVerticesProcessor(container, data.VerticesCount);
				uploadVerts.ignoreNull = initEmptyBuffer;
				for(int i = 0; i < vBuffersCount; i++)
				{
					var meta = data.GetVertexBufferMeta(i);
					uploadVerts.isDynamic = meta.IsDynamic;
					uploadVerts.bufferIndex = i;
					data.ProvideVertices(new VerticesContext(uploadVerts.Process, i));
				}
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			}

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
			data.ProvideIndices(new IndicesContext(new IndicesProcessorImpl(container, data.GetIndicesMeta().IsDynamic).Upload));
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
		}

		/// <summary>
		/// Updates the vertex and index data for the given handle.
		/// If <see cref="IDrawableData.ProvideVertices"/> provides a <see langword="null"/> pointer, then the data will not be modified.
		/// If the number of vertices or indexes has changed since <see cref="UploadDrawable"/>, use <see cref="ReuploadDrawable"/> instead.
		/// </summary>
		public static unsafe void UpdateDrawable(this IRenderAPI rapi, IDrawableHandle handle, IDrawableData data)
		{
			var container = (RefContainer)handle;

			if(data.VerticesCount > 0)
			{
				int vBuffersCount = data.VertexBuffersCount;
				var updateVerts = new UpdateVerticesProcessor(container, data.VerticesCount);
				for(int i = 0; i < vBuffersCount; i++)
				{
					updateVerts.bufferIndex = i;
					data.ProvideVertices(new VerticesContext(updateVerts.Process, i));
				}
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			}

			if(data.IndicesCount > 0)
			{
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
				data.ProvideIndices(new IndicesContext(new IndicesProcessorImpl(container).Update));
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			}
		}

		/// <summary>
		/// Updates part of the data buffers.
		/// If <see cref="IDrawableData.ProvideVertices"/> provides a <see langword="null"/> pointer, then the data will not be modified.
		/// </summary>
		/// <param name="indicesBufferOffset">From which buffer element to start the update</param>
		/// <param name="verticesBufferOffsets">From which buffer element to start the update. Size must be at least <see cref="IDrawableInfo.VertexBuffersCount"/>.</param>
		public static unsafe void UpdateDrawablePart(this IRenderAPI rapi, IDrawableHandle handle, IDrawableData data, int indicesBufferOffset, params int[] verticesBufferOffsets)
		{
			var container = (RefContainer)handle;

			if(data.VerticesCount > 0)
			{
				int vBuffersCount = data.VertexBuffersCount;
				var updateVerts = new UpdatePartialVerticesProcessor(container, data.VerticesCount, verticesBufferOffsets);
				for(int i = 0; i < vBuffersCount; i++)
				{
					updateVerts.bufferIndex = i;
					data.ProvideVertices(new VerticesContext(updateVerts.Process, i));
				}
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			}

			if(data.IndicesCount > 0)
			{
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
				data.ProvideIndices(new IndicesContext(new IndicesProcessorImpl(container, (uint)indicesBufferOffset, data.IndicesCount).UpdatePartial));
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
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
			GL.DrawElements(container.drawMode, (int)container.indicesCount, DrawElementsType.UnsignedInt, 0);
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
			GL.DrawElements(container.drawMode, indicesCount, DrawElementsType.UnsignedInt, (IntPtr)indicesOffset);
			for(int i = 0; i < len; i++)
			{
				GL.DisableVertexAttribArray(attribPointers[i]);
			}
			GL.BindVertexArray(0);
		}

		public static void RenderDrawableInstanced(this IRenderAPI rapi, IDrawableHandle handle, int instancesCount)
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
			GL.DrawElementsInstanced(container.drawMode, (int)container.indicesCount, DrawElementsType.UnsignedInt, 0, instancesCount);
			for(int i = 0; i < len; i++)
			{
				GL.DisableVertexAttribArray(attribPointers[i]);
			}
			GL.BindVertexArray(0);
		}

		public static void RenderDrawableInstanced(this IRenderAPI rapi, IDrawableHandle handle, uint indicesOffset, int indicesCount, int instancesCount)
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
			GL.DrawElementsInstanced(container.drawMode, indicesCount, DrawElementsType.UnsignedInt, (IntPtr)indicesOffset, instancesCount);
			for(int i = 0; i < len; i++)
			{
				GL.DisableVertexAttribArray(attribPointers[i]);
			}
			GL.BindVertexArray(0);
		}

		private static void InitDeclaration(VertexDeclaration declaration, ICollection<int> outLocations)
		{
			var attributes = declaration.Attributes;
			for(int i = attributes.Length - 1; i >= 0; i--)
			{
				ref readonly var attrib = ref attributes[i];
				outLocations.Add(attrib.Location);
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

		private class RefContainer : IDrawableHandle
		{
			public int vao, indexBuffer;
			public int[] vertexBuffers;
			public int[] attribPointers;
			public PrimitiveType drawMode;
			public uint indicesCount;

			public readonly RefContainer proxyFor;

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
				proxyFor = null;
			}

			public RefContainer(RefContainer proxyFor)
			{
				this.proxyFor = proxyFor;
				vertexBuffers = null;
				vao = GL.GenVertexArray();
				indexBuffer = proxyFor.indexBuffer;
			}

			public void Dispose()
			{
				if(!disposed)
				{
					GL.DeleteVertexArray(vao);
					if(proxyFor == null)
					{
						for(int i = vertexBuffers.Length - 1; i >= 0; i--)
						{
							GL.DeleteBuffer(vertexBuffers[i]);
						}
						GL.DeleteBuffer(indexBuffer);
					}

					vao = 0;
					indexBuffer = 0;
					vertexBuffers = null;

					disposed = true;
				}
			}
		}

		private class UploadVerticesProcessor
		{
			public bool isDynamic;
			public int bufferIndex;
			public bool ignoreNull = false;
			public readonly VerticesContext.ProcessorDelegate Process;

			private readonly RefContainer container;
			private readonly uint verticesCount;

			public unsafe UploadVerticesProcessor(RefContainer container, uint verticesCount)
			{
				this.container = container;
				this.verticesCount = verticesCount;
				Process = ProcessImpl;
			}

			private unsafe void ProcessImpl(void* data, int stride)
			{
				if(ignoreNull && data == null) return;
				GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffers[bufferIndex]);
				GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(verticesCount * (uint)stride), (IntPtr)data, isDynamic ? BufferUsageHint.DynamicDraw : BufferUsageHint.StaticDraw);
			}
		}

		private class UpdateVerticesProcessor
		{
			public int bufferIndex;
			public readonly VerticesContext.ProcessorDelegate Process;

			private readonly RefContainer container;
			private readonly uint verticesCount;

			public unsafe UpdateVerticesProcessor(RefContainer container, uint verticesCount)
			{
				this.container = container;
				this.verticesCount = verticesCount;
				Process = ProcessImpl;
			}

			private unsafe void ProcessImpl(void* data, int stride)
			{
				if(data == null) return;
				GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffers[bufferIndex]);
				GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)(verticesCount * (uint)stride), (IntPtr)data);
			}
		}

		private class UpdatePartialVerticesProcessor
		{
			public int bufferIndex;
			public readonly VerticesContext.ProcessorDelegate Process;

			private readonly RefContainer container;
			private readonly uint verticesCount;
			private readonly int[] offsets;

			public unsafe UpdatePartialVerticesProcessor(RefContainer container, uint verticesCount, int[] offsets)
			{
				this.container = container;
				this.verticesCount = verticesCount;
				this.offsets = offsets;
				Process = ProcessImpl;
			}

			private unsafe void ProcessImpl(void* data, int stride)
			{
				if(data == null) return;
				GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffers[bufferIndex]);
				GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)(offsets[bufferIndex] * (uint)stride), (IntPtr)(verticesCount * (uint)stride), (IntPtr)data);
			}
		}

		private class IndicesProcessorImpl
		{
			private readonly RefContainer container;
			private readonly uint offset;
			private readonly bool isDynamic;
			private readonly uint indicesCount;

			public IndicesProcessorImpl(RefContainer container)
			{
				this.container = container;
				indicesCount = container.indicesCount;
				offset = 0;
			}

			public IndicesProcessorImpl(RefContainer container, uint offset, uint indicesCount)
			{
				this.container = container;
				this.indicesCount = indicesCount;
				this.offset = offset;
			}

			public IndicesProcessorImpl(RefContainer container, bool isDynamic)
			{
				this.container = container;
				this.isDynamic = isDynamic;
				indicesCount = container.indicesCount;
				offset = 0;
			}

			public unsafe void Upload(uint* data)
			{
				GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(4 * indicesCount), (IntPtr)data, isDynamic ? BufferUsageHint.DynamicDraw : BufferUsageHint.StaticDraw);
			}

			public unsafe void Update(uint* data)
			{
				if(data == null) return;
				GL.BufferSubData(BufferTarget.ElementArrayBuffer, (IntPtr)0, (IntPtr)(4 * indicesCount), (IntPtr)data);
			}

			public unsafe void UpdatePartial(uint* data)
			{
				if(data == null) return;
				GL.BufferSubData(BufferTarget.ElementArrayBuffer, (IntPtr)(4 * offset), (IntPtr)(4 * indicesCount), (IntPtr)data);
			}
		}
	}

	public interface IDrawableHandle : IDisposable
	{
		bool Initialized { get; }

		bool Disposed { get; }
	}
}