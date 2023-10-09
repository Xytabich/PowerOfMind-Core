using PowerOfMind.Graphics.Shader;
using PowerOfMind.Systems.Graphics;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PowerOfMind.Graphics
{
	/// <summary>
	/// Provides access to internal graphics functionality
	/// </summary>
	public interface IGraphicsSystemInternal
	{
		ICoreClientAPI Api { get; }
		ILogger Logger { get; }
		ShaderPreprocessor ShaderPreprocessor { get; }
		string VertexShaderDefines { get; }
		string FragmentShaderDefines { get; }

		bool TryCompileShaderStage(EnumShaderType type, string code, out int handle, out string error);

		void DeleteShaderStage(int handle);

		/// <summary>
		/// Registers a shader program and links stages
		/// </summary>
		/// <param name="bindStagesCallback">A callback that binds stages. The program handle is passed as a parameter</param>
		/// <param name="enableCaching">Set to true if the bytecode should be generated for the cache later using <see cref="SaveShaderProgramToCache"/></param>
		/// <returns><see langword="true"/> if successful</returns>
		bool TryCreateShaderProgram(Action<int> bindStagesCallback, bool enableCaching, out int handle, out string error);

		void DeleteShaderProgram(int handle);

		/// <summary>
		/// Binds the stage created by <see cref="TryCompileShaderStage"/> to the program, must be called in the callback from <see cref="TryCreateShaderProgram"/>
		/// </summary>
		void AttachStageToProgram(int programHandle, int stageHandle);

		void SetActiveShader(int handle);

		void UnsetActiveShader();

		bool HasCachedShaderProgram(AssetLocation shaderKey);

		/// <summary>
		/// Loads the cached program created via <see cref="SaveShaderProgramToCache"/> if the shader properties match
		/// </summary>
		/// <returns><see langword="true"/> if successful</returns>
		bool TryLoadCachedShaderProgram<TEnumerable>(AssetLocation shaderKey, TEnumerable stagesHash, out int handle, out string error)
			where TEnumerable : IEnumerable<ShaderHashInfo>;

		/// <summary>
		/// The enableCaching parameter in <see cref="TryCreateShaderProgram"/> must be set to <see langword="true"/> for shader caching.
		/// </summary>
		/// <param name="shaderKey">Unique shader key, such as asset path</param>
		/// <param name="handle">Shader program handle</param>
		/// <param name="stagesHash">Code hash and its size for each stage</param>
		void SaveShaderProgramToCache<TEnumerable>(AssetLocation shaderKey, int handle, TEnumerable stagesHash)
			where TEnumerable : IEnumerable<ShaderHashInfo>;

		/// <summary>
		/// Returns a list of shader vertex attributes
		/// </summary>
		/// <param name="handle">Shader program handle</param>
		/// <param name="aliasByName">Optional, association of parameter name with alias</param>
		ShaderVertexAttribute[] GetShaderAttributes(int handle, IReadOnlyDictionary<string, string> aliasByName = null);

		/// <summary>
		/// Returns a list of shader uniforms
		/// </summary>
		/// <param name="handle">Shader program handle</param>
		/// <param name="aliasByName">Optional, association of parameter name with alias</param>
		/// <param name="initTextureSlots">Automatically sets a unique <see cref="UniformPropertyHandle.ReferenceSlot"/> number for samplers</param>
		UniformPropertyHandle[] GetShaderUniforms(int handle, IReadOnlyDictionary<string, string> aliasByName = null, bool initTextureSlots = true);

		void BindTexture(EnumTextureTarget target, int textureId, int textureNumber, ITextureSampler sampler, bool clampTexturesToEdge);

		string GetPlatformShaderVersion(string version);

		/// <exception cref="NotSupportedException"></exception>
		void EnsureShaderVersionSupported(string version, string filename);
	}
}