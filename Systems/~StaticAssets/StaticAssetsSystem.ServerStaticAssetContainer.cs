using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace PowerOfMind.Systems.StaticAssets
{
	public partial class StaticAssetsSystem
	{
		private class ServerStaticAssetContainer<T> : IStaticAsset
		{
			private T value;

			public ServerStaticAssetContainer(T value)
			{
				this.value = value;
			}

			byte[] IStaticAsset.Serialize()
			{
				return SerializerUtil.Serialize(value);
			}

			void IStaticAsset.OnReceived(byte[] data, ILogger logger, string key)
			{
				throw new System.NotImplementedException();
			}
		}
	}
}