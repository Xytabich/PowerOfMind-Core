using System.Collections.Concurrent;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common.Database;

namespace PowerOfMind.Multiblock
{
	public class MultiblockManager : ModSystem
	{
		private ConcurrentDictionary<Xyz, Xyz> pos2main = new ConcurrentDictionary<Xyz, Xyz>();

		public bool GetReferenceToMainBlock(BlockPos fromPos, BlockPos outMainPos = null)
		{
			if(pos2main.TryGetValue(new Xyz(fromPos.X, fromPos.Y, fromPos.Z), out var mainPos))
			{
				if(outMainPos != null)
				{
					outMainPos.X = mainPos.X;
					outMainPos.Y = mainPos.Y;
					outMainPos.Z = mainPos.Z;
				}
				return true;
			}
			return false;
		}

		public bool AddReferenceToMainBlock(BlockPos fromPos, BlockPos mainPos)
		{
			return pos2main.TryAdd(new Xyz(fromPos.X, fromPos.Y, fromPos.Z), new Xyz(mainPos.X, mainPos.Y, mainPos.Z));
		}

		public bool RemoveReferenceToMainBlock(BlockPos fromPos, BlockPos mainPos)
		{
			if(pos2main.TryGetValue(new Xyz(fromPos.X, fromPos.Y, fromPos.Z), out var mPos))
			{
				if(mPos.X == mainPos.X && mPos.Y == mainPos.Y && mPos.Z == mainPos.Z)
				{
					pos2main.TryRemove(new Xyz(fromPos.X, fromPos.Y, fromPos.Z), out mPos);
					return true;
				}
			}
			return false;
		}
	}
}