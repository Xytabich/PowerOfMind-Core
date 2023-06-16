namespace PowerOfMind.Systems.RenderBatching
{
	public interface IBatchDataBuilder
	{
		/// <summary>
		/// Builds the drawable data for the batcher. Called on a separate thread.
		/// </summary>
		void Build(IBatchBuildContext context);
	}
}