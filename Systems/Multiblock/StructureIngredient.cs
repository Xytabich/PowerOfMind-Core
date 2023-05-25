using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace PowerOfMind.Systems.Multiblock
{
	[JsonObject(MemberSerialization.OptIn)]
	public class StructureIngredient
	{
		/// <summary>
		/// Code of the item or block
		/// </summary>
		[JsonProperty]
		public AssetLocation Code { get; set; }

		/// <summary>
		/// Item or Block
		/// </summary>
		[JsonProperty]
		public EnumItemClass Type = EnumItemClass.Block;

		/// <summary>
		/// How much input items are required
		/// </summary>
		[JsonProperty]
		public int Quantity = 1;

		/// <summary>
		/// What attributes this itemstack must have
		/// </summary>
		[JsonProperty, JsonConverter(typeof(JsonAttributesConverter))]
		public JsonObject Attributes;

		/// <summary>
		/// Optional attribute data that you can attach any data to
		/// </summary>
		[JsonProperty, JsonConverter(typeof(JsonAttributesConverter))]
		public JsonObject PatternAttributes;

		/// <summary>
		/// The itemstack made from Code, Quantity and Attributes, populated by the engine
		/// </summary>
		public ItemStack ResolvedItemstack;

		/// <summary>
		/// Turns Type, Code and Attributes into an IItemStack
		/// </summary>
		/// <param name="resolver"></param>
		public virtual bool Resolve(IWorldAccessor resolver, string sourceForErrorLogging)
		{
			if(Type == EnumItemClass.Block)
			{
				Block block = resolver.GetBlock(Code);
				if(block == null || block.IsMissing)
				{
					resolver.Logger.Warning("Failed resolving multiblock material with code {0} in {1}", Code, sourceForErrorLogging);
					return false;
				}

				ResolvedItemstack = new ItemStack(block, Quantity);
			}
			else
			{
				Item item = resolver.GetItem(Code);
				if(item == null || item.IsMissing)
				{
					resolver.Logger.Warning("Failed resolving multiblock material with code {0} in {1}", Code, sourceForErrorLogging);
					return false;
				}
				ResolvedItemstack = new ItemStack(item, Quantity);
			}

			if(Attributes != null)
			{
				IAttribute attributes = Attributes.ToAttribute();
				if(attributes is ITreeAttribute)
				{
					ResolvedItemstack.Attributes = (ITreeAttribute)attributes;
				}
				Attributes = null;
			}

			return true;
		}

		public override string ToString()
		{
			return Type + " code " + Code;
		}

		/// <summary>
		/// Fills placeholders like '{key}' in the material.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		//public void FillPlaceHolder(string key, string value)
		//{
		//	Code = Code.CopyWithPath(Code.Path.Replace("{" + key + "}", value));
		//	Attributes?.FillPlaceHolder(key, value);
		//	PatternAttributes?.FillPlaceHolder(key, value);
		//}

		/// <summary>
		/// Fills placeholders like '[key]' in the material.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		//public void FillTrPlaceHolder(string key, string value)
		//{
		//	Code = Code.CopyWithPath(Code.Path.Replace("[" + key + "]", value));
		//	if(Attributes != null) FillTrPlaceHolder(Attributes.Token, key, value);
		//	if(PatternAttributes != null) FillTrPlaceHolder(PatternAttributes.Token, key, value);
		//}

		public void InitType(string variantName, IReadOnlyDictionary<string, string> variantInfo, IReadOnlyDictionary<string, string> transformInfo)
		{
			//TODO: fill placeholders & init ByType options. string for ByType is variantName
			//there is an example: RegistryObjectType.solveByType
		}

		public virtual void ToBytes(BinaryWriter writer)
		{
			ResolvedItemstack.ToBytes(writer);

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

		public virtual void FromBytes(BinaryReader reader, IWorldAccessor resolver)
		{
			ResolvedItemstack = new ItemStack(reader, resolver);

			Code = ResolvedItemstack.Collectible.Code;
			Type = ResolvedItemstack.Class;
			Quantity = ResolvedItemstack.StackSize;

			if(reader.ReadBoolean())
			{
				PatternAttributes = new JsonObject(JToken.Parse(reader.ReadString()));
			}
		}

		internal static void FillTrPlaceHolder(JToken token, string key, string value)
		{
			switch(token.Type)
			{
				case JTokenType.Object:
					foreach(var pair in (JObject)token)
					{
						FillTrPlaceHolder(pair.Value, key, value);
					}
					break;
				case JTokenType.Array:
					foreach(var item in (JArray)token)
					{
						FillTrPlaceHolder(item, key, value);
					}
					break;
				case JTokenType.String:
					var jval = (JValue)token;
					jval.Value = ((string)jval.Value).Replace("[" + key + "]", value);
					break;
			}
		}
	}
}