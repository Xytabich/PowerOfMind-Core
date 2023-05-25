using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace PowerOfMind.Systems.Multiblock
{
	public partial class MultiblockStructure
	{
		public int Id;

		public AssetLocation Code;

		public string Variant;//generated as "variant/transform", like "red-marble/xm-y90" or "xm-y90" or "red-marble" if one of options is not specified in AllowedTransform

		public EnumAlignment Alignment;

		public EnumBuildType BuildType;

		public BlockPattern[] Patterns;

		public BlockSlot[,,] Blocks;//TODO: serialize shared for all variants

		public JsonObject Attributes;//TODO: serialize shared if between rotation variants

		public Vec3i Dimensions;

		public EnumAllowedTransformation AllowedTransform;

		public MultiblockStructure(int id, AssetLocation code)
		{
			Id = id;
			Code = code;
		}

		public void Serialize(IWorldAccessor resolver, BinaryWriter writer, bool writeSharedData)
		{
			//writer.Write((byte)AllowedTransform);
			//writer.Write(PatternVariants.Count);
			//writer.Write(PatternVariants.First().Value.Length);
			//foreach(var pair in PatternVariants)
			//{
			//	writer.Write(pair.Key);
			//	foreach(var pattern in pair.Value)
			//	{
			//		writer.Write((byte)pattern.DrawType);
			//		if(pattern.DrawType == EnumDrawType.Mesh)
			//		{
			//			writer.Write(pattern.MeshIndex);
			//		}
			//		writer.Write(pattern.Ingredient != null);
			//		if(pattern.Ingredient != null)
			//		{
			//			pattern.Ingredient.ToBytes(writer);
			//		}
			//	}
			//}
			//writer.Write(Dimensions.X);
			//writer.Write(Dimensions.Y);
			//writer.Write(Dimensions.Z);
			//uint fill = 0;
			//int counter = 0;
			//for(int z = 0; z < Dimensions.Z; z++)
			//{
			//	for(int y = 0; y < Dimensions.Y; y++)
			//	{
			//		for(int x = 0; x < Dimensions.X; x++)
			//		{
			//			if(Blocks[x, y, z] != null)
			//			{
			//				fill |= 1u << counter;
			//			}
			//			counter++;
			//			if(counter == 32)
			//			{
			//				writer.Write(fill);
			//				fill = 0;
			//				counter = 0;
			//			}
			//		}
			//	}
			//}
			//if(counter != 0) writer.Write(fill);

			//for(int z = 0; z < Dimensions.Z; z++)
			//{
			//	for(int y = 0; y < Dimensions.Y; y++)
			//	{
			//		for(int x = 0; x < Dimensions.X; x++)
			//		{
			//			var block = Blocks[x, y, z];
			//			if(block != null)
			//			{
			//				writer.Write(block.Pattern);
			//				writer.Write(block.Offset.X);
			//				writer.Write(block.Offset.Y);
			//				writer.Write(block.Offset.Z);
			//				writer.Write(block.Rotation.X);
			//				writer.Write(block.Rotation.Y);
			//				writer.Write(block.Rotation.Z);
			//				writer.Write(block.Rotation.W);
			//			}
			//		}
			//	}
			//}
		}

		public void Deserialize(IWorldAccessor resolver, BinaryReader reader, bool readSharedData)
		{
			//AllowedTransform = (EnumAllowedTransformation)reader.ReadByte();

			//int variantsCount = reader.ReadInt32();
			//PatternVariants = new Dictionary<string, BlockPattern[]>(variantsCount);

			//int patternsCount = reader.ReadInt32();
			//for(int i = 0; i < variantsCount; i++)
			//{
			//	string key = reader.ReadString();
			//	var patterns = new BlockPattern[patternsCount];
			//	for(int j = 0; j < patternsCount; j++)
			//	{
			//		BlockPattern pattern = default;
			//		pattern.DrawType = (EnumDrawType)reader.ReadByte();
			//		if(pattern.DrawType == EnumDrawType.Mesh)
			//		{
			//			pattern.MeshIndex = reader.ReadInt32();
			//		}
			//		if(reader.ReadBoolean())
			//		{
			//			pattern.Ingredient = new CraftingRecipeIngredient();
			//			pattern.Ingredient.FromBytes(reader, resolver);
			//		}
			//		patterns[j] = pattern;
			//	}
			//	PatternVariants[key] = patterns;
			//}

			//int dx = reader.ReadInt32();
			//int dy = reader.ReadInt32();
			//int dz = reader.ReadInt32();
			//Dimensions = new Vec3i(dx, dy, dz);

			//uint fill = 0;
			//int counter = 0;
			//for(int z = 0; z < dz; z++)
			//{
			//	for(int y = 0; y < dy; y++)
			//	{
			//		for(int x = 0; x < dx; x++)
			//		{
			//			if(counter == 0)
			//			{
			//				fill = reader.ReadUInt32();
			//			}
			//			if((fill & (1u << counter)) != 0)
			//			{
			//				Blocks[x, y, z] = new BlockSlot();
			//			}
			//			counter = (counter + 1) & 31;//i.e. same as (counter + 1)%32
			//		}
			//	}
			//}

			//for(int z = 0; z < dz; z++)
			//{
			//	for(int y = 0; y < dy; y++)
			//	{
			//		for(int x = 0; x < dx; x++)
			//		{
			//			var block = Blocks[x, y, z];
			//			if(block != null)
			//			{
			//				//TODO: maybe make another flags to reduce size? i.e. if pattern is -1 or if offset is zero or rotation is identity
			//				block.Pattern = reader.ReadInt32();
			//				block.Offset.X = reader.ReadSingle();
			//				block.Offset.Y = reader.ReadSingle();
			//				block.Offset.Z = reader.ReadSingle();
			//				block.Rotation.X = reader.ReadSingle();
			//				block.Rotation.Y = reader.ReadSingle();
			//				block.Rotation.Z = reader.ReadSingle();
			//				block.Rotation.W = reader.ReadSingle();
			//			}
			//		}
			//	}
			//}
		}
	}
}