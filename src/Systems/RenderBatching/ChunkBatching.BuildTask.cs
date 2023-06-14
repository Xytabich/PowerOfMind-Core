using PowerOfMind.Collections;
using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Shader;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PowerOfMind.Systems.RenderBatching
{
	public partial class ChunkBatching
	{
		private partial class BuildTask
		{
			public const int BLOCK_SIZE = 1024;

			public bool failed = false;

			public readonly int[] builders;
			public readonly int version;
			public readonly int chunkShaderId;
			public RenderPassGroup[] renderPasses;
			public GraphicsCommand[] commands;
			public UniformPointer[] uniformsMap;
			public byte[] uniformsData;

			public uint verticesCount;
			public uint indicesCount;
			public int verticesStride;
			public VertexDeclaration declaration;
			public readonly List<byte[]> verticesBlocks = new List<byte[]>();
			public uint[] indices;

			private readonly IExtendedShaderProgram shader;
			private readonly ChunkBatching container;

			public BuildTask(ChunkBatching container, IExtendedShaderProgram shader, int[] builders, int version, int chunkShaderId)
			{
				this.container = container;
				this.shader = shader;
				this.builders = builders;
				this.version = version;
				this.chunkShaderId = chunkShaderId;
			}

			public void Run()
			{
				try
				{
					var context = new BuilderContext(this);
					context.Init();

					for(int i = 0; i < builders.Length; i++)
					{
						context.SetBuilder(i);

						var builderStruct = container.builders[builders[i]].builderStruct;
						var builder = container.builders[builders[i]].builder;

						context.uniformsMap.Clear();
						shader.MapDeclarationInv(builderStruct.GetUniformsDeclaration(), context.uniformsMap);
						foreach(var pair in context.uniformsMap)
						{
							if(!context.uniformToIndexMap.ContainsKey(pair.Key))
							{
								context.uniformToIndexMap[pair.Key] = context.uniformToIndexMap.Count;
							}
						}

						builder.Build(context);
					}

					context.BuildCommands(out commands, out indices, out uniformsData, out uniformsMap, out renderPasses);
				}
				catch(Exception e)
				{
					failed = true;
					var msg = string.Format("Exception while trying to build a chunk grid:\n{0}", e);
					var capi = container.capi;
					capi.Event.EnqueueMainThreadTask(() => capi.Logger.Log(EnumLogType.Warning, msg), "powerofmind:chunkbuildlog");
				}

				container.completedTasks.Add(this);
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
}