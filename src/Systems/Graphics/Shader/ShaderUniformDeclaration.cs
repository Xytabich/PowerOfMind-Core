namespace PowerOfMind.Systems.Graphics.Shader
{
	public readonly struct ShaderUniformDeclaration
	{
		public readonly UniformPropertyHandle[] Properties;

		public ShaderUniformDeclaration(UniformPropertyHandle[] properties)
		{
			Properties = properties;
		}
	}
}