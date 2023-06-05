﻿using PowerOfMind.Graphics.Drawable;
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

		EnumDrawMode IDrawableData.DrawMode => DrawMode;
		int IDrawableData.IndicesCount => IndicesCount;
		int IDrawableData.VerticesCount => VerticesCount;
		int IDrawableData.VertexBuffersCount => 1;

		public MeshData(T[] vertices, int[] indices)
		{
			Vertices = vertices;
			VerticesCount = vertices.Length;

			Indices = indices;
			IndicesCount = indices.Length;

			VertexDeclaration = vertices[0].GetDeclaration();
		}

		unsafe void IDrawableData.ProvideIndices(IndicesContext context)
		{
			fixed(int* ptr = Indices)
			{
				context.Process(ptr + IndicesOffset, !IndicesStatic);
			}
		}

		unsafe void IDrawableData.ProvideVertices(VerticesContext context)
		{
			fixed(T* ptr = Vertices)
			{
				context.Process(0, ptr + VerticesOffset, sizeof(T), VertexDeclaration, !VerticesStatic);
			}
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

		EnumDrawMode IDrawableData.DrawMode => DrawMode;
		int IDrawableData.IndicesCount => IndicesCount;
		int IDrawableData.VerticesCount => VerticesCount;
		int IDrawableData.VertexBuffersCount => 1;

		public MeshData(TStatic[] staticVertices, TDynamic[] dynamicVertices, int[] indices)
		{
			StaticVertices = staticVertices;
			DynamicVertices = dynamicVertices;
			VerticesCount = StaticVertices.Length;

			Indices = indices;
			IndicesCount = indices.Length;

			StaticVertexDeclaration = staticVertices[0].GetDeclaration();
		}

		unsafe void IDrawableData.ProvideIndices(IndicesContext context)
		{
			fixed(int* ptr = Indices)
			{
				context.Process(ptr + IndicesOffset, !IndicesStatic);
			}
		}

		unsafe void IDrawableData.ProvideVertices(VerticesContext context)
		{
			fixed(T* ptr = Vertices)
			{
				context.Process(0, ptr + VerticesOffset, sizeof(T), VertexDeclaration, !VerticesStatic);
			}
		}
	}
}