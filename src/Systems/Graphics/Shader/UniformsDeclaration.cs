namespace PowerOfMind.Graphics
{
	public readonly struct UniformsDeclaration
	{
		public readonly UniformProperty[] Properties;

		public UniformsDeclaration(params UniformProperty[] properties)
		{
			Properties = properties;
		}

		public static UniformsDeclaration FromStruct<T>() where T : unmanaged
		{
			return UniformsDeclarationUtil.GetDeclarationFromStruct<T>();
		}
	}
}