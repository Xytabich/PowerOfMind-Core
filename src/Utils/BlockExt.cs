using Vintagestory.API.Common;

namespace PowerOfMind.Utils
{
	public static class BlockExt
	{
		public static T BlockOrBehAs<T>(this Block block) where T : class
		{
			if(block == null) return null;
			if(block is T inst) return inst;
			return block.GetBehavior(typeof(T), true) as T;
		}

		public static bool BlockOrBehIs<T>(this Block block, out T inst) where T : class
		{
			if(block == null)
			{
				inst = null;
				return false;
			}

			if((inst = block as T) != null) return true;
			return (inst = block.GetBehavior(typeof(T), true) as T) != null;
		}

		public static T BlockEntityOrBehAs<T>(this BlockEntity be) where T : class
		{
			if(be == null) return null;
			if(be is T inst) return inst;
			return be.GetBehavior<T>();
		}

		public static bool BlockEntityOrBehIs<T>(this BlockEntity be, out T inst) where T : class
		{
			if(be == null)
			{
				inst = null;
				return false;
			}

			if((inst = be as T) != null) return true;
			return (inst = be.GetBehavior<T>()) != null;
		}
	}
}