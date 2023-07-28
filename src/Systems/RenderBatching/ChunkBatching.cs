﻿using PowerOfMind.Collections;
using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Drawable;
using PowerOfMind.Graphics.Shader;
using PowerOfMind.Systems.RenderBatching.Core;
using PowerOfMind.Systems.RenderBatching.Draw;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.Mathematics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;

namespace PowerOfMind.Systems.RenderBatching
{
	public partial class ChunkBatching : IRenderer
	{
		double IRenderer.RenderOrder => 0.35;
		int IRenderer.RenderRange => 0;

		private readonly Dictionary<int3, int> chunkToId = new Dictionary<int3, int>();
		private readonly HeapCollection<ChunkInfo> chunks = new HeapCollection<ChunkInfo>();
		private readonly ChainList<ChunkShaderUsage> chunkShaders = new ChainList<ChunkShaderUsage>();

		private readonly Dictionary<string, int> shaderToId = new Dictionary<string, int>();
		private readonly HeapCollection<ShaderInfo> shaders = new HeapCollection<ShaderInfo>();
		private readonly ChainList<ShaderChunkUsage> shaderChunks = new ChainList<ShaderChunkUsage>();

		private readonly ChainList<BuilderInfo> builders = new ChainList<BuilderInfo>();

		private readonly Queue<int> removeBuilderQueue = new Queue<int>();
		private readonly ConcurrentBag<ChunkBuildTask> completedTasks = new ConcurrentBag<ChunkBuildTask>();

		private readonly HashSet<int> rebuildStructs = new HashSet<int>();

		private readonly List<ChunkBuildTask> tmpTasksList = new List<ChunkBuildTask>();
		private readonly List<IBuilderStructContainer> tmpBuildersList = new List<IBuilderStructContainer>();
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
		private IExtendedShaderProgram shadowShader = null;
		private int shadowOriginPos;

		public ChunkBatching(ICoreClientAPI capi, GraphicsSystem graphics)
		{
			this.capi = capi;
			this.rapi = capi.Render;
			this.graphics = graphics;

			capi.Event.RegisterRenderer(new DummyRenderer() { action = Update, RenderOrder = 0.991f, RenderRange = 0 }, EnumRenderStage.Before, "powerofmindcore:chunkrenderer");
			capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "powerofmindcore:chunkrenderer");
			capi.Event.RegisterRenderer(this, EnumRenderStage.ShadowFar, "powerofmindcore:chunkrenderer");
			capi.Event.RegisterRenderer(this, EnumRenderStage.ShadowNear, "powerofmindcore:chunkrenderer");
			graphics.OnAfterShadersReload += ReloadAll;
		}

		public int AddBuilder<TVertex, TUniform>(int3 chunk, IExtendedShaderProgram shader, IBatchDataBuilder<TVertex, TUniform> builder)
			where TVertex : unmanaged, IVertexStruct
			where TUniform : unmanaged, IUniformsData
		{
			if(shader.Disposed) throw new InvalidOperationException("Shader is not initialized");

			if(!chunkToId.TryGetValue(chunk, out int cid))
			{
				cid = chunks.Add(new ChunkInfo(chunk, chunk * capi.World.BlockAccessor.ChunkSize));
				chunkToId[chunk] = cid;
			}
			if(!shaderToId.TryGetValue(shader.PassName, out int sid))
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
			id = builders.Add(shaderUsage.buildersChain, new BuilderInfo(cid, new BuilderStructContainer<TVertex, TUniform>(builder), chunkShaderId));
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
			if(shaders.Count > 0)
			{
				switch(stage)
				{
					case EnumRenderStage.Opaque://TODO: add another passes
						ScreenManager.FrameProfiler.Mark("powerofmind:chunkbatch-begin");
						RenderStage(EnumChunkRenderPass.Opaque);
						ScreenManager.FrameProfiler.Mark("powerofmind:chunkbatch-opaque");
						RenderStage(EnumChunkRenderPass.TopSoil);
						ScreenManager.FrameProfiler.Mark("powerofmind:chunkbatch-topsoil");
						RenderStage(EnumChunkRenderPass.BlendNoCull);
						ScreenManager.FrameProfiler.Mark("powerofmind:chunkbatch-blend");
						RenderStage(EnumChunkRenderPass.OpaqueNoCull);
						ScreenManager.FrameProfiler.Mark("powerofmind:chunkbatch-nocull");
						break;
					case EnumRenderStage.ShadowFar:
					case EnumRenderStage.ShadowNear:
						ScreenManager.FrameProfiler.Mark("powerofmind:chunkbatch-shadowbegin");
						RenderShadow(EnumChunkRenderPass.Opaque);
						RenderShadow(EnumChunkRenderPass.TopSoil);
						RenderShadow(EnumChunkRenderPass.BlendNoCull);
						RenderShadow(EnumChunkRenderPass.OpaqueNoCull);
						ScreenManager.FrameProfiler.Mark("powerofmind:chunkbatch-shadowend");
						break;
				}
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
				UpdateShader(shader.shader, pair.Value);
			}
			shadowShader = null;
			return true;
		}

