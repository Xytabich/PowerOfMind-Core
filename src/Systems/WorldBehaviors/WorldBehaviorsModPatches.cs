using HarmonyLib;
using PowerOfMind.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using GenMathematics.Vectors;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.Common.Database;
using Vintagestory.Server;

namespace PowerOfMind.Systems.WorldBehaviors
{
	public partial class WorldBehaviorsMod
	{
		private static readonly Func<Packet_Server, int> GetPacketId;
		private static readonly Func<ConnectedClient, IServerPlayer> GetPlayer;
		private static readonly Func<ConnectedClient, ServerMain> GetServer;
		private static readonly Func<ServerMain, ConcurrentDictionary<long, ServerMapChunk>> GetMapChunks;
		private static readonly Func<ServerMain, ConcurrentDictionary<long, ServerMapRegion>> GetMapRegions;

		private delegate void GetMapChunkDataDelegate(Packet_Server packet, out int chunkX, out int chunkZ);
		private static readonly GetMapChunkDataDelegate GetMapChunkData;

		static WorldBehaviorsMod()
		{
			var packet = Expression.Parameter(typeof(Packet_Server));
			GetPacketId = Expression.Lambda<Func<Packet_Server, int>>(Expression.Field(packet, "Id"), packet).Compile();

			var chunkX = Expression.Parameter(typeof(int).MakeByRefType());
			var chunkZ = Expression.Parameter(typeof(int).MakeByRefType());
			GetMapChunkData = Expression.Lambda<GetMapChunkDataDelegate>(
				Expression.Block(
					Expression.Assign(chunkX, Expression.Field(Expression.Field(packet, "MapChunk"), "ChunkX")),
					Expression.Assign(chunkZ, Expression.Field(Expression.Field(packet, "MapChunk"), "ChunkZ"))
				),
				packet, chunkX, chunkZ
			).Compile();

			var client = Expression.Parameter(typeof(ConnectedClient));
			GetPlayer = Expression.Lambda<Func<ConnectedClient, IServerPlayer>>(Expression.PropertyOrField(client, "Player"), client).Compile();
			GetServer = Expression.Lambda<Func<ConnectedClient, ServerMain>>(Expression.PropertyOrField(Expression.PropertyOrField(client, "Player"), "server"), client).Compile();

			var server = Expression.Parameter(typeof(ServerMain));
			GetMapChunks = Expression.Lambda<Func<ServerMain, ConcurrentDictionary<long, ServerMapChunk>>>(Expression.PropertyOrField(server, "loadedMapChunks"), server).Compile();
			GetMapRegions = Expression.Lambda<Func<ServerMain, ConcurrentDictionary<long, ServerMapRegion>>>(Expression.PropertyOrField(server, "loadedMapRegions"), server).Compile();
		}

