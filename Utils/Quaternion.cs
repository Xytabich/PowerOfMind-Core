namespace PowerOfMind.Utils
{
	public struct Quaternion
	{
		public static readonly Quaternion Identity = new Quaternion(0, 0, 0, 1);

		public float X, Y, Z, W;

		public bool IsIdentity => X == 0 & Y == 0 & Z == 0 & W == 1;

		public Quaternion(float x, float y, float z, float w)
		{
			X = x;
			Y = y;
			Z = z;
			W = w;
		}
	}
}