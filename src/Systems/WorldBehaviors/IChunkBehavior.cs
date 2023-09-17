using Unity.Mathematics;
using Vintagestory.API.Common;

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
		void OnUnload();

		void OnDirty(EnumChunkDirtyReason reason);

		/// <summary>
		/// Called when the chunk data is starts saving.
		/// Can be called outside the main thread.
		/// </summary>
		void OnSaving();
	}
}