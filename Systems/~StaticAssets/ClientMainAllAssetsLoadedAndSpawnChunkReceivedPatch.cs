using HarmonyLib;
using System;
using Vintagestory.Client.NoObf;

namespace PowerOfMind.Systems.StaticAssets
{
	[HarmonyPatch(typeof(ClientMain), "AllAssetsLoadedAndSpawnChunkReceived", new Type[0])]
	static class ClientMainAllAssetsLoadedAndSpawnChunkReceivedPatch
	{
		static bool Prefix(ClientMain __instance, ref bool ___SuspendMainThreadTasks, out bool __state)
		{
			var system = __instance.Api.ModLoader.GetModSystem<StaticAssetsSystem>();
			system.loadingCounter++;
			if(system.staticAssetsLoaded)
			{
				if(system.loadingCounter == 2)
				{
					__state = true;
					return true;
				}
			}
			else
			{
				___SuspendMainThreadTasks = false;
			}

			__state = false;
			return false;
		}

		static void Postfix(ClientMain __instance, bool __state)
		{
			if(__state)
			{
				var system = __instance.Api.ModLoader.GetModSystem<StaticAssetsSystem>();
				system.loadingCounter = 0;
				system.staticAssetsLoaded = false;
			}
		}
	}
}