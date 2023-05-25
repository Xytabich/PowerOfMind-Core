using ProtoBuf;
using System.Collections.Generic;

namespace PowerOfMind.Systems.StaticAssets
{
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class AssetsPacket
	{
		public Dictionary<string, byte[]> DataByKey;
	}
}