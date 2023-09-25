using PowerOfMind.Collections;
using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Shader;
using PowerOfMind.Systems.RenderBatching.Draw;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PowerOfMind.Systems.RenderBatching.Core
{
	/// <summary>
	/// Batches static geometry according to shader
	/// </summary>
	public partial class BatchBuildTask
	{
		public const int BLOCK_SIZE = 1024;

		public bool Failed = false;

		public RenderPassGroup[] RenderPasses;
		public GraphicsCommand[] Commands;
		public UniformPointer[] UniformsMap;
		public int[] ShadowUniformsMap = null;
		public byte[] UniformsData;

		public uint VerticesCount;
		public uint IndicesCount;
		public int VerticesStride;
		public VertexDeclaration Declaration;
		public readonly List<byte[]> VerticesBlocks = new List<byte[]>();
		public uint[] Indices;

		public VertexDeclaration ShadowDeclaration = default;

		private readonly ICoreClientAPI capi;
		private readonly IExtendedShaderProgram shader;
		private readonly IExtendedShaderProgram shadowShader;
		private readonly IBuilderStructContainer[] builders;
		private readonly Action<BatchBuildTask> onComplete;

		public BatchBuildTask(ICoreClientAPI capi, IExtendedShaderProgram shader, IExtendedShaderProgram shadowShader, IBuilderStructContainer[] builders, Action<BatchBuildTask> onComplete)
		{
			this.capi = capi;
			this.shader = shader;
			this.shadowShader = shadowShader;
			this.builders = builders;
			this.onComplete = onComplete;
		}

		public void Run()
		{
			try
			{
				for(int i = 0; i < builders.Length; i++)
				{
					builders[i].Init();
				}

				var context = new BuilderContext(this);
				context.Init();

				for(int i = 0; i < builders.Length; i++)
				{
					context.SetBuilder(i);

					var builderStruct = builders[i];

					context.uniformsMap.Clear();
					shader.MapDeclarationInv(builderStruct.GetUniformsDeclaration(), context.uniformsMap);
					foreach(var pair in context.uniformsMap)
					{
						if(!context.uniformToIndexMap.ContainsKey(pair.Key))
						{
							context.uniformToIndexMap[pair.Key] = context.uniformToIndexMap.Count;
						}
					}

					builderStruct.Build(context);
				}

				context.BuildCommands(out Commands, out Indices, out UniformsData, out UniformsMap, out RenderPasses);

				if(shadowShader != null)
				{
					foreach(var pass in RenderPasses)
					{
						if(pass.RenderPass switch {
							EnumChunkRenderPass.Opaque => true,
							EnumChunkRenderPass.OpaqueNoCull => true,
							EnumChunkRenderPass.BlendNoCull => true,
							EnumChunkRenderPass.TopSoil => true,
							_ => false
						})
						{
							var attributes = new RefList<VertexAttribute>();
							shadowShader.MapDeclaration(Declaration, attributes);

							for(int i = 0; i < attributes.Count; i++)
							{
								if(attributes[i].Alias == VertexAttributeAlias.POSITION)
								{
									ShadowDeclaration = new VertexDeclaration(attributes.ToArray());

									context.uniformToIndexMap.Clear();
									for(int j = 0; j < builders.Length; j++)
									{
										var declaration = builders[j].GetUniformsDeclaration();
										context.uniformsMap.Clear();
										shader.MapDeclaration(declaration, context.uniformsMap);
										context.tmpUniformsMap.Clear();
										shadowShader.MapDeclaration(declaration, context.tmpUniformsMap);
										foreach(var pair in context.uniformsMap)
										{
											if(context.tmpUniformsMap.TryGetValue(pair.Key, out var index))
											{
												context.uniformToIndexMap[pair.Value] = index;
											}
										}
									}

									var drawUniforms = shader.Uniforms.Properties;
									var shadowUniforms = shadowShader.Uniforms.Properties;
									ShadowUniformsMap = new int[UniformsMap.Length];
									for(int j = UniformsMap.Length - 1; j >= 0; j--)
									{
										int index = UniformsMap[j].Index;
										if(context.uniformToIndexMap.TryGetValue(index, out var shadowUniformIndex) &&
											drawUniforms[index].Type == shadowUniforms[shadowUniformIndex].Type &&
											drawUniforms[index].UniformSize == shadowUniforms[shadowUniformIndex].UniformSize)
										{
											ShadowUniformsMap[j] = shadowUniformIndex;
										}
										else
										{
											ShadowUniformsMap[j] = -1;
										}
									}
									break;
								}
							}
							break;
						}
					}
				}
			}
			catch(Exception e)
			{
				Failed = true;
				var msg = string.Format("Exception while trying to build a batch:\n{0}", e);
				capi.Event.EnqueueMainThreadTask(() => capi.Logger.Log(EnumLogType.Warning, msg), "powerofmind:batchbuildlog");
			}

			onComplete(this);
		}

		private static void AddAttrib(ref int byteOffset, VertexAttribute[] attributes, int attribIndex, RefList<VertexAttribute> outAttribs)
		{
			ref readonly var attrib = ref attributes[attribIndex];
			outAttribs.Add(new VertexAttribute(
				attrib.Name,
				attrib.Alias,
				attrib.Location,
				0,
				(uint)byteOffset,
				0,
				attrib.Size,
				attrib.Type,
				attrib.Normalized,
				attrib.IntegerTarget
			));
			byteOffset += (int)attrib.Size * ShaderExtensions.GetTypeSize(attrib.Type);
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		private readonly struct BuildCommand
		{
			public readonly CommandType type;
			public readonly uint arg0, arg1;

			public BuildCommand(CommandType type, uint arg0, uint arg1)
			{
				this.type = type;
				this.arg0 = arg0;
				this.arg1 = arg1;
			}

			public BuildCommand(CommandType type, uint arg0)
			{
				this.type = type;
				this.arg0 = arg0;
				this.arg1 = 0;
			}

			public enum CommandType
			{
				DrawIndices,
				SetPass,
				SetBuilder,
				OverrideUniform
			}
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		private readonly struct DrawGroup
		{
			public readonly int uniformsIndex;
			public readonly int cmdIndex;
			public readonly int cmdCount;
			public readonly EnumChunkRenderPass renderPass;

			public DrawGroup(int uniformsIndex, int cmdIndex, int cmdCount, EnumChunkRenderPass renderPass)
			{
				this.uniformsIndex = uniformsIndex;
				this.cmdIndex = cmdIndex;
				this.cmdCount = cmdCount;
				this.renderPass = renderPass;
			}
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		private readonly struct UniformKey : IEquatable<UniformKey>
		{
			public readonly int index;
			public readonly int buffer;

			public UniformKey(int index, int buffer)
			{
				this.index = index;
				this.buffer = buffer;
			}

			public override bool Equals(object obj)
			{
				return obj is UniformKey key && Equals(key);
			}

			public bool Equals(UniformKey other)
			{
				return index == other.index &&
					   buffer == other.buffer;
			}

			public override int GetHashCode()
			{
				int hashCode = 187764702;
				hashCode = hashCode * -1521134295 + index.GetHashCode();
				hashCode = hashCode * -1521134295 + buffer.GetHashCode();
				return hashCode;
			}
		}
	}
}