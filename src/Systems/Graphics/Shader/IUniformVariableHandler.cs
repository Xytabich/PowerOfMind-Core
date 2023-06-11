namespace PowerOfMind.Graphics.Shader
{
	internal interface IUniformVariableHandler
	{
		unsafe void SetValue<T>(in UniformPropertyHandle handle, T* ptr, int count) where T : unmanaged;
	}
}