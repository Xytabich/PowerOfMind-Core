namespace PowerOfMind.Systems.Pipes
{
	public interface INetworkMember
	{
		void GetStats(int index, out NetworkMemberStats stats);
	}

	public interface INetworkProvider : INetworkMember
	{
		/// <summary>
		/// Reserves a certain amount of content, this amount should not be used until released.
		/// </summary>
		void ReserveContent(int index, uint value);

		void ReleaseContent(int index, uint value);

		void Take(int index, uint value);
	}

	public interface INetworkReceiver : INetworkMember
	{
		/// <summary>
		/// Reserves some capacity for content, this capacity should not be used until released.
		/// </summary>
		void ReserveCapacity(int index, uint value);

		void ReleaseCapacity(int index, uint value);

		void Push(int index, uint value, int itemId);
	}

	public struct NetworkMemberStats
	{
		/// <summary>
		/// Item id of content or 0 if no content
		/// </summary>
		public int Content;
		/// <summary>
		/// Amount of content in member
		/// </summary>
		public uint Amount;//TODO: Use Min(Amount,Throughput*(int)(dt*invViscosity)) when collecting data
		/// <summary>
		/// Max content amount
		/// </summary>
		public uint MaxAmount;
		/// <summary>
		/// Connection throughput.
		/// For example, cross-sectional area of a pipe.
		/// </summary>
		public uint Throughput;

		/// <summary>
		/// A mask defining which connection is an input
		/// </summary>
		public uint InputMask;
		/// <summary>
		/// A mask defining which connection is an output
		/// </summary>
		public uint OutputMask;
	}
}