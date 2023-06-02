﻿using System;

namespace PowerOfMind.Graphics.Shader
{
	public readonly struct UniformPropertyHandle
	{
		public readonly string Name;
		public readonly string Alias;
		public readonly int Location;
		public readonly EnumShaderPrimitiveType Type;
		public readonly EnumUniformStructType StructType;
		public readonly int Size;

		private readonly IUniformVariableHandler handler;

		internal UniformPropertyHandle(int location, string name, string alias, EnumShaderPrimitiveType type, EnumUniformStructType structType, int size, IUniformVariableHandler handler)
		{
			Location = location;
			Name = name;
			Alias = alias?.ToUpperInvariant();
			StructType = structType;
			Type = type;
			Size = size;

			this.handler = handler;
		}

		public unsafe void SetValue<T>(T* ptr, int count) where T : unmanaged
		{
			if(handler == null) throw new Exception("This property cannot be assigned in this way");
			handler.SetValue(Location, ptr, count);
		}
	}
}