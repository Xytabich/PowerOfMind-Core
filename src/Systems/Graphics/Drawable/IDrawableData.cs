using System;
using Vintagestory.API.Client;

namespace PowerOfMind.Graphics.Drawable
{
	public interface IDrawableData : IDrawableInfo
	{
		ReadOnlySpan<uint> GetIndicesData();

		ReadOnlySpan<byte> GetVerticesData(int bufferIndex);
	}

	public interface IDrawableInfo
	{
		EnumDrawMode DrawMode { get; }

		uint IndicesCount { get; }

		uint VerticesCount { get; }

		int VertexBuffersCount { get; }

		IndicesMeta GetIndicesMeta();

		VertexBufferMeta GetVertexBufferMeta(int index);
	}

	public readonly struct IndicesMeta
	{
		public readonly bool IsDynamic;

		public IndicesMeta(bool isDynamic)
		{
			IsDynamic = isDynamic;
		}
	}

	public readonly struct VertexBufferMeta
	{
		public readonly VertexDeclaration Declaration;
		public readonly int Stride;
		public readonly bool IsDynamic;

		public VertexBufferMeta(VertexDeclaration declaration, int stride, bool isDynamic)
		{
			Declaration = declaration;
			Stride = stride;
			IsDynamic = isDynamic;
		}
	}
}