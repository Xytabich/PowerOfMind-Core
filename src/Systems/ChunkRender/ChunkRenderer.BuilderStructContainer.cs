﻿using PowerOfMind.Graphics;
using System;
using System.Collections.Generic;

namespace PowerOfMind.Systems.ChunkRender
{
	public partial class ChunkRenderer
	{
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

			unsafe int IBuilderStructContainer.GetUniformsSize()
			{
				return sizeof(TUniform);
			}

			UniformsDeclaration IBuilderStructContainer.GetUniformsDeclaration()
			{
				return uniformDefaults.GetDeclaration();
			}

			unsafe void IBuilderStructContainer.DiffUniforms<T>(in T otherData, Dictionary<int, int> uniformsMap, Dictionary<int, int> otherMap,
				List<KeyValuePair<int, int>> tmpList, UniformsDataCollection uniformsData)
			{
				tmpList.Clear();
				tmpList.AddRange(otherMap);
				var uniforms = uniformDefaults.GetDeclaration().Properties;
				var otherUniforms = otherData.GetDeclaration().Properties;
				fixed(T* oPtr = &otherData)
				{
					fixed(TUniform* uPtr = &uniformDefaults)
					{
						foreach(var pair in tmpList)
						{
							if(!uniformsMap.TryGetValue(pair.Key, out var index))
							{
								otherMap.Remove(pair.Key);
								continue;
							}

							int size = uniforms[index].Size * ShaderExtensions.GetTypeSize(uniforms[index].Type);
							if(MemEquals((byte*)uPtr + uniforms[index].Offset, (byte*)oPtr + otherUniforms[index].Offset, size))
							{
								otherMap.Remove(pair.Key);
							}
							else
							{
								otherMap[pair.Key] = uniformsData.PutUniformData((byte*)oPtr + otherUniforms[index].Offset, size);
							}
						}
					}
				}
			}

			unsafe void IBuilderStructContainer.CollectUniformsData(Dictionary<int, int> uniformsMap, List<KeyValuePair<int, int>> tmpList, UniformsDataCollection uniformsData)
			{
				tmpList.Clear();
				tmpList.AddRange(uniformsMap);
				var uniforms = uniformDefaults.GetDeclaration().Properties;
				fixed(TUniform* uPtr = &uniformDefaults)
				{
					foreach(var pair in tmpList)
					{
						int size = uniforms[pair.Value].Size * ShaderExtensions.GetTypeSize(uniforms[pair.Value].Type);
						uniformsMap[pair.Key] = uniformsData.PutUniformData((byte*)uPtr + uniforms[pair.Value].Offset, size);
					}
				}
			}
		}

		private interface IBuilderStructContainer
		{
			VertexDeclaration GetVertexDeclaration();

			int GetVertexStride();

			void ProvideVertexData(DataProcessor processor);

			int GetUniformsSize();

			UniformsDeclaration GetUniformsDeclaration();

			void DiffUniforms<T>(in T otherData, Dictionary<int, int> uniformsMap, Dictionary<int, int> otherMap,
				List<KeyValuePair<int, int>> tmpList, UniformsDataCollection uniformsData) where T : unmanaged, IUniformsData;

			void CollectUniformsData(Dictionary<int, int> uniformsMap, List<KeyValuePair<int, int>> tmpList, UniformsDataCollection uniformsData);
		}

		private unsafe delegate void DataProcessor(byte* ptr);
	}
}