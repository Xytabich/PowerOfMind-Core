using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PowerOfMind.Multiblock
{
	public interface IMultiblockMainBlock
	{
		void OnPartRemoved(IWorldAccessor world, BlockPos mainPos, BlockPos partPos);

		void OnBlockBroken(IWorldAccessor world, BlockPos mainPos, BlockPos partPos, IPlayer byPlayer, float dropQuantityMultiplier = 1);

		ItemStack[] GetDrops(IWorldAccessor world, BlockPos mainPos, BlockPos partPos, IPlayer byPlayer, float dropQuantityMultiplier = 1);

		ItemStack OnPickBlock(IWorldAccessor world, BlockPos mainPos, BlockPos partPos);

		string GetPlacedBlockInfo(IWorldAccessor world, BlockPos mainPos, BlockPos partPos, IPlayer forPlayer);

		string GetPlacedBlockName(IWorldAccessor world, BlockPos mainPos, BlockPos partPos);

		WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockPos mainPos, BlockSelection partSel, IPlayer forPlayer);

		bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection partSel);

		bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection partSel);

		void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection partSel);

		bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection partSel, EnumItemUseCancelReason cancelReason);

		int GetColorWithoutTint(ICoreClientAPI capi, BlockPos mainPos, BlockPos partPos);

		Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos mainPos, BlockPos partPos);

		int GetRandomColor(ICoreClientAPI capi, BlockPos mainPos, BlockPos partPos, BlockFacing facing, int rndIndex = -1);
	}
}