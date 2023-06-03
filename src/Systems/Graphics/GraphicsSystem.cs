using OpenTK.Graphics.OpenGL;
using PowerOfMind.Graphics.Shader;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

namespace PowerOfMind.Graphics
{
	public partial class GraphicsSystem : ModSystem
	{
		public ShaderPreprocessor ShaderPreprocessor { get; private set; }
		public string VertexShaderDefines { get; private set; }
		public string FragmentShaderDefines { get; private set; }
		public bool IsGraphicsReady => isLoaded;

		public ILogger Logger => api.Logger;

		public event System.Func<bool> OnReloadShaders { add { reloadShaderListeners.Add(value); } remove { reloadShaderListeners.Remove(value); } }

		internal ICoreClientAPI Api => api;

		private ICoreClientAPI api;
		private UniformVariableHandlers uniformHandlers;

		private bool isLoaded = false;

		private List<System.Func<bool>> reloadShaderListeners = new List<System.Func<bool>>();
		private HashSet<IShaderProgram> shaders = new HashSet<IShaderProgram>();

		public override void StartClientSide(ICoreClientAPI api)
		{
			this.api = api;
			uniformHandlers = new UniformVariableHandlers();
			ShaderPreprocessor = new ShaderPreprocessor(this);
			api.Event.ReloadShader += ReloadShaders;

			api.ModLoader.GetModSystem<GraphicsSystem>().LoadAssetShader("testmod:testshader", new AssetLocation("standard"));
		}

		public override void Dispose()
		{
			base.Dispose();
			foreach(var shader in shaders)
			{
				shader.Dispose();
			}
			shaders.Clear();
			reloadShaderListeners.Clear();
		}

		/// <summary>
		/// Creates a shader that will be loaded from an asset.
		/// Automatically recompiled on a shader reload event.
		/// Inclusions can be prefixed with the domain in which to look for the file, for example "game:fogandlight.vsh".
		/// Inclusions without the specified domain will be searched in order from near to far, i.e. order: "shaderDomain:shaders/", "shaderDomain:shaderincludes/", "game:shaders/", "game:shaderincludes/".
		/// </summary>
		/// <param name="location">Shader location (domain and name without extension), shader parts will be searched in "shaders/" directory. For example "game:standard" will load the standard shader from the game folder</param>
		/// <param name="shaderDefinitionsProvider">Can be used to provide custom definitions for a specific shader</param>
		/// <param name="useBindingsFactory">A factory that creates an action that is executed every time <see cref="IShaderProgram.Use"/> is called, which can be used to set uniforms and so on.</param>
		public IExtendedShaderProgram LoadAssetShader(string passName, AssetLocation location,
			System.Func<EnumShaderType, string> shaderDefinitionsProvider = null,
			System.Func<IExtendedShaderProgram, Action> useBindingsFactory = null)
		{
			var shader = new AssetShaderProgram(this, passName, location, shaderDefinitionsProvider, useBindingsFactory);
			RegisterShader(shader);
			return shader;
		}

		public void RegisterShader(IShaderProgram shader)
		{
			if(shaders.Add(shader))
			{
				if(isLoaded) shader.Compile();
			}
		}

		public void UnregisterShader(IShaderProgram shader)
		{
			if(shaders.Remove(shader))
			{
				shader.Dispose();
			}
		}

		internal bool TryCompileShaderStage(EnumShaderType type, string code, out int handle, out string error)
		{
			handle = GL.CreateShader((ShaderType)type);
			GL.ShaderSource(handle, code);
			GL.CompileShader(handle);
			GL.GetShader(handle, ShaderParameter.CompileStatus, out var status);
			if(status != 1)
			{
				error = GL.GetShaderInfoLog(handle);
				GL.DeleteShader(handle);
				handle = 0;
				return false;
			}

			error = null;
			return true;
		}

		internal void DeleteShaderStage(int handle)
		{
			GL.DeleteShader(handle);
		}

