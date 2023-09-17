using Unity.Mathematics;
using Vintagestory.API.Common;

namespace PowerOfMind.Systems.WorldBehaviors
{
	public interface IMapRegionBehavior
	{
		void Initialize(ICoreAPI api, int2 index, IMapRegion region);

		/// <summary>
		/// Called when the region has been loaded.
		/// On the client side, it is called every time data is received from the server.
		/// </summary>
		void OnLoaded();

		/// <summary>
		/// Called when the map region has been unloaded.
		/// </summary>
		void OnUnloaded();

		/// <summary>
		/// Called when the map region data is starts saving.
		/// Can be called outside the main thread.
		/// </summary>
		void OnSaving();
	}
}