using System.Runtime.InteropServices;

namespace PowerOfMind.Systems.RenderBatching.Draw
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public readonly struct UniformPointer
	{
		public readonly int Index;
		public readonly int Size;
		public readonly int Count;

		public UniformPointer(int index, int size, int count)
		{
			Index = index;
			Size = size;
			Count = count;
		}
	}
}