using PowerOfMind.Graphics;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PowerOfMind.Systems.RenderBatching
{
	public class RenderBatchingSystem : ModSystem
	{
		public ChunkBatching ChunkBatcher { get; private set; }

		public override void StartClientSide(ICoreClientAPI api)
		{
			ChunkBatcher = new ChunkBatching(api, api.ModLoader.GetModSystem<GraphicsSystem>());
		}

		public override void Dispose()
		{
			base.Dispose();
			(ChunkBatcher as IDisposable)?.Dispose();
		}
	}
}