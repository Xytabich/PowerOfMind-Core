using System;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace PowerOfMind.Systems.StaticAssets
{
	public partial class StaticAssetsSystem
	{
		private class ClientStaticAssetReceiver<T> : IStaticAsset
		{
			private StaticAssetReceiverDelegate<T> onPacketReceived;

			public ClientStaticAssetReceiver(StaticAssetReceiverDelegate<T> onPacketReceived)
			{
				this.onPacketReceived = onPacketReceived;
			}

			void IStaticAsset.OnReceived(byte[] data, ILogger logger, string key)
			{
				if(data == null)
				{
					onPacketReceived(false, default);
				}
				else
				{
					T value = default;
					bool deserialized = false;
					try
					{
						value = SerializerUtil.Deserialize<T>(data);
						deserialized = true;
					}
					catch(Exception e)
					{
						logger.Warning("An error occurred while deserializing static asset '{0}':\n{1}", key, e);
					}
					if(deserialized)
					{
						onPacketReceived(true, value);
					}
					else
					{
						onPacketReceived(false, default);
					}
				}
			}

			byte[] IStaticAsset.Serialize()
			{
				throw new NotImplementedException();
			}
		}
	}
}