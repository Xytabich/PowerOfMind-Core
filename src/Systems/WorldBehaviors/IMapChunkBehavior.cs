using Unity.Mathematics;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

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
		void OnUnloaded();

		/// <summary>
		/// Called when the map chunk data is saved.
		/// Can be called outside the main thread.
		/// </summary>
		void OnSaved();

		/// <summary>
		/// Called when the player has started tracking a map chunk (i.e. data has started to be sent).
		/// </summary>
		void OnStartTrackingByPlayer(IServerPlayer player);

		/// <summary>
		/// Called when the player has stopped tracking a map chunk (i.e. changes will no longer be sent).
		/// </summary>
		void OnEndTrackingByPlayer(IServerPlayer player);
	}
}