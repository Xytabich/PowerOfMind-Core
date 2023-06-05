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

		public static void BindTexture2D(this IExtendedShaderProgram shader, string samplerName, int textureId)
		{
			shader.BindTexture(shader.FindUniformIndex(samplerName), EnumTextureTarget.Texture2D, textureId);
		}

		public static void BindTexture2D(this IExtendedShaderProgram shader, string samplerName, EnumTextureTarget target, int textureId)
		{
			shader.BindTexture(shader.FindUniformIndex(samplerName), target, textureId);
		}

		public static void BindTexture2D(this IExtendedShaderProgram shader, string samplerName, EnumTextureTarget target, int textureId, int textureNumber)
		{
			shader.BindTexture(shader.FindUniformIndex(samplerName), target, textureId, textureNumber);
		}

		/// <summary>
		/// Creates a declaration that matches the shader, removing unsuitable attributes from the input declaration.
		/// It will also assign such parameters as <see cref="VertexAttribute.Location"/> and <see cref="VertexAttribute.IntegerTarget"/>
		/// </summary>
		public static VertexDeclaration MapDeclaration(this IExtendedShaderProgram shader, VertexDeclaration declaration)
		{
			return default;
		}
	}
}