using Unity.Mathematics;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace PowerOfMind.Systems.WorldBehaviors
{
	public interface IChunkBehavior
	{
		void Initialize(ICoreAPI api, long id, int3 index, IWorldChunk chunk);

		/// <summary>
		/// Called when the chunk has been loaded.
		/// On the client side, it is called every time data is received from the server.
		/// </summary>
		void OnLoaded();

		/// <summary>
		/// Called when the chunk has been unloaded.
		/// </summary>
		void OnUnloaded();

		/// <summary>
		/// Called when the state of a chunk changes.
		/// Can be called on the client before <see cref="OnLoaded"/> if the chunk was loaded for the first time.
		/// </summary>
		void OnDirty(EnumChunkDirtyReason reason);

		/// <summary>
		/// Called when the chunk data is saved.
		/// Can be called outside the main thread.
		/// </summary>
		void OnSaved();

		/// <summary>
		/// Called when the player has started tracking a chunk (i.e. data has started to be sent).
		/// </summary>
		void OnStartTrackingByPlayer(IServerPlayer player);

		/// <summary>
		/// Called when the player has stopped tracking a chunk (i.e. changes will no longer be sent).
		/// </summary>
		void OnEndTrackingByPlayer(IServerPlayer player);
	}
}