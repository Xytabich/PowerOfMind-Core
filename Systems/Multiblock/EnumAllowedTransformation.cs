namespace PowerOfMind.Systems.Multiblock
{
	public enum EnumAllowedTransformation
	{
		None,
		MirrorX = 1,
		MirrorY = 2,
		MirrorZ = 4,

		AlignX = 8,
		AlignY = 16,
		AlignZ = 32,

		MirrorVertical = MirrorY,
		MirrorHorizontal = MirrorX | MirrorZ,
		MirrorAll = MirrorX | MirrorY | MirrorZ,

		AlignVertical = AlignY,
		AlignHorizontal = AlignX | AlignY,
		AlignAll = AlignX | AlignY | AlignZ
	}
}