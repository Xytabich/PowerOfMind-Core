using System.Runtime.CompilerServices;
using Vintagestory.API.Client;

namespace PowerOfMind.Graphics.Drawable
{
	public interface IDrawableData
	{
		EnumDrawMode DrawMode { get; }

		int IndicesCount { get; }

		int VerticesCount { get; }

		int VertexBuffersCount { get; }

		void ProvideIndices(IndicesContext context);

		void ProvideVertices(VerticesContext context);
	}

	public readonly ref struct IndicesContext
	{
		public unsafe delegate void ProcessorDelegate(int* indices, bool isDynamic);

		public readonly bool ProvideDynamicOnly;

		private readonly ProcessorDelegate processor;

		public IndicesContext(ProcessorDelegate processor, bool provideDynamicOnly)
		{
			this.processor = processor;
			ProvideDynamicOnly = provideDynamicOnly;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe void Process(int* indices, bool isDynamic)
		{
			processor(indices, isDynamic);
		}
	}

	public readonly ref struct VerticesContext
	{
		public unsafe delegate void ProcessorDelegate(int bufferIndex, void* data, int stride, VertexDeclaration declaration, bool isDynamic);

		public readonly bool ProvideDynamicOnly;

		private readonly ProcessorDelegate processor;

		public VerticesContext(ProcessorDelegate processor, bool provideDynamicOnly)
		{
			this.processor = processor;
			ProvideDynamicOnly = provideDynamicOnly;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe void Process(int bufferIndex, void* data, int stride, VertexDeclaration declaration, bool isDynamic)
		{
			processor(bufferIndex, data, stride, declaration, isDynamic);
		}
	}
}