using OpenTK.Graphics.OpenGL;
using PowerOfMind.Graphics.Shader;
using PowerOfMind.ShaderCache;
using PowerOfMind.Systems.Graphics;
using PowerOfMind.Systems.Graphics.Shader;
using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

namespace PowerOfMind.Graphics
{
	using TextureWrapMode = OpenTK.Graphics.OpenGL.TextureWrapMode;

	public partial class GraphicsSystem : ModSystem, IGraphicsSystemInternal
	{
		public bool IsGraphicsReady => isLoaded;

		public event System.Action OnBeforeShadersReload { add { beforeReloadShaderListeners.Add(value); } remove { beforeReloadShaderListeners.Remove(value); } }
		public event System.Func<bool> OnAfterShadersReload { add { afterReloadShaderListeners.Add(value); } remove { afterReloadShaderListeners.Remove(value); } }

		ShaderPreprocessor IGraphicsSystemInternal.ShaderPreprocessor => shaderPreprocessor;
		string IGraphicsSystemInternal.VertexShaderDefines => vertexShaderDefines;
		string IGraphicsSystemInternal.FragmentShaderDefines => fragmentShaderDefines;
		ILogger IGraphicsSystemInternal.Logger => api.Logger;
		ShaderCacheSystem IGraphicsSystemInternal.ShaderCache => shaderCache;

		ICoreClientAPI IGraphicsSystemInternal.Api => api;

		private ShaderCacheSystem shaderCache;

		private ICoreClientAPI api;
		private UniformVariableHandlers uniformHandlers;

		private bool isLoaded = false;

		private List<System.Action> beforeReloadShaderListeners = new List<System.Action>();
		private List<System.Func<bool>> afterReloadShaderListeners = new List<System.Func<bool>>();
		private HashSet<IExtendedShaderProgram> shaders = new HashSet<IExtendedShaderProgram>();
		private Dictionary<string, IExtendedShaderProgram> nameToShader = new Dictionary<string, IExtendedShaderProgram>();

		private ShaderPreprocessor shaderPreprocessor;
		private string vertexShaderDefines;
		private string fragmentShaderDefines;

		public override void StartClientSide(ICoreClientAPI api)
		{
			this.api = api;
			shaderCache = api.ModLoader.GetModSystem<ShaderCacheSystem>();
			uniformHandlers = new UniformVariableHandlers();
			shaderPreprocessor = new ShaderPreprocessor(this);
			api.Event.ReloadShader += ReloadShaders;
		}

		public override bool ShouldLoad(EnumAppSide forSide)
		{
			return forSide == EnumAppSide.Client;
		}

