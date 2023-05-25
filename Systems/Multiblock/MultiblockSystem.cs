using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace PowerOfMind.Systems.Multiblock
{
	public class MultiblockSystem : ModSystem
	{
		private MultiblockRegistry multiblockRegistry = null;

		public override void Start(ICoreAPI api)
		{
			base.Start(api);
			multiblockRegistry = api.RegisterRecipeRegistry<MultiblockRegistry>("powerofmind:multiblock");
		}

		public override void AssetsLoaded(ICoreAPI api)
		{
			base.AssetsLoaded(api);
			if(api is IServerAPI sapi)
			{
				//TODO:
			}
		}
	}
}