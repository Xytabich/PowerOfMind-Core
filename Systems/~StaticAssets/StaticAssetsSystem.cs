using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;

namespace PowerOfMind.Systems.StaticAssets
{
	public partial class StaticAssetsSystem : ModSystem
	{
		private const string CHANNEL_NAME = "powerofmind:staticassets";

		private static MethodInfo AllAssetsLoadedAndSpawnChunkReceived = typeof(ClientMain)
			.GetMethod("AllAssetsLoadedAndSpawnChunkReceived", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

		internal int loadingCounter = 0;
		internal bool staticAssetsLoaded = false;

		private Dictionary<string, IStaticAsset> staticAssets = null;
		private ICoreAPI api = null;

		private IServerNetworkChannel netChannel = null;
		private AssetsPacket buildedPacket = null;
		private object assetsLocker = new object();

		private bool dataReceived = false;

		public override double ExecuteOrder()
		{
			return -1;
		}

		public override void Start(ICoreAPI api)
		{
			this.api = api;
			base.Start(api);

			loadingCounter = 0;
			staticAssetsLoaded = false;
			dataReceived = false;
			buildedPacket = null;

			api.Network.RegisterChannel(CHANNEL_NAME)
				.RegisterMessageType<AssetsPacket>();
		}

		public override void StartServerSide(ICoreServerAPI api)
		{
			base.StartServerSide(api);
			netChannel = api.Network.GetChannel(CHANNEL_NAME);
		}

		public override void StartClientSide(ICoreClientAPI api)
		{
			base.StartClientSide(api);
			api.Network.GetChannel(CHANNEL_NAME).SetMessageHandler<AssetsPacket>(OnAssetsPacketReceived);
		}

		public override void Dispose()
		{
			base.Dispose();
			staticAssets = null;
			buildedPacket = null;
		}

		/// <summary>
		/// Adds an asset to the send list on the client.
		/// Should only be called on the server, before the first player joins.
		/// </summary>
		public void AddStaticAsset<T>(string key, T asset)
		{
			if(string.IsNullOrWhiteSpace(key))
			{
				throw new Exception("Key for static asset cannot be empty");
			}

			lock(assetsLocker)
			{
				if(buildedPacket != null)
				{
					api.Logger.Warning("Network packet already generated, static asset '{0}' will not be added to the packet.", key);
					return;
				}

				if(staticAssets == null) staticAssets = new Dictionary<string, IStaticAsset>();
				else if(staticAssets.ContainsKey(key))
				{
					api.Logger.Warning("The static asset with key '{0}' is already in the list.", key);
					return;
				}
				staticAssets.Add(key, new ServerStaticAssetContainer<T>(asset));
			}
		}

		public override void AssetsLoaded(ICoreAPI api)
		{
			base.AssetsLoaded(api);
		}

		/// <summary>
		/// More info in <see cref="StaticAssetReceiverDelegate{T}"/>
		/// Adds the receiver of the asset to the waiting list.
		/// Should only be called on the client, before the player connects to the server.
		/// </summary>
		/// <returns><see langword="true"/> if receiver added to the waiting list, <see langword="false"/> if receiver has not been added</returns>
		public bool AddStaticAssetReceiver<T>(string key, StaticAssetReceiverDelegate<T> onPacketReceived)
		{
			if(string.IsNullOrWhiteSpace(key))
			{
				throw new Exception("Key for static asset cannot be empty");
			}
			if(onPacketReceived == null)
			{
				throw new ArgumentNullException(nameof(onPacketReceived));
			}

			lock(assetsLocker)
			{
				if(dataReceived)
				{
					api.Logger.Warning("Network packet already received, static asset '{0}' could not be received.", key);
					return false;
				}

				if(staticAssets == null) staticAssets = new Dictionary<string, IStaticAsset>();
				else if(staticAssets.ContainsKey(key))
				{
					api.Logger.Warning("The static asset with key '{0}' is already in the list.", key);
					return false;
				}
				staticAssets.Add(key, new ClientStaticAssetReceiver<T>(onPacketReceived));
			}
			return true;
		}

		internal void SendAssetsToPlayer(IServerPlayer byPlayer)
		{
			if(buildedPacket == null)
			{
				Dictionary<string, IStaticAsset> staticAssets;
				lock(assetsLocker)
				{
					buildedPacket = new AssetsPacket() {
						DataByKey = new Dictionary<string, byte[]>()
					};
					staticAssets = this.staticAssets;
					this.staticAssets = null;
				}
				if(staticAssets != null)
				{
					foreach(var pair in staticAssets)
					{
						try
						{
							buildedPacket.DataByKey.Add(pair.Key, pair.Value.Serialize());
						}
						catch(Exception e)
						{
							api.Logger.Warning("An error occurred while serializing static asset '{0}':\n{1}", pair.Key, e);
						}
					}
				}
			}
			netChannel.SendPacket(buildedPacket, byPlayer);
		}

		private void OnAssetsPacketReceived(AssetsPacket packet)
		{
			Dictionary<string, IStaticAsset> staticAssets;
			lock(assetsLocker)
			{
				dataReceived = true;
				staticAssets = this.staticAssets;
				this.staticAssets = null;
			}
			if(staticAssets != null)
			{
				if(packet?.DataByKey != null)
				{
					foreach(var pair in staticAssets)
					{
						if(!packet.DataByKey.TryGetValue(pair.Key, out var data))
						{
							data = null;
						}

						try
						{
							pair.Value.OnReceived(data, api.Logger, pair.Key);
						}
						catch(Exception e)
						{
							api.Logger.Warning("An error occurred while receiving static asset '{0}':\n{1}", pair.Key, e);
						}
					}
				}
				else
				{
					foreach(var pair in staticAssets)
					{
						try
						{
							pair.Value.OnReceived(null, api.Logger, pair.Key);
						}
						catch(Exception e)
						{
							api.Logger.Warning("An error occurred while receiving static asset '{0}':\n{1}", pair.Key, e);
						}
					}
				}
			}
			staticAssetsLoaded = true;
			api.Logger.VerboseDebug("World configs received and block tesselation complete");
			((ClientMain)api.World).EnqueueGameLaunchTask(() => {
				AllAssetsLoadedAndSpawnChunkReceived.Invoke(api.World, new object[0]);
			}, "pomStaticAssetsLoaded");
		}
	}
}