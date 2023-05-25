using System.Runtime.CompilerServices;

namespace PowerOfMind.Utils
{
	public struct Vector3
	{
		public static readonly Vector3 Zero = default;

		public float X, Y, Z;

		public bool IsEmpty => X == 0 & Y == 0 & Z == 0;
	}
}