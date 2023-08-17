using System;
using Vintagestory.API.Client;

namespace PowerOfMind.Graphics.Drawable
{
	public interface IDrawableData : IDrawableInfo
	{
		/// <summary>
		/// Returns indexes.
		/// Span may be empty if no data is provided.
		/// Otherwise, Span can be any size, but not less than <see cref="IDrawableInfo.IndicesCount"/>.
		/// </summary>
		ReadOnlySpan<uint> GetIndicesData();

		/// <summary>
		/// Returns indexes.
		/// Span may be empty if no data is provided.
		/// Otherwise, Span can be any size, but not less than (<see cref="IDrawableInfo.VerticesCount"/> * <see cref="VertexBufferMeta.Stride"/>).
		/// </summary>
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
		/// <summary>
		/// <see langword="true"/> if the data will be updated frequently, <see langword="false"/> if the data is static and rarely changes or does not change at all
		/// </summary>
		public readonly bool IsDynamic;

		public IndicesMeta(bool isDynamic)
		{
			IsDynamic = isDynamic;
		}
	}

	public readonly struct VertexBufferMeta
	{
		public readonly VertexDeclaration Declaration;
		/// <summary>
		/// Data stride in IDrawableData, i.e. how many bytes one vertex takes.
		/// </summary>
		public readonly int Stride;
		/// <summary>
		/// <see langword="true"/> if the data will be updated frequently, <see langword="false"/> if the data is static and rarely changes or does not change at all
		/// </summary>
		public readonly bool IsDynamic;

		public VertexBufferMeta(VertexDeclaration declaration, int stride, bool isDynamic)
		{
			Declaration = declaration;
			Stride = stride;
			IsDynamic = isDynamic;
		}
	}
}