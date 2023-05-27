using System.Runtime.InteropServices;

namespace PowerOfMind.Graphics
{
	/// <summary>
	/// Description of the attribute (component, field) of the vertex, its name, type, offset in the data stream, etc.
	/// </summary>
	[StructLayout(LayoutKind.Auto, Pack = 4)]
	public readonly struct VertexAttribute
	{
		/// <summary>
		/// Attribute field name in the shader.
		/// </summary>
		public readonly string Name;
		/// <summary>
		/// Attribute field alias in the shader, used to unify the target attribute definition.
		/// For example, POSITION is defined as a vertex coordinate, and TEXPOS as a uv coordinate.
		/// Aliases are case insensitive.
		/// </summary>
		public readonly string Alias;
		/// <summary>
		/// Specifies the index of the generic vertex attribute to be modified.
		/// </summary>
		public readonly int Location;
		/// <summary>
		/// Specifies the byte offset between consecutive generic vertex attributes.
		/// If stride is 0, the generic vertex attributes are understood to be tightly packed in the array.
		/// The initial value is 0.
		/// </summary>
		public readonly uint Stride;
		/// <summary>
		/// Specifies a offset of the first component of the first generic vertex attribute in the array in the data store of the buffer.
		/// The initial value is 0.
		/// </summary>
		public readonly uint Offset;
		public readonly uint InstanceDivisor;
		/// <summary>
		/// Specifies the number of components per generic vertex attribute.
		/// Must be 1, 2, 3, 4.
		/// </summary>
		public readonly uint Size;
		/// <summary>
		/// Specifies the data type of each component in the array.
		/// </summary>
		public readonly EnumVertexComponentType Type;
		/// <summary>
		/// Specifies whether fixed-point data values should be normalized or converted directly as fixed-point values when they are accessed.
		/// </summary>
		public readonly bool Normalized;
		/// <summary>
		/// Values are always left as integer values.
		/// </summary>
		public readonly bool IntegerTarget;

		public VertexAttribute(string name, string alias, int location, uint stride, uint offset, uint instanceDivisor, uint size, EnumVertexComponentType type, bool normalized, bool integerTarget)
		{
			Name = name;
			Alias = alias;
			Location = location;
			Stride = stride;
			Offset = offset;
			InstanceDivisor = instanceDivisor;
			Size = size;
			Type = type;
			Normalized = normalized;
			IntegerTarget = integerTarget;
		}
	}
}