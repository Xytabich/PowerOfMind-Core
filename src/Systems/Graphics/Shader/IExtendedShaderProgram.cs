using PowerOfMind.Systems.Graphics;
using Vintagestory.API.Client;

namespace PowerOfMind.Graphics.Shader
{
	public interface IExtendedShaderProgram : IShaderProgram
	{
		ShaderInputDeclaration Inputs { get; }
		ShaderUniformDeclaration Uniforms { get; }

		int FindUniformIndex(string name);

		int FindUniformIndexByAlias(string alias);

		void SetSampler(int textureNumber, ITextureSampler sampler);

		void BindTexture(int uniformIndex, EnumTextureTarget target, int textureId, int textureNumber);
	}
}