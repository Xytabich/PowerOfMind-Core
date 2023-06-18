namespace PowerOfMind.Graphics
{
	public readonly struct UniformProperty
	{
		public readonly string Name;
		public readonly string Alias;
		public readonly uint Offset;
		public readonly EnumShaderPrimitiveType Type;
		public readonly int Size;

		public UniformProperty(string name, string alias, uint offset, EnumShaderPrimitiveType type, int size)
		{
			Name = name;
			Alias = alias;
			Offset = offset;
			Type = type;
			Size = size;
		}
	}
}