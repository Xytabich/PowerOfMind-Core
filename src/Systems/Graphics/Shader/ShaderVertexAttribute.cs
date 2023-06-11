namespace PowerOfMind.Graphics.Shader
{
	public readonly struct ShaderVertexAttribute
	{
		public readonly string Name;
		public readonly string Alias;
		public readonly int Location;
		public readonly EnumShaderPrimitiveType Type;
		public readonly int Size;

		public ShaderVertexAttribute(int location, string name, string alias, EnumShaderPrimitiveType type, int size)
		{
			Location = location;
			Name = name;
			Alias = alias?.ToUpperInvariant();
			Type = type;
			Size = size;
		}

		public override string ToString()
		{
			return string.Format("{0}: {1} {2}({3})", Location, string.Format(string.IsNullOrEmpty(Alias) ? "{0}" : "{0}[{1}]", Name, Alias), Type, Size);
		}
	}
}