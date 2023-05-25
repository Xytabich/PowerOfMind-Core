using HarmonyLib;
using Vintagestory.API.Common;

namespace PowerOfMind
{
	public class PowerOfMindCore : ModSystem
	{
		public const string MODID = "powerofmindcore";

		private Harmony harmony;

		public override void Start(ICoreAPI api)
		{
			base.Start(api);
			harmony = new Harmony(MODID);
			harmony.PatchAll(typeof(PowerOfMindCore).Assembly);
		}

		public override void Dispose()
		{
			harmony.UnpatchAll(MODID);
			base.Dispose();
		}
	}
}