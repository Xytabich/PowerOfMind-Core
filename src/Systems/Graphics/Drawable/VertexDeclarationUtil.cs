using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PowerOfMind.Graphics
{
	public static class VertexDeclarationUtil
	{
		public static VertexDeclaration GetDeclarationFromStruct<T>() where T : unmanaged
		{
			return DeclarationCache<T>.Declaration;
		}

		private static class DeclarationCache<T> where T : unmanaged
		{
			public static readonly VertexDeclaration Declaration;

			static unsafe DeclarationCache()
			{
				var fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				var attribs = new VertexAttribute[fields.Length];
				for(int i = fields.Length - 1; i >= 0; i--)
				{
					var field = fields[i];
					var fieldType = field.FieldType;
					var componentType = EnumShaderPrimitiveType.Unknown;
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
							case EnumShaderPrimitiveType.Unknown: break;
							case EnumShaderPrimitiveType.UByte:
							case EnumShaderPrimitiveType.SByte:
								componentSize = typeSize;
								break;
							case EnumShaderPrimitiveType.UShort:
							case EnumShaderPrimitiveType.Short:
							case EnumShaderPrimitiveType.Half:
								componentSize = typeSize / 2;
								if(typeSize != (componentSize * 2))
								{
									throw new Exception(string.Format("The '{0}' field has an invalid size for the specified component type", field));
								}
								break;
							case EnumShaderPrimitiveType.UInt:
							case EnumShaderPrimitiveType.Int:
							case EnumShaderPrimitiveType.Float:
								componentSize = typeSize / 4;
								if(typeSize != (componentSize * 4))
								{
									throw new Exception(string.Format("The '{0}' field has an invalid size for the specified component type", field));
								}
								break;
							case EnumShaderPrimitiveType.Double:
								componentSize = typeSize / 8;
								if(typeSize != (componentSize * 8))
								{
									throw new Exception(string.Format("The '{0}' field has an invalid size for the specified component type", field));
								}
								break;
							case EnumShaderPrimitiveType.Int2101010Rev:
							case EnumShaderPrimitiveType.UInt2101010Rev:
								componentSize = 4;
								if(typeSize != 4)
								{
									throw new Exception(string.Format("The '{0}' field has an invalid size for the specified component type", field));
								}
								break;
						}
					}

					if(componentType == EnumShaderPrimitiveType.Unknown)
					{
						if(fieldType.IsPrimitive)
						{
							switch(Type.GetTypeCode(fieldType))
							{
								case TypeCode.Boolean:
									componentType = EnumShaderPrimitiveType.UByte;
									break;
								case TypeCode.Byte:
									componentType = EnumShaderPrimitiveType.UByte;
									break;
								case TypeCode.SByte:
									componentType = EnumShaderPrimitiveType.SByte;
									break;
								case TypeCode.Int16:
									componentType = EnumShaderPrimitiveType.Short;
									break;
								case TypeCode.UInt16:
									componentType = EnumShaderPrimitiveType.UShort;
									break;
								case TypeCode.Int32:
									componentType = EnumShaderPrimitiveType.Int;
									break;
								case TypeCode.UInt32:
									componentType = EnumShaderPrimitiveType.UInt;
									break;
								case TypeCode.Single:
									componentType = EnumShaderPrimitiveType.Float;
									break;
								case TypeCode.Double:
									componentType = EnumShaderPrimitiveType.Double;
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