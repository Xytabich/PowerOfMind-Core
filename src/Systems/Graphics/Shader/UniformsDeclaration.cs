namespace PowerOfMind.Graphics
{
	public readonly struct UniformsDeclaration
	{
		public readonly UniformProperty[] Properties;

		public UniformsDeclaration(params UniformProperty[] properties)
		{
			Properties = properties;
		}
	}
}