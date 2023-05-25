using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace PowerOfMind.Systems.Multiblock
{
	[JsonObject(MemberSerialization.OptIn)]
	public class StructureMaterial : StructureIngredient
	{
		/// <summary>
		/// If true, using this item to build a multiblock will not consume the item, but will reduce its durability.
		/// </summary>
		[JsonProperty]
		public bool IsTool;

		/// <summary>
		/// If IsTool is set, this is the durability cost
		/// </summary>
		[JsonProperty]
		public int ToolDurabilityCost = 1;

		/// <summary>
		/// When using a wildcard in the item/block code, setting this field will limit the allowed variants
		/// </summary>
		[JsonProperty]
		public string[] AllowedVariants;

		/// <summary>
		/// When using a wildcard in the item/block code, setting this field will skip these variants
		/// </summary>
		[JsonProperty]
		public string[] SkipVariants;

		/// <summary>
		/// Whether this material contains a wildcard, populated by the engine
		/// </summary>
		public bool IsWildCard = false;

		public ITreeAttribute ItemAttributes = null;

		/// <summary>
		/// Turns Type, Code and Attributes into an IItemStack
		/// </summary>
		/// <param name="resolver"></param>
		public override bool Resolve(IWorldAccessor resolver, string sourceForErrorLogging)
		{
			if(Code.Path.Contains("*"))
			{
				IsWildCard = true;
				ItemAttributes = Attributes?.ToAttribute() as ITreeAttribute;
				return true;
			}

			return base.Resolve(resolver, sourceForErrorLogging);
		}

		/// <summary>
		/// Checks whether or not the input satisfies as an material for the multiblock.
		/// </summary>
		/// <param name="inputStack"></param>
		/// <returns></returns>
		public bool SatisfiesAsIngredient(IWorldAccessor worldForResolve, ItemStack inputStack, bool checkStacksize = true)
		{
			if(inputStack == null) return false;

			if(IsWildCard)
			{
				if(Type != inputStack.Class) return false;
				if(!WildcardUtil.Match(Code, inputStack.Collectible.Code, AllowedVariants)) return false;
				if(SkipVariants != null && WildcardUtil.Match(Code, inputStack.Collectible.Code, SkipVariants)) return false;
				if(ItemAttributes != null && ItemAttributes.Count > 0 && (
					inputStack.Attributes == null ||
					!ItemAttributes.IsSubSetOf(worldForResolve, inputStack.Attributes)
				))
				{
					return false;
				}
				if(checkStacksize && inputStack.StackSize < Quantity) return false;
			}
			else
			{
				if(!ResolvedItemstack.Satisfies(inputStack)) return false;
				if(checkStacksize && inputStack.StackSize < ResolvedItemstack.StackSize) return false;
			}

			return true;
		}

		public override void ToBytes(BinaryWriter writer)
		{
			writer.Write(IsWildCard);
			if(IsWildCard)
			{
				writer.Write((int)Type);
				writer.Write(Code.ToShortString());
				writer.Write(Quantity);
				writer.Write(ItemAttributes != null);
				if(ItemAttributes != null)
				{
					ItemAttributes.ToBytes(writer);
				}
			}
			else
			{
				ResolvedItemstack.ToBytes(writer);
			}

			writer.Write(IsTool);
			writer.Write(ToolDurabilityCost);

			writer.Write(AllowedVariants != null);
			if(AllowedVariants != null)
			{
				writer.Write(AllowedVariants.Length);
				for(int i = 0; i < AllowedVariants.Length; i++)
				{
					writer.Write(AllowedVariants[i]);
				}
			}

			writer.Write(SkipVariants != null);
			if(SkipVariants != null)
			{
				writer.Write(SkipVariants.Length);
				for(int i = 0; i < SkipVariants.Length; i++)
				{
					writer.Write(SkipVariants[i]);
				}
			}

			if(PatternAttributes != null)
			{
				writer.Write(true);
				writer.Write(PatternAttributes.ToString());
			}
			else
			{
				writer.Write(false);
			}
		}

		public override void FromBytes(BinaryReader reader, IWorldAccessor resolver)
		{
			IsWildCard = reader.ReadBoolean();
			if(IsWildCard)
			{
				Type = (EnumItemClass)reader.ReadInt32();
				Code = new AssetLocation(reader.ReadString());
				Quantity = reader.ReadInt32();
				if(reader.ReadBoolean())
				{
					ItemAttributes = new TreeAttribute();
					ItemAttributes.FromBytes(reader);
				}
			}
			else
			{
				ResolvedItemstack = new ItemStack(reader, resolver);
				Code = ResolvedItemstack.Collectible.Code;
				Type = ResolvedItemstack.Class;
				Quantity = ResolvedItemstack.StackSize;
			}

			IsTool = reader.ReadBoolean();
			ToolDurabilityCost = reader.ReadInt32();

			bool haveVariants = reader.ReadBoolean();
			if(haveVariants)
			{
				AllowedVariants = new string[reader.ReadInt32()];
				for(int i = 0; i < AllowedVariants.Length; i++)
				{
					AllowedVariants[i] = reader.ReadString();
				}
			}

			bool haveSkipVariants = reader.ReadBoolean();
			if(haveSkipVariants)
			{
				SkipVariants = new string[reader.ReadInt32()];
				for(int i = 0; i < SkipVariants.Length; i++)
				{
					SkipVariants[i] = reader.ReadString();
				}
			}

			if(reader.ReadBoolean())
			{
				PatternAttributes = new JsonObject(JToken.Parse(reader.ReadString()));
			}
		}
	}
}
