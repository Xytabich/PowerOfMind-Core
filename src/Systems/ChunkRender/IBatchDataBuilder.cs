namespace PowerOfMind.Systems.ChunkRender
{
	public interface IBatchDataBuilder
	{
		/// <summary>
		/// Builds the drawable data for the chunk. Called on a separate thread.
		/// </summary>
		void Build(IBatchBuildContext context);
	}
}