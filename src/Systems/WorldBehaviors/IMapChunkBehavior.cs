using Unity.Mathematics;
using Vintagestory.API.Common;

namespace PowerOfMind.Systems.WorldBehaviors
{
	public interface IMapChunkBehavior
	{
		void Initialize(ICoreAPI api, int2 index, IMapChunk chunk);

		/// <summary>
		/// Called when the map chunk has been loaded.
		/// On the client side, it is called every time data is received from the server.
		/// </summary>
		void OnLoaded();

		/// <summary>
		/// Called when the map chunk has been unloaded.
		/// </summary>
		void OnUnload();

		/// <summary>
		/// Called when the map chunk data is starts saving.
		/// Can be called outside the main thread.
		/// </summary>
		void OnSaving();
	}
}