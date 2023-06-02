using Vintagestory.API.Client;

namespace PowerOfMind.Graphics.Shader
{
	public interface IExtendedShaderProgram : IShaderProgram
	{
		ShaderInputDeclaration Inputs { get; }
		ShaderUniformDeclaration Uniforms { get; }

		int FindUniformLocation(string name);

		int FindUniformLocationByAlias(string alias);

		void BindTexture(int location, EnumTextureTarget target, int textureId);

		void BindTexture(int location, EnumTextureTarget target, int textureId, int textureNumber);
	}
}