using System.Runtime.CompilerServices;
using Vintagestory.API.Client;

namespace PowerOfMind.Graphics.Drawable
{
	public interface IDrawableData : IDrawableInfo
	{
		void ProvideIndices(IndicesContext context);

		void ProvideVertices(VerticesContext context);
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
		public readonly bool IsDynamic;

		public VertexBufferMeta(VertexDeclaration declaration, bool isDynamic)
		{
			Declaration = declaration;
			IsDynamic = isDynamic;
		}
	}

	public readonly ref struct IndicesContext
	{
		/// <summary>
		/// Provide indices to the processor
		/// </summary>
		/// <param name="indices">Pointer to indices, or null. If null is specified, no data will be copied</param>
		public unsafe delegate void ProcessorDelegate(uint* indices);

		private readonly ProcessorDelegate processor;

		public IndicesContext(ProcessorDelegate processor)
		{
			this.processor = processor;
		}

		/// <summary>
		/// Provide indices to the processor
		/// </summary>
		/// <param name="indices">Pointer to indices, or null. If null is specified, no data will be copied</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe void Process(uint* indices)
		{
			processor(indices);
		}

		/// <summary>
		/// Returns a reference to the processor.
		/// Be careful, the reference must be immediately set to null after use to avoid memory leaks.
		/// </summary>
		public ProcessorDelegate GetProcessor()
		{
			return processor;
		}
	}

	public readonly ref struct VerticesContext
	{
		/// <summary>
		/// Provide vertices data to the processor
		/// </summary>
		/// <param name="indices">Pointer to vertices data, or null. If null is specified, no data will be copied</param>
		public unsafe delegate void ProcessorDelegate(void* indices, int stride);

		public readonly int BufferIndex;

		private readonly ProcessorDelegate processor;

		public VerticesContext(ProcessorDelegate processor, int bufferIndex)
		{
			this.processor = processor;
			BufferIndex = bufferIndex;
		}

		/// <summary>
		/// Provide data to the processor
		/// </summary>
		/// <param name="data">Pointer to data, or null. If null is specified, no data will be copied</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe void Process(void* data, int stride)
		{
			processor(data, stride);
		}

		/// <summary>
		/// Returns a reference to the processor.
		/// Be careful, the reference must be immediately set to null after use to avoid memory leaks.
		/// </summary>
		public ProcessorDelegate GetProcessor()
		{
			return processor;
		}
	}
}