using OpenTK.Graphics.OpenGL;
using System;
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
		public static unsafe IMeshHandle UploadMesh<T>(this IRenderAPI rapi, MeshData<T> meshData) where T : unmanaged, IVertexStruct
		{
			var container = new RefContainer();
			container.indicesCount = meshData.IndicesCount;
			container.declaration = meshData.VertexDeclaration;
			switch(meshData.drawMode)
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
			GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffer);
			fixed(T* ptr = meshData.Vertices)
			{
				GL.BufferData(BufferTarget.ArrayBuffer, meshData.VerticesCount * sizeof(T), (IntPtr)(ptr + meshData.VerticesOffset),
					meshData.VerticesStatic ? BufferUsageHint.StaticDraw : BufferUsageHint.DynamicDraw);
			}
			var attributes = meshData.VertexDeclaration.Attributes;
			for(int i = attributes.Length - 1; i >= 0; i--)
			{
				ref readonly var attrib = ref attributes[i];
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
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
			fixed(int* ptr = meshData.Indices)
			{
				GL.BufferData(BufferTarget.ElementArrayBuffer, 4 * meshData.IndicesCount, (IntPtr)(ptr + meshData.IndicesOffset),
					meshData.IndicesStatic ? BufferUsageHint.StaticDraw : BufferUsageHint.DynamicDraw);
			}
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			GL.BindVertexArray(0);
			return container;
		}

		/// <summary>
		/// Recreates the data buffers for the given MeshRef, this may be needed if the number of vertices or indexes has changed.
		/// </summary>
		public static unsafe void ReuploadMesh<T>(this IRenderAPI rapi, IMeshHandle meshRef, MeshData<T> meshData) where T : unmanaged, IVertexStruct
		{
			var container = (RefContainer)meshRef;
			container.indicesCount = meshData.IndicesCount;

			GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffer);
			fixed(T* ptr = meshData.Vertices)
			{
				GL.BufferData(BufferTarget.ArrayBuffer, meshData.VerticesCount * sizeof(T), (IntPtr)(ptr + meshData.VerticesOffset),
					meshData.VerticesStatic ? BufferUsageHint.StaticDraw : BufferUsageHint.DynamicDraw);
			}
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
			fixed(int* ptr = meshData.Indices)
			{
				GL.BufferData(BufferTarget.ElementArrayBuffer, 4 * meshData.IndicesCount, (IntPtr)(ptr + meshData.IndicesOffset),
					meshData.IndicesStatic ? BufferUsageHint.StaticDraw : BufferUsageHint.DynamicDraw);
			}
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
		}

		/// <summary>
		/// Updates the vertex and index data for the given MeshRef.
		/// If the number of vertices or indexes has changed since <see cref="UploadMesh"/>, use <see cref="ReuploadMesh"/> instead.
		/// </summary>
		public static unsafe void UpdateMesh<T>(this IRenderAPI rapi, IMeshHandle meshRef, MeshData<T> meshData) where T : unmanaged, IVertexStruct
		{
			var container = (RefContainer)meshRef;

			GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffer);
			fixed(T* ptr = meshData.Vertices)
			{
				GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, meshData.VerticesCount * sizeof(T), (IntPtr)(ptr + meshData.VerticesOffset));
			}
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
			fixed(int* ptr = meshData.Indices)
			{
				GL.BufferSubData(BufferTarget.ElementArrayBuffer, (IntPtr)0, 4 * meshData.IndicesCount, (IntPtr)(ptr + meshData.IndicesOffset));
			}
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
		}

		/// <summary>
		/// Updates part of the mesh's data.
		/// </summary>
		/// <param name="verticesBufferOffset">From which buffer element to start the update</param>
		/// <param name="indicesBufferOffset">From which buffer element to start the update</param>
		public static unsafe void UpdateMeshPart<T>(this IRenderAPI rapi, IMeshHandle meshRef, MeshData<T> meshData, int verticesBufferOffset, int indicesBufferOffset) where T : unmanaged, IVertexStruct
		{
			var container = (RefContainer)meshRef;

			if(meshData.VerticesCount > 0)
			{
				GL.BindBuffer(BufferTarget.ArrayBuffer, container.vertexBuffer);
				fixed(T* ptr = meshData.Vertices)
				{
					GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)(verticesBufferOffset * sizeof(T)),
						meshData.VerticesCount * sizeof(T), (IntPtr)(ptr + meshData.VerticesOffset));
				}
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			}

			if(meshData.IndicesCount > 0)
			{
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
				fixed(int* ptr = meshData.Indices)
				{
					GL.BufferSubData(BufferTarget.ElementArrayBuffer, (IntPtr)(indicesBufferOffset * 4),
						4 * meshData.IndicesCount, (IntPtr)(ptr + meshData.IndicesOffset));
				}
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			}
		}

		public static void RenderMesh(this IRenderAPI rapi, IMeshHandle meshRef)
		{
			RuntimeStats.drawCallsCount++;
			if(!meshRef.Initialized)
			{
				throw new ArgumentException("Fatal: Trying to render an uninitialized mesh");
			}
			if(meshRef.Disposed)
			{
				throw new ArgumentException("Fatal: Trying to render a disposed mesh");
			}

			var container = (RefContainer)meshRef;
			GL.BindVertexArray(container.vao);

			var attributes = container.declaration.Attributes;
			int len = attributes.Length;
			for(int i = 0; i < len; i++)
			{
				GL.EnableVertexAttribArray(attributes[i].Location);
			}
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, container.indexBuffer);
			GL.DrawElements(container.drawMode, container.indicesCount, DrawElementsType.UnsignedInt, 0);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			for(int i = 0; i < len; i++)
			{
				GL.DisableVertexAttribArray(attributes[i].Location);
			}
			GL.BindVertexArray(0);
		}

		private class RefContainer : IMeshHandle
		{
			public int vao, vertexBuffer, indexBuffer;
			public VertexDeclaration declaration;
			public BeginMode drawMode;
			public int indicesCount;

			public bool Initialized => vao != 0;
			public bool Disposed => disposed;

			private bool disposed = false;

			public RefContainer()
			{
				indexBuffer = GL.GenBuffer();
				vertexBuffer = GL.GenBuffer();
				vao = GL.GenVertexArray();
			}

			public void Dispose()
			{
				if(!disposed)
				{
					GL.DeleteVertexArray(vao);
					GL.DeleteBuffer(vertexBuffer);
					GL.DeleteBuffer(indexBuffer);

					vao = 0;
					indexBuffer = 0;
					vertexBuffer = 0;

					disposed = true;
				}
			}
		}
	}

	public interface IMeshHandle : IDisposable
	{
		bool Initialized { get; }

		bool Disposed { get; }
	}
}