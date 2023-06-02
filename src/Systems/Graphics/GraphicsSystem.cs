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
			uniformHandlers = new UniformVariableHandlers();
			ShaderPreprocessor = new ShaderPreprocessor(this);
			api.Event.ReloadShader += ReloadShaders;
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

		public IShaderProgram LoadAssetShader(string passName, AssetLocation location)
		{
			var shader = new AssetShaderProgram(this, passName, location);
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

		internal void BindTexture(int location, EnumTextureTarget target, int textureId, int textureNumber, int sampler, bool clampTexturesToEdge)
		{
			GL.Uniform1(location, textureNumber);
			GL.ActiveTexture((TextureUnit)(33984 + textureNumber));
			var texTarget = target == EnumTextureTarget.TextureCubeMap ? TextureTarget.TextureCubeMap : TextureTarget.Texture2D;
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