using Vintagestory.API.Common;

namespace PowerOfMind.Systems.StaticAssets
{
	/// <summary>
	/// Client-side asset recipient.
	/// At the time of receiving an asset, the client is still at the initialization stage, and not all assets are loaded or initialized.
	/// Therefore, it is better to perform actions with an asset during the <see cref="ModSystem.AssetsLoaded(ICoreAPI)"/> event.
	/// </summary>
	/// <param name="assetReceived">Indicates whether the asset was in the list of received assets</param>
	/// <param name="asset">The received asset, or the default value if the asset was not received</param>
	public delegate void StaticAssetReceiverDelegate<T>(bool assetReceived, T asset);
}