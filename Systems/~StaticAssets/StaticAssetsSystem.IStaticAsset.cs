using Vintagestory.API.Common;

namespace PowerOfMind.Systems.StaticAssets
{
	public partial class StaticAssetsSystem
	{
		private interface IStaticAsset
		{
			byte[] Serialize();

			void OnReceived(byte[] data, ILogger logger, string key);
		}
	}
}