		internal bool TryCreateShaderProgram(Action<int> bindStagesCallback, out int handle, out string error)
		{
			handle = GL.CreateProgram();
			bindStagesCallback(handle);
			GL.LinkProgram(handle);
			GL.GetProgram(handle, GetProgramParameterName.LinkStatus, out var outval);
			if(outval != 1)
			{
				error = GL.GetProgramInfoLog(handle);
				GL.DeleteProgram(handle);
				handle = 0;
				return false;
			}

			error = null;
			return true;
		}

		internal void DeleteShaderProgram(int handle)
		{
			GL.DeleteProgram(handle);
		}

		internal void AttachStageToProgram(int programHandle, int stageHandle)
		{
			GL.AttachShader(programHandle, stageHandle);
		}

		internal void SetActiveShader(int handle)
		{
			GL.UseProgram(handle);
		}

		internal void UnsetActiveShader()
		{
			GL.UseProgram(0);
		}

		internal void BindTexture(EnumTextureTarget target, int textureId, int textureNumber, int sampler, bool clampTexturesToEdge)
		{
			GL.ActiveTexture(TextureUnit.Texture0 + textureNumber);
			var texTarget = TextureTarget.Texture2D;
			switch(target)
			{
				case EnumTextureTarget.Texture1D: texTarget = TextureTarget.Texture1D; break;
				case EnumTextureTarget.Texture2D: texTarget = TextureTarget.Texture2D; break;
				case EnumTextureTarget.Texture3D: texTarget = TextureTarget.Texture3D; break;
				case EnumTextureTarget.TextureRectangle: texTarget = TextureTarget.TextureRectangle; break;
				case EnumTextureTarget.TextureCubeMap: texTarget = TextureTarget.TextureCubeMap; break;
				case EnumTextureTarget.Texture1DArray: texTarget = TextureTarget.Texture1DArray; break;
				case EnumTextureTarget.Texture2DArray: texTarget = TextureTarget.Texture2DArray; break;
				case EnumTextureTarget.TextureBuffer: texTarget = TextureTarget.TextureBuffer; break;
				case EnumTextureTarget.TextureCubeMapArray: texTarget = TextureTarget.TextureCubeMapArray; break;
				case EnumTextureTarget.Texture2DMultisample: texTarget = TextureTarget.Texture2DMultisample; break;
				case EnumTextureTarget.Texture2DMultisampleArray: texTarget = TextureTarget.Texture2DMultisampleArray; break;
			}
			GL.BindTexture(texTarget, textureId);
			if(sampler != 0)
			{
				GL.BindSampler(textureNumber, sampler);
			}
			if(clampTexturesToEdge)
			{
				GL.TexParameter(texTarget, TextureParameterName.TextureWrapT, Convert.ToInt32(TextureWrapMode.ClampToEdge));
			}
		}

		internal string GetPlatformShaderVersion(string version)
		{
			if(RuntimeEnv.OS == OS.Mac)
			{
				return "330";
			}
			return version;
		}

		internal void EnsureShaderVersionSupported(string version, string filename)
		{
			Vintagestory.Client.NoObf.Shader.EnsureVersionSupported(version, filename);
		}

		private bool ReloadShaders()
		{
			isLoaded = true;
			var dummyShader = new ShaderProgram();
			dummyShader.VertexShader = new Vintagestory.Client.NoObf.Shader();
			dummyShader.FragmentShader = new Vintagestory.Client.NoObf.Shader();
			api.Shader.RegisterMemoryShaderProgram("powerofmindcore:graphicshader", dummyShader);

			VertexShaderDefines = dummyShader.VertexShader.PrefixCode;
			FragmentShaderDefines = dummyShader.FragmentShader.PrefixCode;

			foreach(var shader in shaders)
			{
				shader.Dispose();
			}

			bool allCompiled = true;
			foreach(var listener in reloadShaderListeners)
			{
				allCompiled &= listener();
			}

			foreach(var shader in shaders)
			{
				allCompiled &= shader.Compile();
			}

			ShaderPreprocessor.ClearCache();
			return allCompiled;
		}
	}
}