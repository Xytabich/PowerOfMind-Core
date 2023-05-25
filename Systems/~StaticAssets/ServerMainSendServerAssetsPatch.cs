using HarmonyLib;
using System;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace PowerOfMind.Systems.StaticAssets
{
	[HarmonyPatch(typeof(ServerMain), "SendServerAssets", new Type[] { typeof(IServerPlayer) })]
	static class ServerMainSendServerAssetsPatch
	{
		static void Prefix(IServerPlayer player, ServerMain __instance)
		{
			if(player == null || player.ConnectionState == EnumClientState.Offline)
			{
				return;
			}
			var system = __instance.Api.ModLoader.GetModSystem<StaticAssetsSystem>();
			system.SendAssetsToPlayer(player);
		}
	}
}