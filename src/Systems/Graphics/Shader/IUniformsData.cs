namespace PowerOfMind.Graphics
{
	public interface IUniformsData
	{
		UniformsDeclaration GetDeclaration();
	}

	public readonly struct UniformsDeclaration
	{
		public readonly UniformProperty[] Properties;
	}

	public readonly struct UniformProperty
	{
		public readonly string Name;
		public readonly string Alias;
		public readonly uint Offset;
		public readonly EnumShaderPrimitiveType Type;
		public readonly EnumUniformStructType StructType;
		public readonly int Size;
	}
}