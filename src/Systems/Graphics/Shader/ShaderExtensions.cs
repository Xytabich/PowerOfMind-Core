using PowerOfMind.Graphics.Shader;

namespace PowerOfMind.Graphics
{
	public static class ShaderExtensions
	{
		public static void FindTextureBindings(this IExtendedShaderProgram shader, string uniformName, out int uniformIndex, out int textureNumber)
		{
			uniformIndex = shader.FindUniformIndex(uniformName);
			if(uniformIndex < 0 || !shader.Uniforms.IndexToTextureUnit.TryGetValue(uniformIndex, out textureNumber))
			{
				textureNumber = -1;
			}
		}

		public static void FindTextureBindingsByAlias(this IExtendedShaderProgram shader, string uniformAlias, out int uniformIndex, out int textureNumber)
		{
			uniformIndex = shader.FindUniformIndexByAlias(uniformAlias);
			if(uniformIndex < 0 || !shader.Uniforms.IndexToTextureUnit.TryGetValue(uniformIndex, out textureNumber))
			{
				textureNumber = -1;
			}
		}
	}
}