using GenMathematics;
using GenMathematics.Matrices;
using GenMathematics.Vectors;

namespace PowerOfMind.Utils
{
	public static class MathUtils
	{
		public static float4x4 Identity4x4()
		{
			float4x4 result;
			GenMath.Identity(out result);
			return result;
		}

		public static float4x4 Translation4x4(float3 translation)
		{
			float4x4 result;
			GenMath.Identity(out result);
			GenMath.SetTranslation(translation, ref result);
			return result;
		}

		public static float4x4 TRS4x4(float3 translation, float4 rotation, float3 scale)
		{
			float4x4 result;
			GenMath.Identity(out result);
			QuaternionMath.SetRotationScale(rotation, scale, ref result);
			GenMath.SetTranslation(translation, ref result);
			return result;
		}

		public static float4 RotationQ(float angle, int axis)
		{
			var a = float3.Zero;
			a[axis] = 1;
			return QuaternionMath.FromAxisAngle(a, angle);
		}

		public static float4x4 Rotation4x4(float angle, int axis)
		{
			var a = float3.Zero;
			a[axis] = 1;
			float4x4 result;
			GenMath.Identity(out result);
			AxisAngleMath.SetRotation(a, angle, ref result);
			return result;
		}
	}
}