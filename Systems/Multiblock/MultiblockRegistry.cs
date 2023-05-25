using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;

namespace PowerOfMind.Systems.Multiblock
{
	public class MultiblockRegistry : RecipeRegistryBase
	{
		private Dictionary<AssetLocation, StructureInfo> structures = new Dictionary<AssetLocation, StructureInfo>();
		private List<MultiblockStructure> structureVariants = new List<MultiblockStructure>();

		public override void ToBytes(IWorldAccessor resolver, out byte[] data, out int quantity)
		{
			quantity = 0;
			using(var ms = new MemoryStream())
			{
				var writer = new BinaryWriter(ms);
				foreach(var pair in structures)
				{
					writer.Write(pair.Key.ToShortString());
					writer.Write((byte)pair.Value.count);
					int last = pair.Value.index + pair.Value.count;
					for(int i = pair.Value.index; i < last; i++)
					{
						structureVariants[i].Serialize(resolver, writer, true);
					}
					quantity++;
				}
				data = ms.ToArray();
			}
		}

		public override void FromBytes(IWorldAccessor resolver, int quantity, byte[] data)
		{
			if(quantity <= 0) return;

			using(var ms = new MemoryStream(data))
			{
				var reader = new BinaryReader(ms);
				for(int i = 0; i < quantity; i++)
				{
					var key = new AssetLocation(reader.ReadString());
					int index = structureVariants.Count;
					int count = reader.ReadByte();
					for(int j = 0; j < count; j++)
					{
						var value = new MultiblockStructure(structureVariants.Count, key);
						value.Deserialize(resolver, reader, true);
						structureVariants.Add(value);
					}
					structures[key] = new StructureInfo(index, count);
				}
			}
		}

		private struct StructureInfo
		{
			public int index;
			public int count;

			public StructureInfo(int index, int count)
			{
				this.index = index;
				this.count = count;
			}
		}
	}
}