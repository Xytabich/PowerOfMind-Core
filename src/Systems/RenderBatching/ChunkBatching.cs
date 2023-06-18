using PowerOfMind.Collections;
using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Shader;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PowerOfMind.Systems.RenderBatching
{
	public partial class ChunkBatching : IRenderer
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
			new UniformProperty("projectionMatrix", UniformAlias.PROJ_MATRIX, 0, EnumShaderPrimitiveType.Float, 16),
			new UniformProperty("modelViewMatrix", UniformAlias.MV_MATRIX, 0, EnumShaderPrimitiveType.Float, 16),
			new UniformProperty("viewMatrix", UniformAlias.VIEW_MATRIX, 0, EnumShaderPrimitiveType.Float, 16),
			new UniformProperty("modelMatrix", UniformAlias.MODEL_MATRIX, 0, EnumShaderPrimitiveType.Float, 16),
			new UniformProperty("origin", UniformAlias.MODEL_ORIGIN, 0, EnumShaderPrimitiveType.Float, 3),

			new UniformProperty("rgbaFogIn", UniformAlias.FOG_COLOR, 0, EnumShaderPrimitiveType.Float, 4),
			new UniformProperty("rgbaAmbientIn", UniformAlias.AMBIENT_COLOR, 0, EnumShaderPrimitiveType.Float, 3),
			new UniformProperty("fogDensityIn", UniformAlias.FOG_DENSITY, 0, EnumShaderPrimitiveType.Float, 1),
			new UniformProperty("fogMinIn", UniformAlias.FOG_MIN, 0, EnumShaderPrimitiveType.Float, 1)
		);

		private readonly IRenderAPI rapi;
		private readonly ICoreClientAPI capi;
		private readonly GraphicsSystem graphics;

		private byte[] dummyBytes = new byte[256];

		public ChunkBatching(ICoreClientAPI capi, GraphicsSystem graphics)
		{
			this.capi = capi;
			this.rapi = capi.Render;
			this.graphics = graphics;

			capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "powerofmindcore:chunkrenderer");
			capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "powerofmindcore:chunkrenderer");
			graphics.OnReloadShaders += ReloadAll;
		}

		public int AddBuilder<TVertex, TUniform>(int3 chunk, IExtendedShaderProgram shader, IBatchDataBuilder builder, in TVertex vertexStruct, in TUniform uniformStruct)
			where TVertex : unmanaged, IVertexStruct
			where TUniform : unmanaged, IUniformsData
		{
			if(shader.Disposed) throw new InvalidOperationException("Shader is not initialized");

			if(!chunkToId.TryGetValue(chunk, out int cid))
			{
				cid = chunks.Add(new ChunkInfo(chunk, chunk * capi.World.BlockAccessor.ChunkSize));
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
			id = builders.Add(shaderUsage.buildersChain, new BuilderInfo(cid, builder, new BuilderStructContainer<TVertex, TUniform>(vertexStruct, uniformStruct), chunkShaderId));
			if(shaderUsage.buildersChain < 0) shaderUsage.buildersChain = id;

			return id;
		}

		public void MarkBuilderDirty(int id)
		{
			rebuildStructs.Add(builders[id].chunkShaderId);
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
				case EnumRenderStage.Opaque://TODO: add another passes
					if(shaders.Count > 0) RenderStage(EnumChunkRenderPass.Opaque);
					break;
			}
		}

		void IDisposable.Dispose()
		{
			capi.Event.UnregisterRenderer(this, EnumRenderStage.Before);
			capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
			foreach(var pair in shaderToId)
			{
				ref readonly var shader = ref shaders[pair.Value];
				if(shader.shaderChunksChain >= 0)
				{
					foreach(var chunk in shaderChunks.GetEnumerable(shader.shaderChunksChain))
					{
						chunkShaders[chunk.chunkShaderId].drawer?.Dispose();
					}
				}
			}
		}

		private bool ReloadAll()
		{
			foreach(var pair in shaderToId)
			{
				ref readonly var shader = ref shaders[pair.Value];
				if(shader.shaderChunksChain >= 0)
				{
					foreach(var chunk in shaderChunks.GetEnumerable(shader.shaderChunksChain))
					{
						ref var info = ref chunkShaders[chunk.chunkShaderId];
						if(info.drawer != null)
						{
							info.drawer.Dispose();
							info.drawer = null;
						}
						rebuildStructs.Add(chunk.chunkShaderId);
					}
				}
			}
			return true;
		}

		private int AddShader(IExtendedShaderProgram shader)
		{
			tmpPairs.Clear();
			shader.MapDeclaration(uniformsDeclaration, tmpPairs);

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
				if(mvMatrix < 0 && modelMatrix < 0 && originPos < 0)
				{
					goto _fail;
				}

				int id = shaders.Add(new ShaderInfo(shader, projMatrix, viewMatrix, modelMatrix, mvMatrix, originPos,
					tmpPairs.TryGetValue(5, out var fogColor) ? fogColor : -1,
					tmpPairs.TryGetValue(6, out var ambientColor) ? ambientColor : -1,
					tmpPairs.TryGetValue(7, out var fogDensity) ? fogDensity : -1,
					tmpPairs.TryGetValue(8, out var fogMin) ? fogMin : -1
				));
				shaderToId[shader] = id;
				return id;
			}

