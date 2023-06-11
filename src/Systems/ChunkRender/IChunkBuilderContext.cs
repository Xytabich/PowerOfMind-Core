using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Drawable;

namespace PowerOfMind.Systems.ChunkRender
{
	public interface IChunkBuilderContext
	{
		/// <summary>
		/// Adds drawable data to the chunk, only static data will be added, dynamic data will be ignored
		/// </summary>
		/// <param name="uniformsData">Additional data by which rendering will be grouped. For example MAIN_TEXTURE.</param>
		unsafe void AddData<T>(IDrawableData data, in T uniformsData) where T : unmanaged, IUniformsData;

		/// <summary>
		/// Adds drawable data to the chunk, only static data will be added, dynamic data will be ignored
		/// </summary>
		void AddData(IDrawableData data);
	}
}