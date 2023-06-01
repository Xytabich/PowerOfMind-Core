using Vintagestory.API.Client;

namespace PowerOfMind.Systems.Graphics.Shader
{
	public interface IExtendedShaderProgram : IShaderProgram
	{
		ShaderInputDeclaration[] Inputs { get; }
		ShaderUniformDeclaration[] Uniforms { get; }

		ref readonly UniformPropertyHandle FindUniform(string name);

		ref readonly UniformPropertyHandle FindUniformByAlias(string name);
	}
}