using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Shader;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace PowerOfMind.Systems.Graphics.Shader
{
	public class StandardShaderProxy : IExtendedShaderProgram
	{
		public int PassId => shader.PassId;

		public string PassName => shader.PassName;

		public bool ClampTexturesToEdge { get => shader.ClampTexturesToEdge; set => shader.ClampTexturesToEdge = value; }
		public IShader VertexShader { get => shader.VertexShader; set => shader.VertexShader = value; }
		public IShader FragmentShader { get => shader.FragmentShader; set => shader.FragmentShader = value; }
		public IShader GeometryShader { get => shader.GeometryShader; set => shader.GeometryShader = value; }

		public bool Disposed => shader.Disposed;

		public bool LoadError => shader.LoadError;

		public ShaderInputDeclaration Inputs { get; }
		public ShaderUniformDeclaration Uniforms { get; }

		protected Dictionary<string, int> uniformNameToIndex = new Dictionary<string, int>();
		protected Dictionary<string, int> uniformAliasToIndex = null;

		private readonly IShaderProgram shader;

		public StandardShaderProxy(ShaderProgramBase shader, GraphicsSystem graphics,
			IReadOnlyDictionary<string, string> attribNameToAlias = null, IReadOnlyDictionary<string, string> uniformNameToAlias = null)
		{
			this.shader = shader;

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
		}

		public void BindTexture2D(string samplerName, int textureId, int textureNumber)
		{
			shader.BindTexture2D(samplerName, textureId, textureNumber);
		}

		public void BindTextureCube(string samplerName, int textureId, int textureNumber)
		{
			shader.BindTextureCube(samplerName, textureId, textureNumber);
		}

		public bool Compile()
		{
			return shader.Compile();
		}

		public void Dispose()
		{
			shader.Dispose();
		}

		public void Stop()
		{
			shader.Stop();
		}

		public void Uniform(string uniformName, float value)
		{
			shader.Uniform(uniformName, value);
		}

		public void Uniform(string uniformName, int value)
		{
			shader.Uniform(uniformName, value);
		}

		public void Uniform(string uniformName, Vec2f value)
		{
			shader.Uniform(uniformName, value);
		}

		public void Uniform(string uniformName, Vec3f value)
		{
			shader.Uniform(uniformName, value);
		}

		public void Uniform(string uniformName, Vec4f value)
		{
			shader.Uniform(uniformName, value);
		}

		public void UniformMatrices(string uniformName, int count, float[] matrix)
		{
			shader.UniformMatrices(uniformName, count, matrix);
		}

		public void UniformMatrices4x3(string uniformName, int count, float[] matrix)
		{
			shader.UniformMatrices4x3(uniformName, count, matrix);
		}

		public void UniformMatrix(string uniformName, float[] matrix)
		{
			shader.UniformMatrix(uniformName, matrix);
		}

		public void Uniforms4(string uniformName, int count, float[] values)
		{
			shader.Uniforms4(uniformName, count, values);
		}

		public void Use()
		{
			shader.Use();
		}

		public void SetSampler(int textureNumber, ITextureSampler sampler)
		{
			throw new System.NotImplementedException();
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