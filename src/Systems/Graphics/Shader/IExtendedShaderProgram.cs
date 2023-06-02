using Vintagestory.API.Client;

namespace PowerOfMind.Graphics.Shader
{
	public interface IExtendedShaderProgram : IShaderProgram
	{
		ShaderInputDeclaration Inputs { get; }
		ShaderUniformDeclaration Uniforms { get; }

		int FindUniformIndex(string name);

		int FindUniformIndexByAlias(string alias);

		void BindTexture(int uniformIndex, EnumTextureTarget target, int textureId);

		void BindTexture(int uniformIndex, EnumTextureTarget target, int textureId, int textureNumber);
	}
}