		public override void Dispose()
		{
			base.Dispose();
			foreach(var shader in shaders)
			{
				shader.Dispose();
			}
			shaders.Clear();
			beforeReloadShaderListeners.Clear();
			afterReloadShaderListeners.Clear();
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

		public void RegisterShader(IExtendedShaderProgram shader)
		{
			if(shaders.Add(shader))
			{
				if(!string.IsNullOrEmpty(shader.PassName))
				{
					if(nameToShader.TryGetValue(shader.PassName, out var s))
					{
						shaders.Remove(s);
						s.Dispose();
					}
					nameToShader[shader.PassName] = shader;
				}
				if(isLoaded)
				{
					shader.Compile();
				}
			}
		}

		public void UnregisterShader(IExtendedShaderProgram shader)
		{
			if(shaders.Remove(shader))
			{
				if(!string.IsNullOrEmpty(shader.PassName))
				{
					nameToShader.Remove(shader.PassName);
				}
				shader.Dispose();
			}
		}

		public IExtendedShaderProgram GetShader(string passName)
		{
			if(nameToShader.TryGetValue(passName, out var shader))
			{
				return shader;
			}
			return null;
		}

		public IExtendedShaderProgram ExtendStandardShader(EnumShaderProgram shader,
			IReadOnlyDictionary<string, string> attribNameToAlias = null,
			IReadOnlyDictionary<string, string> uniformNameToAlias = null)
		{
			if(ShaderRegistry.getProgram(shader)?.Disposed ?? true) throw new InvalidOperationException("Shader must be initialized");
			var extShader = new StandardShaderProxy(shader, this, attribNameToAlias, uniformNameToAlias);
			extShader.Compile();
			return extShader;
		}

		bool IGraphicsSystemInternal.TryCompileShaderStage(EnumShaderType type, string code, out int handle, out string error)
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

		void IGraphicsSystemInternal.DeleteShaderStage(int handle)
		{
			GL.DeleteShader(handle);
		}

		bool IGraphicsSystemInternal.TryCreateShaderProgram(Action<int> bindStagesCallback, bool enableCaching, out int handle, out string error)
		{
			handle = GL.CreateProgram();
			bindStagesCallback(handle);

			if(enableCaching && shaderCache.HasProgramBinaryArb())
			{
				GL.ProgramParameter(handle, ProgramParameterName.ProgramBinaryRetrievableHint, (int)All.True);
			}
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

		void IGraphicsSystemInternal.DeleteShaderProgram(int handle)
		{
			GL.DeleteProgram(handle);
		}

		void IGraphicsSystemInternal.AttachStageToProgram(int programHandle, int stageHandle)
		{
			GL.AttachShader(programHandle, stageHandle);
		}

		void IGraphicsSystemInternal.SetActiveShader(int handle)
		{
			GL.UseProgram(handle);
		}

		void IGraphicsSystemInternal.UnsetActiveShader()
		{
			GL.UseProgram(0);
		}

		void IGraphicsSystemInternal.BindTexture(EnumTextureTarget target, int textureId, int textureNumber, ITextureSampler sampler, bool clampTexturesToEdge)
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

			sampler?.Bind(textureNumber);

			if(clampTexturesToEdge)
			{
				GL.TexParameter(texTarget, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
			}
		}

		string IGraphicsSystemInternal.GetPlatformShaderVersion(string version)
		{
			if(RuntimeEnv.OS == OS.Mac)
			{
				return "330";
			}
			return version;
		}

		void IGraphicsSystemInternal.EnsureShaderVersionSupported(string version, string filename)
		{
			Vintagestory.Client.NoObf.Shader.EnsureVersionSupported(version, filename);
		}

		private bool ReloadShaders()
		{
			isLoaded = true;

			var dummyShader = new DummyShader();
			dummyShader.VertexShader = new Vintagestory.Client.NoObf.Shader();
			dummyShader.FragmentShader = new Vintagestory.Client.NoObf.Shader();
			api.Shader.RegisterMemoryShaderProgram("powerofmindcore:graphicdummyshader", dummyShader);

			vertexShaderDefines = dummyShader.VertexShader.PrefixCode;
			fragmentShaderDefines = dummyShader.FragmentShader.PrefixCode;

			for(int i = beforeReloadShaderListeners.Count - 1; i >= 0; i--)
			{
				beforeReloadShaderListeners[i].Invoke();
			}

			foreach(var shader in shaders)
			{
				shader.Dispose();
			}

			bool allCompiled = true;
			foreach(var shader in shaders)
			{
				allCompiled &= shader.Compile();
			}

			for(int i = afterReloadShaderListeners.Count - 1; i >= 0; i--)
			{
				allCompiled &= afterReloadShaderListeners[i].Invoke();
			}

			shaderPreprocessor.ClearCache();
			shaderCache.SaveShadersCache();
			return allCompiled;
		}

		private class DummyShader : ShaderProgram
		{
			private static readonly FieldInfo disposedField = typeof(ShaderProgramBase).GetField("disposed", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

			public DummyShader()
			{
				disposedField.SetValue(this, true);
			}

			public override bool Compile()
			{
				disposedField.SetValue(this, true);
				return true;
			}
		}
	}
}