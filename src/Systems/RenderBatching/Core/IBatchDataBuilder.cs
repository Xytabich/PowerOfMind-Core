using PowerOfMind.Graphics;

namespace PowerOfMind.Systems.RenderBatching
{
	public interface IBatchDataBuilder<TVertex, TUniform>
		where TVertex : unmanaged, IVertexStruct
		where TUniform : unmanaged, IUniformsData
	{
		/// <summary>
		/// Builds the drawable data for the batcher.
		/// Called on a separate thread.
		/// </summary>
		void Build(IBatchBuildContext context);

		/// <summary>
		/// Provides default data for vertices and uniforms.
		/// Called on a separate thread.
		/// </summary>
		void GetDefaultData(out TVertex vertexDefault, out TUniform uniformsDefault);
	}
}