using Unity.Mathematics;

namespace PowerOfMind.Systems.RenderBatching
{
	public interface IBlockDataProvider
	{
		/// <summary>
		/// Provides the block data for the batcher.
		/// Sometimes can be called for invalid coordinates, as it is processed in a different thread.
		/// </summary>
		void ProvideData(int3 pos, IBatchBuildContext context);

		/// <summary>
		/// Returns mask of block sides that should be culled.
		/// Sometimes can be called for invalid coordinates, as it is processed in a different thread.
		/// </summary>
		int GetCullSides(int3 pos);
	}
}