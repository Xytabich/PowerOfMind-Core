using PowerOfMind.Graphics;
using System.Runtime.InteropServices;

namespace PowerOfMind.Systems.RenderBatching.Draw
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public readonly struct GraphicsCommand
	{
		public readonly GraphicsCommandType Type;
		/// <summary>
		/// Index offset for <see cref="GraphicsCommandType.Draw"/> or data offset in byte array for <see cref="GraphicsCommandType.SetUniform"/>
		/// </summary>
		public readonly uint Offset;
		/// <summary>
		/// Number of indexes for <see cref="GraphicsCommandType.Draw"/> or number of elements for <see cref="GraphicsCommandType.SetUniform"/>
		/// </summary>
		public readonly uint Count;
		/// <summary>
		/// Index of uniform for <see cref="GraphicsCommandType.SetUniform"/> or <see cref="GraphicsCommandType.BindTexture"/>
		/// </summary>
		public readonly uint Index;
		/// <summary>
		/// Texture target for <see cref="GraphicsCommandType.BindTexture"/>
		/// </summary>
		public readonly uint Arg;

		public GraphicsCommand(uint offset, uint count)
		{
			Type = GraphicsCommandType.Draw;
			Offset = offset;
			Count = count;
			Index = 0;
			Arg = 0;
		}

		public GraphicsCommand(uint offset, uint count, uint index)
		{
			Type = offset == uint.MaxValue ? GraphicsCommandType.SetUniformZero : (offset == uint.MaxValue - 1 ? GraphicsCommandType.SetUniformDefault : GraphicsCommandType.SetUniform);
			Offset = offset;
			Count = count;
			Index = index;
			Arg = 0;
		}

		public GraphicsCommand(uint offset, uint count, uint index, EnumTextureTarget target)
		{
			Type = offset == uint.MaxValue ? GraphicsCommandType.SetUniformZero : (offset == uint.MaxValue - 1 ? GraphicsCommandType.SetUniformDefault : GraphicsCommandType.BindTexture);
			Offset = offset;
			Count = count;
			Index = index;
			Arg = (uint)target;
		}
	}

	public enum GraphicsCommandType : uint
	{
		Draw,
		SetUniform,
		SetUniformZero,
		SetUniformDefault,
		BindTexture
	}
}