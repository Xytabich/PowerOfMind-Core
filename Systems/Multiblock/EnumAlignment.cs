using System;

namespace PowerOfMind.Systems.Multiblock
{
	[Flags]
	public enum EnumAlignment
	{
		None = 0,

		North = 1,
		East = 2,
		South = 4,
		West = 8,

		Up = 16,
		Down = 32,

		MirrorX = 64,
		MirrorY = 128,
		MirrorZ = 256,

		Horizontal = North | East | South | West,
		Vertical = Up | Down,
	}
}