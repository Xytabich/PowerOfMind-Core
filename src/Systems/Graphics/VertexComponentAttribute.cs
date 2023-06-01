using System;

namespace PowerOfMind.Graphics
{
	/// <summary>
	/// Utility attribute to describe field in structure
	/// </summary>
	public class VertexComponentAttribute : Attribute
	{
		public int Location = -1;
		public string Alias = null;
		public bool Normalized = false;
		public bool AsInteger = false;
		public EnumVertexComponentType Type = EnumVertexComponentType.Unknown;
	}
}