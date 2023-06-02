using System.Collections.Generic;

namespace PowerOfMind.Graphics.Shader
{
	public readonly struct ShaderUniformDeclaration
	{
		public readonly UniformPropertyHandle[] Properties;
		public readonly IReadOnlyDictionary<int, int> LocationToTextureUnit;

		public ref readonly UniformPropertyHandle this[int location] { get { return ref Properties[location]; } }

		public ShaderUniformDeclaration(UniformPropertyHandle[] properties, IReadOnlyDictionary<int, int> locationToTextureUnit)
		{
			Properties = properties;
			LocationToTextureUnit = locationToTextureUnit;
		}
	}
}