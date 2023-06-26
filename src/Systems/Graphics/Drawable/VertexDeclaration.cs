namespace PowerOfMind.Graphics
{
	/// <summary>
	/// Vertex structure description
	/// </summary>
	public readonly struct VertexDeclaration
	{
		public readonly VertexAttribute[] Attributes;

		public bool IsEmpty => Attributes == null || Attributes.Length == 0;

		public VertexDeclaration(VertexAttribute[] attributes)
		{
			Attributes = attributes;
		}

		public static VertexDeclaration FromStruct<T>() where T : unmanaged
		{
			return VertexDeclarationUtil.GetDeclarationFromStruct<T>();
		}
	}
}