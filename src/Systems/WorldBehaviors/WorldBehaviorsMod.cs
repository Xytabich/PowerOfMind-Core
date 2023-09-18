using HarmonyLib;
using PowerOfMind.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reflection;
using Unity.Mathematics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.Server;

namespace PowerOfMind.Systems.WorldBehaviors
{
	public partial class WorldBehaviorsMod : ModSystem, IShutDownMonitor//TODO: database tables for storing data per each chunk/map/column and modid
	{
		private const string PATCH_NAME = "powerofmind:worldbehaviors";

		private static int clientSystemIndex = -1;
		private static int serverSystemIndex = -1;

		bool IShutDownMonitor.ShuttingDown => sapi == null ? false : sapi.Server.IsShuttingDown;

		private BehaviorContainer<IChunkBehavior, (long id, int3 index, IWorldChunk chunk)> chunks;
		private BehaviorContainer<IMapChunkBehavior, (int2 index, IMapChunk chunk)> mapChunks;
		private BehaviorContainer<IMapRegionBehavior, (int2 index, IMapRegion region)> mapRegions;

		private ICoreAPI api;
		private ICoreServerAPI sapi = null;
		private Harmony harmony = null;

		private WorldBehaviorsMod()
		{
			chunks = new(this, (beh, data) => beh.Initialize(api, data.id, data.index, data.chunk), beh => beh.OnUnloaded());
			mapChunks = new(this, (beh, data) => beh.Initialize(api, data.index, data.chunk), beh => beh.OnUnloaded());
			mapRegions = new(this, (beh, data) => beh.Initialize(api, data.index, data.region), beh => beh.OnUnloaded());
		}

		public override void Start(ICoreAPI api)
		{
			this.api = api;
			sapi = api as ICoreServerAPI;
			UpdateSystemIndex(api);

			harmony = new Harmony(PATCH_NAME);
			if(api.Side == EnumAppSide.Client)
			{
				harmony.Patch(SymbolExtensions.GetMethodInfo((SystemUnloadChunks s) => s.Dispose),
					prefix: HarmonyExt.GetHarmonyMethod(() => ClientUnloadChunksDisposePrefix));
				harmony.Patch(HarmonyExt.GetMethodInfo<SystemUnloadChunks>("HandleChunkUnload", BindingFlags.Instance),
					transpiler: HarmonyExt.GetHarmonyMethod(() => ClientHandleChunkUnloadTranspiler));
				harmony.Patch(HarmonyExt.GetMethodInfo<SystemNetworkProcess>("ProcessInBackground", BindingFlags.Instance),
					postfix: HarmonyExt.GetHarmonyMethod(() => ClientProcessInBackgroundPostfix));
			}
			else
			{
				var serverLoadAndSaveSystem = typeof(ServerSystem).Assembly.GetType("Vintagestory.Server.ServerSystemLoadAndSaveGame");
				harmony.Patch(HarmonyExt.GetMethodInfo(serverLoadAndSaveSystem, "SaveAllDirtyLoadedChunks", BindingFlags.Instance),
					transpiler: HarmonyExt.GetHarmonyMethod(() => ServerSaveChunksTranspiler));
				harmony.Patch(HarmonyExt.GetMethodInfo(serverLoadAndSaveSystem, "SaveAllDirtyMapChunks", BindingFlags.Instance),
					prefix: HarmonyExt.GetHarmonyMethod(() => ServerSaveMapChunksPrefix));
				harmony.Patch(HarmonyExt.GetMethodInfo(serverLoadAndSaveSystem, "SaveAllDirtyMapRegions", BindingFlags.Instance),
					prefix: HarmonyExt.GetHarmonyMethod(() => ServerSaveMapRegionsPrefix));

				harmony.Patch(SymbolExtensions.GetMethodInfo((ConnectedClient c) => c.SetChunkSent), prefix: HarmonyExt.GetHarmonyMethod(() => ServerSetChunkSentPrefix));
				harmony.Patch(SymbolExtensions.GetMethodInfo((ConnectedClient c) => c.SetMapChunkSent), prefix: HarmonyExt.GetHarmonyMethod(() => ServerSetMapChunkSentPrefix));
				harmony.Patch(SymbolExtensions.GetMethodInfo((ConnectedClient c) => c.SetMapRegionSent), prefix: HarmonyExt.GetHarmonyMethod(() => ServerSetMapRegionSentPrefix));
				harmony.Patch(SymbolExtensions.GetMethodInfo((ConnectedClient c) => c.RemoveChunkSent), prefix: HarmonyExt.GetHarmonyMethod(() => ServerRemoveChunkSentPrefix));
				harmony.Patch(SymbolExtensions.GetMethodInfo((ConnectedClient c) => c.RemoveMapChunkSent), prefix: HarmonyExt.GetHarmonyMethod(() => ServerRemoveMapChunkSentPrefix));
				harmony.Patch(SymbolExtensions.GetMethodInfo((ConnectedClient c) => c.RemoveMapRegionSent), prefix: HarmonyExt.GetHarmonyMethod(() => ServerRemoveMapRegionSentPrefix));
			}
		}

