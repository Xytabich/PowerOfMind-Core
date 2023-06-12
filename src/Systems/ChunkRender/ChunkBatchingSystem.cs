using PowerOfMind.Graphics;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PowerOfMind.Systems.ChunkRender
{
	public class ChunkBatchingSystem : ModSystem
	{
		public ChunkBatchRender ChunkBatcher { get; private set; }

		public override void StartClientSide(ICoreClientAPI api)
		{
			ChunkBatcher = new ChunkBatchRender(api, api.ModLoader.GetModSystem<GraphicsSystem>());
		}

		public override void Dispose()
		{
			base.Dispose();
			(ChunkBatcher as IDisposable)?.Dispose();
		}
	}
}