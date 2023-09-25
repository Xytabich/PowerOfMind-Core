using OpenTK.Graphics.OpenGL;
using PowerOfMind.Graphics.Shader;

namespace PowerOfMind.Graphics
{
	public partial class GraphicsSystem
	{
		private unsafe class UniformVariableHandlers
		{
			private readonly UniformVariableHandlerBase[] handlers = new UniformVariableHandlerBase[]
			{
				new UniformVariableHandler<int>(GL.Uniform1, EnumUniformStructType.Primitive, EnumShaderPrimitiveType.Int, 1),
				new UniformVariableHandler<uint>(GL.Uniform1, EnumUniformStructType.Primitive, EnumShaderPrimitiveType.UInt, 1),
				new UniformVariableHandler<float>(GL.Uniform1, EnumUniformStructType.Primitive, EnumShaderPrimitiveType.Float, 1),
				new UniformVariableHandler<double>(GL.Uniform1, EnumUniformStructType.Primitive, EnumShaderPrimitiveType.Double, 1),

				new UniformVariableHandler<int>(GL.Uniform2, EnumUniformStructType.Vector, EnumShaderPrimitiveType.Int, 2),
				new UniformVariableHandler<uint>(GL.Uniform2, EnumUniformStructType.Vector, EnumShaderPrimitiveType.UInt, 2),
				new UniformVariableHandler<float>(GL.Uniform2, EnumUniformStructType.Vector, EnumShaderPrimitiveType.Float, 2),
				new UniformVariableHandler<double>(GL.Uniform2, EnumUniformStructType.Vector, EnumShaderPrimitiveType.Double, 2),

				new UniformVariableHandler<int>(GL.Uniform3, EnumUniformStructType.Vector, EnumShaderPrimitiveType.Int, 3),
				new UniformVariableHandler<uint>(GL.Uniform3, EnumUniformStructType.Vector, EnumShaderPrimitiveType.UInt, 3),
				new UniformVariableHandler<float>(GL.Uniform3, EnumUniformStructType.Vector, EnumShaderPrimitiveType.Float, 3),
				new UniformVariableHandler<double>(GL.Uniform3, EnumUniformStructType.Vector, EnumShaderPrimitiveType.Double, 3),

				new UniformVariableHandler<int>(GL.Uniform4, EnumUniformStructType.Vector, EnumShaderPrimitiveType.Int, 4),
				new UniformVariableHandler<uint>(GL.Uniform4, EnumUniformStructType.Vector, EnumShaderPrimitiveType.UInt, 4),
				new UniformVariableHandler<float>(GL.Uniform4, EnumUniformStructType.Vector, EnumShaderPrimitiveType.Float, 4),
				new UniformVariableHandler<double>(GL.Uniform4, EnumUniformStructType.Vector, EnumShaderPrimitiveType.Double, 4),

				new UniformMatrixHandler<float>(GL.UniformMatrix2, EnumUniformStructType.SquareMatrix, EnumShaderPrimitiveType.Float, 4),
				new UniformMatrixHandler<float>(GL.UniformMatrix3, EnumUniformStructType.SquareMatrix, EnumShaderPrimitiveType.Float, 9),
				new UniformMatrixHandler<float>(GL.UniformMatrix4, EnumUniformStructType.SquareMatrix, EnumShaderPrimitiveType.Float, 16),

				new UniformMatrixHandler<float>(GL.UniformMatrix3x2, EnumUniformStructType.ColumnMatrix, EnumShaderPrimitiveType.Float, 6),
				new UniformMatrixHandler<float>(GL.UniformMatrix4x2, EnumUniformStructType.ColumnMatrix, EnumShaderPrimitiveType.Float, 8),
				new UniformMatrixHandler<float>(GL.UniformMatrix4x3, EnumUniformStructType.ColumnMatrix, EnumShaderPrimitiveType.Float, 12),

				new UniformMatrixHandler<float>(GL.UniformMatrix2x3, EnumUniformStructType.RowMatrix, EnumShaderPrimitiveType.Float, 6),
				new UniformMatrixHandler<float>(GL.UniformMatrix2x4, EnumUniformStructType.RowMatrix, EnumShaderPrimitiveType.Float, 8),
				new UniformMatrixHandler<float>(GL.UniformMatrix3x4, EnumUniformStructType.RowMatrix, EnumShaderPrimitiveType.Float, 12),
			};

			public IUniformVariableHandler TryFindHandler(EnumUniformStructType structType, EnumShaderPrimitiveType type, int size)
			{
				switch(structType)
				{
					case EnumUniformStructType.Primitive:
					case EnumUniformStructType.Vector:
					case EnumUniformStructType.SquareMatrix:
					case EnumUniformStructType.ColumnMatrix:
					case EnumUniformStructType.RowMatrix:
						break;
					default:
						structType = EnumUniformStructType.Primitive;
						break;
				}
				foreach(var handler in handlers)
				{
					if(handler.primitiveType == type && handler.structType == structType && handler.size == size)
					{
						return (IUniformVariableHandler)handler;
					}
				}
				return null;
			}

			private class UniformVariableHandlerBase
			{
				public readonly EnumUniformStructType structType;
				public readonly EnumShaderPrimitiveType primitiveType;
				public readonly int size;

				public UniformVariableHandlerBase(EnumUniformStructType structType, EnumShaderPrimitiveType primitiveType, int size)
				{
					this.structType = structType;
					this.primitiveType = primitiveType;
					this.size = size;
				}
			}

			private class UniformVariableHandler<T> : UniformVariableHandlerBase, IUniformVariableHandler where T : unmanaged
			{
				private readonly SetValueCallback setValue;

				public UniformVariableHandler(SetValueCallback setValue, EnumUniformStructType structType, EnumShaderPrimitiveType primitiveType, int size) : base(structType, primitiveType, size)
				{
					this.setValue = setValue;
				}

				void IUniformVariableHandler.SetValue(in UniformPropertyHandle handle, void* ptr, int count)
				{
					setValue(handle.Location, count, (T*)ptr);
				}

				public delegate void SetValueCallback(int location, int count, T* value);
			}

			private class UniformMatrixHandler<T> : UniformVariableHandlerBase, IUniformVariableHandler where T : unmanaged
			{
				private readonly SetValueCallback setValue;

				public UniformMatrixHandler(SetValueCallback setValue, EnumUniformStructType structType, EnumShaderPrimitiveType primitiveType, int size) : base(structType, primitiveType, size)
				{
					this.setValue = setValue;
				}

				void IUniformVariableHandler.SetValue(in UniformPropertyHandle handle, void* ptr, int count)
				{
					setValue(handle.Location, count, false, (T*)ptr);
				}

				public delegate void SetValueCallback(int location, int count, bool transpose, T* value);
			}
		}
	}
}