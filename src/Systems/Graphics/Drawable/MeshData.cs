using PowerOfMind.Graphics.Drawable;
using System;
using System.Runtime.InteropServices;
using Vintagestory.API.Client;

namespace PowerOfMind.Graphics
{
	public class MeshData<T> : IDrawableData where T : unmanaged, IVertexStruct
	{
		public EnumDrawMode DrawMode;

		public T[] Vertices;
		public int VerticesCount = 0;
		/// <summary>
		/// Offset in <see cref="Vertices"/> from which the vertices of this mesh start
		/// </summary>
		public int VerticesOffset = 0;
		public bool VerticesStatic = true;

		public int[] Indices;
		public int IndicesCount = 0;
		/// <summary>
		/// Offset in <see cref="Indices"/> from which the indices of this mesh start
		/// </summary>
		public int IndicesOffset = 0;
		public bool IndicesStatic = true;

		public VertexDeclaration VertexDeclaration;

		EnumDrawMode IDrawableInfo.DrawMode => DrawMode;
		uint IDrawableInfo.IndicesCount => (uint)IndicesCount;
		uint IDrawableInfo.VerticesCount => (uint)VerticesCount;
		int IDrawableInfo.VertexBuffersCount => 1;

		public MeshData(T[] vertices, int[] indices)
		{
			Vertices = vertices;
			VerticesCount = vertices.Length;

			Indices = indices;
			IndicesCount = indices.Length;

			VertexDeclaration = vertices[0].GetDeclaration();
		}

		public MeshData(T[] vertices, int[] indices, VertexDeclaration vertexDeclaration)
		{
			Vertices = vertices;
			VerticesCount = vertices.Length;

			Indices = indices;
			IndicesCount = indices.Length;

			VertexDeclaration = vertexDeclaration;
		}

		ReadOnlySpan<uint> IDrawableData.GetIndicesData()
		{
			if(Indices == null) return default;
			return MemoryMarshal.Cast<int, uint>(Indices.AsSpan(IndicesOffset, IndicesCount));
		}

		ReadOnlySpan<byte> IDrawableData.GetVerticesData(int bufferIndex)
		{
			if(Vertices == null) return default;
			return MemoryMarshal.AsBytes(Vertices.AsSpan(VerticesOffset, VerticesCount));
		}

		IndicesMeta IDrawableInfo.GetIndicesMeta()
		{
			return new IndicesMeta(!IndicesStatic);
		}

		unsafe VertexBufferMeta IDrawableInfo.GetVertexBufferMeta(int index)
		{
			return new VertexBufferMeta(VertexDeclaration, sizeof(T), !VerticesStatic);
		}
	}

	public class MeshData<TStatic, TDynamic> : IDrawableData where TStatic : unmanaged, IVertexStruct where TDynamic : unmanaged, IVertexStruct
	{
		public EnumDrawMode DrawMode;

		public TStatic[] StaticVertices;
		public TDynamic[] DynamicVertices;
		public int VerticesCount = 0;
		/// <summary>
		/// Offset in <see cref="Vertices"/> from which the vertices of this mesh start
		/// </summary>
		public int VerticesOffset = 0;

		public int[] Indices;
		public int IndicesCount = 0;
		/// <summary>
		/// Offset in <see cref="Indices"/> from which the indices of this mesh start
		/// </summary>
		public int IndicesOffset = 0;
		public bool IndicesStatic = true;

		public VertexDeclaration StaticVertexDeclaration;
		public VertexDeclaration DynamicVertexDeclaration;

		EnumDrawMode IDrawableInfo.DrawMode => DrawMode;
		uint IDrawableInfo.IndicesCount => (uint)IndicesCount;
		uint IDrawableInfo.VerticesCount => (uint)VerticesCount;
		int IDrawableInfo.VertexBuffersCount => 1;

		public MeshData(TStatic[] staticVertices, TDynamic[] dynamicVertices, int[] indices)
		{
			StaticVertices = staticVertices;
			DynamicVertices = dynamicVertices;
			VerticesCount = StaticVertices.Length;

			Indices = indices;
			IndicesCount = indices.Length;

			StaticVertexDeclaration = staticVertices[0].GetDeclaration();
			DynamicVertexDeclaration = dynamicVertices[0].GetDeclaration();
		}

		ReadOnlySpan<uint> IDrawableData.GetIndicesData()
		{
			if(Indices == null) return default;
			return MemoryMarshal.Cast<int, uint>(Indices.AsSpan(IndicesOffset, IndicesCount));
		}

		ReadOnlySpan<byte> IDrawableData.GetVerticesData(int bufferIndex)
		{
			if(bufferIndex == 0)
			{
				if(StaticVertices == null) return default;
				return MemoryMarshal.AsBytes(StaticVertices.AsSpan(VerticesOffset, VerticesCount));
			}
			else
			{
				if(DynamicVertices == null) return default;
				return MemoryMarshal.AsBytes(DynamicVertices.AsSpan(VerticesOffset, VerticesCount));
			}
		}

		IndicesMeta IDrawableInfo.GetIndicesMeta()
		{
			return new IndicesMeta(!IndicesStatic);
		}

		unsafe VertexBufferMeta IDrawableInfo.GetVertexBufferMeta(int index)
		{
			if(index == 0)
			{
				return new VertexBufferMeta(StaticVertexDeclaration, sizeof(TStatic), false);
			}
			else
			{
				return new VertexBufferMeta(DynamicVertexDeclaration, sizeof(TDynamic), true);
			}
		}
	}
}