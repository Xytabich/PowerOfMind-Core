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
		/// <param name="initEmptyBuffer">Affects the behavior if <see cref="IDrawableData.GetVerticesData"/> provides an empty span. If set to <see langword="false"/>, then the buffer will not be initialized. If <see langword="true"/>, memory will be allocated for the buffer without filling with data.</param>
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

			try
			{
				var attribPointers = new List<int>();
				uint vertCount = data.VerticesCount;
				for(int i = 0; i < vBuffersCount; i++)
				{
					UploadVertBuffer(container, data, i, initEmptyBuffer, false, attribPointers);
				}
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

				container.attribPointers = attribPointers.ToArray();

				GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
				UploadIndBuffer(container, data, initEmptyBuffer, false);
				GL.BindVertexArray(0);
			}
			catch
			{
				GL.BindVertexArray(0);
				container.Dispose();
				throw;
			}
			return container;
		}

		/// <summary>
		/// Creates a proxy for the reference handle data. Proxy allows to use a different set of vertex attributers for the same data buffers.
		/// </summary>
		public static IDrawableHandle CreateDrawableProxy(this IRenderAPI rapi, IDrawableHandle refHandle, IDrawableInfo info)
		{
			var refContainer = (RefContainer)refHandle;
			if(refContainer.proxyFor != null) throw new ArgumentException("An original handle is required to create a proxy", nameof(refContainer));

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
			if(container.proxyFor == null) throw new ArgumentException("Handle is not a proxy", nameof(proxyHandle));

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
		/// <param name="initEmptyBuffer">Affects the behavior if <see cref="IDrawableData.GetVerticesData"/> provides an empty span. If set to <see langword="false"/>, it will not modify the buffer. If <see langword="true"/>, memory will be allocated for the buffer without filling with data.</param>
		public static unsafe void ReuploadDrawable(this IRenderAPI rapi, IDrawableHandle handle, IDrawableData data, bool updateVertexDeclaration = false, bool initEmptyBuffer = true)
		{
			var container = (RefContainer)handle;
			if(container.proxyFor != null) throw new ArgumentException("Unable to update data with proxy, use original handle instead", nameof(handle));

			container.indicesCount = data.IndicesCount;

			try
			{
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
					for(int i = 0; i < vBuffersCount; i++)
					{
						UploadVertBuffer(container, data, i, initEmptyBuffer, true, attribPointers);
					}
					GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

					container.attribPointers = attribPointers.ToArray();
					GL.BindVertexArray(0);
				}
				else
				{
					for(int i = 0; i < vBuffersCount; i++)
					{
						UploadVertBuffer(container, data, i, initEmptyBuffer, true, null);
					}
					GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
				}

				GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
				UploadIndBuffer(container, data, initEmptyBuffer, true);
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			}
			catch
			{
				GL.BindVertexArray(0);
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
				throw;
			}
		}

		/// <summary>
		/// Updates the vertex and index data for the given handle.
		/// If <see cref="IDrawableData.GetVerticesData"/> provides an empty span, then the data will not be modified.
		/// If the number of vertices or indexes has changed since <see cref="UploadDrawable"/>, use <see cref="ReuploadDrawable"/> instead.
		/// </summary>
		public static unsafe void UpdateDrawable(this IRenderAPI rapi, IDrawableHandle handle, IDrawableData data)
		{
			var container = (RefContainer)handle;
			if(container.proxyFor != null) throw new ArgumentException("Unable to update data with proxy, use original handle instead", nameof(handle));

			if(data.VerticesCount > 0)
			{
				try
				{
					int vBuffersCount = data.VertexBuffersCount;
					for(int i = 0; i < vBuffersCount; i++)
					{
						UpdateVertBuffer(container, data, i, 0);
					}
				}
				finally
				{
					GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
				}
			}

			if(data.IndicesCount > 0)
			{
				try
				{
					GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
					UpdateIndBuffer(container, data, 0);
				}
				finally
				{
					GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
				}
			}
		}

		/// <summary>
		/// Updates part of the data buffers.
		/// If <see cref="IDrawableData.GetVerticesData"/> provides an empty span, then the data will not be modified.
		/// </summary>
		/// <param name="indicesBufferOffset">From which buffer element to start the update</param>
		/// <param name="verticesBufferOffsets">From which buffer element to start the update. Size must be at least <see cref="IDrawableInfo.VertexBuffersCount"/>.</param>
		public static unsafe void UpdateDrawablePart(this IRenderAPI rapi, IDrawableHandle handle, IDrawableData data, int indicesBufferOffset, params int[] verticesBufferOffsets)
		{
			var container = (RefContainer)handle;
			if(container.proxyFor != null) throw new ArgumentException("Unable to update data with proxy, use original handle instead", nameof(handle));

			if(data.VerticesCount > 0)
			{
				try
				{
					int vBuffersCount = data.VertexBuffersCount;
					for(int i = 0; i < vBuffersCount; i++)
					{
						UpdateVertBuffer(container, data, i, (uint)verticesBufferOffsets[i]);
					}
				}
				finally
				{
					GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
				}
			}

			if(data.IndicesCount > 0)
			{
				try
				{
					GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
					UpdateIndBuffer(container, data, (uint)indicesBufferOffset);
				}
				finally
				{
					GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
				}
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
			for(int i = 0; i < len; i++)//TODO: enable/disable attributes only when uploading/updating vao attributes
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

		private static unsafe void UploadIndBuffer(RefContainer container, IDrawableData data, bool initEmptyBuffer, bool invalidateBuffer)
		{
			var indData = data.GetIndicesData();
			if(indData.IsEmpty)
			{
				if(initEmptyBuffer)
				{
					GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(4 * container.indicesCount), (IntPtr)null, data.GetIndicesMeta().IsDynamic ? BufferUsageHint.DynamicDraw : BufferUsageHint.StaticDraw);
				}
				else if(invalidateBuffer)
				{
					GL.InvalidateBufferData(container.indexBuffer);
				}
			}
			else
			{
				if((uint)indData.Length < container.indicesCount)
				{
					throw new Exception("The size of the index buffer must not be less than the indices count");
				}
				fixed(uint* ptr = indData)
				{
					GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(4 * container.indicesCount), (IntPtr)ptr, data.GetIndicesMeta().IsDynamic ? BufferUsageHint.DynamicDraw : BufferUsageHint.StaticDraw);
				}
			}
		}

		private static unsafe void UploadVertBuffer(RefContainer container, IDrawableData data, int buffIndex, bool initEmptyBuffer, bool invalidateBuffer, List<int> attribPointers)
		{
			var meta = data.GetVertexBufferMeta(buffIndex);
			var buffData = data.GetVerticesData(buffIndex);
			var vertCount = data.VerticesCount;
			if(buffData.IsEmpty)
			{
				if(initEmptyBuffer)
				{
					GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffers[buffIndex]);
					GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(vertCount * (uint)meta.Stride), (IntPtr)null, meta.IsDynamic ? BufferUsageHint.DynamicDraw : BufferUsageHint.StaticDraw);
				}
				else if(invalidateBuffer)
				{
					GL.InvalidateBufferData(container.vertexBuffers[buffIndex]);
				}
			}
			else
			{
				if((uint)meta.Stride * (uint)buffData.Length < vertCount)
				{
					throw new Exception("The size of the vertex buffer must not be less than the product of the vertices count and the stride");
				}
				GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffers[buffIndex]);
				fixed(byte* ptr = buffData)
				{
					GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(vertCount * (uint)meta.Stride), (IntPtr)ptr, meta.IsDynamic ? BufferUsageHint.DynamicDraw : BufferUsageHint.StaticDraw);
				}
			}
			if(attribPointers != null) InitDeclaration(meta.Declaration, attribPointers);
		}

		private static unsafe void UpdateVertBuffer(RefContainer container, IDrawableData data, int buffIndex, uint offset)
		{
			var buffData = data.GetVerticesData(buffIndex);
			if(buffData.IsEmpty) return;

			var meta = data.GetVertexBufferMeta(buffIndex);
			var vertCount = data.VerticesCount;

			if((uint)meta.Stride * (uint)buffData.Length < vertCount)
			{
				throw new Exception("The size of the vertex buffer must not be less than the product of the vertices count and the stride");
			}

			GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffers[buffIndex]);
			fixed(byte* ptr = buffData)
			{
				GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)(offset * (uint)meta.Stride), (IntPtr)(vertCount * (uint)meta.Stride), (IntPtr)ptr);
			}
		}

		private static unsafe void UpdateIndBuffer(RefContainer container, IDrawableData data, uint offset)
		{
			var buffData = data.GetIndicesData();
			if(buffData.IsEmpty) return;

			if((uint)buffData.Length < container.indicesCount)
			{
				throw new Exception("The size of the index buffer must not be less than the indices count");
			}
			fixed(uint* ptr = buffData)
			{
				GL.BufferSubData(BufferTarget.ElementArrayBuffer, (IntPtr)(4 * offset), (IntPtr)(4 * container.indicesCount), (IntPtr)ptr);
			}
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
	}

	public interface IDrawableHandle : IDisposable
	{
		bool Initialized { get; }

		bool Disposed { get; }
	}
}