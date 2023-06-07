namespace PowerOfMind.Graphics
{
	public readonly struct UniformProperty
	{
		public readonly string Name;
		public readonly string Alias;
		public readonly uint Offset;
		public readonly EnumShaderPrimitiveType Type;
		public readonly EnumUniformStructType StructType;
		public readonly int Size;

		public UniformProperty(string name, string alias, uint offset, EnumShaderPrimitiveType type, EnumUniformStructType structType, int size)
		{
			Name = name;
			Alias = alias;
			Offset = offset;
			Type = type;
			StructType = structType;
			Size = size;
		}
	}
}