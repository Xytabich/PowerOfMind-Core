using System.Runtime.InteropServices;
using Vintagestory.API.Client;

namespace PowerOfMind.Systems.RenderBatching.Draw
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public readonly struct RenderPassGroup
	{
		public readonly EnumChunkRenderPass RenderPass;
		public readonly int Index;
		public readonly int Count;

		public RenderPassGroup(EnumChunkRenderPass renderPass, int index, int count)
		{
			RenderPass = renderPass;
			Index = index;
			Count = count;
		}
	}
}