using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PowerOfMind.Multiblock
{
	public abstract class BlockMultiblockMain : Block, IMultiblockMainBlock
	{
		public sealed override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			return OnBlockInteractStart(world, byPlayer, blockSel.Position, blockSel);
		}

		public sealed override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			return OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel.Position, blockSel);
		}

		public sealed override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel.Position, blockSel);
		}

		public sealed override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
		{
			return OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel.Position, blockSel, cancelReason);
		}

		public sealed override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
		{
			OnBlockBroken(world, pos, pos, byPlayer, dropQuantityMultiplier);
		}

		public sealed override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
		{
			return GetDrops(world, pos, pos, byPlayer, dropQuantityMultiplier);
		}

		public sealed override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
		{
			return OnPickBlock(world, pos, pos);
		}

		public sealed override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
		{
			return GetPlacedBlockName(world, pos, pos);
		}

		public sealed override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
		{
			return GetPlacedBlockInfo(world, pos, pos, forPlayer);
		}

		public sealed override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
		{
			return GetPlacedBlockInteractionHelp(world, selection.Position, selection, forPlayer);
		}

		public sealed override int GetColorWithoutTint(ICoreClientAPI capi, BlockPos pos)
		{
			return GetColorWithoutTint(capi, pos, pos);
		}

		public sealed override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
		{
			return GetCollisionBoxes(blockAccessor, pos, pos);
		}

		public sealed override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
		{
			return GetRandomColor(capi, pos, pos, facing, rndIndex);
		}

		public virtual bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection partSel)
		{
			var mainSel = partSel.Clone();
			mainSel.Position = mainPos;
			return base.OnBlockInteractStart(world, byPlayer, mainSel);
		}

		public virtual bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection partSel)
		{
			var mainSel = partSel.Clone();
			mainSel.Position = mainPos;
			return base.OnBlockInteractStep(secondsUsed, world, byPlayer, mainSel);
		}

		public virtual void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection partSel)
		{
			var mainSel = partSel.Clone();
			mainSel.Position = mainPos;
			base.OnBlockInteractStop(secondsUsed, world, byPlayer, mainSel);
		}

		public virtual bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection partSel, EnumItemUseCancelReason cancelReason)
		{
			var mainSel = partSel.Clone();
			mainSel.Position = mainPos;
			return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, mainSel, cancelReason);
		}

		public virtual void OnBlockBroken(IWorldAccessor world, BlockPos mainPos, BlockPos partPos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
		{
			base.OnBlockBroken(world, mainPos, byPlayer, dropQuantityMultiplier);
		}

		public abstract void OnPartRemoved(IWorldAccessor world, BlockPos mainPos, BlockPos partPos);

		public virtual ItemStack[] GetDrops(IWorldAccessor world, BlockPos mainPos, BlockPos partPos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
		{
			return base.GetDrops(world, mainPos, byPlayer, dropQuantityMultiplier);
		}

		public virtual ItemStack OnPickBlock(IWorldAccessor world, BlockPos mainPos, BlockPos partPos)
		{
			return base.OnPickBlock(world, mainPos);
		}

		public virtual string GetPlacedBlockName(IWorldAccessor world, BlockPos mainPos, BlockPos partPos)
		{
			return base.GetPlacedBlockName(world, mainPos);
		}

		public virtual string GetPlacedBlockInfo(IWorldAccessor world, BlockPos mainPos, BlockPos partPos, IPlayer forPlayer)
		{
			return base.GetPlacedBlockInfo(world, mainPos, forPlayer);
		}

		public virtual WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockPos mainPos, BlockSelection partSel, IPlayer forPlayer)
		{
			var mainSel = partSel.Clone();
			mainSel.Position = mainPos;
			return base.GetPlacedBlockInteractionHelp(world, mainSel, forPlayer);
		}

		public virtual int GetColorWithoutTint(ICoreClientAPI capi, BlockPos mainPos, BlockPos partPos)
		{
			return base.GetColorWithoutTint(capi, mainPos);
		}

		public virtual Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos mainPos, BlockPos partPos)
		{
			return base.GetCollisionBoxes(blockAccessor, mainPos);
		}

		public virtual int GetRandomColor(ICoreClientAPI capi, BlockPos mainPos, BlockPos partPos, BlockFacing facing, int rndIndex = -1)
		{
			return base.GetRandomColor(capi, mainPos, facing, rndIndex);
		}
	}
}