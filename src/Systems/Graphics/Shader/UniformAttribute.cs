using System;

namespace PowerOfMind.Graphics
{
	/// <summary>
	/// Utility attribute to describe field in structure
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class UniformAttribute : Attribute
	{
		public string Alias = null;
		public EnumShaderPrimitiveType Type = EnumShaderPrimitiveType.Unknown;

		public UniformAttribute()
		{
		}

		public UniformAttribute(string alias)
		{
			this.Alias = alias;
		}
	}
}