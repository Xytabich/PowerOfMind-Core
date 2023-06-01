using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PowerOfMind.Graphics
{
	public static class VertexDeclarationUtil
	{
		public static VertexDeclaration GetDeclarationFromStruct<T>() where T : unmanaged
		{
			return VertexDeclarationCache<T>.Declaration;
		}

		private static class VertexDeclarationCache<T> where T : unmanaged
		{
			public static readonly VertexDeclaration Declaration;

			static unsafe VertexDeclarationCache()
			{
				var fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				var attribs = new VertexAttribute[fields.Length];
				for(int i = fields.Length - 1; i >= 0; i--)
				{
					var field = fields[i];
					var fieldType = field.FieldType;
					var componentType = EnumVertexComponentType.Unknown;
					uint componentSize = 1;
					int location = -1;
					string alias = null;

					bool normalized = false;
					bool asInteger = false;

					var attr = field.GetCustomAttribute<VertexComponentAttribute>();
					if(attr != null)
					{
						alias = attr.Alias;
						location = attr.Location;
						normalized = attr.Normalized;
						asInteger = attr.AsInteger;

						uint typeSize = (uint)Marshal.SizeOf(fieldType);
						componentType = attr.Type;
						switch(componentType)
						{
							case EnumVertexComponentType.Unknown: break;
							case EnumVertexComponentType.UByte:
							case EnumVertexComponentType.SByte:
								componentSize = typeSize;
								break;
							case EnumVertexComponentType.UShort:
							case EnumVertexComponentType.Short:
							case EnumVertexComponentType.Half:
								componentSize = typeSize / 2;
								if(typeSize != (componentSize * 2))
								{
									throw new Exception(string.Format("The '{0}' field has an invalid size for the specified component type", field));
								}
								break;
							case EnumVertexComponentType.UInt:
							case EnumVertexComponentType.Int:
							case EnumVertexComponentType.Float:
								componentSize = typeSize / 4;
								if(typeSize != (componentSize * 4))
								{
									throw new Exception(string.Format("The '{0}' field has an invalid size for the specified component type", field));
								}
								break;
							case EnumVertexComponentType.Int2101010Rev:
							case EnumVertexComponentType.UInt2101010Rev:
								componentSize = 4;
								if(typeSize != 4)
								{
									throw new Exception(string.Format("The '{0}' field has an invalid size for the specified component type", field));
								}
								break;
						}
					}

					if(componentType == EnumVertexComponentType.Unknown)
					{
						if(fieldType.IsPrimitive)
						{
							switch(Type.GetTypeCode(fieldType))
							{
								case TypeCode.Boolean:
									componentType = EnumVertexComponentType.UByte;
									break;
								case TypeCode.Byte:
									componentType = EnumVertexComponentType.UByte;
									break;
								case TypeCode.SByte:
									componentType = EnumVertexComponentType.SByte;
									break;
								case TypeCode.Int16:
									componentType = EnumVertexComponentType.Short;
									break;
								case TypeCode.UInt16:
									componentType = EnumVertexComponentType.UShort;
									break;
								case TypeCode.Int32:
									componentType = EnumVertexComponentType.Int;
									break;
								case TypeCode.UInt32:
									componentType = EnumVertexComponentType.UInt;
									break;
								case TypeCode.Single:
									componentType = EnumVertexComponentType.Float;
									break;
								default:
									throw new Exception(string.Format("Type {0} cannot be converted to component type", fieldType));
							}
						}
						else
						{
							throw new Exception(string.Format("{0} is not primitive type", fieldType));
						}
					}
					if(componentSize < 1 || componentSize > 4)
					{
						throw new Exception(string.Format("Invalid component size {0} for field '{1}'", componentSize, field));
					}
					attribs[i] = new VertexAttribute(
						field.Name,
						alias,
						location,
						(uint)sizeof(T),
						(uint)Marshal.OffsetOf<T>(field.Name),
						0,
						componentSize,
						componentType,
						normalized,
						asInteger
					);
				}
				Declaration = new VertexDeclaration(attribs);
			}
		}
	}
}