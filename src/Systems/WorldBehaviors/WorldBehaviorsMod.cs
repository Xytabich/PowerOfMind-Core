using HarmonyLib;
using PowerOfMind.Utils;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Mathematics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;

namespace PowerOfMind.Systems.WorldBehaviors
{
	public class WorldBehaviorsMod : ModSystem
	{
		private const string PATCH_NAME = "powerofmind:worldbehaviors";

		private static int clientSystemIndex = -1;
		private static int serverSystemIndex = -1;

		private List<ICtorContainer<IChunkBehavior>> chunkBehaviors = null;
		private List<ICtorContainer<IMapChunkBehavior>> mapChunkBehaviors = null;
		private List<ICtorContainer<IMapRegionBehavior>> mapRegionBehaviors = null;

		private Dictionary<long, IChunkBehavior[]> chunks = null;//TODO: in offthread get using try-catch
		private Dictionary<long, IMapChunkBehavior[]> mapChunks = null;
		private Dictionary<long, IMapRegionBehavior[]> mapRegions = null;

		private ICoreAPI api;
		private Harmony harmony = null;

		public override void Start(ICoreAPI api)
		{
			UpdateSystemIndex(api);
			if(api.Side == EnumAppSide.Client)
			{
				harmony = new Harmony(PATCH_NAME);
				harmony.Patch(SymbolExtensions.GetMethodInfo((SystemUnloadChunks s) => s.Dispose),
					prefix: HarmonyExt.GetHarmonyMethod(() => ClientUnloadChunksDisposePrefix));
				harmony.Patch(HarmonyExt.GetMethodInfo<SystemUnloadChunks>("HandleChunkUnload", BindingFlags.Instance),
					transpiler: HarmonyExt.GetHarmonyMethod(() => ClientHandleChunkUnloadTranspiler));
				//TODO: map chunks
			}
			else
			{

			}
		}

