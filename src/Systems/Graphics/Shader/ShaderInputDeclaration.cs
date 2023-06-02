namespace PowerOfMind.Graphics.Shader
{
	public readonly struct ShaderInputDeclaration
	{
		public readonly ShaderVertexAttribute[] Attributes;

		public ref readonly ShaderVertexAttribute this[int location] { get { return ref Attributes[location]; } }

		public ShaderInputDeclaration(ShaderVertexAttribute[] attributes)
		{
			Attributes = attributes;
		}
	}
}