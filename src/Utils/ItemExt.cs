using Vintagestory.API.Common;

namespace PowerOfMind.Utils
{
	public static class ItemExt
	{
		public static T ItemOrBehAs<T>(this CollectibleObject item) where T : class
		{
			if(item == null) return null;
			if(item is T inst) return inst;
			return item.GetCollectibleBehavior(typeof(T), true) as T;
		}

		public static bool ItemOrBehIs<T>(this CollectibleObject item, out T inst) where T : class
		{
			if(item == null)
			{
				inst = null;
				return false;
			}

			if((inst = item as T) != null) return true;
			return (inst = item.GetCollectibleBehavior(typeof(T), true) as T) != null;
		}
	}
}