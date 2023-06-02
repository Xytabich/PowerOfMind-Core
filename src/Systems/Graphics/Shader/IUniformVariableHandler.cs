namespace PowerOfMind.Graphics.Shader
{
	internal interface IUniformVariableHandler
	{
		unsafe void SetValue<T>(int location, T* ptr, int count) where T : unmanaged;
	}
}