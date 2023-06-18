using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PowerOfMind.Graphics
{
	public static class UniformsDeclarationUtil
	{
		public static UniformsDeclaration GetDeclarationFromStruct<T>() where T : unmanaged
		{
			return DeclarationCache<T>.Declaration;
		}

		private static class DeclarationCache<T> where T : unmanaged
		{
			public static readonly UniformsDeclaration Declaration;

			static unsafe DeclarationCache()
			{
				var fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				var uniforms = new UniformProperty[fields.Length];
				for(int i = fields.Length - 1; i >= 0; i--)
				{
					var field = fields[i];
					var fieldType = field.FieldType;
					var uniformType = EnumShaderPrimitiveType.Unknown;
					int uniformSize = 1;
					string alias = null;

					var attr = field.GetCustomAttribute<UniformAttribute>();
					if(attr != null)
					{
						alias = attr.Alias;

						int typeSize = Marshal.SizeOf(fieldType);
						uniformType = attr.Type;
						switch(uniformType)
						{
							case EnumShaderPrimitiveType.Unknown: break;
							case EnumShaderPrimitiveType.UInt:
							case EnumShaderPrimitiveType.Int:
							case EnumShaderPrimitiveType.Float:
								uniformSize = typeSize / 4;
								if(typeSize != (uniformSize * 4))
								{
									throw new Exception(string.Format("The '{0}' field has an invalid size for the specified component type", field));
								}
								break;
							case EnumShaderPrimitiveType.Double:
								uniformSize = typeSize / 8;
								if(typeSize != (uniformSize * 8))
								{
									throw new Exception(string.Format("The '{0}' field has an invalid size for the specified component type", field));
								}
								break;
							default:
								throw new Exception(string.Format("Field '{0}' has invalid type in attribute", field));
						}
					}

					if(uniformType == EnumShaderPrimitiveType.Unknown)
					{
						if(fieldType.IsPrimitive)
						{
							switch(Type.GetTypeCode(fieldType))
							{
								case TypeCode.Int32:
									uniformType = EnumShaderPrimitiveType.Int;
									break;
								case TypeCode.UInt32:
									uniformType = EnumShaderPrimitiveType.UInt;
									break;
								case TypeCode.Single:
									uniformType = EnumShaderPrimitiveType.Float;
									break;
								case TypeCode.Double:
									uniformType = EnumShaderPrimitiveType.Double;
									break;
								default:
									throw new Exception(string.Format("Type {0} cannot be converted to uniform type", fieldType));
							}
						}
						else
						{
							throw new Exception(string.Format("{0} is not primitive type", fieldType));
						}
					}
					if(uniformSize < 1 || uniformSize > 4)
					{
						throw new Exception(string.Format("Invalid component size {0} for field '{1}'", uniformSize, field));
					}
					uniforms[i] = new UniformProperty(
						field.Name,
						alias,
						(uint)Marshal.OffsetOf<T>(field.Name),
						uniformType,
						uniformSize
					);
				}
				Declaration = new UniformsDeclaration(uniforms);
			}
		}
	}
}