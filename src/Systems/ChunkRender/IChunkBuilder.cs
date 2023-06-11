namespace PowerOfMind.Systems.ChunkRender
{
	public interface IChunkBuilder
	{
		/// <summary>
		/// Builds the drawable data for the chunk. Called on a separate thread.
		/// </summary>
		void Build(IChunkBuilderContext context);
	}
}