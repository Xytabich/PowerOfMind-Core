using HarmonyLib;
using PowerOfMind.Utils;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Mathematics;
using Vintagestory.API.MathTools;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;

namespace PowerOfMind.Systems.WorldBehaviors
{
	public partial class WorldBehaviorsMod
	{
		private const BindingFlags InstanceFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

		private static readonly Func<Packet_Server, int> GetPacketId;

		private delegate void GetMapChunkDataDelegate(Packet_Server packet, out int chunkX, out int chunkZ);
		private static readonly GetMapChunkDataDelegate GetMapChunkData;

		static WorldBehaviorsMod()
		{
			var packet = Expression.Parameter(typeof(Packet_Server));
			GetPacketId = Expression.Lambda<Func<Packet_Server, int>>(Expression.Field(packet, typeof(Packet_Server).GetField("Id", InstanceFlags)), packet).Compile();

			var mapChunkField = typeof(Packet_Server).GetField("MapChunk", InstanceFlags);
			var chunkX = Expression.Parameter(typeof(int).MakeByRefType());
			var chunkZ = Expression.Parameter(typeof(int).MakeByRefType());
			GetMapChunkData = Expression.Lambda<GetMapChunkDataDelegate>(
				Expression.Block(
					Expression.Assign(chunkX, Expression.Field(Expression.Field(packet, mapChunkField), typeof(Packet_ServerMapChunk).GetField("ChunkX", InstanceFlags))),
					Expression.Assign(chunkZ, Expression.Field(Expression.Field(packet, mapChunkField), typeof(Packet_ServerMapChunk).GetField("ChunkZ", InstanceFlags)))
				),
				packet, chunkX, chunkZ
			).Compile();
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
	}
}