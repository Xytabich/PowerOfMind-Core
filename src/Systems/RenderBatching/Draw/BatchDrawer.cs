using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Shader;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vintagestory.API.Client;

namespace PowerOfMind.Systems.RenderBatching.Draw
{
	public class BatchDrawer
	{
		public readonly IDrawableHandle DrawHandle;
		public readonly IDrawableHandle ShadowHandle;

		private readonly RenderPassGroup[] renderPasses;
		private readonly GraphicsCommand[] commands;
		private readonly UniformPointer[] uniformsMap;
		private readonly int[] shadowUniformsMap;
		private readonly byte[] uniformsData;
		private readonly int maxUniformSize;

		public BatchDrawer(IDrawableHandle drawHandle, IDrawableHandle shadowHandle, RenderPassGroup[] renderPasses, GraphicsCommand[] commands, UniformPointer[] uniformsMap, int[] shadowUniformsMap, byte[] uniformsData)
		{
			this.DrawHandle = drawHandle;
			this.ShadowHandle = shadowHandle;
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
			DrawHandle.Dispose();
			ShadowHandle?.Dispose();
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

			fixed(byte* zeroPtr = zeroBytes)
			{
				int index;
				UniformsStateInfo.UniformInfo state;
				var uniformsState = callInfo.UniformsState.Uniforms;
				fixed(byte* defPtr = callInfo.UniformsState.Data)
				{
					fixed(byte* dataPtr = uniformsData)
					{
						int last = renderPasses[callInfo.Pass].Index + renderPasses[callInfo.Pass].Count;
						for(int i = renderPasses[callInfo.Pass].Index; i < last; i++)
						{
							var cmd = commands[i];
							switch(cmd.Type)
							{
								case GraphicsCommandType.Draw:
									callInfo.Rapi.RenderDrawable(ShadowHandle, cmd.Offset, (int)cmd.Count);
									break;
								case GraphicsCommandType.SetUniform:
									index = shadowUniformsMap[cmd.Index];
									if(index >= 0)
									{
										uniformsState[index].OnChanged();
										callInfo.ShaderUniforms[index].SetValue(dataPtr + cmd.Offset, (int)cmd.Count);
									}
									break;
								case GraphicsCommandType.SetUniformZero:
									index = shadowUniformsMap[cmd.Index];
									if(index >= 0)
									{
										if(uniformsState[index].DefaultIsZero && !uniformsState[index].IsChanged) continue;
										uniformsState[index].OnChangedToZero();
										callInfo.ShaderUniforms[index].SetValue(zeroPtr, (int)cmd.Count);
									}
									break;
								case GraphicsCommandType.SetUniformDefault:
									index = shadowUniformsMap[cmd.Index];
									if(index >= 0)
									{
										if(!uniformsState[index].IsChanged) continue;
										state = uniformsState[index];

										if(state.DefaultIsZero)
										{
											callInfo.ShaderUniforms[index].SetValue(zeroPtr, (int)cmd.Count);
										}
										else
										{
											callInfo.ShaderUniforms[index].SetValue(defPtr + state.DataIndex, state.Count);
										}
										uniformsState[index].IsChanged = false;
									}
									break;
								case GraphicsCommandType.BindTexture:
									index = shadowUniformsMap[cmd.Index];
									if(index >= 0)
									{
										callInfo.Shader.BindTexture(index, (EnumTextureTarget)cmd.Arg, *(int*)(cmd.Offset == uint.MaxValue ? zeroPtr : (dataPtr + cmd.Offset)));
									}
									break;
							}
						}
					}

					for(int i = shadowUniformsMap.Length - 1; i >= 0; i--)
					{
						index = shadowUniformsMap[i];
						if(index < 0) continue;
						if(uniformsState[index].IsChanged)
						{
							state = uniformsState[index];
							if(state.DefaultIsZero)
							{
								callInfo.ShaderUniforms[index].SetValue(zeroPtr, uniformsMap[i].Count);
							}
							else
							{
								callInfo.ShaderUniforms[index].SetValue(defPtr + state.DataIndex, state.Count);
							}
							uniformsState[index].IsChanged = false;
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

			fixed(byte* zeroPtr = zeroBytes)
			{
				int index;
				UniformsStateInfo.UniformInfo state;
				var uniformsState = callInfo.UniformsState.Uniforms;
				fixed(byte* defPtr = callInfo.UniformsState.Data)
				{
					fixed(byte* dataPtr = uniformsData)
					{
						int last = renderPasses[callInfo.Pass].Index + renderPasses[callInfo.Pass].Count;
						for(int i = renderPasses[callInfo.Pass].Index; i < last; i++)
						{
							var cmd = commands[i];
							switch(cmd.Type)
							{
								case GraphicsCommandType.Draw:
									callInfo.Rapi.RenderDrawable(DrawHandle, cmd.Offset, (int)cmd.Count);
									break;
								case GraphicsCommandType.SetUniform:
									index = uniformsMap[cmd.Index].Index;
									uniformsState[index].OnChanged();
									callInfo.ShaderUniforms[index].SetValue(dataPtr + cmd.Offset, (int)cmd.Count);
									break;
								case GraphicsCommandType.SetUniformZero:
									index = uniformsMap[cmd.Index].Index;
									if(uniformsState[index].DefaultIsZero && !uniformsState[index].IsChanged) continue;

									uniformsState[index].OnChangedToZero();
									callInfo.ShaderUniforms[index].SetValue(zeroPtr, (int)cmd.Count);
									break;
								case GraphicsCommandType.SetUniformDefault:
									index = uniformsMap[cmd.Index].Index;
									if(!uniformsState[index].IsChanged) continue;

									state = uniformsState[index];
									if(state.DefaultIsZero)
									{
										callInfo.ShaderUniforms[index].SetValue(zeroPtr, (int)cmd.Count);
									}
									else
									{
										callInfo.ShaderUniforms[index].SetValue(defPtr + state.DataIndex, state.Count);
									}
									uniformsState[index].IsChanged = false;
									break;
								case GraphicsCommandType.BindTexture:
									callInfo.Shader.BindTexture(uniformsMap[cmd.Index].Index, (EnumTextureTarget)cmd.Arg, *(int*)(cmd.Offset == uint.MaxValue ? zeroPtr : (dataPtr + cmd.Offset)));
									break;
							}
						}
					}

					for(int i = uniformsMap.Length - 1; i >= 0; i--)
					{
						index = uniformsMap[i].Index;
						if(uniformsState[index].IsChanged)
						{
							state = uniformsState[index];
							if(state.DefaultIsZero)
							{
								callInfo.ShaderUniforms[index].SetValue(zeroPtr, uniformsMap[i].Count);
							}
							else
							{
								callInfo.ShaderUniforms[index].SetValue(defPtr + state.DataIndex, state.Count);
							}
							uniformsState[index].IsChanged = false;
						}
					}
				}
			}
		}
	}

	public ref struct BatchDrawCall
	{
		public IRenderAPI Rapi;
		public int Pass;
		public GraphicsSystem Graphics;
		public UniformPropertyHandle[] ShaderUniforms;
		public IExtendedShaderProgram Shader;

		public UniformsStateInfo UniformsState;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct UniformsStateInfo
	{
		public byte[] Data;
		public UniformInfo[] Uniforms;

		public void EnsureDataSize(int size)
		{
			if(Data == null || Data.Length < size)
			{
				Data = new byte[size];
			}
		}

		public void EnsureUniformsCount(int count)
		{
			if(Uniforms == null || Uniforms.Length < count)
			{
				Uniforms = new UniformInfo[count];
			}
		}

		public unsafe void Reset()
		{
			fixed(UniformInfo* ptr = Uniforms)
			{
				Unsafe.InitBlockUnaligned(ptr, 255, (uint)sizeof(UniformInfo) * (uint)Uniforms.Length);// Set -1 to all integers
			}
		}

		public unsafe void AddUniformData<T>(int uniformIndex, T data, int count, int byteArrayOffset) where T : unmanaged
		{
			fixed(byte* ptr = Data)
			{
				Unsafe.CopyBlockUnaligned(ptr, &data, (uint)sizeof(T));
				Uniforms[uniformIndex] = new UniformInfo(byteArrayOffset, count, false);
			}
		}

		public unsafe void AddUniformData(int uniformIndex, void* dataPtr, int dataSize, int count, int byteArrayOffset)
		{
			fixed(byte* ptr = Data)
			{
				Unsafe.CopyBlockUnaligned(ptr, dataPtr, (uint)dataSize);
				Uniforms[uniformIndex] = new UniformInfo(byteArrayOffset, count, false);
			}
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		public struct UniformInfo
		{
			public readonly int DataIndex;
			public readonly int Count;

			public bool DefaultIsZero => DataIndex < 0;
			public bool IsChanged { get => state == 1; set => state = value ? 1 : -1; }

			private int state;

			public UniformInfo(int dataIndex, int count, bool isChanged)
			{
				DataIndex = dataIndex;
				Count = count;
				state = isChanged ? 1 : -1;
			}

			public void OnChangedToZero()
			{
				if(DataIndex < 0) return;
				state = 1;
			}

			public void OnChanged()
			{
				state = 1;
			}
		}
	}
}