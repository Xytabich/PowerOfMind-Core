﻿using PowerOfMind.Collections;
using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Drawable;
using PowerOfMind.Graphics.Shader;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.Mathematics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PowerOfMind.Systems.ChunkRender
{
	public class ChunkRenderer : IRenderer
	{
		double IRenderer.RenderOrder => 0.35;
		int IRenderer.RenderRange => 0;

		private Dictionary<int3, int> chunkToId = new Dictionary<int3, int>();
		private HeapCollection<ChunkInfo> chunks = new HeapCollection<ChunkInfo>();
		private ChainList<ChunkShaderUsage> chunkShaders = new ChainList<ChunkShaderUsage>();

		private Dictionary<IExtendedShaderProgram, int> shaderToId = new Dictionary<IExtendedShaderProgram, int>();
		private HeapCollection<ShaderInfo> shaders = new HeapCollection<ShaderInfo>();
		private ChainList<ShaderChunkUsage> shaderChunks = new ChainList<ShaderChunkUsage>();

		private ChainList<BuilderInfo> builders = new ChainList<BuilderInfo>();

		private Queue<int> removeBuilderQueue = new Queue<int>();
		private ConcurrentBag<BuildTask> completedTasks = new ConcurrentBag<BuildTask>();

		private HashSet<int> rebuildStructs = new HashSet<int>();

		private List<BuildTask> tmpTasksList = new List<BuildTask>();
		private List<int> tmpIdsList = new List<int>();

		public int AddBuilder<TVertex, TUniform>(int3 chunk, IExtendedShaderProgram shader, IChunkBuilder builder, in TVertex defaultVertex, in TUniform defaultUniform)
			where TVertex : struct, IVertexStruct
			where TUniform : struct, IUniformsData
		{
			if(!chunkToId.TryGetValue(chunk, out int cid))
			{
				cid = chunks.Add(new ChunkInfo(chunk));
				chunkToId[chunk] = cid;
			}
			if(!shaderToId.TryGetValue(shader, out int sid))
			{
				sid = shaders.Add(new ShaderInfo(shader));
				shaderToId[shader] = sid;
			}

			ref var chunkRef = ref chunks[cid];
			int chunkShaderId = -1;
			int id;
			if(chunkRef.chunkShadersChain >= 0)
			{
				id = chunkRef.chunkShadersChain;
				do
				{
					if(chunkShaders[id].shaderId == sid)
					{
						chunkShaderId = id;
						break;
					}
				}
				while(chunkShaders.TryGetNextId(chunkRef.chunkShadersChain, id, out id));
			}
			if(chunkShaderId < 0)
			{
				ref var shaderRef = ref shaders[sid];
				int shaderChunkId = shaderChunks.Add(shaderRef.shaderChunksChain, new ShaderChunkUsage(cid));
				if(shaderRef.shaderChunksChain < 0) shaderRef.shaderChunksChain = shaderChunkId;

				chunkShaderId = chunkShaders.Add(chunkRef.chunkShadersChain, new ChunkShaderUsage(sid, shaderChunkId));
				if(chunkRef.chunkShadersChain < 0) chunkRef.chunkShadersChain = chunkShaderId;
				shaderChunks[shaderChunkId].chunkShaderId = chunkShaderId;
			}

			rebuildStructs.Add(chunkShaderId);

			ref var shaderUsage = ref chunkShaders[chunkShaderId];
			id = builders.Add(shaderUsage.buildersChain, new BuilderInfo(cid, builder, new BuilderStructContainer<TVertex, TUniform>(defaultVertex, defaultUniform), chunkShaderId));
			if(shaderUsage.buildersChain < 0) shaderUsage.buildersChain = id;

			return id;
		}

		public void RemoveBuilder(int id)
		{
			removeBuilderQueue.Enqueue(id);
		}

		void IRenderer.OnRenderFrame(float deltaTime, EnumRenderStage stage)
		{
			throw new NotImplementedException();
		}

		void IDisposable.Dispose()
		{
			throw new NotImplementedException();
		}

		private void Update()
		{
			while(removeBuilderQueue.Count > 0)
			{
				ReduceBuilderUsage(removeBuilderQueue.Dequeue());
			}
			tmpTasksList.Clear();
			while(completedTasks.TryTake(out var task))
			{
				ProcessCompletedTask(task);
			}
			if(rebuildStructs.Count > 0)
			{
				CollectBuildTasks();
			}
			if(tmpTasksList.Count > 0)
			{
				foreach(var task in tmpTasksList)
				{
					TyronThreadPool.QueueTask(task.Run);
				}

				tmpTasksList.Clear();
			}
		}

		private void ProcessCompletedTask(BuildTask task)
		{
			tmpIdsList.Clear();
			foreach(var id in task.builders)
			{
				var result = ReduceBuilderUsage(id);
				if(result == EnumReduceUsageResult.RemovedChunkShader)
				{
					return;
				}
				if(result == EnumReduceUsageResult.RemovedBuilder)
				{
					tmpIdsList.Add(id);
				}
			}
			if(!task.failed)
			{
				if(tmpIdsList.Count == 0)//nothing to skip, so just upload everything
				{
					//TODO: upload data & update declaration if needed
				}
				else
				{
					//TODO: upload data, but skip tmpIdsList, and probably indices should be changed, i.e. add some offset
				}
			}
			if(task.version == chunkShaders[task.chunkShaderId].version && !rebuildStructs.Contains(task.chunkShaderId))
			{
				chunkShaders[task.chunkShaderId].version = 0;
			}
			else
			{
				rebuildStructs.Remove(task.chunkShaderId);
				CreateChunkBuildTask(task.chunkShaderId);
			}
		}

		private void CollectBuildTasks()
		{
			foreach(int chunkShaderId in rebuildStructs)
			{
				if(chunkShaders[chunkShaderId].version > 0)
				{
					chunkShaders[chunkShaderId].version++;
					continue;
				}
				CreateChunkBuildTask(chunkShaderId);
			}
			rebuildStructs.Clear();
		}

		private void CreateChunkBuildTask(int chunkShaderId)
		{
			tmpIdsList.Clear();
			var chunkShaderInfo = chunkShaders[chunkShaderId];
			chunkShaders[chunkShaderId].version = 1;
			int id = chunkShaderInfo.buildersChain;
			do
			{
				tmpIdsList.Add(id);
				builders[id].usageCounter++;
			}
			while(builders.TryGetNextId(chunkShaderInfo.buildersChain, id, out id));
			tmpTasksList.Add(new BuildTask(this, shaders[chunkShaders[chunkShaderId].shaderId].shader, tmpIdsList.ToArray(), 1, chunkShaderId));
		}

		private EnumReduceUsageResult ReduceBuilderUsage(int id)
		{
			builders[id].usageCounter--;
			if(builders[id].usageCounter == 0)
			{
				int chunkId = builders[id].chunkId;
				int chunkShaderId = builders[id].chunkShaderId;

				chunkShaders[chunkShaderId].buildersChain = builders.Remove(chunkShaders[chunkShaderId].buildersChain, id);
				if(chunkShaders[chunkShaderId].buildersChain < 0)
				{
					//TODO: remove & dispose handles & mesh data

					rebuildStructs.Remove(chunkShaderId);//since it's removed, there's nothing to update

					int shaderId = chunkShaders[chunkShaderId].shaderId;
					int shaderChunkId = chunkShaders[chunkShaderId].shaderChunkId;

					chunks[chunkId].chunkShadersChain = chunkShaders.Remove(chunks[chunkId].chunkShadersChain, chunkShaderId);
					if(chunks[chunkId].chunkShadersChain < 0)
					{
						chunkToId.Remove(chunks[chunkId].position);
						chunks.Remove(chunkId);
					}

					shaders[shaderId].shaderChunksChain = shaderChunks.Remove(shaders[shaderId].shaderChunksChain, shaderChunkId);
					if(shaders[shaderId].shaderChunksChain < 0)
					{
						shaderToId.Remove(shaders[shaderId].shader);
						shaders.Remove(shaderId);
					}
					return EnumReduceUsageResult.RemovedChunkShader;
				}
				return EnumReduceUsageResult.RemovedBuilder;
			}
			return EnumReduceUsageResult.None;
		}

		private static int GetTypeSize(EnumShaderPrimitiveType type)
		{
			switch(type)
			{
				case EnumShaderPrimitiveType.UByte: return 1;
				case EnumShaderPrimitiveType.SByte: return 1;
				case EnumShaderPrimitiveType.UShort: return 2;
				case EnumShaderPrimitiveType.Short: return 2;
				case EnumShaderPrimitiveType.UInt: return 4;
				case EnumShaderPrimitiveType.Int: return 4;
				case EnumShaderPrimitiveType.Half: return 2;
				case EnumShaderPrimitiveType.Float: return 4;
				case EnumShaderPrimitiveType.Double: return 8;
				case EnumShaderPrimitiveType.UInt2101010Rev: return 4;
				case EnumShaderPrimitiveType.Int2101010Rev: return 4;
				default: throw new Exception("Invalid attribute type: " + type);
			}
		}

		private enum EnumReduceUsageResult
		{
			None,
			RemovedBuilder,
			RemovedChunkShader
		}

		private struct ShaderInfo
		{
			public readonly IExtendedShaderProgram shader;

			public int shaderChunksChain;

			public ShaderInfo(IExtendedShaderProgram shader)
			{
				this.shader = shader;
				this.shaderChunksChain = -1;
			}
		}

		private struct BuilderInfo
		{
			public readonly IChunkBuilder builder;
			public readonly IBuilderStructContainer builderStruct;
			public readonly int chunkId;
			public readonly int chunkShaderId;

			public int usageCounter;

			public BuilderInfo(int chunkId, IChunkBuilder builder, IBuilderStructContainer builderStruct, int chunkShaderId)
			{
				this.chunkId = chunkId;
				this.builder = builder;
				this.builderStruct = builderStruct;
				this.chunkShaderId = chunkShaderId;
				this.usageCounter = 1;
			}
		}

		private struct ChunkInfo
		{
			public readonly int3 position;

			public int chunkShadersChain;

			public ChunkInfo(int3 position)
			{
				this.position = position;
				this.chunkShadersChain = -1;
			}
		}

		private struct ChunkShaderUsage
		{
			public readonly int shaderId;
			public readonly int shaderChunkId;
			public int buildersChain;
			public int version;

			public ChunkShaderUsage(int shaderId, int shaderChunkId)
			{
				this.shaderId = shaderId;
				this.shaderChunkId = shaderChunkId;
				this.buildersChain = -1;
				this.version = 0;
			}
		}

		private struct ShaderChunkUsage
		{
			public readonly int chunkId;
			public int chunkShaderId;

			public ShaderChunkUsage(int chunkId)
			{
				this.chunkId = chunkId;
				this.chunkShaderId = -1;
			}
		}

		private class BuildTask
		{
			public const int BLOCK_SIZE = 1024;

			public readonly int[] builders;
			public readonly int version;
			public readonly int chunkShaderId;

			public bool failed = false;

			public int verticesCount;
			public int indicesCount;
			public int verticesStride;
			public VertexDeclaration declaration;
			public List<byte[]> verticesBlocks;
			public List<int[]> indicesBlocks;

			public KeyValuePair<uint, uint>[][] builderVertexMaps;
			public int[] builderVertexOffsets;
			public int[] builderIndexOffsets;

			private readonly IExtendedShaderProgram shader;
			private readonly ChunkRenderer container;

			public BuildTask(ChunkRenderer container, IExtendedShaderProgram shader, int[] builders, int version, int chunkShaderId)
			{
				this.container = container;
				this.shader = shader;
				this.builders = builders;
				this.version = version;
				this.chunkShaderId = chunkShaderId;
			}

			public void Run()
			{
				builderVertexOffsets = new int[builders.Length];
				builderIndexOffsets = new int[builders.Length];

				try
				{
					InitDeclaration();

					var context = new BuilderContext(this, verticesStride);
					for(int i = 0; i < builders.Length; i++)
					{
						builderVertexOffsets[i] = context.vertOffsetGlobal;
						builderIndexOffsets[i] = context.indOffsetGlobal;

						var builderStruct = container.builders[builders[i]].builderStruct;
						var builder = container.builders[builders[i]].builder;

						builder.Build(context);
					}

					context.EndBuild();

					//TODO: group indices by uniforms, but since there is stream writing, uniforms should probably have some dictionary, and need a list where will be written uniform changes sequence(i.e. if uniform change detected, current offset & uniform index will be written)
					//TODO: loop through builders & collect data, data should be splitted into the fixed blocks to reduce gc usage, block size is BLOCK_SIZE*(VertexSize or IndexSize)
				}
				catch(Exception e)
				{
					failed = true;
					//TODO: log exception
				}
			}

			private void InitDeclaration()
			{
				this.builderVertexMaps = new KeyValuePair<uint, uint>[builders.Length][];
				var mappedDeclarations = new VertexDeclaration[builders.Length];
				var attributes = new RefList<VertexAttribute>();
				for(int i = 0; i < builders.Length; i++)
				{
					shader.MapDeclaration(container.builders[builders[i]].builderStruct.GetVertexDeclaration(), attributes);
					mappedDeclarations[i] = new VertexDeclaration(attributes.ToArray());
					attributes.Clear();
				}

				int vertStride = 0;
				var tmpMap = new List<KeyValuePair<uint, uint>>();
				var locationToAttrib = new Dictionary<int, int>();
				for(int i = 0; i < mappedDeclarations.Length; i++)
				{
					var declaration = mappedDeclarations[i];
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
								//TODO: log error
								//logger.LogWarning("Vertex component {0} of builder {1} does not match size or type with other users of this shader. {2}({3}) was expected but {4}({5}) was provided.",
								//	string.Format(string.IsNullOrEmpty(declaration.Attributes[j].Alias) ? "`{0}`" : "`{0}`[{1}]", declaration.Attributes[j].Name, declaration.Attributes[j].Alias),
								//	builders[i], attributes[index].Type, attributes[index].Size, declaration.Attributes[j].Type, declaration.Attributes[j].Size);
								continue;
							}
						}
						tmpMap.Add(new KeyValuePair<uint, uint>(declaration.Attributes[j].Offset, attributes[index].Offset));
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

				this.declaration = new VertexDeclaration(attributes.ToArray());
				this.verticesStride = vertStride;
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
				byteOffset += (int)attrib.Size * GetTypeSize(attrib.Type);
			}

			private class BuilderContext : IChunkBuilderContext, VerticesContext.IProcessor
			{
				public int vertOffsetGlobal = 0;
				public int vertOffsetLocal = 0;
				public int indOffsetGlobal = 0;
				public int indOffsetLocal = 0;

				private readonly BuildTask task;
				private readonly int verticesStride;

				private byte[] currentVertBlock;
				private int[] currentIndBlock;

				private int currentVertCount;
				private int currentIndCount;

				private bool hasIndices = false;
				private IndicesContext.ProcessorDelegate addIndicesCallback;

				public unsafe BuilderContext(BuildTask task, int verticesStride)
				{
					this.task = task;
					this.verticesStride = verticesStride;
					currentVertBlock = new byte[verticesStride * BLOCK_SIZE];
					currentIndBlock = new int[BLOCK_SIZE];
					addIndicesCallback = InsertIndices;
				}

				public void EndBuild()
				{
					task.verticesBlocks.Add(currentVertBlock);
					task.indicesBlocks.Add(currentIndBlock);
				}

				void IChunkBuilderContext.AddData(IDrawableData data)
				{
					currentVertCount = data.VerticesCount;
					currentIndCount = data.IndicesCount;
					EnsureCapacity();
					//Buffer.MemoryCopy();
					//TODO: create verts remap & fill missing components by builder's defaults
					hasIndices = false;
					data.ProvideIndices(new IndicesContext(addIndicesCallback, false));
					data.ProvideVertices(new VerticesContext(this, false));
				}

				unsafe void IChunkBuilderContext.AddData<T>(IDrawableData data, T* uniformsData)
				{
					throw new NotImplementedException();
				}

				unsafe void VerticesContext.IProcessor.Process<T>(int bufferIndex, T* data, int stride, bool isDynamic)
				{
					if(isDynamic || (data == null && !hasIndices)) return;
					//TODO: insert vertices
				}

				private unsafe void InsertIndices(int* indices, bool isDynamic)
				{
					if(isDynamic || indices == null) return;
					hasIndices = true;
					//TODO: insert indices
				}

				private void EnsureCapacity()
				{
					if(currentVertCount < BLOCK_SIZE - vertOffsetLocal)
					{
						int addBlocks = ((currentVertCount - (BLOCK_SIZE - vertOffsetLocal) - 1) / BLOCK_SIZE + 1) * BLOCK_SIZE;
						while(addBlocks > 0)
						{
							task.verticesBlocks.Add(new byte[verticesStride * BLOCK_SIZE]);
							addBlocks--;
						}
					}
					if(currentIndCount < BLOCK_SIZE - indOffsetLocal)
					{
						int addBlocks = ((currentIndCount - (BLOCK_SIZE - indOffsetLocal) - 1) / BLOCK_SIZE + 1) * BLOCK_SIZE;
						while(addBlocks > 0)
						{
							task.indicesBlocks.Add(new int[BLOCK_SIZE]);
							addBlocks--;
						}
					}
				}
			}
		}

		private interface IBuilderStructContainer
		{
			VertexDeclaration GetVertexDeclaration();
		}

		private class BuilderStructContainer<TVertex, TUniform> : IBuilderStructContainer
			where TVertex : struct, IVertexStruct
			where TUniform : struct, IUniformsData
		{
			private readonly TVertex vertexDefaults;
			private readonly TUniform uniformDefaults;

			public BuilderStructContainer(in TVertex vertexDefaults, in TUniform uniformDefaults)
			{
				this.vertexDefaults = vertexDefaults;
				this.uniformDefaults = uniformDefaults;
			}

			VertexDeclaration IBuilderStructContainer.GetVertexDeclaration()
			{
				throw new NotImplementedException();
			}
		}

		public interface IChunkBuilder
		{
			/// <summary>
			/// Builds the drawable data for the chunk. Called on a separate thread.
			/// </summary>
			void Build(IChunkBuilderContext context);
		}

		public interface IChunkBuilderContext//TODO: allow only triangles(drawable type)
		{
			/// <summary>
			/// Adds drawable data to the chunk, only static data will be added, dynamic data will be ignored
			/// </summary>
			/// <param name="uniformsData">Additional data by which rendering will be grouped. For example MAIN_TEXTURE.</param>
			unsafe void AddData<T>(IDrawableData data, T* uniformsData) where T : unmanaged, IUniformsData;

			/// <summary>
			/// Adds drawable data to the chunk, only static data will be added, dynamic data will be ignored
			/// </summary>
			void AddData(IDrawableData data);
		}
	}
}