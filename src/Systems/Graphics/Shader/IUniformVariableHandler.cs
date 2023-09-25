namespace PowerOfMind.Graphics.Shader
{
	internal interface IUniformVariableHandler
	{
		unsafe void SetValue(in UniformPropertyHandle handle, void* ptr, int count);
	}
}