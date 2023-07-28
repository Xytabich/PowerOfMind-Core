using PowerOfMind.Graphics;
using System.Runtime.InteropServices;

namespace PowerOfMind.Systems.RenderBatching.Draw
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public readonly struct GraphicsCommand
	{
		public readonly GraphicsCommandType Type;
		public readonly uint Offset;
		public readonly uint Count;
		public readonly uint Index;
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
			Type = GraphicsCommandType.SetUniform;
			Offset = offset;
			Count = count;
			Index = index;
			Arg = 0;
		}

		public GraphicsCommand(uint offset, uint count, uint index, EnumTextureTarget target)
		{
			Type = GraphicsCommandType.BindTexture;
			Offset = offset;
			Count = count;
			Index = index;
			Arg = (uint)target;
		}
	}

	public enum GraphicsCommandType
	{
		Draw,
		SetUniform,
		BindTexture
	}
}