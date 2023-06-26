using PowerOfMind.Collections;
using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Drawable;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace PowerOfMind.Systems.RenderBatching
{
	public partial class ChunkBatching
	{
		private partial class BuildTask
		{
			private class BuilderContext : IBatchBuildContext
			{
				public int vertOffsetLocal = 0;
				public int indOffsetLocal = 0;

				public int builderIndex = 0;

				public readonly Dictionary<int, int> uniformsMap = new Dictionary<int, int>();
				public readonly Dictionary<int, int> tmpUniformsMap = new Dictionary<int, int>();
				public readonly Dictionary<int, int> uniformToIndexMap = new Dictionary<int, int>();

				public KeyValuePair<int, int>[][] builderVertexMaps;
				public VertexDeclaration[] builderDeclarations;

				private readonly BuildTask task;

				private readonly List<BuildCommand> commands = new List<BuildCommand>();
				private readonly List<uint[]> indicesBlocks = new List<uint[]>();
				private readonly UniformsDataCollection uniformsData = new UniformsDataCollection(BLOCK_SIZE);

				private int currentVertCount;
				private int currentIndCount;
				private int currentVertBlock = 0;
				private int currentIndBlock = 0;

				private bool hasIndices = false;
				private bool hasVertices = false;
				private IndicesContext.ProcessorDelegate addIndicesCallback;
				private VerticesContext.ProcessorDelegate addVerticesCallback;

				private readonly Dictionary<int, int> componentsToMap = new Dictionary<int, int>();
				private readonly RefList<VertexAttribute> tmpAttributes = new RefList<VertexAttribute>();
				private readonly List<KeyValuePair<int, int>> tmpPairs = new List<KeyValuePair<int, int>>();

				private readonly Action addMappedUniformsCallback;

				public unsafe BuilderContext(BuildTask task)
				{
					this.task = task;

					addIndicesCallback = InsertIndices;
					addVerticesCallback = ProcessVerties;
					addMappedUniformsCallback = AddMappedUniforms;
				}

				public void Init()
				{
					var builders = task.builders;
					this.builderVertexMaps = new KeyValuePair<int, int>[builders.Length][];
					this.builderDeclarations = new VertexDeclaration[builders.Length];
					var attributes = new RefList<VertexAttribute>();
					for(int i = 0; i < builders.Length; i++)
					{
						task.shader.MapDeclaration(task.container.builders[builders[i]].builderStruct.GetVertexDeclaration(), attributes);
						builderDeclarations[i] = new VertexDeclaration(attributes.ToArray());
						attributes.Clear();
					}

					int vertStride = 0;
					var tmpMap = new List<KeyValuePair<int, int>>();
					var locationToAttrib = new Dictionary<int, int>();
					for(int i = 0; i < builderDeclarations.Length; i++)
					{
						var declaration = builderDeclarations[i];
						for(int j = 0; j < declaration.Attributes.Length; j++)
						{
							if(!locationToAttrib.TryGetValue(declaration.Attributes[j].Location, out var index))
							{
								index = attributes.Count;
								AddAttrib(ref vertStride, declaration.Attributes, j, attributes);
								locationToAttrib[declaration.Attributes[j].Location] = index;
							}
							else
							{
								if(declaration.Attributes[j].Size != attributes[index].Size || declaration.Attributes[j].Type != attributes[index].Type)
								{
									var msg = string.Format(
										"Vertex component {0} of builder {1} does not match size or type with other users of this shader. {2}({3}) was expected but {4}({5}) was provided.",
										string.Format(string.IsNullOrEmpty(declaration.Attributes[j].Alias) ? "`{0}`" : "`{0}`[{1}]", declaration.Attributes[j].Name, declaration.Attributes[j].Alias),
										builders[i],
										attributes[index].Type,
										attributes[index].Size,
										declaration.Attributes[j].Type,
										declaration.Attributes[j].Size
									);
									var capi = task.container.capi;
									capi.Event.EnqueueMainThreadTask(() => capi.Logger.Log(EnumLogType.Warning, msg), "powerofmind:chunkbuildlog");
									continue;
								}
							}
							tmpMap.Add(new KeyValuePair<int, int>(j, index));
						}
						if(vertStride == 0)
						{
							throw new Exception(string.Format("Builder {0} has no vertex attributes associated with the shader", task.container.builders[builders[i]].builder));
						}

						builderVertexMaps[i] = tmpMap.ToArray();
						tmpMap.Clear();
					}
					for(int i = 0; i < attributes.Count; i++)
					{
						ref readonly var attrib = ref attributes[i];
						attributes[i] = new VertexAttribute(
							attrib.Name,
							attrib.Alias,
							attrib.Location,
							(uint)vertStride,
							attrib.Offset,
							attrib.InstanceDivisor,
							attrib.Size,
							attrib.Type,
							attrib.Normalized,
							attrib.IntegerTarget
						);
					}

					task.declaration = new VertexDeclaration(attributes.ToArray());
					task.verticesStride = vertStride;

					task.verticesBlocks.Add(new byte[task.verticesStride * BLOCK_SIZE]);
					indicesBlocks.Add(new uint[BLOCK_SIZE]);
				}

				public void SetBuilder(int builderIndex)
				{
					this.builderIndex = builderIndex;
					this.commands.Add(new BuildCommand(BuildCommand.CommandType.SetBuilder, (uint)builderIndex));
				}

				public void BuildCommands(out GraphicsCommand[] commands, out uint[] indices, out byte[] uniformsData,
					out UniformPointer[] uniformPointers, out RenderPassGroup[] renderPasses)
				{
					int uniformsCount = uniformToIndexMap.Count;
					var currentUniforms = new int[uniformsCount];
					currentUniforms.Fill(-1);

					var uniformsVariantsCount = new int[uniformsCount];

					var usedUniformVariants = new HashSet<UniformKey>();
					var uniformsPerDrawGroup = new RefList<int>();
					var drawGroups = new List<DrawGroup>();

					bool firstDraw = true;
					int drawUniformGroup = 0;
					int drawStartIndex = 0;
					int drawCount = 0;
					var renderPass = EnumChunkRenderPass.Opaque;
					IBuilderStructContainer currentStruct = null;
					for(int i = 0; i < this.commands.Count; i++)
					{
						switch(this.commands[i].type)
						{
							case BuildCommand.CommandType.SetBuilder:
								if(!firstDraw)
								{
									firstDraw = true;
									drawGroups.Add(new DrawGroup(drawUniformGroup, drawStartIndex, drawCount, renderPass));
									drawCount = 0;
								}
								currentStruct = task.container.builders[task.builders[this.commands[i].arg0]].builderStruct;

								tmpUniformsMap.Clear();
								task.shader.MapDeclarationInv(currentStruct.GetUniformsDeclaration(), tmpUniformsMap);
								break;
							case BuildCommand.CommandType.OverrideUniform:
								if(!firstDraw)
								{
									firstDraw = true;
									drawGroups.Add(new DrawGroup(drawUniformGroup, drawStartIndex, drawCount, renderPass));
									drawCount = 0;
								}
								tmpUniformsMap.Remove((int)this.commands[i].arg0);
								currentUniforms[uniformToIndexMap[(int)this.commands[i].arg0]] = (int)this.commands[i].arg1;
								break;
							case BuildCommand.CommandType.SetPass:
								if(renderPass != (EnumChunkRenderPass)this.commands[i].arg0)
								{
									if(!firstDraw)
									{
										firstDraw = true;
										drawGroups.Add(new DrawGroup(drawUniformGroup, drawStartIndex, drawCount, renderPass));
										drawCount = 0;
									}
									renderPass = (EnumChunkRenderPass)this.commands[i].arg0;
								}
								break;
							case BuildCommand.CommandType.DrawIndices:
								if(firstDraw)
								{
									if(tmpUniformsMap.Count > 0)
									{
										currentStruct.CollectUniformsData(tmpUniformsMap, tmpPairs, this.uniformsData);
										foreach(var pair in tmpUniformsMap)
										{
											currentUniforms[uniformToIndexMap[pair.Key]] = pair.Value;
										}
									}

									drawUniformGroup = uniformsPerDrawGroup.Count;

									firstDraw = false;
									for(int j = 0; j < uniformsCount; j++)
									{
										if(usedUniformVariants.Add(new UniformKey(j, currentUniforms[j])))
										{
											uniformsVariantsCount[j]++;
										}

										uniformsPerDrawGroup.Add(currentUniforms[j]);
									}

									drawStartIndex = i;
								}
								drawCount++;
								break;
						}
					}
					if(!firstDraw)
					{
						drawGroups.Add(new DrawGroup(drawUniformGroup, drawStartIndex, drawCount, renderPass));
					}

					var pointers = new List<UniformPointer>();
					var uniformsMap = new int[uniformsCount];
					var shaderUniforms = task.shader.Uniforms.Properties;
					foreach(var pair in uniformToIndexMap)
					{
						if(uniformsVariantsCount[pair.Value] == 0)
						{
							uniformsMap[pair.Value] = -1;
						}
						else
						{
							uniformsMap[pair.Value] = pointers.Count;
							pointers.Add(new UniformPointer(pair.Key, shaderUniforms[pair.Key].UniformSize * ShaderExtensions.GetTypeSize(shaderUniforms[pair.Key].Type), shaderUniforms[pair.Key].UniformSize / shaderUniforms[pair.Key].StructSize));
						}
					}

					var compareOrderByUsage = new int[uniformsCount];
					for(int i = 0; i < uniformsCount; i++) compareOrderByUsage[i] = i;
					Array.Sort(compareOrderByUsage, (a, b) => {
						int c = uniformsVariantsCount[b].CompareTo(uniformsVariantsCount[a]);
						if(c != 0) return c;
						return a.CompareTo(b);
					});
					drawGroups.Sort((a, b) => {
						int c = a.renderPass.CompareTo(b.renderPass);
						if(c != 0) return c;
						return CompareUniformGroups(a.uniformsIndex, b.uniformsIndex, uniformsPerDrawGroup, compareOrderByUsage);
					});

					indices = new uint[task.indicesCount];
					var list = new List<GraphicsCommand>();
					var rpGroups = new List<RenderPassGroup>();
					currentUniforms.Fill(-1);
					renderPass = EnumChunkRenderPass.Opaque;
					int renderPassGroupStart = 0;
					uint indicesCount = 0;
					uint indicesStart = 0;
					for(int i = 0; i < drawGroups.Count; i++)
					{
						var drawGroup = drawGroups[i];

						if(renderPass != drawGroup.renderPass)
						{
							if(indicesCount > 0)
							{
								list.Add(new GraphicsCommand(indicesStart, indicesCount));
								indicesStart += indicesCount;
								indicesCount = 0;
							}

							if(renderPassGroupStart < list.Count)
							{
								rpGroups.Add(new RenderPassGroup(renderPass, renderPassGroupStart, list.Count - renderPassGroupStart));

								renderPassGroupStart = list.Count;
							}

							for(int j = 0; j < uniformsCount; j++)
							{
								currentUniforms[j] = -1;
							}

							renderPass = drawGroup.renderPass;
						}
						for(int j = 0; j < uniformsCount; j++)
						{
							if(uniformsPerDrawGroup[j + drawGroup.uniformsIndex] != currentUniforms[j])
							{
								if(indicesCount > 0)
								{
									list.Add(new GraphicsCommand(indicesStart, indicesCount));
									indicesStart += indicesCount;
									indicesCount = 0;
								}

								currentUniforms[j] = uniformsPerDrawGroup[j + drawGroup.uniformsIndex];

								var block = this.uniformsData.blocks[currentUniforms[j]];
								var uniformIndex = pointers[uniformsMap[j]].Index;

								var count = (uint)(block.size / (shaderUniforms[uniformIndex].StructSize * ShaderExtensions.GetTypeSize(shaderUniforms[uniformIndex].Type)));
								switch(shaderUniforms[uniformIndex].StructType)
								{
									case EnumUniformStructType.Sampler1D:
									case EnumUniformStructType.Sampler1DShadow:
									case EnumUniformStructType.IntSampler1D:
									case EnumUniformStructType.UnsignedIntSampler1D:
										list.Add(new GraphicsCommand((uint)block.offset, count, (uint)uniformsMap[j], EnumTextureTarget.Texture1D));
										break;
									case EnumUniformStructType.Sampler2D:
									case EnumUniformStructType.Sampler2DShadow:
									case EnumUniformStructType.IntSampler2D:
									case EnumUniformStructType.UnsignedIntSampler2D:
										list.Add(new GraphicsCommand((uint)block.offset, count, (uint)uniformsMap[j], EnumTextureTarget.Texture2D));
										break;
									case EnumUniformStructType.Sampler3D:
									case EnumUniformStructType.IntSampler3D:
									case EnumUniformStructType.UnsignedIntSampler3D:
										list.Add(new GraphicsCommand((uint)block.offset, count, (uint)uniformsMap[j], EnumTextureTarget.Texture3D));
										break;
									case EnumUniformStructType.Sampler2DRect:
									case EnumUniformStructType.Sampler2DRectShadow:
									case EnumUniformStructType.IntSampler2DRect:
									case EnumUniformStructType.UnsignedIntSampler2DRect:
										list.Add(new GraphicsCommand((uint)block.offset, count, (uint)uniformsMap[j], EnumTextureTarget.TextureRectangle));
										break;
									case EnumUniformStructType.SamplerCube:
									case EnumUniformStructType.SamplerCubeShadow:
									case EnumUniformStructType.IntSamplerCube:
									case EnumUniformStructType.UnsignedIntSamplerCube:
										list.Add(new GraphicsCommand((uint)block.offset, count, (uint)uniformsMap[j], EnumTextureTarget.TextureCubeMap));
										break;
									case EnumUniformStructType.Sampler1DArray:
									case EnumUniformStructType.Sampler1DArrayShadow:
									case EnumUniformStructType.IntSampler1DArray:
									case EnumUniformStructType.UnsignedIntSampler1DArray:
										list.Add(new GraphicsCommand((uint)block.offset, count, (uint)uniformsMap[j], EnumTextureTarget.Texture1DArray));
										break;
									case EnumUniformStructType.Sampler2DArray:
									case EnumUniformStructType.Sampler2DArrayShadow:
									case EnumUniformStructType.IntSampler2DArray:
									case EnumUniformStructType.UnsignedIntSampler2DArray:
										list.Add(new GraphicsCommand((uint)block.offset, count, (uint)uniformsMap[j], EnumTextureTarget.Texture2DArray));
										break;
									case EnumUniformStructType.SamplerBuffer:
									case EnumUniformStructType.IntSamplerBuffer:
									case EnumUniformStructType.UnsignedIntSamplerBuffer:
										list.Add(new GraphicsCommand((uint)block.offset, count, (uint)uniformsMap[j], EnumTextureTarget.TextureBuffer));
										break;
									case EnumUniformStructType.SamplerCubeMapArray:
									case EnumUniformStructType.SamplerCubeMapArrayShadow:
									case EnumUniformStructType.IntSamplerCubeMapArray:
									case EnumUniformStructType.UnsignedIntSamplerCubeMapArray:
										list.Add(new GraphicsCommand((uint)block.offset, count, (uint)uniformsMap[j], EnumTextureTarget.TextureCubeMapArray));
										break;
									case EnumUniformStructType.Sampler2DMultisample:
									case EnumUniformStructType.IntSampler2DMultisample:
									case EnumUniformStructType.UnsignedIntSampler2DMultisample:
										list.Add(new GraphicsCommand((uint)block.offset, count, (uint)uniformsMap[j], EnumTextureTarget.Texture2DMultisample));
										break;
									case EnumUniformStructType.Sampler2DMultisampleArray:
									case EnumUniformStructType.IntSampler2DMultisampleArray:
									case EnumUniformStructType.UnsignedIntSampler2DMultisampleArray:
										list.Add(new GraphicsCommand((uint)block.offset, count, (uint)uniformsMap[j], EnumTextureTarget.Texture2DMultisampleArray));
										break;
									default:
										list.Add(new GraphicsCommand((uint)block.offset, count, (uint)uniformsMap[j]));
										break;
								}
							}
						}

						for(int j = 0, k = 0; j < drawGroup.cmdCount; k++)
						{
							var cmd = this.commands[drawGroup.cmdIndex + k];
							if(cmd.type != BuildCommand.CommandType.DrawIndices)
							{
								continue;
							}
							CopyIndicesFromBlock((int)cmd.arg0, indices, indicesStart + indicesCount, (int)cmd.arg1);
							indicesCount += cmd.arg1;
							j++;
						}
					}
					if(indicesCount > 0)
					{
						list.Add(new GraphicsCommand(indicesStart, indicesCount));
					}
					if(renderPassGroupStart < list.Count)
					{
						rpGroups.Add(new RenderPassGroup(renderPass, renderPassGroupStart, list.Count - renderPassGroupStart));
					}

					commands = list.ToArray();
					uniformsData = this.uniformsData.dataBuffer.ToArray();
					uniformPointers = pointers.ToArray();

					renderPasses = rpGroups.ToArray();
				}

				unsafe void IBatchBuildContext.AddData(IDrawableData data, EnumChunkRenderPass renderPass)
				{
					if(data.DrawMode != EnumDrawMode.Triangles) throw new InvalidOperationException("Only triangles are allowed here");
					AddData(data, renderPass);
				}

				unsafe void IBatchBuildContext.AddData<T>(IDrawableData data, in T uniformsData, EnumChunkRenderPass renderPass)
				{
					if(data.DrawMode != EnumDrawMode.Triangles) throw new InvalidOperationException("Only triangles are allowed here");
					MapUniforms(uniformsData);
					if(tmpUniformsMap.Count > 0)
					{
						AddData(data, renderPass, addMappedUniformsCallback);
					}
					else
					{
						AddData(data, renderPass);
					}
				}

				private unsafe void ProcessVerties(void* data, int stride)
				{
					if(data == null) return;
					for(int i = 0; i < tmpAttributes.Count; i++)
					{
						if(componentsToMap.TryGetValue(tmpAttributes[i].Location, out int mapIndex))
						{
							int attribIndex = builderVertexMaps[builderIndex][mapIndex].Value;
							ref readonly var vertAttrib = ref task.declaration.Attributes[attribIndex];
							if(ShaderExtensions.GetTypeSize(tmpAttributes[i].Type) == ShaderExtensions.GetTypeSize(vertAttrib.Type))//TODO: log warning otherwise?
							{
								componentsToMap.Remove(tmpAttributes[i].Location);

								hasVertices = true;
								CopyComponentData(attribIndex, (byte*)data + tmpAttributes[i].Offset, stride);
							}
						}
					}
				}

				private unsafe void CopyIndicesFromBlock(int index, uint[] outIndices, uint outIndicesIndex, int count)
				{
					int blockIndex = index / BLOCK_SIZE;
					int blockOffset = index % BLOCK_SIZE;
					fixed(uint* ptr = outIndices)
					{
						uint* iPtr = ptr + outIndicesIndex;
						while(count > 0)
						{
							int c = CopyIndicesBlock(iPtr, count, blockIndex, blockOffset);
							count -= c;
							iPtr += c;
							blockIndex++;
							blockOffset = 0;
						}
					}
				}

				private unsafe void AddData(IDrawableData data, EnumChunkRenderPass renderPass, Action beforeCmd = null)
				{
					if(data.GetIndicesMeta().IsDynamic) return;

					currentVertCount = (int)data.VerticesCount;
					currentIndCount = (int)data.IndicesCount;
					EnsureCapacity();

					hasIndices = false;
					data.ProvideIndices(new IndicesContext(addIndicesCallback));

					if(!hasIndices) return;

					componentsToMap.Clear();
					var map = builderVertexMaps[builderIndex];
					for(int i = 0; i < map.Length; i++)
					{
						componentsToMap[task.declaration.Attributes[map[i].Value].Location] = i;
					}

					hasVertices = false;
					int vBuffCount = data.VertexBuffersCount;
					for(int i = 0; i < vBuffCount; i++)
					{
						var meta = data.GetVertexBufferMeta(i);
						if(meta.IsDynamic) continue;

						tmpAttributes.Clear();
						task.shader.MapDeclaration(meta.Declaration, tmpAttributes);
						data.ProvideVertices(new VerticesContext(addVerticesCallback, i));
					}

					if(hasVertices)
					{
						if(componentsToMap.Count > 0)
						{
							var declaration = builderDeclarations[builderIndex];
							var builderStruct = task.container.builders[task.builders[builderIndex]].builderStruct;
							var stride = builderStruct.GetVertexStride();
							builderStruct.ProvideVertexData(ptr => {
								foreach(var p in componentsToMap)
								{
									var pair = map[p.Value];
									CopyComponentData(pair.Value, ptr + declaration.Attributes[pair.Key].Offset, stride);
								}
							});
						}

						commands.Add(new BuildCommand(BuildCommand.CommandType.SetPass, (uint)renderPass));

						beforeCmd?.Invoke();
						commands.Add(new BuildCommand(BuildCommand.CommandType.DrawIndices, task.indicesCount, (uint)currentIndCount));

						task.verticesCount += (uint)currentVertCount;
						task.indicesCount += (uint)currentIndCount;
						currentVertBlock += (vertOffsetLocal + currentVertCount) / BLOCK_SIZE;
						currentIndBlock += (indOffsetLocal + currentIndCount) / BLOCK_SIZE;
						vertOffsetLocal = (vertOffsetLocal + currentVertCount) % BLOCK_SIZE;
						indOffsetLocal = (indOffsetLocal + currentIndCount) % BLOCK_SIZE;
					}
				}

				private unsafe void MapUniforms<T>(in T uniformsData) where T : unmanaged, IUniformsData
				{
					var builderStruct = task.container.builders[task.builders[builderIndex]].builderStruct;
					tmpUniformsMap.Clear();
					task.shader.MapDeclarationInv(uniformsData.GetDeclaration(), tmpUniformsMap);
					builderStruct.DiffUniforms(uniformsData, uniformsMap, tmpUniformsMap, tmpPairs, this.uniformsData);
				}

				private void AddMappedUniforms()
				{
					foreach(var pair in tmpUniformsMap)
					{
						commands.Add(new BuildCommand(BuildCommand.CommandType.OverrideUniform, (uint)pair.Key, (uint)pair.Value));
					}
				}

				private unsafe void CopyComponentData(int attribIndex, byte* dataPtr, int stride)
				{
					ref readonly var vertAttrib = ref task.declaration.Attributes[attribIndex];
					int buffStride = task.verticesStride;

					long copyCount = ShaderExtensions.GetTypeSize(vertAttrib.Type) * vertAttrib.Size;
					long buffOffset = vertOffsetLocal * task.verticesStride;
					int countToCopy = currentVertCount;

					int blockIndex = this.currentVertBlock;
					var currentDataBlock = task.verticesBlocks[blockIndex];
					if(countToCopy > BLOCK_SIZE - vertOffsetLocal)
					{
						int copyLeft = BLOCK_SIZE - vertOffsetLocal;
						countToCopy -= copyLeft;
						fixed(byte* ptr = currentDataBlock)
						{
							var buffPtr = ptr + buffOffset + vertAttrib.Offset;
							while(copyLeft > 0)
							{
								Buffer.MemoryCopy(dataPtr, buffPtr, copyCount, copyCount);
								dataPtr += stride;
								buffPtr += buffStride;
								copyLeft--;
							}
							buffOffset = 0;
						}

						currentDataBlock = task.verticesBlocks[++blockIndex];
						int count = countToCopy / BLOCK_SIZE;
						while(count > 0)
						{
							copyLeft = BLOCK_SIZE;
							fixed(byte* ptr = currentDataBlock)
							{
								var buffPtr = ptr + vertAttrib.Offset;
								while(copyLeft > 0)
								{
									Buffer.MemoryCopy(dataPtr, buffPtr, copyCount, copyCount);
									dataPtr += stride;
									buffPtr += buffStride;
									copyLeft--;
								}
							}

							currentDataBlock = task.verticesBlocks[++blockIndex];
							countToCopy -= BLOCK_SIZE;
							count--;
						}
					}
					if(countToCopy > 0)
					{
						fixed(byte* ptr = currentDataBlock)
						{
							var buffPtr = ptr + buffOffset + vertAttrib.Offset;
							while(countToCopy > 0)
							{
								Buffer.MemoryCopy(dataPtr, buffPtr, copyCount, copyCount);
								dataPtr += stride;
								buffPtr += buffStride;
								countToCopy--;
							}
						}
					}
				}

				private unsafe void InsertIndices(uint* indices)
				{
					if(indices == null) return;
					hasIndices = true;

					int countToCopy = currentIndCount;
					uint indicesOffset = task.verticesCount;
					int blockIndex = currentIndBlock;
					int blockOffset = indOffsetLocal;
					while(countToCopy > 0)
					{
						int count = AddIndicesBlock(indices, indicesOffset, countToCopy, blockIndex, blockOffset);
						countToCopy -= count;
						indices += count;
						blockIndex++;
						blockOffset = 0;
					}
				}

				private unsafe int AddIndicesBlock(uint* indices, uint offset, int count, int blockIndex, int blockOffset)
				{
					var currentDataBlock = indicesBlocks[blockIndex];
					int countToCopy = Math.Min(BLOCK_SIZE - blockOffset, count);
					fixed(uint* ptr = currentDataBlock)
					{
						var iPtr = ptr + blockOffset;
						for(int i = 0; i < countToCopy; i++)
						{
							*iPtr = *indices + offset;
							indices++;
							iPtr++;
						}
					}
					return countToCopy;
				}

				private unsafe int CopyIndicesBlock(uint* outIndices, int count, int blockIndex, int blockOffset)
				{
					var currentDataBlock = indicesBlocks[blockIndex];
					int copyCount = Math.Min(BLOCK_SIZE - blockOffset, count);
					fixed(uint* ptr = currentDataBlock)
					{
						Buffer.MemoryCopy(ptr + blockOffset, outIndices, count * 4, copyCount * 4);
					}
					return copyCount;
				}

				private void EnsureCapacity()
				{
					if(currentVertCount > BLOCK_SIZE - vertOffsetLocal)
					{
						int addBlocks = (currentVertCount - (BLOCK_SIZE - vertOffsetLocal) - 1) / BLOCK_SIZE + 1;
						while(addBlocks > 0)
						{
							task.verticesBlocks.Add(new byte[task.verticesStride * BLOCK_SIZE]);
							addBlocks--;
						}
					}
					if(currentIndCount > BLOCK_SIZE - indOffsetLocal)
					{
						int addBlocks = (currentIndCount - (BLOCK_SIZE - indOffsetLocal) - 1) / BLOCK_SIZE + 1;
						while(addBlocks > 0)
						{
							indicesBlocks.Add(new uint[BLOCK_SIZE]);
							addBlocks--;
						}
					}
				}

				private static int CompareUniformGroups(int aIndex, int bIndex, RefList<int> groupList, int[] compareOrderByUsage)
				{
					int len = compareOrderByUsage.Length;
					for(int i = 0; i < len; i++)
					{
						int c = groupList[aIndex + compareOrderByUsage[i]].CompareTo(groupList[bIndex + compareOrderByUsage[i]]);
						if(c != 0) return c;
					}
					return aIndex.CompareTo(bIndex);
				}
			}
		}
	}
}