_fail:
			throw new Exception("Invalid shader, shader must contain projection and view matrices as well as model matrix or origin position.");
		}

		private void RenderStage(EnumChunkRenderPass pass)
		{
			rapi.GLDepthMask(true);
			rapi.GLEnableDepthTest();
			switch(pass)
			{
				case EnumChunkRenderPass.OpaqueNoCull:
				case EnumChunkRenderPass.BlendNoCull:
					rapi.GlDisableCullFace();
					break;
				default:
					rapi.GlEnableCullFace();
					break;
			}
			rapi.GlToggleBlend(true);
			rapi.GlMatrixModeModelView();
			rapi.GlPushMatrix();
			rapi.GlLoadMatrix(rapi.CameraMatrixOrigin);

			RenderCall renderInfo = default;
			renderInfo.graphics = graphics;
			renderInfo.rapi = rapi;

			var playerCamPos = capi.World.Player.Entity.CameraPos;
			foreach(var pair in shaderToId)
			{
				ref readonly var shader = ref shaders[pair.Value];
				if(shader.shaderChunksChain >= 0)
				{
					renderInfo.shader = shader.shader;
					renderInfo.shaderUniforms = renderInfo.shader.Uniforms.Properties;
					renderInfo.shader.Use();

					if(shader.ambientColor >= 0) renderInfo.shaderUniforms[shader.ambientColor].SetValue(rapi.AmbientColor);
					if(shader.fogColor >= 0) renderInfo.shaderUniforms[shader.fogColor].SetValue(rapi.FogColor);
					if(shader.fogDensity >= 0) renderInfo.shaderUniforms[shader.fogDensity].SetValue(rapi.FogDensity);
					if(shader.fogMin >= 0) renderInfo.shaderUniforms[shader.fogMin].SetValue(rapi.FogMin);

					renderInfo.shaderUniforms[shader.projMatrix].SetValue(rapi.CurrentProjectionMatrix);
					if(shader.mvMatrix >= 0) renderInfo.shaderUniforms[shader.mvMatrix].SetValue(rapi.CurrentModelviewMatrix);
					if(shader.viewMatrix >= 0) renderInfo.shaderUniforms[shader.viewMatrix].SetValue(rapi.CameraMatrixOriginf);

					foreach(var chunk in shaderChunks.GetEnumerable(shader.shaderChunksChain))
					{
						ref readonly var info = ref chunkShaders[chunk.chunkShaderId];
						if(info.drawer != null)
						{
							renderInfo.pass = info.drawer.TryGetPassIndex(pass);
							if(renderInfo.pass >= 0)
							{
								var origin = chunks[chunk.chunkId].origin;

								if(shader.originPos >= 0)
								{
									if(shader.modelMatrix >= 0)
									{
										renderInfo.shaderUniforms[shader.modelMatrix].SetValue(float4x4.identity);
									}

									renderInfo.shaderUniforms[shader.originPos].SetValue(
										new float3((float)(origin.x - playerCamPos.X), (float)(origin.y - playerCamPos.Y), (float)(origin.z - playerCamPos.Z)));
								}
								else
								{
									var mat = new float[16];
									Mat4f.Identity(mat);
									Mat4f.Translate(mat, mat, (float)(origin.x - playerCamPos.X), (float)(origin.y - playerCamPos.Y), (float)(origin.z - playerCamPos.Z));
									renderInfo.shaderUniforms[shader.modelMatrix].SetValue(mat);
								}

								info.drawer.Render(ref renderInfo, ref dummyBytes);
							}
						}
					}
					renderInfo.shader.Stop();
				}
			}
			rapi.GlPopMatrix();
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
			bool forceRebuild = false;
			foreach(var id in task.builders)
			{
				var result = ReduceBuilderUsage(id);
				if(result == EnumReduceUsageResult.RemovedChunkShader)
				{
					return;
				}
				if(result == EnumReduceUsageResult.RemovedBuilder)
				{
					forceRebuild = true;
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

				var drawableHandle = chunkShader.drawer?.drawableHandle;
				if(task.verticesBlocks.Count == 1)
				{
					chunkDataHelper.verticesData = task.verticesBlocks[0];
					chunkDataHelper.indicesData = task.indices;
					if(drawableHandle == null)
					{
						drawableHandle = rapi.UploadDrawable(chunkDataHelper);
					}
					else
					{
						rapi.ReuploadDrawable(drawableHandle, chunkDataHelper, true);
					}
				}
				else
				{
					chunkDataHelper.indicesData = task.indices;

					//First allocate the required size (i.e. a null pointer will just allocate the buffer and won't upload any data)
					chunkDataHelper.verticesData = null;
					if(drawableHandle == null)
					{
						drawableHandle = rapi.UploadDrawable(chunkDataHelper);
					}
					else
					{
						rapi.ReuploadDrawable(drawableHandle, chunkDataHelper, true);
					}

					//Then uploading data block-by-block
					int lastIndex = task.verticesBlocks.Count - 1;
					chunkDataHelper.verticesCount = BuildTask.BLOCK_SIZE;
					chunkDataHelper.indicesCount = 0;//Uploading only vertices
					chunkDataHelper.indicesData = null;
					var vertBlockOffset = new int[1];
					for(int i = 0; i <= lastIndex; i++)
					{
						if(i == lastIndex)
						{
							chunkDataHelper.verticesCount = task.verticesCount % BuildTask.BLOCK_SIZE;
						}
						chunkDataHelper.verticesData = task.verticesBlocks[i];
						rapi.UpdateDrawablePart(drawableHandle, chunkDataHelper, 0, vertBlockOffset);
						vertBlockOffset[0] += BuildTask.BLOCK_SIZE;
					}
				}
				chunkShader.drawer = new ChunkPartDrawer(drawableHandle, task.renderPasses, task.commands, task.uniformsMap, task.uniformsData);

				chunkDataHelper.Clear();
			}
			if(!forceRebuild && task.version == chunkShaders[task.chunkShaderId].version && !rebuildStructs.Contains(task.chunkShaderId))
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
					chunkShaders[chunkShaderId].drawer?.Dispose();

					int shaderId = chunkShaders[chunkShaderId].shaderId;
					int shaderChunkId = chunkShaders[chunkShaderId].shaderChunkId;

					chunks[chunkId].chunkShadersChain = chunkShaders.Remove(chunks[chunkId].chunkShadersChain, chunkShaderId);
					if(chunks[chunkId].chunkShadersChain < 0)
					{
						chunkToId.Remove(chunks[chunkId].index);
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

				rebuildStructs.Add(chunkShaderId);
				return EnumReduceUsageResult.RemovedBuilder;
			}
			return EnumReduceUsageResult.None;
		}

		private unsafe static bool MemEquals(byte* a, byte* b, int length)
		{
			for(int i = length / 8; i > 0; i--)
			{
				if(*(long*)a != *(long*)b)
				{
					return false;
				}
				a += 8;
				b += 8;
			}

			if((length & 4) != 0)
			{
				if(*((int*)a) != *((int*)b))
				{
					return false;
				}
				a += 4;
				b += 4;
			}

			if((length & 2) != 0)
			{
				if(*((short*)a) != *((short*)b))
				{
					return false;
				}
				a += 2;
				b += 2;
			}

			if((length & 1) != 0)
			{
				if(*a != *b)
				{
					return false;
				}
			}

			return true;
		}

		private unsafe static bool MemIsZero(byte* a, int length)
		{
			for(int i = length / 8; i > 0; i--)
			{
				if(*(long*)a != 0)
				{
					return false;
				}
				a += 8;
			}

			if((length & 4) != 0)
			{
				if(*((int*)a) != 0)
				{
					return false;
				}
				a += 4;
			}

			if((length & 2) != 0)
			{
				if(*((short*)a) != 0)
				{
					return false;
				}
				a += 2;
			}

			if((length & 1) != 0)
			{
				if(*a != 0)
				{
					return false;
				}
			}

			return true;
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

			public int shaderChunksChain;

			public ShaderInfo(IExtendedShaderProgram shader, int projMatrix, int viewMatrix, int modelMatrix, int mvMatrix, int originPos,
				int fogColor, int ambientColor, int fogDensity, int fogMin)
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
				this.shaderChunksChain = -1;
			}
		}

		private struct BuilderInfo
		{
			public readonly IBatchDataBuilder builder;
			public readonly IBuilderStructContainer builderStruct;
			public readonly int chunkId;
			public readonly int chunkShaderId;

			public int usageCounter;

			public BuilderInfo(int chunkId, IBatchDataBuilder builder, IBuilderStructContainer builderStruct, int chunkShaderId)
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
			public readonly int3 index;
			public readonly int3 origin;

			public int chunkShadersChain;

			public ChunkInfo(int3 index, int3 origin)
			{
				this.index = index;
				this.origin = origin;
				this.chunkShadersChain = -1;
			}
		}

		private struct ChunkShaderUsage
		{
			public readonly int shaderId;
			public readonly int shaderChunkId;
			public int buildersChain;
			public int version;

			public ChunkPartDrawer drawer;

			public ChunkShaderUsage(int shaderId, int shaderChunkId)
			{
				this.shaderId = shaderId;
				this.shaderChunkId = shaderChunkId;
				this.buildersChain = -1;
				this.version = 0;
				this.drawer = null;
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

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		private readonly struct GraphicsCommand
		{
			public readonly GraphicsCommandType Type;
			public readonly uint Offset;
			public readonly uint Count;
			public readonly uint Index;
			public readonly uint Arg;

			public GraphicsCommand(uint offset, uint count)
			{
				Type = GraphicsCommandType.Draw;
				Offset = offset;
				Count = count;
				Index = 0;
				Arg = 0;
			}

			public GraphicsCommand(uint offset, uint count, uint index)
			{
				Type = GraphicsCommandType.SetUniform;
				Offset = offset;
				Count = count;
				Index = index;
				Arg = 0;
			}

			public GraphicsCommand(uint offset, uint count, uint index, EnumTextureTarget target)
			{
				Type = GraphicsCommandType.BindTexture;
				Offset = offset;
				Count = count;
				Index = index;
				Arg = (uint)target;
			}
		}

		private enum GraphicsCommandType
		{
			Draw,
			SetUniform,
			BindTexture
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		private readonly struct UniformPointer
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

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		private readonly struct RenderPassGroup
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

		private class ChunkPartDrawer
		{
			public readonly IDrawableHandle drawableHandle;
			private readonly RenderPassGroup[] renderPasses;
			private readonly GraphicsCommand[] commands;
			private readonly UniformPointer[] uniformsMap;
			private readonly byte[] uniformsData;
			private readonly int maxUniformSize;

			public ChunkPartDrawer(IDrawableHandle drawableHandle, RenderPassGroup[] renderPasses, GraphicsCommand[] commands, UniformPointer[] uniformsMap, byte[] uniformsData)
			{
				this.drawableHandle = drawableHandle;
				this.renderPasses = renderPasses;
				this.commands = commands;
				this.uniformsMap = uniformsMap;
				this.uniformsData = uniformsData;

				maxUniformSize = 0;
				for(int i = uniformsMap.Length - 1; i >= 0; i--)
				{
					maxUniformSize = Math.Max(maxUniformSize, uniformsMap[i].Size);
				}
			}

			public void Dispose()
			{
				drawableHandle.Dispose();
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

			public unsafe void Render(ref RenderCall callInfo, ref byte[] dummyBytes)
			{
				if(dummyBytes.Length < maxUniformSize)
				{
					dummyBytes = new byte[maxUniformSize];
				}

				fixed(byte* dataPtr = uniformsData)
				{
					fixed(byte* zeroPtr = dummyBytes)
					{
						int last = renderPasses[callInfo.pass].Index + renderPasses[callInfo.pass].Count;
						for(int i = renderPasses[callInfo.pass].Index; i < last; i++)
						{
							var cmd = commands[i];
							switch(cmd.Type)
							{
								case GraphicsCommandType.Draw:
									callInfo.rapi.RenderDrawable(drawableHandle, cmd.Offset, (int)cmd.Count);
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

		private ref struct RenderCall
		{
			public IRenderAPI rapi;
			public int pass;
			public GraphicsSystem graphics;
			public UniformPropertyHandle[] shaderUniforms;
			public IExtendedShaderProgram shader;
		}
	}
}