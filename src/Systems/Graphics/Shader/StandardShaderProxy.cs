using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Shader;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace PowerOfMind.Systems.Graphics.Shader
{
	public class StandardShaderProxy : IExtendedShaderProgram
	{
		public int PassId => ShaderRegistry.getProgram(shaderType).PassId;

		public string PassName => ShaderRegistry.getProgram(shaderType).PassName;

		public bool ClampTexturesToEdge { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
		public IShader VertexShader { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
		public IShader FragmentShader { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
		public IShader GeometryShader { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public bool Disposed => ShaderRegistry.getProgram(shaderType).Disposed;

		public bool LoadError => ShaderRegistry.getProgram(shaderType).LoadError;

		public ShaderInputDeclaration Inputs { get; private set; }
		public ShaderUniformDeclaration Uniforms { get; private set; }

		protected Dictionary<string, int> uniformNameToIndex = new Dictionary<string, int>();
		protected Dictionary<string, int> uniformAliasToIndex = null;

		private readonly EnumShaderProgram shaderType;
		private readonly GraphicsSystem graphics;
		private readonly IReadOnlyDictionary<string, string> attribNameToAlias;
		private readonly IReadOnlyDictionary<string, string> uniformNameToAlias;

		public StandardShaderProxy(EnumShaderProgram shaderType, GraphicsSystem graphics,
			IReadOnlyDictionary<string, string> attribNameToAlias = null, IReadOnlyDictionary<string, string> uniformNameToAlias = null)
		{
			this.shaderType = shaderType;
			this.graphics = graphics;
			this.attribNameToAlias = attribNameToAlias;
			this.uniformNameToAlias = uniformNameToAlias;
		}

		public void BindTexture2D(string samplerName, int textureId, int textureNumber)
		{
			ShaderRegistry.getProgram(shaderType).BindTexture2D(samplerName, textureId, textureNumber);
		}

		public void BindTextureCube(string samplerName, int textureId, int textureNumber)
		{
			ShaderRegistry.getProgram(shaderType).BindTextureCube(samplerName, textureId, textureNumber);
		}

		public bool Compile()
		{
			var shader = ShaderRegistry.getProgram(shaderType);
			Inputs = new ShaderInputDeclaration(graphics.GetShaderAttributes(shader.ProgramId, attribNameToAlias));

			var uniforms = graphics.GetShaderUniforms(shader.ProgramId, uniformNameToAlias, false);
			for(int i = 0; i < uniforms.Length; i++)
			{
				uniformNameToIndex[uniforms[i].Name] = i;
				if(!string.IsNullOrEmpty(uniforms[i].Alias))
				{
					if(uniformAliasToIndex == null) uniformAliasToIndex = new Dictionary<string, int>();
					uniformAliasToIndex[uniforms[i].Alias] = i;
				}
				if(shader.textureLocations.TryGetValue(uniforms[i].Name, out var slot))
				{
					uniforms[i] = uniforms[i].WithReferenceSlot(slot);
				}
			}
			Uniforms = new ShaderUniformDeclaration(uniforms);
			return true;
		}

		public void Dispose()
		{
		}

		public void Stop()
		{
			ShaderRegistry.getProgram(shaderType).Stop();
		}

		public void Uniform(string uniformName, float value)
		{
			ShaderRegistry.getProgram(shaderType).Uniform(uniformName, value);
		}

		public void Uniform(string uniformName, int value)
		{
			ShaderRegistry.getProgram(shaderType).Uniform(uniformName, value);
		}

		public void Uniform(string uniformName, Vec2f value)
		{
			ShaderRegistry.getProgram(shaderType).Uniform(uniformName, value);
		}

		public void Uniform(string uniformName, Vec3f value)
		{
			ShaderRegistry.getProgram(shaderType).Uniform(uniformName, value);
		}

		public void Uniform(string uniformName, Vec4f value)
		{
			ShaderRegistry.getProgram(shaderType).Uniform(uniformName, value);
		}

		public void UniformMatrices(string uniformName, int count, float[] matrix)
		{
			ShaderRegistry.getProgram(shaderType).UniformMatrices(uniformName, count, matrix);
		}

		public void UniformMatrices4x3(string uniformName, int count, float[] matrix)
		{
			ShaderRegistry.getProgram(shaderType).UniformMatrices4x3(uniformName, count, matrix);
		}

		public void UniformMatrix(string uniformName, float[] matrix)
		{
			((IShaderProgram)ShaderRegistry.getProgram(shaderType)).UniformMatrix(uniformName, matrix);
		}

		public void Uniforms4(string uniformName, int count, float[] values)
		{
			ShaderRegistry.getProgram(shaderType).Uniforms4(uniformName, count, values);
		}

		public void Use()
		{
			ShaderRegistry.getProgram(shaderType).Use();
		}

		public void SetSampler(int textureNumber, ITextureSampler sampler)
		{
			throw new NotImplementedException();
		}

		public void BindTexture(int uniformIndex, EnumTextureTarget target, int textureId, int textureNumber)
		{
			if(target == EnumTextureTarget.TextureCubeMap)
			{
				BindTextureCube(Uniforms[uniformIndex].Name, textureId, textureNumber);
			}
			else
			{
				BindTexture2D(Uniforms[uniformIndex].Name, textureId, textureNumber);
			}
		}

		public int FindUniformIndex(string name)
		{
			if(uniformNameToIndex.TryGetValue(name, out int index))
			{
				return index;
			}
			return -1;
		}

		public int FindUniformIndexByAlias(string alias)
		{
			if(uniformAliasToIndex != null && uniformAliasToIndex.TryGetValue(alias, out int index))
			{
				return index;
			}
			return -1;
		}
	}
}