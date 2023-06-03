using Vintagestory.API.Client;

namespace PowerOfMind.Graphics
{
	public class MeshData<T> where T : unmanaged, IVertexStruct
	{
		public EnumDrawMode drawMode;

		public T[] Vertices;
		public int VerticesCount = 0;
		/// <summary>
		/// Offset in <see cref="Vertices"/> from which the vertices of this mesh start
		/// </summary>
		public int VerticesOffset = 0;
		public bool VerticesStatic = true;

		public int[] Indices;
		public int IndicesCount = 0;
		/// <summary>
		/// Offset in <see cref="Indices"/> from which the indices of this mesh start
		/// </summary>
		public int IndicesOffset = 0;
		public bool IndicesStatic = true;

		public VertexDeclaration VertexDeclaration;

		public MeshData(T[] vertices, int[] indices)
		{
			Vertices = vertices;
			VerticesCount = vertices.Length;

			Indices = indices;
			IndicesCount = indices.Length;

			VertexDeclaration = vertices[0].GetDeclaration();
		}
	}
}