		private int AddShader(IExtendedShaderProgram shader)
		{
			int id = shaders.Allocate();
			shaders[id].shaderChunksChain = -1;

			UpdateShader(shader, id);
			shaderToId[shader.PassName] = id;
			return id;
		}

		private void UpdateShader(IExtendedShaderProgram shader, int id)
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

				shaders[id] = new ShaderInfo(shader, projMatrix, viewMatrix, modelMatrix, mvMatrix, originPos,
					tmpPairs.TryGetValue(5, out var fogColor) ? fogColor : -1,
					tmpPairs.TryGetValue(6, out var ambientColor) ? ambientColor : -1,
					tmpPairs.TryGetValue(7, out var fogDensity) ? fogDensity : -1,
					tmpPairs.TryGetValue(8, out var fogMin) ? fogMin : -1,
					shaders[id].shaderChunksChain
				);
				return;
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
			switch(pass)
			{
				case EnumChunkRenderPass.OpaqueNoCull:
					rapi.GlToggleBlend(false);
					break;
				default:
					rapi.GlToggleBlend(true);
					break;
			}
			rapi.GlMatrixModeModelView();
			rapi.GlPushMatrix();
			rapi.GlLoadMatrix(rapi.CameraMatrixOrigin);

			BatchDrawCall renderInfo = default;
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

					bool useShader = true;
					foreach(var chunk in shaderChunks.GetEnumerable(shader.shaderChunksChain))
					{
						ref readonly var info = ref chunkShaders[chunk.chunkShaderId];
						if(info.drawer != null)
						{
							renderInfo.pass = info.drawer.TryGetPassIndex(pass);
							if(renderInfo.pass >= 0)
							{
								if(useShader)
								{
									useShader = false;

									renderInfo.shader.Use();
									if(shader.ambientColor >= 0) renderInfo.shaderUniforms[shader.ambientColor].SetValue(rapi.AmbientColor);
									if(shader.fogColor >= 0) renderInfo.shaderUniforms[shader.fogColor].SetValue(rapi.FogColor);
									if(shader.fogDensity >= 0) renderInfo.shaderUniforms[shader.fogDensity].SetValue(rapi.FogDensity);
									if(shader.fogMin >= 0) renderInfo.shaderUniforms[shader.fogMin].SetValue(rapi.FogMin);

									renderInfo.shaderUniforms[shader.projMatrix].SetValue(rapi.CurrentProjectionMatrix);
									if(shader.mvMatrix >= 0) renderInfo.shaderUniforms[shader.mvMatrix].SetValue(rapi.CurrentModelviewMatrix);
									if(shader.viewMatrix >= 0) renderInfo.shaderUniforms[shader.viewMatrix].SetValue(rapi.CameraMatrixOriginf);
								}

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
					if(!useShader) renderInfo.shader.Stop();
				}
			}
			rapi.GlPopMatrix();
		}