		public override void StartClientSide(ICoreClientAPI api)
		{
			this.api = api;
			UpdateSystemIndex(api);

			var evtManager = (ClientEventManager)typeof(ClientMain).GetField("eventManager", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(api.World);
			evtManager.OnChunkLoaded += OnChunkLoaded;
			api.Event.ChunkDirty += OnChunkDirty;
			api.Event.MapRegionLoaded += OnRegionLoaded;
			api.Event.MapRegionUnloaded += OnRegionUnloaded;
		}

		public override void StartServerSide(ICoreServerAPI api)
		{
			this.api = api;
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
			(chunkBehaviors ??= new()).Add(new CtorContainer<T, IChunkBehavior>());
			return chunkBehaviors.Count - 1;
		}

		/// <summary>
		/// Registers behavior for a map chunk
		/// </summary>
		/// <returns>Id of behavior</returns>
		public int RegisterMapChunkBehavior<T>() where T : class, IMapChunkBehavior, new()
		{
			(mapChunkBehaviors ??= new()).Add(new CtorContainer<T, IMapChunkBehavior>());
			return mapChunkBehaviors.Count - 1;
		}

		/// <summary>
		/// Registers behavior for a map region
		/// </summary>
		/// <returns>Id of behavior</returns>
		public int RegisterMapRegionBehavior<T>() where T : class, IMapRegionBehavior, new()
		{
			(mapRegionBehaviors ??= new()).Add(new CtorContainer<T, IMapRegionBehavior>());
			return mapRegionBehaviors.Count - 1;
		}

		/// <summary>
		/// Returns a list of behaviors for the chunk, if any presented
		/// </summary>
		/// <param name="index">3d index of chunk</param>
		/// <returns>List of behaviors or null if the chunk is not found or there are no behaviors for it</returns>
		public IChunkBehavior[] GetChunkBehaviors(long index)
		{
			if(chunks != null && chunks.TryGetValue(index, out var behaviors)) return behaviors;
			return null;
		}

		/// <summary>
		/// Returns a list of behaviors for the map chunk, if any presented
		/// </summary>
		/// <param name="index">2d index of map chunk</param>
		/// <returns>List of behaviors or null if the map chunk is not found or there are no behaviors for it</returns>
		public IMapChunkBehavior[] GetMapChunkBehaviors(long index)
		{
			if(mapChunks != null && mapChunks.TryGetValue(index, out var behaviors)) return behaviors;
			return null;
		}

		/// <summary>
		/// Returns a list of behaviors for the map region, if any presented
		/// </summary>
		/// <param name="index">2d index of map region</param>
		/// <returns>List of behaviors or null if the map region is not found or there are no behaviors for it</returns>
		public IMapRegionBehavior[] GetMapRegionBehaviors(long index)
		{
			if(mapRegions != null && mapRegions.TryGetValue(index, out var behaviors)) return behaviors;
			return null;
		}

		/// <summary>
		/// Returns a list of behaviors for the chunk, if any presented
		/// </summary>
		/// <param name="index">3d index of chunk</param>
		/// <returns>List of behaviors or null if the chunk is not found or there are no behaviors for it</returns>
		public IChunkBehavior[] GetChunkBehaviors(int3 index)
		{
			var acc = api.World.BlockAccessor;
			var id = MapUtil.Index3dL(index.x, index.y, index.z, acc.MapSizeX / acc.ChunkSize, acc.MapSizeZ / acc.ChunkSize);
			if(chunks != null && chunks.TryGetValue(id, out var behaviors)) return behaviors;
			return null;
		}

		/// <summary>
		/// Returns a list of behaviors for the map chunk, if any presented
		/// </summary>
		/// <param name="index">2d index of map chunk</param>
		/// <returns>List of behaviors or null if the map chunk is not found or there are no behaviors for it</returns>
		public IMapChunkBehavior[] GetMapChunkBehaviors(int2 index)
		{
			var acc = api.World.BlockAccessor;
			var id = MapUtil.Index2dL(index.x, index.y, acc.MapSizeX / acc.ChunkSize);
			if(mapChunks != null && mapChunks.TryGetValue(id, out var behaviors)) return behaviors;
			return null;
		}

		/// <summary>
		/// Returns a list of behaviors for the map region, if any presented
		/// </summary>
		/// <param name="index">2d index of map region</param>
		/// <returns>List of behaviors or null if the map region is not found or there are no behaviors for it</returns>
		public IMapRegionBehavior[] GetMapRegionBehaviors(int2 index)
		{
			var acc = api.World.BlockAccessor;
			var id = MapUtil.Index2dL(index.x, index.y, acc.RegionMapSizeX);
			if(mapRegions != null && mapRegions.TryGetValue(id, out var behaviors)) return behaviors;
			return null;
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
			if(mapRegionBehaviors == null) return;

			RegionLoadedImpl(api, mapCoord, mapRegionBehaviors, mapRegions);

			static void RegionLoadedImpl(ICoreAPI api, Vec2i mapCoord,
				List<ICtorContainer<IMapRegionBehavior>> mapRegionBehaviors, Dictionary<long, IMapRegionBehavior[]> mapRegions)
			{
				var acc = api.World.BlockAccessor;
				var region = acc.GetMapRegion(mapCoord.X, mapCoord.Y);
				if(region == null) return;

				var id = MapUtil.Index2dL(mapCoord.X, mapCoord.Y, acc.RegionMapSizeX);
				if(mapRegions == null || !mapRegions.TryGetValue(id, out var behaviors))
				{
					if(mapRegions == null) mapRegions = new();

					var index = new int2(mapCoord.X, mapCoord.Y);
					behaviors = ArrayPool<IMapRegionBehavior>.Shared.Rent(mapRegionBehaviors.Count);
					for(int i = 0; i < mapRegionBehaviors.Count; i++)
					{
						var beh = mapRegionBehaviors[i].Create();
						behaviors[i].Initialize(api, index, region);
						behaviors[i] = beh;
					}
					mapRegions[id] = behaviors;
				}

				foreach(var beh in behaviors)
				{
					beh?.OnLoaded();
				}
			}
		}

		private void OnRegionUnloaded(Vec2i mapCoord, IMapRegion region)
		{
			if(mapRegions == null) return;

			var acc = api.World.BlockAccessor;
			var id = MapUtil.Index2dL(mapCoord.X, mapCoord.Y, acc.RegionMapSizeX);
			if(mapRegions.TryGetValue(id, out var behaviors))
			{
				mapRegions.Remove(id);
				foreach(var beh in behaviors)
				{
					beh?.OnUnload();
				}
				ArrayPool<IMapRegionBehavior>.Shared.Return(behaviors, true);
			}
		}

		private void OnChunkColumnLoaded(Vec2i chunkCoord, IWorldChunk[] worldChunks)
		{
			if(chunkBehaviors != null)
			{
				ChunkColumnLoadedImpl(api, chunkCoord, worldChunks, chunkBehaviors, chunks);
			}

			if(mapChunkBehaviors != null)
			{
				MapChunkLoadedImpl(api, chunkCoord, mapChunkBehaviors, mapChunks);
			}

			static void ChunkColumnLoadedImpl(ICoreAPI api, Vec2i chunkCoord, IWorldChunk[] worldChunks,
				List<ICtorContainer<IChunkBehavior>> chunkBehaviors, Dictionary<long, IChunkBehavior[]> chunks)
			{
				var acc = api.World.BlockAccessor;
				int height = acc.MapSizeY;
				int mcsx = acc.MapSizeX / acc.ChunkSize;
				int mcsz = acc.MapSizeZ / acc.ChunkSize;
				for(int i = 0; i < height; i++)
				{
					var chunk = worldChunks[i];
					if(chunk == null) continue;

					long id = MapUtil.Index3dL(chunkCoord.X, i, chunkCoord.Y, mcsx, mcsz);
					if(chunks == null || !chunks.TryGetValue(id, out var behaviors))
					{
						if(chunks == null) chunks = new();

						var index = new int3(chunkCoord.X, i, chunkCoord.Y);
						behaviors = ArrayPool<IChunkBehavior>.Shared.Rent(chunkBehaviors.Count);
						for(int j = 0; j < chunkBehaviors.Count; j++)
						{
							var beh = chunkBehaviors[j].Create();
							behaviors[j].Initialize(api, id, index, chunk);
							behaviors[j] = beh;
						}
						chunks[id] = behaviors;
					}

					foreach(var beh in behaviors)
					{
						beh?.OnLoaded();
					}
				}
			}

			static void MapChunkLoadedImpl(ICoreAPI api, Vec2i chunkCoord,
				List<ICtorContainer<IMapChunkBehavior>> mapChunkBehaviors, Dictionary<long, IMapChunkBehavior[]> mapChunks)
			{
				var acc = api.World.BlockAccessor;
				long id = MapUtil.Index2dL(chunkCoord.X, chunkCoord.Y, acc.MapSizeX / acc.ChunkSize);
				if(mapChunks == null || !mapChunks.TryGetValue(id, out var behaviors))
				{
					if(mapChunks == null) mapChunks = new();

					var mapChunk = acc.GetMapChunk(chunkCoord);
					var index = new int2(chunkCoord.X, chunkCoord.Y);
					behaviors = ArrayPool<IMapChunkBehavior>.Shared.Rent(mapChunkBehaviors.Count);
					for(int i = 0; i < mapChunkBehaviors.Count; i++)
					{
						var beh = mapChunkBehaviors[i].Create();
						behaviors[i].Initialize(api, index, mapChunk);
						behaviors[i] = beh;
					}
					mapChunks[id] = behaviors;
				}

				foreach(var beh in behaviors)
				{
					beh?.OnLoaded();
				}
			}
		}

		private void OnChunkColumnUnloaded(Vec3i chunkCoord)
		{
			if(chunkCoord.Y != 0) return;

			var acc = api.World.BlockAccessor;
			long id;
			if(chunks != null)
			{
				int height = acc.MapSizeY;
				int mcsx = acc.MapSizeX / acc.ChunkSize;
				int mcsz = acc.MapSizeZ / acc.ChunkSize;
				for(int i = 0; i < height; i++)
				{
					id = MapUtil.Index3dL(chunkCoord.X, i, chunkCoord.Z, mcsx, mcsz);
					if(chunks.TryGetValue(id, out var behaviors))
					{
						chunks.Remove(id);
						foreach(var beh in behaviors)
						{
							beh?.OnUnload();
						}
						ArrayPool<IChunkBehavior>.Shared.Return(behaviors, true);
					}
				}
			}

			if(mapChunks != null)
			{
				id = MapUtil.Index2dL(chunkCoord.X, chunkCoord.Z, acc.MapSizeX / acc.ChunkSize);
				if(mapChunks.TryGetValue(id, out var behaviors))
				{
					mapChunks.Remove(id);
					foreach(var beh in behaviors)
					{
						beh?.OnUnload();
					}
					ArrayPool<IMapChunkBehavior>.Shared.Return(behaviors, true);
				}
			}
		}

		private void OnChunkDirty(Vec3i chunkCoord, IWorldChunk chunk, EnumChunkDirtyReason reason)
		{
			if(chunks == null) return;

			var acc = api.World.BlockAccessor;
			var id = MapUtil.Index3dL(chunkCoord.X, chunkCoord.Y, chunkCoord.Z, acc.MapSizeX / acc.ChunkSize, acc.MapSizeZ / acc.ChunkSize);
			if(chunks.TryGetValue(id, out var behaviors))
			{
				foreach(var beh in behaviors)
				{
					beh?.OnDirty(reason);
				}
			}
		}

		private void OnChunkLoaded(Vec3i chunkCoord)
		{
			if(chunkBehaviors == null) return;

			ChunkLoadedImpl(api, chunkCoord, chunkBehaviors, chunks);

			static void ChunkLoadedImpl(ICoreAPI api, Vec3i chunkCoord,
				List<ICtorContainer<IChunkBehavior>> chunkBehaviors, Dictionary<long, IChunkBehavior[]> chunks)
			{
				var acc = api.World.BlockAccessor;
				var id = MapUtil.Index3dL(chunkCoord.X, chunkCoord.Y, chunkCoord.Z, acc.MapSizeX / acc.ChunkSize, acc.MapSizeZ / acc.ChunkSize);
				var chunk = acc.GetChunk(id);
				if(chunk == null) return;

				if(chunks == null || !chunks.TryGetValue(id, out var behaviors))
				{
					if(chunks == null) chunks = new();

					var index = new int3(chunkCoord.X, chunkCoord.Y, chunkCoord.Z);
					behaviors = ArrayPool<IChunkBehavior>.Shared.Rent(chunkBehaviors.Count);
					for(int i = 0; i < chunkBehaviors.Count; i++)
					{
						var beh = chunkBehaviors[i].Create();
						behaviors[i].Initialize(api, id, index, chunk);
						behaviors[i] = beh;
					}
					chunks[id] = behaviors;
				}

				foreach(var beh in behaviors)
				{
					beh?.OnLoaded();
				}
			}
		}

		private void OnClientMapChunkUnload(Vec2i chunkCoord)
		{
			if(mapChunks != null)
			{
				var acc = api.World.BlockAccessor;
				var id = MapUtil.Index2dL(chunkCoord.X, chunkCoord.Y, acc.MapSizeX / acc.ChunkSize);
				if(mapChunks.TryGetValue(id, out var behaviors))
				{
					mapChunks.Remove(id);
					foreach(var beh in behaviors)
					{
						beh?.OnUnload();
					}
					ArrayPool<IMapChunkBehavior>.Shared.Return(behaviors, true);
				}
			}
		}

		private void OnClientChunksUnload()
		{
			if(chunks != null)
			{
				foreach(var pair in chunks)
				{
					foreach(var beh in pair.Value)
					{
						beh?.OnUnload();
					}
					ArrayPool<IChunkBehavior>.Shared.Return(pair.Value, true);
				}
				chunks = null;
			}

			if(mapChunks != null)
			{
				foreach(var pair in mapChunks)
				{
					foreach(var beh in pair.Value)
					{
						beh?.OnUnload();
					}
					ArrayPool<IMapChunkBehavior>.Shared.Return(pair.Value, true);
				}
				mapChunks = null;
			}
		}

		private void OnClientChunkUnload(long id)
		{
			if(chunks != null)
			{
				if(chunks.TryGetValue(id, out var behaviors))
				{
					chunks.Remove(id);
					foreach(var beh in behaviors)
					{
						beh?.OnUnload();
					}
					ArrayPool<IChunkBehavior>.Shared.Return(behaviors, true);
				}
			}
		}

		private static void ClientUnloadChunksDisposePrefix(ClientMain game)
		{
			if(clientSystemIndex < 0) return;
			if(CommonExt.GetModSystemByIndex(game.Api.ModLoader, clientSystemIndex) is WorldBehaviorsMod mod)
			{
				mod.OnClientChunksUnload();
			}
		}

		private static void CallOnChunkUnloaded(ClientMain game, long id)
		{
			if(clientSystemIndex < 0) return;
			if(CommonExt.GetModSystemByIndex(game.Api.ModLoader, clientSystemIndex) is WorldBehaviorsMod mod)
			{
				mod.OnClientChunkUnload(id);
			}
		}

		private static void CallOnMapChunkUnloaded(ClientMain game, Vec2i coord)
		{
			if(clientSystemIndex < 0) return;
			if(CommonExt.GetModSystemByIndex(game.Api.ModLoader, clientSystemIndex) is WorldBehaviorsMod mod)
			{
				mod.OnClientMapChunkUnload(coord);
			}
		}

		private static IEnumerable<CodeInstruction> ClientHandleChunkUnloadTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
		{
			var chunksTryGetValue = SymbolExtensions.GetMethodInfo((Dictionary<long, ClientChunk> d, ClientChunk c) => d.TryGetValue(0, out c));
			var removeMapChunk = SymbolExtensions.GetMethodInfo((Dictionary<long, ClientMapChunk> d) => d.Remove(0));
			var unloadChunk = HarmonyExt.GetMethodInfo<SystemUnloadChunks>("UnloadChunk", BindingFlags.Instance);
			var index3d = SymbolExtensions.GetMethodInfo(() => MapUtil.Index3dL);
			var locals = original.GetMethodBody().LocalVariables;

			LocalVariableInfo longLocal = null;
			LocalVariableInfo enumeratorLocal = null;
			var matcher = new CodeMatcher(instructions);
			matcher.Start();
			if(matcher.Match(
					inst => inst.Calls(index3d),
					inst => {
						if(inst.TryGetLdloc(locals, typeof(long), out var local))
						{
							longLocal = local;
							return true;
						}
						return false;
					},
					inst => inst.Calls(chunksTryGetValue),
					inst => inst.Calls(unloadChunk),
					inst => {
						if(inst.IsLdloc(locals, typeof(HashSet<Vec2i>)))
						{
							return true;
						}
						return false;
					},
					inst => {
						if(inst.TryGetLdloc(locals, typeof(HashSet<Vec2i>.Enumerator), out var local))
						{
							enumeratorLocal = local;
							return true;
						}
						return false;
					},
					inst => inst.Calls(removeMapChunk)
				))
			{
				matcher.SearchForward(inst => inst.Calls(unloadChunk));
				matcher.Insert(
					CodeInstruction.LoadArgument(0),
					CodeInstruction.LoadField(typeof(SystemUnloadChunks), "game"),
					CodeInstruction.LoadLocal(longLocal.LocalIndex),
					CodeInstruction.Call(() => CallOnChunkUnloaded)
				);
				matcher.SearchForward(inst => {
					if(inst.IsLdloc(locals, typeof(HashSet<Vec2i>)))
					{
						return true;
					}
					return false;
				});
				matcher.SearchForward(inst => {
					if(inst.IsLdloc() && inst.operand is LocalBuilder local)
					{
						if(local.LocalType == typeof(HashSet<Vec2i>.Enumerator))
						{
							return true;
						}
					}
					return false;
				});
				matcher.SearchForward(inst => inst.Calls(removeMapChunk));
				matcher.Insert(
					CodeInstruction.LoadArgument(0),
					CodeInstruction.LoadField(typeof(SystemUnloadChunks), "game"),
					CodeInstruction.LoadLocal(enumeratorLocal.LocalIndex, true),
					CodeInstruction.Call(typeof(HashSet<Vec2i>.Enumerator), "get_Current"),
					CodeInstruction.Call(() => CallOnMapChunkUnloaded)
				);
			}
			return matcher.InstructionEnumeration();
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
	}
}