using PowerOfMind.Graphics;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PowerOfMind.Systems.ChunkRender
{
	public class ChunkRenderSystem : ModSystem
	{
		public ChunkRenderer ChunkRenderer { get; private set; }

		public override void StartClientSide(ICoreClientAPI api)
		{
			ChunkRenderer = new ChunkRenderer(api, api.ModLoader.GetModSystem<GraphicsSystem>());
		}

		public override void Dispose()
		{
			base.Dispose();
			(ChunkRenderer as IDisposable)?.Dispose();
		}
	}
}