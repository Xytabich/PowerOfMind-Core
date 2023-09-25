using System;

namespace PowerOfMind.Graphics.Shader
{
	public readonly struct UniformPropertyHandle
	{
		/// <summary>
		/// Name of the uniform in the shader
		/// </summary>
		public readonly string Name;
		/// <summary>
		/// General alias for uniform
		/// </summary>
		public readonly string Alias;
		/// <summary>
		/// The location to which this uniform is bound
		/// </summary>
		public readonly int Location;
		/// <summary>
		/// Uniform component type
		/// </summary>
		public readonly EnumShaderPrimitiveType Type;
		/// <summary>
		/// Type of structure described by the uniform
		/// </summary>
		public readonly EnumUniformStructType StructType;
		/// <summary>
		/// If uniform is an array, denotes the number of components multiplied by the size of the array.
		/// Otherwise it will be the same size as <see cref="StructSize"/>.
		/// </summary>
		public readonly int UniformSize;
		/// <summary>
		/// Number of components in a vector or matrix
		/// </summary>
		public readonly int StructSize;
		/// <summary>
		/// Data slot (for example texture slot) used by default for this uniform
		/// </summary>
		public readonly int ReferenceSlot;

		private readonly IUniformVariableHandler handler;

		internal UniformPropertyHandle(int location, string name, string alias, EnumShaderPrimitiveType type,
			EnumUniformStructType structType, int uniformSize, int structSize, IUniformVariableHandler handler)
		{
			Location = location;
			Name = name;
			Alias = alias?.ToUpperInvariant();
			StructType = structType;
			Type = type;
			UniformSize = uniformSize;
			StructSize = structSize;
			ReferenceSlot = 0;

			this.handler = handler;
		}

		internal UniformPropertyHandle(int location, int referenceSlot, string name, string alias, EnumShaderPrimitiveType type,
			EnumUniformStructType structType, int uniformSize, int structSize, IUniformVariableHandler handler)
		{
			Location = location;
			Name = name;
			Alias = alias?.ToUpperInvariant();
			StructType = structType;
			Type = type;
			UniformSize = uniformSize;
			StructSize = structSize;
			ReferenceSlot = referenceSlot;

			this.handler = handler;
		}

		internal UniformPropertyHandle WithReferenceSlot(int referenceSlot)
		{
			return new UniformPropertyHandle(Location, referenceSlot, Name, Alias, Type, StructType, UniformSize, StructSize, handler);
		}

		public unsafe void SetValue(void* ptr, int count)
		{
			if(handler == null) throw new Exception("This property cannot be assigned in this way");
			handler.SetValue(this, ptr, count);
		}

		public override string ToString()
		{
			return string.Format("{0}: {1} {2}({3})", Location, string.Format(string.IsNullOrEmpty(Alias) ? "{0}" : "{0}[{1}]", Name, Alias), Type, UniformSize);
		}
	}
}