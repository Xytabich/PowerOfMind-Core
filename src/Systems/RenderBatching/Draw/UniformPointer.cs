using System.Runtime.InteropServices;

namespace PowerOfMind.Systems.RenderBatching.Draw
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public readonly struct UniformPointer
	{
		/// <summary>
		/// Index of uniform in shader
		/// </summary>
		public readonly int Index;
		/// <summary>
		/// The total size of the uniform in the shader in bytes
		/// </summary>
		public readonly int Size;
		/// <summary>
		/// Maximum number of array elements a uniform can contain, or 1 for a simple variable
		/// </summary>
		public readonly int Count;

		public UniformPointer(int index, int size, int count)
		{
			Index = index;
			Size = size;
			Count = count;
		}
	}
}