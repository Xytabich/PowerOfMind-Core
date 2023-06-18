using System.Runtime.CompilerServices;
using Vintagestory.API.Client;

namespace PowerOfMind.Graphics.Drawable
{
	public interface IDrawableData
	{
		EnumDrawMode DrawMode { get; }

		uint IndicesCount { get; }

		uint VerticesCount { get; }

		int VertexBuffersCount { get; }

		void ProvideIndices(IndicesContext context);

		void ProvideVertices(VerticesContext context);
	}

	public readonly ref struct IndicesContext
	{
		/// <summary>
		/// Provide indices to the processor
		/// </summary>
		/// <param name="indices">Pointer to indices, or null. If null is specified, no data will be copied</param>
		public unsafe delegate void ProcessorDelegate(uint* indices, bool isDynamic);

		public readonly bool ProvideDynamicOnly;

		private readonly ProcessorDelegate processor;

		public IndicesContext(ProcessorDelegate processor, bool provideDynamicOnly)
		{
			this.processor = processor;
			ProvideDynamicOnly = provideDynamicOnly;
		}

		/// <summary>
		/// Provide indices to the processor
		/// </summary>
		/// <param name="indices">Pointer to indices, or null. If null is specified, no data will be copied</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe void Process(uint* indices, bool isDynamic)
		{
			processor(indices, isDynamic);
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
		public readonly bool ProvideDynamicOnly;

		private readonly IProcessor processor;

		public VerticesContext(IProcessor processor, bool provideDynamicOnly)
		{
			this.processor = processor;
			ProvideDynamicOnly = provideDynamicOnly;
		}

		/// <summary>
		/// Provide data to the processor
		/// </summary>
		/// <param name="data">Pointer to data, or null. If null is specified, no data will be copied</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe void Process<T>(int bufferIndex, T* data, VertexDeclaration declaration, int stride, bool isDynamic) where T : unmanaged//TODO: maybe add bufferIndex field to context instead? so that the mesh does not enumerates buffers, but the processor itself requests the buffer by index
		{
			processor.Process(bufferIndex, data, declaration, stride, isDynamic);
		}

		/// <summary>
		/// Returns a reference to the processor.
		/// Be careful, the reference must be immediately set to null after use to avoid memory leaks.
		/// </summary>
		public IProcessor GetProcessor()
		{
			return processor;
		}

		public interface IProcessor
		{
			/// <summary>
			/// Provide data to the processor
			/// </summary>
			/// <param name="data">Pointer to data, or null. If null is specified, no data will be copied</param>
			unsafe void Process<T>(int bufferIndex, T* data, VertexDeclaration declaration, int stride, bool isDynamic) where T : unmanaged;
		}
	}
}