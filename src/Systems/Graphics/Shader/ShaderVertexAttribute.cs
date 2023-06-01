using PowerOfMind.Graphics;

namespace PowerOfMind.Systems.Graphics.Shader
{
	public readonly struct ShaderVertexAttribute
	{
		public readonly int Location;
		public readonly string Name;
		public readonly string Alias;
		public readonly EnumVertexComponentType Type;
		public readonly int Size;

		public ShaderVertexAttribute(int location, string name, string alias, EnumVertexComponentType type, int size)
		{
			Location = location;
			Name = name;
			Alias = alias;
			Type = type;
			Size = size;
		}
	}
}