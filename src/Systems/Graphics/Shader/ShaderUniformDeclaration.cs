namespace PowerOfMind.Graphics.Shader
{
	public readonly struct ShaderUniformDeclaration
	{
		private static readonly UniformPropertyHandle DummyHandle = new UniformPropertyHandle(-1, null, null, default, default, default, default, new DummyHandler());

		public readonly UniformPropertyHandle[] Properties;

		public ref readonly UniformPropertyHandle this[int index] { get { return ref GetPropertyOrDummy(index); } }

		public ShaderUniformDeclaration(UniformPropertyHandle[] properties)
		{
			Properties = properties;
		}

		public ref readonly UniformPropertyHandle GetPropertyOrDummy(int index)
		{
			if(index < 0 || index >= Properties.Length) return ref DummyHandle;
			return ref Properties[index];
		}

		private class DummyHandler : IUniformVariableHandler
		{
			unsafe void IUniformVariableHandler.SetValue<T>(in UniformPropertyHandle handle, T* ptr, int count) { }
		}
	}
}