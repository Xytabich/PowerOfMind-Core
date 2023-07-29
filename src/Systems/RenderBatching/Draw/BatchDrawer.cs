using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Shader;
using System;
using Vintagestory.API.Client;

namespace PowerOfMind.Systems.RenderBatching.Draw
{
	public class BatchDrawer
	{
		public readonly IDrawableHandle drawHandle;
		public readonly IDrawableHandle shadowHandle;
		private readonly RenderPassGroup[] renderPasses;
		private readonly GraphicsCommand[] commands;
		private readonly UniformPointer[] uniformsMap;
		private readonly int[] shadowUniformsMap;
		private readonly byte[] uniformsData;
		private readonly int maxUniformSize;

		public BatchDrawer(IDrawableHandle drawHandle, IDrawableHandle shadowHandle, RenderPassGroup[] renderPasses, GraphicsCommand[] commands, UniformPointer[] uniformsMap, int[] shadowUniformsMap, byte[] uniformsData)
		{
			this.drawHandle = drawHandle;
			this.shadowHandle = shadowHandle;
			this.renderPasses = renderPasses;
			this.commands = commands;
			this.uniformsMap = uniformsMap;
			this.shadowUniformsMap = shadowUniformsMap;
			this.uniformsData = uniformsData;

			maxUniformSize = 0;
			for(int i = uniformsMap.Length - 1; i >= 0; i--)
			{
				maxUniformSize = Math.Max(maxUniformSize, uniformsMap[i].Size);
			}
		}

		public void Dispose()
		{
			drawHandle.Dispose();
			shadowHandle?.Dispose();
		}

		public int TryGetPassIndex(EnumChunkRenderPass renderPass)
		{
			int lo = 0;
			int hi = renderPasses.Length - 1;
			while(lo <= hi)
			{
				int i = lo + ((hi - lo) >> 1);
				int order = renderPasses[i].RenderPass.CompareTo(renderPass);

				if(order == 0)
				{
					return i;
				}

				if(order < 0)
				{
					lo = i + 1;
				}
				else
				{
					hi = i - 1;
				}
			}

			return -1;
		}

		public unsafe void RenderShadow(ref BatchDrawCall callInfo, ref byte[] zeroBytes)
		{
			if(zeroBytes.Length < maxUniformSize)
			{
				zeroBytes = new byte[maxUniformSize];
			}

			fixed(byte* dataPtr = uniformsData)
			{
				fixed(byte* zeroPtr = zeroBytes)
				{
					int last = renderPasses[callInfo.pass].Index + renderPasses[callInfo.pass].Count;
					int index;
					for(int i = renderPasses[callInfo.pass].Index; i < last; i++)
					{
						var cmd = commands[i];
						switch(cmd.Type)
						{
							case GraphicsCommandType.Draw:
								callInfo.rapi.RenderDrawable(shadowHandle, cmd.Offset, (int)cmd.Count);
								break;
							case GraphicsCommandType.SetUniform:
								index = shadowUniformsMap[cmd.Index];
								if(index >= 0)
								{
									callInfo.shaderUniforms[index].SetValue(cmd.Offset == uint.MaxValue ? zeroPtr : (dataPtr + cmd.Offset), (int)cmd.Count);
								}
								break;
							case GraphicsCommandType.BindTexture:
								index = shadowUniformsMap[cmd.Index];
								if(index >= 0)
								{
									callInfo.shader.BindTexture(index, (EnumTextureTarget)cmd.Arg, *(int*)(cmd.Offset == uint.MaxValue ? zeroPtr : (dataPtr + cmd.Offset)));
								}
								break;
						}
					}

					//Reset uniforms
					for(int i = shadowUniformsMap.Length - 1; i >= 0; i--)
					{
						index = shadowUniformsMap[i];
						if(index >= 0)
						{
							callInfo.shaderUniforms[index].SetValue(zeroPtr, uniformsMap[i].Count);
						}
					}
				}
			}
		}

		public unsafe void Render(ref BatchDrawCall callInfo, ref byte[] zeroBytes)
		{
			if(zeroBytes.Length < maxUniformSize)
			{
				zeroBytes = new byte[maxUniformSize];
			}

			fixed(byte* dataPtr = uniformsData)
			{
				fixed(byte* zeroPtr = zeroBytes)
				{
					int last = renderPasses[callInfo.pass].Index + renderPasses[callInfo.pass].Count;
					for(int i = renderPasses[callInfo.pass].Index; i < last; i++)
					{
						var cmd = commands[i];
						switch(cmd.Type)
						{
							case GraphicsCommandType.Draw:
								callInfo.rapi.RenderDrawable(drawHandle, cmd.Offset, (int)cmd.Count);
								break;
							case GraphicsCommandType.SetUniform:
								callInfo.shaderUniforms[uniformsMap[cmd.Index].Index].SetValue(cmd.Offset == uint.MaxValue ? zeroPtr : (dataPtr + cmd.Offset), (int)cmd.Count);
								break;
							case GraphicsCommandType.BindTexture:
								callInfo.shader.BindTexture(uniformsMap[cmd.Index].Index, (EnumTextureTarget)cmd.Arg, *(int*)(cmd.Offset == uint.MaxValue ? zeroPtr : (dataPtr + cmd.Offset)));
								break;
						}
					}

					//Reset uniforms
					for(int i = uniformsMap.Length - 1; i >= 0; i--)
					{
						callInfo.shaderUniforms[uniformsMap[i].Index].SetValue(zeroPtr, uniformsMap[i].Count);
					}
				}
			}
		}
	}

	public ref struct BatchDrawCall
	{
		public IRenderAPI rapi;
		public int pass;
		public GraphicsSystem graphics;
		public UniformPropertyHandle[] shaderUniforms;
		public IExtendedShaderProgram shader;
	}
}