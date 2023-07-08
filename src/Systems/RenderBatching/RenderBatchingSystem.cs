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

		private Dictionary<BatcherKey, object> batchers = null;
		private Dictionary<int3, List<Action<int3>>> chunkDirtyListeners = null;
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

		public override bool ShouldLoad(EnumAppSide forSide)
		{
			return forSide == EnumAppSide.Client;
		}

		public override void Dispose()
		{
			base.Dispose();
			(ChunkBatcher as IDisposable)?.Dispose();
			harmony?.UnpatchAll("powerofmind:renderbatch");
		}

		public void RegisterChunkDirtyListener(int3 coord, Action<int3> callback)
		{
			if(chunkDirtyListeners == null) chunkDirtyListeners = new Dictionary<int3, List<Action<int3>>>();
			if(!chunkDirtyListeners.TryGetValue(coord, out var callbacks))
			{
				callbacks = new List<Action<int3>>();
				chunkDirtyListeners[coord] = callbacks;
			}
			if(!callbacks.Contains(callback)) callbacks.Add(callback);
		}

		public void UnregisterChunkDirtyListener(int3 coord, Action<int3> callback)
		{
			if(chunkDirtyListeners == null) return;
			if(chunkDirtyListeners.TryGetValue(coord, out var callbacks))
			{
				if(callbacks.Remove(callback))
				{
					if(callbacks.Count == 0) chunkDirtyListeners.Remove(coord);
				}
			}
		}

		/// <summary>
		/// Returns a batcher for the given parameters.
		/// Batchers must have a full set of vertex and uniform parameters for compatibility.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="shaderPass">Shader pass name</param>
		/// <param name="ctor">Create batcher if it doesn't exist</param>
		/// <returns>Batcher instance</returns>
		public T GetBatcher<T>(string shaderPass, System.Func<T> ctor = null) where T : class
		{
			if(batchers == null && ctor == null) return null;
			var key = new BatcherKey(shaderPass, typeof(T));
			if(batchers == null) batchers = new Dictionary<BatcherKey, object>();
			else if(batchers.TryGetValue(key, out var instance))
			{
				return instance as T;
			}
			if(ctor == null) return null;

			var inst = ctor();
			batchers[key] = inst;
			return inst;
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
			int3 coord;
			if(Thread.CurrentThread.ManagedThreadId == RuntimeEnv.MainThreadId)
			{
				var mod = (RenderBatchingSystem)___game.Api.ObjectCache["powerofmind:renderbatchsystem"];
				if(mod.chunkDirtyListeners == null) return;
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
					var mod = (RenderBatchingSystem)___game.Api.ObjectCache["powerofmind:renderbatchsystem"];
					if(mod.chunkDirtyListeners == null) return;
					if(mod.chunkDirtyListeners.Count > 0) mod.OnChunkDirty(coord);
				}, "powerofmind:renderbatch-chunkdirty");
			}
		}

		private static void MarkChunkDirtyPrefix(int cx, int cy, int cz, ClientMain ___game)
		{
			if(Thread.CurrentThread.ManagedThreadId == RuntimeEnv.MainThreadId)
			{
				var mod = (RenderBatchingSystem)___game.Api.ObjectCache["powerofmind:renderbatchsystem"];
				if(mod.chunkDirtyListeners == null) return;
				if(mod.chunkDirtyListeners.Count > 0)
				{
					mod.OnChunkDirty(new int3(cx, cy, cz));
				}
			}
			else
			{
				var coord = new int3(cx, cy, cz);
				___game.EnqueueMainThreadTask(() => {
					var mod = (RenderBatchingSystem)___game.Api.ObjectCache["powerofmind:renderbatchsystem"];
					if(mod.chunkDirtyListeners == null) return;
					if(mod.chunkDirtyListeners.Count > 0) mod.OnChunkDirty(coord);
				}, "powerofmind:renderbatch-chunkdirty");
			}
		}

		private readonly struct BatcherKey : IEquatable<BatcherKey>
		{
			public readonly string shaderName;
			public readonly Type type;

			public BatcherKey(string shaderName, Type type)
			{
				this.shaderName = shaderName;
				this.type = type;
			}

			public override bool Equals(object obj)
			{
				return obj is BatcherKey key && Equals(key);
			}

			public bool Equals(BatcherKey other)
			{
				return shaderName == other.shaderName && type == other.type;
			}

			public override int GetHashCode()
			{
				return HashCode.Combine(shaderName, type);
			}
		}
	}
}