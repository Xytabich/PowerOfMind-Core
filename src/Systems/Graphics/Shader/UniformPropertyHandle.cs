using System;

namespace PowerOfMind.Graphics.Shader
{
	public readonly struct UniformPropertyHandle
	{
		public readonly string Name;
		public readonly string Alias;
		public readonly int Location;
		public readonly EnumShaderPrimitiveType Type;
		public readonly EnumUniformStructType StructType;
		public readonly int UniformSize;
		public readonly int StructSize;
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

		public unsafe void SetValue<T>(T* ptr, int count) where T : unmanaged
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