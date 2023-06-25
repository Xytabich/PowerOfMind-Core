using PowerOfMind.Systems.Graphics;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace PowerOfMind.Graphics.Shader
{
	public class ExtendedShaderProgram : IExtendedShaderProgram
	{
		public int PassId => 0;
		public string PassName { get; }
		public IShader VertexShader { get; set; }
		public IShader FragmentShader { get; set; }
		public IShader GeometryShader { get; set; }
		public bool ClampTexturesToEdge { get; set; }
		public bool LoadError { get; private set; }
		public bool Disposed => handle == 0;
		public ShaderInputDeclaration Inputs { get; private set; }
		public ShaderUniformDeclaration Uniforms { get; private set; }

		protected Dictionary<string, int> uniformNameToIndex = new Dictionary<string, int>();
		protected Dictionary<string, int> uniformAliasToIndex = new Dictionary<string, int>();
		protected Dictionary<int, ITextureSampler> customSamplers = null;
		protected readonly GraphicsSystem graphics;
		protected ShaderStage[] stages;
		protected int handle = 0;

		protected Action[] useBindings = null;

		private Func<IExtendedShaderProgram, Action> createUseBindings = null;

		public ExtendedShaderProgram(GraphicsSystem graphics, string passName, Func<IExtendedShaderProgram, Action> createUseBindings, params ShaderStage[] stages)
		{
			this.graphics = graphics;
			this.PassName = passName;
			this.createUseBindings = createUseBindings;
			foreach(var stage in stages)
			{
				switch(stage.Type)
				{
					case EnumShaderType.VertexShader:
						VertexShader = stage;
						break;
					case EnumShaderType.FragmentShader:
						FragmentShader = stage;
						break;
					case EnumShaderType.GeometryShader:
						GeometryShader = stage;
						break;
				}
			}
		}

		protected ExtendedShaderProgram(GraphicsSystem graphics, string passName)
		{
			this.graphics = graphics;
			this.PassName = passName;
		}

		public virtual bool Compile()
		{
			if(handle != 0) return true;
			LoadError = false;

			foreach(var stage in stages)
			{
				if(!stage.EnsureVersionSupported(graphics, graphics.Logger))
				{
					LoadError = true;
					return false;
				}
			}

			foreach(var stage in stages)
			{
				if(!stage.Compile(graphics, graphics.Logger))
				{
					LoadError = true;
					break;
				}
			}
			if(LoadError)
			{
				foreach(var stage in stages)
				{
					stage.Dispose();
				}
				return false;
			}

			if(!graphics.TryCreateShaderProgram(p => {
				foreach(var stage in stages)
				{
					stage.Attach(p);
				}
			}, out handle, out var error))
			{
				foreach(var stage in stages)
				{
					stage.Dispose();
				}

				handle = 0;
				LoadError = true;

				graphics.Logger.Error("Link error in shader program for pass {0}: {1}", PassName, error.TrimEnd());
				return false;
			}

			foreach(var stage in stages)//All stages are included in the program, there is no need to store them anymore
			{
				stage.Dispose();
			}

			useBindings = null;
			if(createUseBindings != null)
			{
				useBindings = new Action[] { createUseBindings(this) };
			}

			graphics.Logger.Notification("Loaded shader programm for render pass {0}.", PassName);

			InitInputDeclaration();
			InitUniformDeclaration();

			if(customSamplers != null)
			{
				foreach(var pair in customSamplers)
				{
					pair.Value.Allocate();
				}
			}

			return true;
		}

		public void Use()
		{
			graphics.SetActiveShader(handle);
			if(useBindings != null)
			{
				foreach(var binding in useBindings)
				{
					binding.Invoke();
				}
			}
		}

		public void Stop()
		{
			graphics.UnsetActiveShader();
			if(customSamplers != null)
			{
				foreach(var pair in customSamplers)
				{
					pair.Value.Unbind(pair.Key);
				}
			}
		}

		public void Dispose()
		{
			if(handle == 0) return;
			graphics.DeleteShaderProgram(handle);
			handle = 0;

			foreach(var stage in stages)
			{
				stage.Dispose();
			}

			if(customSamplers != null)
			{
				foreach(var pair in customSamplers)
				{
					pair.Value.Release();
				}
			}
		}

		public void SetSampler(int textureNumber, ITextureSampler sampler)
		{
			if(sampler == null)
			{
				if(customSamplers != null && customSamplers.TryGetValue(textureNumber, out var s))
				{
					if(handle != 0) s.Release();
					customSamplers.Remove(textureNumber);
				}
			}
			else
			{
				if(customSamplers == null)
				{
					customSamplers = new Dictionary<int, ITextureSampler>();
				}
				else if(customSamplers.TryGetValue(textureNumber, out var s))
				{
					if(handle != 0) s.Release();
				}
				customSamplers[textureNumber] = sampler;
				if(handle != 0) sampler.Allocate();
			}
		}

		public unsafe void BindTexture(int uniformIndex, EnumTextureTarget target, int textureId, int textureNumber)
		{
			if(uniformIndex < 0) return;
			Uniforms[uniformIndex].SetValue(&textureNumber, 1);
			ITextureSampler sampler = null;
			if(customSamplers != null && !customSamplers.TryGetValue(textureNumber, out sampler))
			{
				sampler = null;
			}
			graphics.BindTexture(target, textureId, textureNumber, sampler, ClampTexturesToEdge);
		}

		public void BindTexture2D(string samplerName, int textureId, int textureNumber)
		{
			BindTexture(FindUniformIndex(samplerName), EnumTextureTarget.Texture2D, textureId, textureNumber);
		}

		public void BindTextureCube(string samplerName, int textureId, int textureNumber)
		{
			BindTexture(FindUniformIndex(samplerName), EnumTextureTarget.TextureCubeMap, textureId, textureNumber);
		}

		public void Uniform(string uniformName, float value)
		{
			Uniforms[FindUniformIndex(uniformName)].SetValue(value);
		}

		public void Uniform(string uniformName, int value)
		{
			Uniforms[FindUniformIndex(uniformName)].SetValue(value);
		}

		public void Uniform(string uniformName, Vec2f value)
		{
			Uniforms[FindUniformIndex(uniformName)].SetValue(value);
		}

		public void Uniform(string uniformName, Vec3f value)
		{
			Uniforms[FindUniformIndex(uniformName)].SetValue(value);
		}

		public void Uniform(string uniformName, Vec4f value)
		{
			Uniforms[FindUniformIndex(uniformName)].SetValue(value);
		}

		public void Uniforms4(string uniformName, int count, float[] values)
		{
			Uniforms[FindUniformIndex(uniformName)].SetValues(values, count);
		}

		public void UniformMatrices(string uniformName, int count, float[] matrix)
		{
			Uniforms[FindUniformIndex(uniformName)].SetValues(matrix, count);
		}

		public void UniformMatrices4x3(string uniformName, int count, float[] matrix)
		{
			Uniforms[FindUniformIndex(uniformName)].SetValues(matrix, count);
		}

		public void UniformMatrix(string uniformName, float[] matrix)
		{
			Uniforms[FindUniformIndex(uniformName)].SetValue(matrix);
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
			if(uniformAliasToIndex.TryGetValue(alias.ToUpperInvariant(), out int index))
			{
				return index;
			}
			return -1;
		}

		protected void InitInputDeclaration()
		{
			var name2alias = new Dictionary<string, string>();
			for(int i = 0; i < stages.Length; i++)
			{
				if(stages[i].Type == EnumShaderType.VertexShader)
				{
					stages[i].CollectInputsAlias(name2alias);
					break;
				}
			}
			Inputs = new ShaderInputDeclaration(graphics.GetShaderAttributes(handle, name2alias));
		}

		protected void InitUniformDeclaration()
		{
			var name2alias = new Dictionary<string, string>();
			for(int i = 0; i < stages.Length; i++)
			{
				if(stages[i].Type == EnumShaderType.VertexShader)
				{
					stages[i].CollectUniformsAlias(name2alias);
					break;
				}
			}
			var uniforms = graphics.GetShaderUniforms(handle, name2alias);
			uniformNameToIndex.Clear();
			uniformAliasToIndex.Clear();
			for(int i = 0; i < uniforms.Length; i++)
			{
				if(!string.IsNullOrEmpty(uniforms[i].Name)) uniformNameToIndex[uniforms[i].Name] = i;
				if(!string.IsNullOrEmpty(uniforms[i].Alias)) uniformAliasToIndex[uniforms[i].Alias] = i;
			}
			Uniforms = new ShaderUniformDeclaration(uniforms);
		}
	}
}