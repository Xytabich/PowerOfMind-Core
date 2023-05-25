using PowerOfMind.Utils;
using Vintagestory.API.Common;

namespace PowerOfMind.Systems.Multiblock
{
	public partial class MultiblockStructure
	{
		public struct BlockPattern
		{
			public EnumDrawType DrawType;
			public int MeshIndex;
			public JsonItemStack Placeholder;
			public StructureMaterial[] Materials;
			public EnumMaterialSource MaterialSource;
		}

		public class BlockSlot
		{
			public int Pattern = -1;
			public Vector3 Offset = Vector3.Zero;
			public Quaternion Rotation = Quaternion.Identity;
		}

		public enum EnumIngredientType
		{
			None,
			Block,
			Item
		}

		public enum EnumDrawType
		{
			None,
			/// <summary>
			/// Render block used as an ingredient
			/// </summary>
			Block,
			Mesh
		}

		public enum EnumBuildType
		{
			/// <summary>
			/// Places ghost blocks that need to be filled with ingredients
			/// </summary>
			Plan,
			/// <summary>
			/// The structure is created from already placed blocks
			/// </summary>
			Structure
		}

		public enum EnumMaterialSource
		{
			Hand,
			Inventory,
			MainHand,
			Offhand,
			Block,
			Any
		}
	}
}