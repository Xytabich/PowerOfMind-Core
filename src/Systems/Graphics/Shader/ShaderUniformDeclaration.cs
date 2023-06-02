using System.Collections.Generic;

namespace PowerOfMind.Graphics.Shader
{
	public readonly struct ShaderUniformDeclaration
	{
		private static readonly UniformPropertyHandle DummyHandle = new UniformPropertyHandle(-1, null, null, default, default, default, new DummyHandler());

		public readonly UniformPropertyHandle[] Properties;
		public readonly IReadOnlyDictionary<int, int> IndexToTextureUnit;

		public ref readonly UniformPropertyHandle this[int index] { get { return ref GetPropertyOrDummy(index); } }

		public ShaderUniformDeclaration(UniformPropertyHandle[] properties, IReadOnlyDictionary<int, int> indexToTextureUnit)
		{
			Properties = properties;
			IndexToTextureUnit = indexToTextureUnit;
		}

		public ref readonly UniformPropertyHandle GetPropertyOrDummy(int index)
		{
			if(index < 0 || index >= Properties.Length) return ref DummyHandle;
			return ref Properties[index];
		}

		private class DummyHandler : IUniformVariableHandler
		{
			unsafe void IUniformVariableHandler.SetValue<T>(int location, T* ptr, int count) { }
		}
	}
}