using PowerOfMind.Graphics.Shader;
using Vintagestory.API.MathTools;

namespace PowerOfMind.Graphics
{
	public static class ShaderUniformExtensions
	{
		public static unsafe void SetValue<T>(this in UniformPropertyHandle handle, T value) where T : unmanaged
		{
			handle.SetValue(&value, 1);
		}

		public static unsafe void SetValue<T>(this in UniformPropertyHandle handle, T[] values) where T : unmanaged
		{
			fixed(T* ptr = values)
			{
				handle.SetValue(ptr, 1);
			}
		}

		public static unsafe void SetValues<T>(this in UniformPropertyHandle handle, T value, int count) where T : unmanaged
		{
			handle.SetValue(&value, count);
		}

		public static unsafe void SetValues<T>(this in UniformPropertyHandle handle, T[] values, int count) where T : unmanaged
		{
			fixed(T* ptr = values)
			{
				handle.SetValue(ptr, count);
			}
		}

		public static unsafe void SetValue(this in UniformPropertyHandle handle, Vec2f value)
		{
			float* values = stackalloc float[2];
			values[0] = value.X;
			values[1] = value.Y;
			handle.SetValue(values, 1);
		}

		public static unsafe void SetValue(this in UniformPropertyHandle handle, Vec3f value)
		{
			float* values = stackalloc float[3];
			values[0] = value.X;
			values[1] = value.Y;
			values[2] = value.Z;
			handle.SetValue(values, 1);
		}

		public static unsafe void SetValue(this in UniformPropertyHandle handle, Vec4f value)
		{
			float* values = stackalloc float[4];
			values[0] = value.X;
			values[1] = value.Y;
			values[2] = value.Z;
			values[3] = value.W;
			handle.SetValue(values, 1);
		}
	}
}