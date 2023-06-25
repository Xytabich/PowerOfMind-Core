using HarmonyLib;
using PowerOfMind.Graphics;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Unity.Mathematics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

namespace PowerOfMind.Systems.RenderBatching
{
	public class RenderBatchingSystem : ModSystem
	{
		public ChunkBatching ChunkBatcher { get; private set; }

		private Dictionary<int3, List<Action<int3>>> chunkDirtyListeners = new Dictionary<int3, List<Action<int3>>>();
		private Harmony harmony = null;

		public override void StartClientSide(ICoreClientAPI api)
		{
			ChunkBatcher = new ChunkBatching(api, api.ModLoader.GetModSystem<GraphicsSystem>());

			api.ObjectCache["powerofmind:renderbatchsystem"] = this;

			harmony = new Harmony("powerofmind:renderbatch");
			harmony.Patch(typeof(ClientWorldMap).GetMethod(nameof(ClientWorldMap.SetChunkDirty)),
				new HarmonyMethod(typeof(RenderBatchingSystem).GetMethod(nameof(SetChunkDirtyPrefix), BindingFlags.NonPublic | BindingFlags.Static)));
			harmony.Patch(typeof(ClientWorldMap).GetMethod(nameof(ClientWorldMap.MarkChunkDirty)),
				new HarmonyMethod(typeof(RenderBatchingSystem).GetMethod(nameof(MarkChunkDirtyPrefix), BindingFlags.NonPublic | BindingFlags.Static)));
		}

		public override void Dispose()
		{
			base.Dispose();
			(ChunkBatcher as IDisposable)?.Dispose();
			harmony?.UnpatchAll("powerofmind:renderbatch");
		}

		public void RegisterChunkDirtyListener(int3 coord, Action<int3> callback)
		{
			if(!chunkDirtyListeners.TryGetValue(coord, out var callbacks))
			{
				callbacks = new List<Action<int3>>();
				chunkDirtyListeners[coord] = callbacks;
			}
			if(!callbacks.Contains(callback)) callbacks.Add(callback);
		}

		public void UnregisterChunkDirtyListener(int3 coord, Action<int3> callback)
		{
			if(chunkDirtyListeners.TryGetValue(coord, out var callbacks))
			{
				if(callbacks.Remove(callback))
				{
					if(callbacks.Count == 0) chunkDirtyListeners.Remove(coord);
				}
			}
		}

		private void OnChunkDirty(int3 coord)
		{
			if(chunkDirtyListeners.TryGetValue(coord, out var callbacks))
			{
				for(int i = callbacks.Count - 1; i >= 0; i--)
				{
					callbacks[i].Invoke(coord);
				}
			}
		}

		private static void SetChunkDirtyPrefix(long index3d, ClientWorldMap __instance, ClientMain ___game)
		{
			var mod = (RenderBatchingSystem)___game.Api.ObjectCache["powerofmind:renderbatchsystem"];
			int3 coord;
			if(Thread.CurrentThread.ManagedThreadId == RuntimeEnv.MainThreadId)
			{
				if(mod.chunkDirtyListeners.Count > 0)
				{
					coord.x = (int)(index3d % __instance.chunkMapSizeXFast);
					coord.y = (int)(index3d / ((long)__instance.chunkMapSizeXFast * (long)__instance.chunkMapSizeZFast));
					coord.z = (int)(index3d / __instance.chunkMapSizeXFast % __instance.chunkMapSizeZFast);

					mod.OnChunkDirty(coord);
				}
			}
			else
			{
				coord.x = (int)(index3d % __instance.chunkMapSizeXFast);
				coord.y = (int)(index3d / ((long)__instance.chunkMapSizeXFast * (long)__instance.chunkMapSizeZFast));
				coord.z = (int)(index3d / __instance.chunkMapSizeXFast % __instance.chunkMapSizeZFast);

				___game.EnqueueMainThreadTask(() => {
					if(mod.chunkDirtyListeners.Count > 0) mod.OnChunkDirty(coord);
				}, "powerofmind:renderbatch-chunkdirty");
			}
		}

		private static void MarkChunkDirtyPrefix(int cx, int cy, int cz, ClientMain ___game)
		{
			var mod = (RenderBatchingSystem)___game.Api.ObjectCache["powerofmind:renderbatchsystem"];
			if(Thread.CurrentThread.ManagedThreadId == RuntimeEnv.MainThreadId)
			{
				if(mod.chunkDirtyListeners.Count > 0)
				{
					mod.OnChunkDirty(new int3(cx, cy, cz));
				}
			}
			else
			{
				var coord = new int3(cx, cy, cz);
				___game.EnqueueMainThreadTask(() => {
					if(mod.chunkDirtyListeners.Count > 0) mod.OnChunkDirty(coord);
				}, "powerofmind:renderbatch-chunkdirty");
			}
		}
	}
}