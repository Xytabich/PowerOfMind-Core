using PowerOfMind.Graphics;
using System;

namespace PowerOfMind.Systems.Graphics.Shader
{
	public readonly struct UniformPropertyHandle
	{
		public readonly string Name;
		public readonly string Alias;
		public readonly int Location;
		public readonly EnumVertexComponentType ComponentType;
		public readonly int ComponentSize;

		private readonly IUniformVariableHandler handler;

		internal UniformPropertyHandle(string name, string alias, int location, EnumVertexComponentType componentType, int componentSize, IUniformVariableHandler handler)
		{
			Name = name;
			Alias = alias;
			Location = location;
			ComponentType = componentType;
			ComponentSize = componentSize;
			this.handler = handler;
		}

		public unsafe void SetValue<T>(T* ptr, int count = 1) where T : unmanaged
		{
			if(handler == null) throw new Exception("This property cannot be assigned in this way");
			handler.SetValue(Location, ptr, count);
		}
	}
}