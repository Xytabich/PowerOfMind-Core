namespace PowerOfMind.Systems.Graphics.Shader
{
	public readonly struct ShaderInputDeclaration
	{
		public readonly ShaderVertexAttribute[] Properties;

		public ShaderInputDeclaration(ShaderVertexAttribute[] properties)
		{
			Properties = properties;
		}
	}
}