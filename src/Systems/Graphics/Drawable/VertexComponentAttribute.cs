using System;

namespace PowerOfMind.Graphics
{
	/// <summary>
	/// Utility attribute to describe field in structure
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class VertexComponentAttribute : Attribute
	{
		public int Location = -1;
		public string Alias = null;
		public bool Normalized = false;
		public bool AsInteger = false;
		public EnumShaderPrimitiveType Type = EnumShaderPrimitiveType.Unknown;

		public VertexComponentAttribute(int location)
		{
			this.Location = location;
		}

		public VertexComponentAttribute(string alias)
		{
			this.Alias = alias;
		}
	}
}