		private static void ClientProcessInBackgroundPostfix(Packet_Server packet, ClientMain ___game)
		{
			if(clientSystemIndex < 0 || GetPacketId(packet) != Packet_ServerIdEnum.MapChunk) return;
			if(CommonExt.GetModSystemByIndex(___game.Api.ModLoader, clientSystemIndex) is WorldBehaviorsMod mod)
			{
				GetMapChunkData(packet, out var x, out var z);
				___game.EnqueueMainThreadTask(() => mod.OnClientMapChunkLoaded(new int2(x, z)), "powerofmind:mapchunkloaded");
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
			matcher.End();
			if(matcher.MatchRelaxed(
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
					inst => inst.IsLdloc(locals, typeof(HashSet<Vec2i>)),
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
				matcher.Advance(1);
				matcher.Insert(
					CodeInstruction.LoadArgument(0),
					CodeInstruction.LoadField(typeof(SystemUnloadChunks), "game"),
					CodeInstruction.LoadLocal(longLocal.LocalIndex),
					CodeInstruction.Call(() => CallOnChunkUnloaded)
				);
				matcher.SearchForward(inst => inst.IsLdloc(locals, typeof(HashSet<Vec2i>)));
				matcher.SearchForward(inst => inst.IsLdloc(locals, typeof(HashSet<Vec2i>.Enumerator)));
				matcher.SearchForward(inst => inst.Calls(removeMapChunk));
				matcher.Advance(1);
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

		private static void CallOnSaveDirtyChunks(ServerMain server, List<DbChunk> list)
		{
			if(serverSystemIndex < 0 || list.Count == 0) return;
			if(CommonExt.GetModSystemByIndex(server.Api.ModLoader, serverSystemIndex) is WorldBehaviorsMod mod)
			{
				foreach(var chunk in list)
				{
					mod.OnSaveDirtyChunk(chunk.Position);
				}
			}
		}

		private static void CallOnSaveDirtyMapChunks(ServerMain server, List<DbChunk> list)
		{
			if(serverSystemIndex < 0 || list.Count == 0) return;
			if(CommonExt.GetModSystemByIndex(server.Api.ModLoader, serverSystemIndex) is WorldBehaviorsMod mod)
			{
				foreach(var chunk in list)
				{
					mod.OnSaveDirtyMapChunk(chunk.Position);
				}
			}
		}

		private static void CallOnSaveDirtyMapRegions(ServerMain server, List<DbChunk> list)
		{
			if(serverSystemIndex < 0 || list.Count == 0) return;
			if(CommonExt.GetModSystemByIndex(server.Api.ModLoader, serverSystemIndex) is WorldBehaviorsMod mod)
			{
				foreach(var chunk in list)
				{
					mod.OnSaveDirtyMapRegion(chunk.Position);
				}
			}
		}

		private static IEnumerable<CodeInstruction> ServerSaveChunksTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
		{
			var setChunksDb = SymbolExtensions.GetMethodInfo((GameDatabase db) => db.SetChunks);
			var locals = original.GetMethodBody().LocalVariables;

			LocalVariableInfo listLocal = null;

			var matcher = new CodeMatcher(instructions);
			matcher.End();
			if(matcher.MatchRelaxed(
					inst => {
						if(inst.TryGetLdloc(locals, typeof(List<DbChunk>), out var local))
						{
							listLocal = local;
							return true;
						}
						return false;
					},
					inst => inst.Calls(setChunksDb)
				))
			{
				matcher.SearchForward(inst => inst.IsLdloc(locals, typeof(List<DbChunk>)));
				matcher.SearchForward(inst => inst.Calls(setChunksDb));
				matcher.Advance(1);
				matcher.Insert(
					CodeInstruction.LoadArgument(0),
					CodeInstruction.LoadField(typeof(ServerSystem), "server"),
					CodeInstruction.LoadLocal(listLocal.LocalIndex),
					CodeInstruction.Call(() => CallOnSaveDirtyChunks)
				);
			}
			return matcher.InstructionEnumeration();
		}

		private static IEnumerable<CodeInstruction> ServerSaveMapChunksTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
		{
			var setChunksDb = SymbolExtensions.GetMethodInfo((GameDatabase db) => db.SetMapChunks);
			var locals = original.GetMethodBody().LocalVariables;

			LocalVariableInfo listLocal = null;

			var matcher = new CodeMatcher(instructions);
			matcher.End();
			if(matcher.MatchRelaxed(
					inst => {
						if(inst.TryGetLdloc(locals, typeof(List<DbChunk>), out var local))
						{
							listLocal = local;
							return true;
						}
						return false;
					},
					inst => inst.Calls(setChunksDb)
				))
			{
				matcher.SearchForward(inst => inst.IsLdloc(locals, typeof(List<DbChunk>)));
				matcher.SearchForward(inst => inst.Calls(setChunksDb));
				matcher.Advance(1);
				matcher.Insert(
					CodeInstruction.LoadArgument(0),
					CodeInstruction.LoadField(typeof(ServerSystem), "server"),
					CodeInstruction.LoadLocal(listLocal.LocalIndex),
					CodeInstruction.Call(() => CallOnSaveDirtyMapChunks)
				);
			}
			return matcher.InstructionEnumeration();
		}

		private static IEnumerable<CodeInstruction> ServerSaveMapRegionsTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
		{
			var setChunksDb = SymbolExtensions.GetMethodInfo((GameDatabase db) => db.SetMapRegions);
			var locals = original.GetMethodBody().LocalVariables;

			LocalVariableInfo listLocal = null;

			var matcher = new CodeMatcher(instructions);
			matcher.End();
			if(matcher.MatchRelaxed(
					inst => {
						if(inst.TryGetLdloc(locals, typeof(List<DbChunk>), out var local))
						{
							listLocal = local;
							return true;
						}
						return false;
					},
					inst => inst.Calls(setChunksDb)
				))
			{
				matcher.SearchForward(inst => inst.IsLdloc(locals, typeof(List<DbChunk>)));
				matcher.SearchForward(inst => inst.Calls(setChunksDb));
				matcher.Advance(1);
				matcher.Insert(
					CodeInstruction.LoadArgument(0),
					CodeInstruction.LoadField(typeof(ServerSystem), "server"),
					CodeInstruction.LoadLocal(listLocal.LocalIndex),
					CodeInstruction.Call(() => CallOnSaveDirtyMapRegions)
				);
			}
			return matcher.InstructionEnumeration();
		}

		private static void ServerSetChunkSentPrefix(long index3d, ConnectedClient __instance)
		{
			if(serverSystemIndex < 0) return;
			if(CommonExt.GetModSystemByIndex(GetServer(__instance).Api.ModLoader, serverSystemIndex) is WorldBehaviorsMod mod)
			{
				mod.OnServerTrackChunk(GetPlayer(__instance), index3d);
			}
		}

		private static void ServerSetMapChunkSentPrefix(long index2d, ConnectedClient __instance)
		{
			if(serverSystemIndex < 0) return;
			if(CommonExt.GetModSystemByIndex(GetServer(__instance).Api.ModLoader, serverSystemIndex) is WorldBehaviorsMod mod)
			{
				mod.OnServerTrackMapChunk(GetPlayer(__instance), index2d);
			}
		}

		private static void ServerSetMapRegionSentPrefix(long index2d, ConnectedClient __instance)
		{
			if(serverSystemIndex < 0) return;
			if(CommonExt.GetModSystemByIndex(GetServer(__instance).Api.ModLoader, serverSystemIndex) is WorldBehaviorsMod mod)
			{
				mod.OnServerTrackMapRegion(GetPlayer(__instance), index2d);
			}
		}

		private static void ServerRemoveChunkSentPrefix(long index3d, ConnectedClient __instance)
		{
			if(serverSystemIndex < 0 || !__instance.DidSendChunk(index3d)) return;
			if(CommonExt.GetModSystemByIndex(GetServer(__instance).Api.ModLoader, serverSystemIndex) is WorldBehaviorsMod mod)
			{
				mod.OnServerUntrackChunk(GetPlayer(__instance), index3d);
			}
		}

		private static void ServerRemoveMapChunkSentPrefix(long index2d, ConnectedClient __instance)
		{
			if(serverSystemIndex < 0 || !__instance.DidSendMapChunk(index2d)) return;
			if(CommonExt.GetModSystemByIndex(GetServer(__instance).Api.ModLoader, serverSystemIndex) is WorldBehaviorsMod mod)
			{
				mod.OnServerUntrackMapChunk(GetPlayer(__instance), index2d);
			}
		}

		private static void ServerRemoveMapRegionSentPrefix(long index2d, ConnectedClient __instance)
		{
			if(serverSystemIndex < 0 || !__instance.DidSendMapRegion(index2d)) return;
			if(CommonExt.GetModSystemByIndex(GetServer(__instance).Api.ModLoader, serverSystemIndex) is WorldBehaviorsMod mod)
			{
				mod.OnServerUntrackMapRegion(GetPlayer(__instance), index2d);
			}
		}
	}
}