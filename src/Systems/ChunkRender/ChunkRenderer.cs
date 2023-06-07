using PowerOfMind.Collections;
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
	public class ChunkRenderer : IRenderer//TODO: subscribe to shader reload event & rebuild all chunks
	{
		double IRenderer.RenderOrder => 0.35;
		int IRenderer.RenderRange => 0;

		private readonly Dictionary<int3, int> chunkToId = new Dictionary<int3, int>();
		private readonly HeapCollection<ChunkInfo> chunks = new HeapCollection<ChunkInfo>();
		private readonly ChainList<ChunkShaderUsage> chunkShaders = new ChainList<ChunkShaderUsage>();

		private readonly Dictionary<IExtendedShaderProgram, int> shaderToId = new Dictionary<IExtendedShaderProgram, int>();
		private readonly HeapCollection<ShaderInfo> shaders = new HeapCollection<ShaderInfo>();
		private readonly ChainList<ShaderChunkUsage> shaderChunks = new ChainList<ShaderChunkUsage>();

		private readonly ChainList<BuilderInfo> builders = new ChainList<BuilderInfo>();

		private readonly Queue<int> removeBuilderQueue = new Queue<int>();
		private readonly ConcurrentBag<BuildTask> completedTasks = new ConcurrentBag<BuildTask>();

		private readonly HashSet<int> rebuildStructs = new HashSet<int>();

		private readonly List<BuildTask> tmpTasksList = new List<BuildTask>();
		private readonly List<int> tmpIdsList = new List<int>();
		private readonly Dictionary<int, int> tmpPairs = new Dictionary<int, int>();
		private readonly ChunkDrawableData chunkDataHelper = new ChunkDrawableData();

		private readonly UniformsDeclaration uniformsDeclaration = new UniformsDeclaration(
			new UniformProperty("projectionMatrix", UniformAlias.PROJ_MATRIX, 0, EnumShaderPrimitiveType.Float, EnumUniformStructType.SquareMatrix, 16),
			new UniformProperty("modelViewMatrix", UniformAlias.MV_MATRIX, 0, EnumShaderPrimitiveType.Float, EnumUniformStructType.SquareMatrix, 16),
			new UniformProperty("viewMatrix", UniformAlias.VIEW_MATRIX, 0, EnumShaderPrimitiveType.Float, EnumUniformStructType.SquareMatrix, 16),
			new UniformProperty("modelMatrix", UniformAlias.MODEL_MATRIX, 0, EnumShaderPrimitiveType.Float, EnumUniformStructType.SquareMatrix, 16),
			new UniformProperty("origin", UniformAlias.MODEL_ORIGIN, 0, EnumShaderPrimitiveType.Float, EnumUniformStructType.Vector, 3),

			new UniformProperty("rgbaFogIn", UniformAlias.FOG_COLOR, 0, EnumShaderPrimitiveType.Float, EnumUniformStructType.Vector, 4),
			new UniformProperty("rgbaAmbientIn", UniformAlias.AMBIENT_COLOR, 0, EnumShaderPrimitiveType.Float, EnumUniformStructType.Vector, 3),
			new UniformProperty("fogDensityIn", UniformAlias.FOG_DENSITY, 0, EnumShaderPrimitiveType.Float, EnumUniformStructType.Primitive, 1),
			new UniformProperty("fogMinIn", UniformAlias.FOG_MIN, 0, EnumShaderPrimitiveType.Float, EnumUniformStructType.Primitive, 1),
			new UniformProperty("alphaTest", UniformAlias.ALPHA_TEST, 0, EnumShaderPrimitiveType.Float, EnumUniformStructType.Primitive, 1)
		);

		private readonly IRenderAPI rapi;
		private readonly ICoreClientAPI capi;

		public int AddBuilder<TVertex, TUniform>(int3 chunk, IExtendedShaderProgram shader, IChunkBuilder builder, in TVertex defaultVertex, in TUniform defaultUniform)
			where TVertex : unmanaged, IVertexStruct
			where TUniform : unmanaged, IUniformsData
		{
			if(!chunkToId.TryGetValue(chunk, out int cid))
			{
				cid = chunks.Add(new ChunkInfo(chunk));
				chunkToId[chunk] = cid;
			}
			if(!shaderToId.TryGetValue(shader, out int sid))
			{
				sid = AddShader(shader);
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
			switch(stage)
			{
				case EnumRenderStage.Before:
					Update();
					break;
				case EnumRenderStage.Opaque:
					if(shaders.Count > 0) RenderStage(stage);
					break;
			}
		}

		void IDisposable.Dispose()
		{
			throw new NotImplementedException();
		}

		private int AddShader(IExtendedShaderProgram shader)
		{
			tmpPairs.Clear();
			shader.MapDeclarationByIdentifier(uniformsDeclaration, tmpPairs);

			if(tmpPairs.TryGetValue(0, out var projMatrix))
			{
				int viewMatrix = -1;
				if(!tmpPairs.TryGetValue(1, out var mvMatrix))
				{
					mvMatrix = -1;
					if(!tmpPairs.TryGetValue(2, out viewMatrix))
					{
						goto _fail;
					}
				}
				if(!tmpPairs.TryGetValue(3, out var modelMatrix))
				{
					modelMatrix = -1;
				}
				if(!tmpPairs.TryGetValue(4, out var originPos))
				{
					originPos = -1;
				}
				if(mvMatrix < 0 && (modelMatrix < 0 || originPos < 0))
				{
					goto _fail;
				}

				int id = shaders.Add(new ShaderInfo(shader, projMatrix, viewMatrix, modelMatrix, mvMatrix, originPos,
					tmpPairs.TryGetValue(5, out var fogColor) ? fogColor : -1,
					tmpPairs.TryGetValue(6, out var ambientColor) ? ambientColor : -1,
					tmpPairs.TryGetValue(7, out var fogDensity) ? fogDensity : -1,
					tmpPairs.TryGetValue(8, out var fogMin) ? fogMin : -1,
					tmpPairs.TryGetValue(9, out var alphaTest) ? alphaTest : -1
				));
				shaderToId[shader] = id;
				return id;
			}

_fail:
			throw new Exception("Invalid shader, shader must contain projection and view matrices as well as model matrix or origin position.");
		}

		private void RenderStage(EnumRenderStage stage)
		{
			foreach(var pair in shaderToId)
			{
				ref readonly var shader = ref shaders[pair.Value];
				if(shader.shaderChunksChain >= 0)
				{
					var prog = shader.shader;
					var uniforms = prog.Uniforms.Properties;
					prog.Use();

					if(shader.ambientColor >= 0) uniforms[shader.ambientColor].SetValue(rapi.AmbientColor);
					if(shader.fogColor >= 0) uniforms[shader.fogColor].SetValue(rapi.FogColor);
					if(shader.fogDensity >= 0) uniforms[shader.fogDensity].SetValue(rapi.FogDensity);
					if(shader.fogMin >= 0) uniforms[shader.fogMin].SetValue(rapi.FogMin);
					if(shader.alphaTest >= 0) uniforms[shader.alphaTest].SetValue(0.001f);

					uniforms[shader.projMatrix].SetValue(rapi.CurrentProjectionMatrix);
					if(shader.mvMatrix >= 0) uniforms[shader.mvMatrix].SetValue(rapi.CurrentModelviewMatrix);
					if(shader.viewMatrix >= 0) uniforms[shader.viewMatrix].SetValue(rapi.CameraMatrixOriginf);
					var playerCamPos = capi.World.Player.Entity.CameraPos;
					foreach(var chunk in shaderChunks.GetEnumerable(shader.shaderChunksChain))
					{
						ref readonly var info = ref chunkShaders[chunk.chunkShaderId];
						if(info.drawableHandle != null)
						{
							var chunkIndex = chunks[chunk.chunkId].position;

							if(shader.originPos >= 0)
							{
								if(shader.modelMatrix >= 0)
								{
									uniforms[shader.modelMatrix].SetValue(float4x4.identity);
								}

								uniforms[shader.originPos].SetValue(
									new float3((float)(chunkIndex.x - playerCamPos.X), (float)(chunkIndex.y - playerCamPos.Y), (float)(chunkIndex.z - playerCamPos.Z)));
							}
							else
							{
								uniforms[shader.modelMatrix].SetValue(float4x4.Translate(
									new float3((float)(chunkIndex.x - playerCamPos.X), (float)(chunkIndex.y - playerCamPos.Y), (float)(chunkIndex.z - playerCamPos.Z))));
							}

							rapi.RenderDrawable(info.drawableHandle);
						}
					}
					prog.Stop();
				}
			}
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
				ref var chunkShader = ref chunkShaders[task.chunkShaderId];
				chunkDataHelper.Clear();
				chunkDataHelper.verticesStride = task.verticesStride;
				chunkDataHelper.verticesCount = task.verticesCount;
				chunkDataHelper.indicesCount = task.indicesCount;
				chunkDataHelper.vertexDeclaration = task.declaration;
				if(tmpIdsList.Count == 0)//nothing to skip, so just upload everything
				{
					if(task.verticesBlocks.Count == 1 && task.indicesBlocks.Count == 1)
					{
						chunkDataHelper.verticesData = task.verticesBlocks[0];
						chunkDataHelper.indicesData = task.indicesBlocks[0];
						if(chunkShader.drawableHandle == null)
						{
							chunkShader.drawableHandle = rapi.UploadDrawable(chunkDataHelper);
						}
						else
						{
							rapi.ReuploadDrawable(chunkShader.drawableHandle, chunkDataHelper, true);
						}
					}
					else
					{
						//First allocate the required size (i.e. a null pointer will just allocate the buffer and won't upload any data)
						if(chunkShader.drawableHandle == null)
						{
							chunkShader.drawableHandle = rapi.UploadDrawable(chunkDataHelper);
						}
						else
						{
							rapi.ReuploadDrawable(chunkShader.drawableHandle, chunkDataHelper, true);
						}

						//Then uploading data block-by-block
						int lastIndex = task.verticesBlocks.Count - 1;
						chunkDataHelper.verticesCount = BuildTask.BLOCK_SIZE;
						chunkDataHelper.indicesCount = 0;//Uploading only vertices
						for(int i = 0; i <= lastIndex; i++)
						{
							if(i == lastIndex)
							{
								chunkDataHelper.verticesCount = task.verticesCount % BuildTask.BLOCK_SIZE;
							}
							chunkDataHelper.verticesData = task.verticesBlocks[i];
							rapi.UpdateDrawable(chunkShader.drawableHandle, chunkDataHelper);
						}

						lastIndex = task.indicesBlocks.Count - 1;
						chunkDataHelper.verticesCount = 0;//Uploading only indices
						chunkDataHelper.indicesCount = BuildTask.BLOCK_SIZE;
						for(int i = 0; i <= lastIndex; i++)
						{
							if(i == lastIndex)
							{
								chunkDataHelper.indicesCount = task.indicesCount % BuildTask.BLOCK_SIZE;
							}
							chunkDataHelper.indicesData = task.indicesBlocks[i];
							rapi.UpdateDrawable(chunkShader.drawableHandle, chunkDataHelper);
						}
					}
				}
				else
				{
					//TODO: upload data, but skip tmpIdsList, and probably indices should be changed, i.e. add some offset
				}
				chunkDataHelper.Clear();
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
					rebuildStructs.Remove(chunkShaderId);//since it's removed, there's nothing to update
					chunkShaders[chunkShaderId].drawableHandle?.Dispose();

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
			public readonly int projMatrix;
			public readonly int viewMatrix;
			public readonly int modelMatrix;
			public readonly int mvMatrix;
			public readonly int originPos;

			public readonly int fogColor;
			public readonly int ambientColor;
			public readonly int fogDensity;
			public readonly int fogMin;
			public readonly int alphaTest;

			public int shaderChunksChain;

			public ShaderInfo(IExtendedShaderProgram shader, int projMatrix, int viewMatrix, int modelMatrix, int mvMatrix, int originPos,
				int fogColor, int ambientColor, int fogDensity, int fogMin, int alphaTest)
			{
				this.shader = shader;
				this.projMatrix = projMatrix;
				this.viewMatrix = viewMatrix;
				this.modelMatrix = modelMatrix;
				this.mvMatrix = mvMatrix;
				this.originPos = originPos;
				this.fogColor = fogColor;
				this.ambientColor = ambientColor;
				this.fogDensity = fogDensity;
				this.fogMin = fogMin;
				this.alphaTest = alphaTest;
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

			public IDrawableHandle drawableHandle;

			public ChunkShaderUsage(int shaderId, int shaderChunkId)
			{
				this.shaderId = shaderId;
				this.shaderChunkId = shaderChunkId;
				this.buildersChain = -1;
				this.version = 0;
				this.drawableHandle = null;
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
			public readonly List<byte[]> verticesBlocks = new List<byte[]>();
			public readonly List<int[]> indicesBlocks = new List<int[]>();

			public KeyValuePair<int, int>[][] builderVertexMaps;
			public VertexDeclaration[] builderDeclarations;
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
						context.builderIndex = i;

						builderVertexOffsets[i] = verticesCount;
						builderIndexOffsets[i] = indicesCount;

						var builderStruct = container.builders[builders[i]].builderStruct;
						var builder = container.builders[builders[i]].builder;

						builder.Build(context);
					}

					//TODO: group indices by uniforms, but since there is stream writing, uniforms should probably have some dictionary, and need a list where will be written uniform changes sequence(i.e. if uniform change detected, current offset & uniform index will be written)
				}
				catch(Exception e)
				{
					failed = true;
					//TODO: log exception
				}
			}

			private void InitDeclaration()
			{
				this.builderVertexMaps = new KeyValuePair<int, int>[builders.Length][];
				this.builderDeclarations = new VertexDeclaration[builders.Length];
				var attributes = new RefList<VertexAttribute>();
				for(int i = 0; i < builders.Length; i++)
				{
					shader.MapDeclaration(container.builders[builders[i]].builderStruct.GetVertexDeclaration(), attributes);
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
								//TODO: log error
								//logger.LogWarning("Vertex component {0} of builder {1} does not match size or type with other users of this shader. {2}({3}) was expected but {4}({5}) was provided.",
								//	string.Format(string.IsNullOrEmpty(declaration.Attributes[j].Alias) ? "`{0}`" : "`{0}`[{1}]", declaration.Attributes[j].Name, declaration.Attributes[j].Alias),
								//	builders[i], attributes[index].Type, attributes[index].Size, declaration.Attributes[j].Type, declaration.Attributes[j].Size);
								continue;
							}
						}
						tmpMap.Add(new KeyValuePair<int, int>(j, index));
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
				public int vertOffsetLocal = 0;
				public int indOffsetLocal = 0;

				public int builderIndex = 0;

				private readonly BuildTask task;
				private readonly int verticesStride;

				private int currentVertCount;
				private int currentIndCount;
				private int currentVertBlock = 0;
				private int currentIndBlock = 0;

				private bool hasIndices = false;
				private IndicesContext.ProcessorDelegate addIndicesCallback;

				private Dictionary<int, int> componentsToMap = new Dictionary<int, int>();
				private RefList<VertexAttribute> tmpAttributes = new RefList<VertexAttribute>();

				public unsafe BuilderContext(BuildTask task, int verticesStride)
				{
					this.task = task;
					this.verticesStride = verticesStride;

					task.verticesBlocks.Add(new byte[verticesStride * BLOCK_SIZE]);
					task.indicesBlocks.Add(new int[BLOCK_SIZE]);

					addIndicesCallback = InsertIndices;
				}

				unsafe void IChunkBuilderContext.AddData(IDrawableData data)
				{
					currentVertCount = data.VerticesCount;
					currentIndCount = data.IndicesCount;
					EnsureCapacity();
					componentsToMap.Clear();
					var map = task.builderVertexMaps[builderIndex];
					for(int i = 0; i < map.Length; i++)
					{
						componentsToMap[task.declaration.Attributes[map[i].Value].Location] = i;
					}
					hasIndices = false;
					data.ProvideIndices(new IndicesContext(addIndicesCallback, false));
					data.ProvideVertices(new VerticesContext(this, false));

					if(hasIndices)
					{
						if(componentsToMap.Count > 0)
						{
							var declaration = task.builderDeclarations[builderIndex];
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

						task.verticesCount += currentVertCount;
						task.indicesCount += currentIndCount;
						currentVertBlock += (vertOffsetLocal + currentVertCount) / BLOCK_SIZE;
						currentIndBlock += (indOffsetLocal + currentIndCount) / BLOCK_SIZE;
						vertOffsetLocal = (vertOffsetLocal + currentVertCount) % BLOCK_SIZE;
						indOffsetLocal = (indOffsetLocal + currentIndCount) % BLOCK_SIZE;
					}
				}

				unsafe void IChunkBuilderContext.AddData<T>(IDrawableData data, T* uniformsData)
				{
					throw new NotImplementedException();
				}

				unsafe void VerticesContext.IProcessor.Process<T>(int bufferIndex, T* data, VertexDeclaration declaration, int stride, bool isDynamic)
				{
					if(isDynamic || (data == null && !hasIndices)) return;
					tmpAttributes.Clear();
					task.shader.MapDeclaration(declaration, tmpAttributes);
					for(int i = 0; i < tmpAttributes.Count; i++)
					{
						if(componentsToMap.TryGetValue(tmpAttributes[i].Location, out int mapIndex))
						{
							int attribIndex = task.builderVertexMaps[builderIndex][mapIndex].Value;
							ref readonly var vertAttrib = ref task.declaration.Attributes[attribIndex];
							if(tmpAttributes[i].Size == vertAttrib.Size && tmpAttributes[i].Type == vertAttrib.Type)//TODO: log warning otherwise?
							{
								componentsToMap.Remove(tmpAttributes[i].Location);

								CopyComponentData(attribIndex, (byte*)data + tmpAttributes[i].Offset, stride);
							}
						}
					}
				}

				private unsafe void CopyComponentData(int attribIndex, byte* dataPtr, int stride)
				{
					ref readonly var vertAttrib = ref task.declaration.Attributes[attribIndex];
					int buffStride = task.verticesStride;

					long copyCount = GetTypeSize(vertAttrib.Type) * vertAttrib.Size;
					long buffOffset = vertOffsetLocal;
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

				private unsafe void InsertIndices(int* indices, bool isDynamic)
				{
					if(isDynamic || indices == null) return;
					hasIndices = true;
					var dataPtr = indices;
					int countToCopy = currentIndCount;
					long buffOffset = indOffsetLocal;

					long copyCount;
					int blockIndex = this.currentIndBlock;
					var currentDataBlock = task.indicesBlocks[blockIndex];
					if(countToCopy > BLOCK_SIZE - indOffsetLocal)
					{
						int count = BLOCK_SIZE - indOffsetLocal;
						copyCount = count * 4;
						fixed(int* ptr = currentDataBlock)
						{
							Buffer.MemoryCopy(dataPtr, ptr + buffOffset, copyCount, copyCount);
						}
						countToCopy -= count;
						dataPtr += count;
						buffOffset = 0;

						copyCount = BLOCK_SIZE * 4;
						currentDataBlock = task.indicesBlocks[++blockIndex];
						count = countToCopy / BLOCK_SIZE;
						while(count > 0)
						{
							fixed(int* ptr = currentDataBlock)
							{
								Buffer.MemoryCopy(dataPtr, (byte*)ptr, copyCount, copyCount);
							}

							dataPtr += BLOCK_SIZE;
							currentDataBlock = task.indicesBlocks[++blockIndex];
							countToCopy -= BLOCK_SIZE;
							count--;
						}
					}
					if(countToCopy > 0)
					{
						copyCount = countToCopy * 4;
						fixed(int* ptr = currentDataBlock)
						{
							Buffer.MemoryCopy(dataPtr, ptr + buffOffset, countToCopy, countToCopy);
						}
					}
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

			int GetVertexStride();

			void ProvideVertexData(DataProcessor processor);
		}

		private unsafe delegate void DataProcessor(byte* ptr);

		private class BuilderStructContainer<TVertex, TUniform> : IBuilderStructContainer
			where TVertex : unmanaged, IVertexStruct
			where TUniform : unmanaged, IUniformsData
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
				return vertexDefaults.GetDeclaration();
			}

			unsafe int IBuilderStructContainer.GetVertexStride()
			{
				return sizeof(TVertex);
			}

			unsafe void IBuilderStructContainer.ProvideVertexData(DataProcessor processor)
			{
				fixed(TVertex* ptr = &vertexDefaults)
				{
					processor((byte*)ptr);
				}
			}
		}

		private class ChunkDrawableData : IDrawableData
		{
			EnumDrawMode IDrawableData.DrawMode => EnumDrawMode.Triangles;
			int IDrawableData.IndicesCount => indicesCount;
			int IDrawableData.VerticesCount => verticesCount;
			int IDrawableData.VertexBuffersCount => 1;

			public int indicesCount, verticesCount, verticesStride;
			public VertexDeclaration vertexDeclaration;

			public byte[] verticesData;
			public int[] indicesData;

			public void Clear()
			{
				verticesData = null;
				indicesData = null;
			}

			unsafe void IDrawableData.ProvideIndices(IndicesContext context)
			{
				if(!context.ProvideDynamicOnly)
				{
					if(indicesData == null) context.Process(null, false);
					else
					{
						fixed(int* ptr = indicesData)
						{
							context.Process(ptr, false);
						}
					}
				}
			}

			unsafe void IDrawableData.ProvideVertices(VerticesContext context)
			{
				if(!context.ProvideDynamicOnly)
				{
					if(verticesData == null) context.Process(0, (byte*)null, vertexDeclaration, verticesStride, false);
					else
					{
						fixed(byte* ptr = verticesData)
						{
							context.Process(0, ptr, vertexDeclaration, verticesStride, false);
						}
					}
				}
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