		public override void StartClientSide(ICoreClientAPI api)
		{
			UpdateSystemIndex(api);

			var evtManager = (ClientEventManager)typeof(ClientMain).GetField("eventManager", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(api.World);
			evtManager.OnChunkLoaded += OnChunkLoaded;
			api.Event.ChunkDirty += OnChunkDirty;
			api.Event.MapRegionLoaded += OnRegionLoaded;
			api.Event.MapRegionUnloaded += OnRegionUnloaded;
		}

		public override void StartServerSide(ICoreServerAPI api)
		{
			UpdateSystemIndex(api);

			api.Event.ChunkColumnLoaded += OnChunkColumnLoaded;
			api.Event.ChunkColumnUnloaded += OnChunkColumnUnloaded;
			api.Event.ChunkDirty += OnChunkDirty;
			api.Event.MapRegionLoaded += OnRegionLoaded;
			api.Event.MapRegionUnloaded += OnRegionUnloaded;
		}

		public override void AssetsLoaded(ICoreAPI api)
		{
			UpdateSystemIndex(api);
		}

		public override void AssetsFinalize(ICoreAPI api)
		{
			UpdateSystemIndex(api);
		}

		public override void Dispose()
		{
			harmony?.UnpatchAll(PATCH_NAME);
			harmony = null;
		}

		/// <summary>
		/// Registers behavior for a chunk
		/// </summary>
		/// <returns>Id of behavior</returns>
		public int RegisterChunkBehavior<T>() where T : class, IChunkBehavior, new()
		{
			return chunks.AddBehavior<T>();
		}

		/// <summary>
		/// Registers behavior for a map chunk
		/// </summary>
		/// <returns>Id of behavior</returns>
		public int RegisterMapChunkBehavior<T>() where T : class, IMapChunkBehavior, new()
		{
			return mapChunks.AddBehavior<T>();
		}

		/// <summary>
		/// Registers behavior for a map region
		/// </summary>
		/// <returns>Id of behavior</returns>
		public int RegisterMapRegionBehavior<T>() where T : class, IMapRegionBehavior, new()
		{
			return mapRegions.AddBehavior<T>();
		}

		/// <summary>
		/// Returns a list of behaviors for the chunk, if any presented
		/// </summary>
		/// <param name="index">3d index of chunk</param>
		/// <returns>List of behaviors or null if the chunk is not found or there are no behaviors for it</returns>
		public IChunkBehavior[] GetChunkBehaviors(long index)
		{
			return chunks.TryGet(index, out var behaviors) ? behaviors : null;
		}

		/// <summary>
		/// Returns a list of behaviors for the map chunk, if any presented
		/// </summary>
		/// <param name="index">2d index of map chunk</param>
		/// <returns>List of behaviors or null if the map chunk is not found or there are no behaviors for it</returns>
		public IMapChunkBehavior[] GetMapChunkBehaviors(long index)
		{
			return mapChunks.TryGet(index, out var behaviors) ? behaviors : null;
		}

		/// <summary>
		/// Returns a list of behaviors for the map region, if any presented
		/// </summary>
		/// <param name="index">2d index of map region</param>
		/// <returns>List of behaviors or null if the map region is not found or there are no behaviors for it</returns>
		public IMapRegionBehavior[] GetMapRegionBehaviors(long index)
		{
			return mapRegions.TryGet(index, out var behaviors) ? behaviors : null;
		}

		/// <summary>
		/// Returns a list of behaviors for the chunk, if any presented
		/// </summary>
		/// <param name="index">3d index of chunk</param>
		/// <returns>List of behaviors or null if the chunk is not found or there are no behaviors for it</returns>
		public IChunkBehavior[] GetChunkBehaviors(int3 index)
		{
			if(chunks.isEmpty) return null;
			var acc = api.World.BlockAccessor;
			var id = MapUtil.Index3dL(index.x, index.y, index.z, acc.MapSizeX / acc.ChunkSize, acc.MapSizeZ / acc.ChunkSize);
			return GetChunkBehaviors(id);
		}

		/// <summary>
		/// Returns a list of behaviors for the map chunk, if any presented
		/// </summary>
		/// <param name="index">2d index of map chunk</param>
		/// <returns>List of behaviors or null if the map chunk is not found or there are no behaviors for it</returns>
		public IMapChunkBehavior[] GetMapChunkBehaviors(int2 index)
		{
			if(mapChunks.isEmpty) return null;
			var acc = api.World.BlockAccessor;
			var id = MapUtil.Index2dL(index.x, index.y, acc.MapSizeX / acc.ChunkSize);
			return GetMapChunkBehaviors(id);
		}

		/// <summary>
		/// Returns a list of behaviors for the map region, if any presented
		/// </summary>
		/// <param name="index">2d index of map region</param>
		/// <returns>List of behaviors or null if the map region is not found or there are no behaviors for it</returns>
		public IMapRegionBehavior[] GetMapRegionBehaviors(int2 index)
		{
			if(mapRegions.isEmpty) return null;
			var acc = api.World.BlockAccessor;
			var id = MapUtil.Index2dL(index.x, index.y, acc.RegionMapSizeX);
			return GetMapRegionBehaviors(id);
		}

		private void UpdateSystemIndex(ICoreAPI api)
		{
			if(api.Side == EnumAppSide.Client)
			{
				clientSystemIndex = CommonExt.GetModSystemIndex(api.ModLoader, this);
			}
			else
			{
				serverSystemIndex = CommonExt.GetModSystemIndex(api.ModLoader, this);
			}
		}

		private void OnRegionLoaded(Vec2i mapCoord, IMapRegion region)
		{
			if(mapRegions.noBehaviors) return;

			var acc = api.World.BlockAccessor;
			var id = MapUtil.Index2dL(mapCoord.X, mapCoord.Y, acc.RegionMapSizeX);
			if(mapRegions.GetOrCreate(id, (new int2(mapCoord.X, mapCoord.Y), region), out var behaviors))
			{
				foreach(var beh in behaviors)
				{
					beh?.OnLoaded();
				}
			}
		}

		private void OnRegionUnloaded(Vec2i mapCoord, IMapRegion region)
		{
			if(mapRegions.isEmpty) return;

			var acc = api.World.BlockAccessor;
			var id = MapUtil.Index2dL(mapCoord.X, mapCoord.Y, acc.RegionMapSizeX);
			mapRegions.RemoveDispose(id);
		}

		private void OnChunkColumnLoaded(Vec2i chunkCoord, IWorldChunk[] worldChunks)
		{
			if(chunks.hasBehaviors)
			{
				ChunkColumnLoadedImpl(api, chunkCoord, worldChunks, ref chunks);
			}

			if(mapChunks.hasBehaviors)
			{
				MapChunkLoadedImpl(api, chunkCoord, ref mapChunks);
			}

			static void ChunkColumnLoadedImpl(ICoreAPI api, Vec2i chunkCoord, IWorldChunk[] worldChunks,
				ref BehaviorContainer<IChunkBehavior, (long id, int3 index, IWorldChunk chunk)> chunks)
			{
				var acc = api.World.BlockAccessor;
				int height = acc.MapSizeY;
				int mcsx = acc.MapSizeX / acc.ChunkSize;
				int mcsz = acc.MapSizeZ / acc.ChunkSize;
				for(int i = 0; i < height; i++)
				{
					var chunk = worldChunks[i];
					if(chunk == null) continue;

					var id = MapUtil.Index3dL(chunkCoord.X, i, chunkCoord.Y, mcsx, mcsz);
					if(chunks.GetOrCreate(id, (id, new int3(chunkCoord.X, i, chunkCoord.Y), chunk), out var behaviors))
					{
						foreach(var beh in behaviors)
						{
							beh?.OnLoaded();
						}
					}
				}
			}

			static void MapChunkLoadedImpl(ICoreAPI api, Vec2i chunkCoord, ref BehaviorContainer<IMapChunkBehavior, (int2 index, IMapChunk chunk)> mapChunks)
			{
				var acc = api.World.BlockAccessor;
				var id = MapUtil.Index2dL(chunkCoord.X, chunkCoord.Y, acc.MapSizeX / acc.ChunkSize);

				var mapChunk = acc.GetMapChunk(chunkCoord);
				if(mapChunk == null) return;

				if(mapChunks.GetOrCreate(id, (new int2(chunkCoord.X, chunkCoord.Y), mapChunk), out var behaviors))
				{
					foreach(var beh in behaviors)
					{
						beh?.OnLoaded();
					}
				}
			}
		}

		private void OnChunkColumnUnloaded(Vec3i chunkCoord)
		{
			if(chunkCoord.Y != 0) return;

			if(!chunks.isEmpty)
			{
				ChunkUnloadImpl(api, chunkCoord, ref chunks);
			}

			if(!mapChunks.isEmpty)
			{
				var acc = api.World.BlockAccessor;
				mapChunks.RemoveDispose(MapUtil.Index2dL(chunkCoord.X, chunkCoord.Z, acc.MapSizeX / acc.ChunkSize));
			}

			static void ChunkUnloadImpl(ICoreAPI api, Vec3i chunkCoord, ref BehaviorContainer<IChunkBehavior, (long id, int3 index, IWorldChunk chunk)> chunks)
			{
				var acc = api.World.BlockAccessor;
				int height = acc.MapSizeY;
				int mcsx = acc.MapSizeX / acc.ChunkSize;
				int mcsz = acc.MapSizeZ / acc.ChunkSize;
				for(int i = 0; i < height; i++)
				{
					chunks.RemoveDispose(MapUtil.Index3dL(chunkCoord.X, i, chunkCoord.Z, mcsx, mcsz));
				}
			}
		}

		private void OnChunkDirty(Vec3i chunkCoord, IWorldChunk chunk, EnumChunkDirtyReason reason)
		{
			if(chunks.noBehaviors) return;

			ChunkDirtyImpl(api, chunkCoord, chunk, reason, ref chunks);

			static void ChunkDirtyImpl(ICoreAPI api, Vec3i chunkCoord, IWorldChunk chunk, EnumChunkDirtyReason reason,
				ref BehaviorContainer<IChunkBehavior, (long id, int3 index, IWorldChunk chunk)> chunks)
			{
				var acc = api.World.BlockAccessor;
				var id = MapUtil.Index3dL(chunkCoord.X, chunkCoord.Y, chunkCoord.Z, acc.MapSizeX / acc.ChunkSize, acc.MapSizeZ / acc.ChunkSize);

				IChunkBehavior[] behaviors;
				if(reason == EnumChunkDirtyReason.NewlyCreated || reason == EnumChunkDirtyReason.NewlyLoaded)
				{
					if(chunks.GetOrCreate(id, (id, new int3(chunkCoord.X, chunkCoord.Y, chunkCoord.Z), chunk), out behaviors))
					{
						foreach(var beh in behaviors)
						{
							beh?.OnDirty(reason);
						}
					}
				}
				else
				{
					if(chunks.TryGet(id, out behaviors))
					{
						foreach(var beh in behaviors)
						{
							beh?.OnDirty(reason);
						}
					}
				}
			}
		}

		private void OnChunkLoaded(Vec3i chunkCoord)
		{
			if(chunks.noBehaviors) return;

			ChunkLoadedImpl(api, chunkCoord, ref chunks);

			static void ChunkLoadedImpl(ICoreAPI api, Vec3i chunkCoord, ref BehaviorContainer<IChunkBehavior, (long id, int3 index, IWorldChunk chunk)> chunks)
			{
				var acc = api.World.BlockAccessor;
				var id = MapUtil.Index3dL(chunkCoord.X, chunkCoord.Y, chunkCoord.Z, acc.MapSizeX / acc.ChunkSize, acc.MapSizeZ / acc.ChunkSize);
				var chunk = acc.GetChunk(id);
				if(chunk == null) return;

				if(chunks.GetOrCreate(id, (id, new int3(chunkCoord.X, chunkCoord.Y, chunkCoord.Z), chunk), out var behaviors))
				{
					foreach(var beh in behaviors)
					{
						beh?.OnLoaded();
					}
				}
			}
		}

		private void OnClientMapChunkLoaded(int2 chunkCoord)
		{
			if(mapChunks.noBehaviors) return;

			ChunkMapLoadedImpl(api, chunkCoord, ref mapChunks);

			static void ChunkMapLoadedImpl(ICoreAPI api, int2 index, ref BehaviorContainer<IMapChunkBehavior, (int2 index, IMapChunk chunk)> mapChunks)
			{
				var acc = api.World.BlockAccessor;
				var mapChunk = acc.GetMapChunk(index.x, index.y);
				if(mapChunk == null) return;

				var id = MapUtil.Index2dL(index.x, index.y, acc.MapSizeX / acc.ChunkSize);
				if(mapChunks.GetOrCreate(id, (new int2(index.x, index.y), mapChunk), out var behaviors))
				{
					foreach(var beh in behaviors)
					{
						beh?.OnLoaded();
					}
				}
			}
		}

		private void OnClientMapChunkUnload(Vec2i chunkCoord)
		{
			if(!mapChunks.isEmpty)
			{
				var acc = api.World.BlockAccessor;
				var id = MapUtil.Index2dL(chunkCoord.X, chunkCoord.Y, acc.MapSizeX / acc.ChunkSize);
				mapChunks.RemoveDispose(id);
			}
		}

		private void OnClientChunksUnload()
		{
			chunks.Dispose();
			mapChunks.Dispose();
		}

		private void OnClientChunkUnload(long id)
		{
			chunks.RemoveDispose(id);
		}

		private interface ICtorContainer<T>
		{
			T Create();
		}

		private class CtorContainer<T, TI> : ICtorContainer<TI> where T : class, TI, new()
		{
			TI ICtorContainer<TI>.Create()
			{
				return new T();
			}
		}

		private struct BehaviorContainer<T, TInitData>
		{
			public FastRWLock locker;

			public bool hasBehaviors => behaviors != null;
			public bool noBehaviors => behaviors == null;

			public bool isEmpty => instances == null;

			private List<ICtorContainer<T>> behaviors;
			private Dictionary<long, T[]> instances;

			private readonly Action<T, TInitData> initializer;
			private readonly Action<T> disposer;

			public BehaviorContainer(WorldBehaviorsMod mod, Action<T, TInitData> initializer, Action<T> disposer)
			{
				this.initializer = initializer;
				this.disposer = disposer;
				locker = new FastRWLock(mod);

				behaviors = null;
				instances = null;
			}

			public int AddBehavior<TInstance>() where TInstance : class, T, new()
			{
				(behaviors ??= new()).Add(new CtorContainer<TInstance, T>());
				return behaviors.Count - 1;
			}

			public bool GetOrCreate(long id, TInitData data, out T[] instanceBehaviors)
			{
				instanceBehaviors = null;

				bool addNew = instances == null;
				if(instances != null)
				{
					locker.AcquireReadLock();
					if(locker.monitor.ShuttingDown) return false;
					addNew = !instances.TryGetValue(id, out instanceBehaviors);
					locker.ReleaseReadLock();
				}

				if(addNew)
				{
					if(instances == null)
					{
						locker.AcquireWriteLock();
						if(locker.monitor.ShuttingDown) return false;
						instances = new();
						locker.ReleaseWriteLock();
					}

					instanceBehaviors = ArrayPool<T>.Shared.Rent(behaviors.Count);
					for(int j = 0; j < behaviors.Count; j++)
					{
						var beh = behaviors[j].Create();
						try
						{
							initializer(beh, data);
							instanceBehaviors[j] = beh;
						}
						catch { }
					}
					locker.AcquireWriteLock();
					if(locker.monitor.ShuttingDown) return false;
					instances[id] = instanceBehaviors;
					locker.ReleaseWriteLock();
				}

				return true;
			}

			public bool TryGet(long id, out T[] instanceBehaviors)
			{
				if(instances == null)
				{
					instanceBehaviors = null;
					return false;
				}

				locker.AcquireReadLock();
				bool isFound = instances.TryGetValue(id, out instanceBehaviors);
				locker.ReleaseReadLock();

				return isFound;
			}

			public void RemoveDispose(long id)
			{
				if(instances == null) return;

				locker.AcquireReadLock();
				bool isFound = instances.TryGetValue(id, out var instanceBehaviors);
				locker.AcquireWriteLock();
				if(isFound)
				{
					foreach(var beh in instanceBehaviors)
					{
						if(beh != null) disposer(beh);
					}
					ArrayPool<T>.Shared.Return(instanceBehaviors);
				}

				locker.AcquireWriteLock();
				if(locker.monitor.ShuttingDown) return;
				if(isFound) instances.Remove(id);
				locker.ReleaseWriteLock();
			}

			public void Dispose()
			{
				if(instances == null) return;

				locker.AcquireWriteLock();
				foreach(var pair in instances)
				{
					foreach(var beh in pair.Value)
					{
						if(beh != null) disposer(beh);
					}
					ArrayPool<T>.Shared.Return(pair.Value);
				}
				instances = null;
				locker.ReleaseWriteLock();
			}
		}
	}
}