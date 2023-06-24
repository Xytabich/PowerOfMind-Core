using System;
using System.Linq.Expressions;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace PowerOfMind.Utils
{
	public static class CommonExt
	{
		private delegate ColorUtil.LightUtil LightUtilCtor(ClientWorldMap clientWorld);
		private static readonly LightUtilCtor createLightUtil;

		static CommonExt()
		{
			var clientWorldArg = Expression.Parameter(typeof(ClientWorldMap));
			createLightUtil = Expression.Lambda<LightUtilCtor>(
				Expression.New(typeof(ColorUtil.LightUtil).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null,
					new Type[] { typeof(float[]), typeof(float[]), typeof(byte[]), typeof(byte[]) }, null),
					Expression.Field(clientWorldArg, typeof(ClientWorldMap).GetField("BlockLightLevels", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)),
					Expression.Field(clientWorldArg, typeof(ClientWorldMap).GetField("SunLightLevels", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)),
					Expression.Field(clientWorldArg, typeof(ClientWorldMap).GetField("hueLevels", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)),
					Expression.Field(clientWorldArg, typeof(ClientWorldMap).GetField("satLevels", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
				),
				clientWorldArg
			).Compile();
		}

		public static ColorUtil.LightUtil CreateLightUtil(IClientWorldAccessor world)
		{
			return createLightUtil(world.Claims as ClientWorldMap);
		}

		public static BlockFacing FaceFromVector(FastVec3f vec)
		{
			vec.Normalize();
			BlockFacing facing = null;
			var allFaces = BlockFacing.ALLFACES;
			float maxDir = -1f;
			for(int i = allFaces.Length; i >= 0; i--)
			{
				float dir = allFaces[i].Normalf.Dot(vec);
				if(dir > maxDir)
				{
					maxDir = dir;
					facing = allFaces[i];
				}
			}
			return facing;
		}
	}
}