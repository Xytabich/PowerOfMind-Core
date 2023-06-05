using PowerOfMind.Collections;
using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Drawable;
using PowerOfMind.Graphics.Shader;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Vintagestory.API.Client;

namespace PowerOfMind.Systems.ChunkRender
{
	public class ChunkRenderer : IRenderer
	{
		double IRenderer.RenderOrder => 0.35;
		int IRenderer.RenderRange => 0;

		private Dictionary<int3, int> chunkToId = new Dictionary<int3, int>();
		private HeapCollection<ChunkInfo> chunks = new HeapCollection<ChunkInfo>();

		private Dictionary<ShaderStructKey, int> shaderStructToId = new Dictionary<ShaderStructKey, int>();
		private HeapCollection<ShaderStructInfo> shaderStructs = new HeapCollection<ShaderStructInfo>();

		private ChainList<BuilderInfo> builders = new ChainList<BuilderInfo>();

		private HashSet<int> rebuildStructs = new HashSet<int>();//TODO: build in separate thread(using tasks), make settings property "maxConcurrentBuildTasks", by default - cpuCount/2, don't forget about try/catch

		public int AddBuilder<T>(int3 chunk, IExtendedShaderProgram shader, IChunkBuilder builder) where T : struct, IVertexStruct
		{
			if(!chunkToId.TryGetValue(chunk, out int cid))
			{
				cid = chunks.Add(new ChunkInfo(chunk));
				chunkToId[chunk] = cid;
			}
			rebuildStructs.Add(cid);//TODO: rebuild only parts related to shader

			var declaration = shader.MapDeclaration(default(T).GetDeclaration());
			//TODO: create mesh data container if new shader struct

			builders.Add(new BuilderInfo(cid, builder));//TODO: add to shader chain

			return default;
		}

		public void RemoveBuilder(int id)
		{
			//TODO: add to remove queue & set isDirty if builder had vertices
		}

		void IRenderer.OnRenderFrame(float deltaTime, EnumRenderStage stage)
		{
			throw new NotImplementedException();
		}

		void IDisposable.Dispose()
		{
			throw new NotImplementedException();
		}

		private readonly struct ShaderStructKey : IEquatable<ShaderStructKey>
		{
			public readonly IExtendedShaderProgram shader;
			public readonly VertexAttribute[] attributes;

			public ShaderStructKey(IExtendedShaderProgram shader, VertexAttribute[] attributes)
			{
				this.shader = shader;
				this.attributes = attributes;
			}

			public override bool Equals(object obj)
			{
				return obj is ShaderStructKey key && Equals(key);
			}

			public bool Equals(ShaderStructKey other)
			{
				if(shader != other.shader) return false;
				if(attributes.Length != other.attributes.Length) return false;
				for(int i = attributes.Length - 1; i >= 0; i--)
				{
					if(!attributes[i].Equals(other.attributes[i])) return false;
				}
				return true;
			}

			public override int GetHashCode()
			{
				int hashCode = -824831684;
				hashCode = hashCode * -1521134295 + EqualityComparer<IExtendedShaderProgram>.Default.GetHashCode(shader);

				int arrHash = 0;
				int arrLen = attributes.Length;
				for(int i = (arrLen >= 8) ? (arrLen - 8) : 0; i < arrLen; i++)
				{
					arrHash = ((arrHash << 5) + arrHash) ^ attributes[i].GetHashCode();
				}

				hashCode = hashCode * -1521134295 + arrHash;
				return hashCode;
			}

			public readonly struct VertexAttribute : IEquatable<VertexAttribute>
			{
				public readonly int location;
				public readonly int size;
				public readonly EnumShaderPrimitiveType type;

				public VertexAttribute(int location, int size, EnumShaderPrimitiveType type)
				{
					this.location = location;
					this.size = size;
					this.type = type;
				}

				public override bool Equals(object obj)
				{
					return obj is VertexAttribute attribute && Equals(attribute);
				}

				public bool Equals(VertexAttribute other)
				{
					return location == other.location &&
						   size == other.size &&
						   type == other.type;
				}

				public override int GetHashCode()
				{
					int hashCode = 574940175;
					hashCode = hashCode * -1521134295 + location.GetHashCode();
					hashCode = hashCode * -1521134295 + size.GetHashCode();
					hashCode = hashCode * -1521134295 + type.GetHashCode();
					return hashCode;
				}
			}
		}

		private struct ShaderStructInfo
		{
			public readonly IExtendedShaderProgram shader;
			public readonly VertexDeclaration declaration;
			public readonly int stride;

			public int targetChunkPartsChain;
		}

		private struct BuilderInfo
		{
			public readonly IChunkBuilder builder;
			public readonly int chunkId;
			public readonly int shaderStructId;

			public BuilderInfo(int chunkId, IChunkBuilder builder, int shaderStructId)
			{
				this.chunkId = chunkId;
				this.builder = builder;
				this.shaderStructId = shaderStructId;
			}
		}

		private struct ChunkInfo
		{
			public readonly int3 position;

			public ChunkInfo(int3 position)
			{
				this.position = position;
			}
		}

		public interface IChunkBuilder
		{
			/// <summary>
			/// Builds the drawable data for the chunk. Called on a separate thread.
			/// </summary>
			void Build(IChunkBuilderContext context);
		}

		public interface IChunkBuilderContext
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