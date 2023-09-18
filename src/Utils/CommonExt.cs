using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;

namespace PowerOfMind.Utils
{
	public static class CommonExt
	{
		private static readonly System.Func<IModLoader, int, ModSystem> getModSystemByIndex;

		private delegate ColorUtil.LightUtil LightUtilCtor(ClientWorldMap clientWorld);
		private static readonly LightUtilCtor createLightUtil;

		static CommonExt()
		{
			{
				var clientWorldArg = Expression.Parameter(typeof(ClientWorldMap));
				createLightUtil = Expression.Lambda<LightUtilCtor>(
					Expression.New(typeof(ColorUtil.LightUtil).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null,
						new Type[] { typeof(float[]), typeof(float[]), typeof(byte[]), typeof(byte[]) }, null),
						Expression.PropertyOrField(clientWorldArg, "BlockLightLevels"),
						Expression.PropertyOrField(clientWorldArg, "SunLightLevels"),
						Expression.PropertyOrField(clientWorldArg, "hueLevels"),
						Expression.PropertyOrField(clientWorldArg, "satLevels")
					),
					clientWorldArg
				).Compile();
			}

			{
				var modLoader = Expression.Parameter(typeof(IModLoader));
				var index = Expression.Parameter(typeof(int));
				var systems = Expression.Variable(typeof(List<ModSystem>));
				var returnLabel = Expression.Label(typeof(ModSystem));
				getModSystemByIndex = Expression.Lambda<System.Func<IModLoader, int, ModSystem>>(
					Expression.Block(typeof(ModSystem), new[] { systems },
						Expression.Assign(systems, Expression.Field(Expression.Convert(modLoader, typeof(ModLoader)), "enabledSystems")),
						Expression.IfThen(Expression.GreaterThanOrEqual(index, Expression.Property(systems, "Count")),
							Expression.Return(returnLabel, Expression.Constant(null, typeof(ModSystem)))),
						Expression.Label(returnLabel, Expression.Property(systems, "Item", index))
					),
					modLoader, index
				).Compile();
			}
		}

		/// <summary>
		/// Returns the ModSystem by index or null if the index is out of the list range
		/// </summary>
		public static ModSystem GetModSystemByIndex(IModLoader modLoader, int index)
		{
			return getModSystemByIndex(modLoader, index);
		}

		public static int GetModSystemIndex(IModLoader modLoader, ModSystem system)
		{
			return ((ModLoader)modLoader).Systems.Select((s, i) => (s, i)).First(p => p.s == system).i;
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