		private void RenderShadow(EnumChunkRenderPass pass)
		{
			rapi.GLDepthMask(true);
			rapi.GlToggleBlend(false);
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

			BatchDrawCall renderInfo = default;
			renderInfo.graphics = graphics;
			renderInfo.rapi = rapi;
			renderInfo.shader = shadowShader;
			renderInfo.shaderUniforms = shadowShader.Uniforms.Properties;

			var playerCamPos = capi.World.Player.Entity.CameraPos;
			var camPos = new double3(playerCamPos.X, playerCamPos.Y, playerCamPos.Z);
			foreach(var pair in shaderToId)
			{
				ref readonly var shader = ref shaders[pair.Value];
				if(shader.shaderChunksChain >= 0)
				{
					foreach(var chunk in shaderChunks.GetEnumerable(shader.shaderChunksChain))
					{
						ref readonly var info = ref chunkShaders[chunk.chunkShaderId];
						if(info.drawer != null)
						{
							renderInfo.pass = info.drawer.TryGetPassIndex(pass);
							if(renderInfo.pass >= 0)
							{
								renderInfo.shaderUniforms[shadowOriginPos].SetValue((float3)(chunks[chunk.chunkId].origin - camPos));

								info.drawer.RenderShadow(ref renderInfo, ref dummyBytes);
							}
						}
					}
				}
			}
			rapi.GlToggleBlend(true);
		}

		private void Update(float dt)
		{
			ScreenManager.FrameProfiler.Mark("powerofmind:chunkbatch-updatebegin");
			if(ClientSettings.ShadowMapQuality > 0 && shadowShader == null)
			{
				shadowShader = graphics.ExtendStandardShader(EnumShaderProgram.Shadowmapgeneric, new Dictionary<string, string>() {
					{ "vertexPositionIn", VertexAttributeAlias.POSITION },
					{ "uvIn", VertexAttributeAlias.TEXCOORD_0 },
					{ "rgbaLightIn", VertexAttributeAlias.LIGHT },
					{ "renderFlagsIn", VertexAttributeAlias.FLAGS }
				}, new Dictionary<string, string>() {
					{ "tex2d", UniformAlias.MAIN_TEXTURE }
				});
				shadowOriginPos = shadowShader.FindUniformIndex("origin");
			}

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
			ScreenManager.FrameProfiler.Mark("powerofmind:chunkbatch-updateend");
		}

		private void ProcessCompletedTask(ChunkBuildTask task)
		{
			bool forceRebuild = false;
			foreach(var id in task.builderIds)
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

				var drawHandle = chunkShader.drawer?.drawHandle;
				var shadowHandle = chunkShader.drawer?.shadowHandle;
				if(task.verticesBlocks.Count == 1)
				{
					chunkDataHelper.verticesData = task.verticesBlocks[0];
					chunkDataHelper.indicesData = task.indices;
					if(drawHandle == null)
					{
						drawHandle = rapi.UploadDrawable(chunkDataHelper);
					}
					else
					{
						rapi.ReuploadDrawable(drawHandle, chunkDataHelper, true);
					}
				}
				else
				{
					chunkDataHelper.indicesData = task.indices;

					//First allocate the required size (i.e. a null pointer will just allocate the buffer and won't upload any data)
					chunkDataHelper.verticesData = null;
					if(drawHandle == null)
					{
						drawHandle = rapi.UploadDrawable(chunkDataHelper);
					}
					else
					{
						rapi.ReuploadDrawable(drawHandle, chunkDataHelper, true);
					}

					//Then uploading data block-by-block
					int lastIndex = task.verticesBlocks.Count - 1;
					chunkDataHelper.verticesCount = BatchBuildTask.BLOCK_SIZE;
					chunkDataHelper.indicesCount = 0;//Uploading only vertices
					chunkDataHelper.indicesData = null;
					var vertBlockOffset = new int[1];
					for(int i = 0; i <= lastIndex; i++)
					{
						if(i == lastIndex)
						{
							chunkDataHelper.verticesCount = task.verticesCount % BatchBuildTask.BLOCK_SIZE;
						}
						chunkDataHelper.verticesData = task.verticesBlocks[i];
						rapi.UpdateDrawablePart(drawHandle, chunkDataHelper, 0, vertBlockOffset);
						vertBlockOffset[0] += BatchBuildTask.BLOCK_SIZE;
					}
				}

				if(!task.shadowDeclaration.IsEmpty)
				{
					chunkDataHelper.vertexDeclaration = task.shadowDeclaration;
					chunkDataHelper.indicesCount = task.indicesCount;

					if(shadowHandle == null)
					{
						shadowHandle = rapi.CreateDrawableProxy(drawHandle, chunkDataHelper);
					}
					else
					{
						rapi.UpdateDrawableProxy(shadowHandle, chunkDataHelper);
					}
				}
				else if(shadowHandle != null)
				{
					shadowHandle.Dispose();
					shadowHandle = null;
				}

				chunkShader.drawer = new BatchDrawer(drawHandle, shadowHandle, task.renderPasses, task.commands, task.uniformsMap, task.shadowUniformsMap, task.uniformsData);

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
			tmpBuildersList.Clear();
			tmpIdsList.Clear();
			var chunkShaderInfo = chunkShaders[chunkShaderId];
			chunkShaders[chunkShaderId].version = 1;
			int id = chunkShaderInfo.buildersChain;
			do
			{
				tmpIdsList.Add(id);
				tmpBuildersList.Add(builders[id].builderStruct);
				builders[id].usageCounter++;
			}
			while(builders.TryGetNextId(chunkShaderInfo.buildersChain, id, out id));
			tmpTasksList.Add(new ChunkBuildTask(capi, shaders[chunkShaders[chunkShaderId].shaderId].shader, shadowShader,
				tmpBuildersList.ToArray(), OnTaskComplete, 1, chunkShaderId, tmpIdsList.ToArray()));
			tmpBuildersList.Clear();
		}

