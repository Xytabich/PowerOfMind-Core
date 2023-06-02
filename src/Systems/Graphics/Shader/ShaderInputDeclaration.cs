namespace PowerOfMind.Graphics.Shader
{
	public readonly struct ShaderInputDeclaration
	{
		public readonly ShaderVertexAttribute[] Attributes;

		public ref readonly ShaderVertexAttribute this[int index] { get { return ref Attributes[index]; } }

		public ShaderInputDeclaration(ShaderVertexAttribute[] attributes)
		{
			Attributes = attributes;
		}
	}
}