		private void OnTaskComplete(BatchBuildTask task)
		{
			completedTasks.Add((ChunkBuildTask)task);
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
						shaderToId.Remove(shaders[shaderId].shader.PassName);
						shaders.Remove(shaderId);
					}
					return EnumReduceUsageResult.RemovedChunkShader;
				}

				rebuildStructs.Add(chunkShaderId);
				return EnumReduceUsageResult.RemovedBuilder;
			}
			return EnumReduceUsageResult.None;
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
				int fogColor, int ambientColor, int fogDensity, int fogMin, int shaderChunksChain)
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
				this.shaderChunksChain = shaderChunksChain;
			}
		}

		private struct BuilderInfo
		{
			public readonly IBuilderStructContainer builderStruct;
			public readonly int chunkId;
			public readonly int chunkShaderId;

			public int usageCounter;

			public BuilderInfo(int chunkId, IBuilderStructContainer builderStruct, int chunkShaderId)
			{
				this.chunkId = chunkId;
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

			public BatchDrawer drawer;

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

		private class ChunkBuildTask : BatchBuildTask
		{
			public readonly int chunkShaderId;
			public readonly int version;
			public readonly int[] builderIds;

			public ChunkBuildTask(ICoreClientAPI capi, IExtendedShaderProgram shader, IExtendedShaderProgram shadowShader, IBuilderStructContainer[] builders, Action<BatchBuildTask> onComplete,
				int version, int chunkShaderId, int[] builderIds) : base(capi, shader, shadowShader, builders, onComplete)
			{
				this.version = version;
				this.chunkShaderId = chunkShaderId;
				this.builderIds = builderIds;
			}
		}

		private class ChunkDrawableData : IDrawableData
		{
			EnumDrawMode IDrawableInfo.DrawMode => EnumDrawMode.Triangles;
			uint IDrawableInfo.IndicesCount => indicesCount;
			uint IDrawableInfo.VerticesCount => verticesCount;
			int IDrawableInfo.VertexBuffersCount => 1;

			public uint indicesCount, verticesCount;
			public int verticesStride;
			public VertexDeclaration vertexDeclaration;

			public byte[] verticesData;
			public uint[] indicesData;

			public void Clear()
			{
				vertexDeclaration = default;
				verticesData = null;
				indicesData = null;
			}

			unsafe void IDrawableData.ProvideIndices(IndicesContext context)
			{
				if(indicesData == null) context.Process(null);
				else
				{
					fixed(uint* ptr = indicesData)
					{
						context.Process(ptr);
					}
				}
			}

			unsafe void IDrawableData.ProvideVertices(VerticesContext context)
			{
				if(verticesData == null)
				{
					context.Process((byte*)null, verticesStride);
				}
				else
				{
					fixed(byte* ptr = verticesData)
					{
						context.Process(ptr, verticesStride);
					}
				}
			}

			IndicesMeta IDrawableInfo.GetIndicesMeta()
			{
				return new IndicesMeta(false);
			}

			VertexBufferMeta IDrawableInfo.GetVertexBufferMeta(int index)
			{
				return new VertexBufferMeta(vertexDeclaration, false);
			}
		}
	}
}