using OpenTK.Graphics.OpenGL;
using PowerOfMind.Graphics.Shader;
using System;
using System.Collections.Generic;

namespace PowerOfMind.Graphics
{
	public partial class GraphicsSystem
	{
		internal ShaderVertexAttribute[] GetShaderAttributes(int handle, IReadOnlyDictionary<string, string> aliasByName = null)
		{
			GL.GetProgram(handle, GetProgramParameterName.ActiveAttributes, out int count);
			GL.GetProgram(handle, GetProgramParameterName.ActiveAttributeMaxLength, out int maxNameLen);
			maxNameLen = (maxNameLen + 1) * 2;
			var attributes = new ShaderVertexAttribute[count];
			for(int i = 0; i < count; i++)
			{
				GL.GetActiveAttrib(handle, i, maxNameLen, out _, out int size, out var type, out var name);
				EnumShaderPrimitiveType compType;
				int compSize = 1;
				switch(type)
				{
					case ActiveAttribType.Int: compType = EnumShaderPrimitiveType.Int; break;
					case ActiveAttribType.UnsignedInt: compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveAttribType.Float: compType = EnumShaderPrimitiveType.Float; break;
					case ActiveAttribType.Double: compType = EnumShaderPrimitiveType.Double; break;
					case ActiveAttribType.IntVec2: compType = EnumShaderPrimitiveType.Int; compSize = 2; break;
					case ActiveAttribType.IntVec3: compType = EnumShaderPrimitiveType.Int; compSize = 3; break;
					case ActiveAttribType.IntVec4: compType = EnumShaderPrimitiveType.Int; compSize = 4; break;
					case ActiveAttribType.UnsignedIntVec2: compType = EnumShaderPrimitiveType.UInt; compSize = 2; break;
					case ActiveAttribType.UnsignedIntVec3: compType = EnumShaderPrimitiveType.UInt; compSize = 3; break;
					case ActiveAttribType.UnsignedIntVec4: compType = EnumShaderPrimitiveType.UInt; compSize = 4; break;
					case ActiveAttribType.FloatVec2: compType = EnumShaderPrimitiveType.Float; compSize = 2; break;
					case ActiveAttribType.FloatVec3: compType = EnumShaderPrimitiveType.Float; compSize = 3; break;
					case ActiveAttribType.FloatVec4: compType = EnumShaderPrimitiveType.Float; compSize = 4; break;
					case ActiveAttribType.FloatMat2: compType = EnumShaderPrimitiveType.Float; compSize = 4; break;
					case ActiveAttribType.FloatMat3: compType = EnumShaderPrimitiveType.Float; compSize = 9; break;
					case ActiveAttribType.FloatMat4: compType = EnumShaderPrimitiveType.Float; compSize = 16; break;
					case ActiveAttribType.FloatMat2x3: compType = EnumShaderPrimitiveType.Float; compSize = 6; break;
					case ActiveAttribType.FloatMat2x4: compType = EnumShaderPrimitiveType.Float; compSize = 8; break;
					case ActiveAttribType.FloatMat3x2: compType = EnumShaderPrimitiveType.Float; compSize = 6; break;
					case ActiveAttribType.FloatMat3x4: compType = EnumShaderPrimitiveType.Float; compSize = 12; break;
					case ActiveAttribType.FloatMat4x2: compType = EnumShaderPrimitiveType.Float; compSize = 8; break;
					case ActiveAttribType.FloatMat4x3: compType = EnumShaderPrimitiveType.Float; compSize = 12; break;
					case ActiveAttribType.DoubleVec2: compType = EnumShaderPrimitiveType.Double; compSize = 2; break;
					case ActiveAttribType.DoubleVec3: compType = EnumShaderPrimitiveType.Double; compSize = 3; break;
					case ActiveAttribType.DoubleVec4: compType = EnumShaderPrimitiveType.Double; compSize = 4; break;
					case ActiveAttribType.DoubleMat2: compType = EnumShaderPrimitiveType.Double; compSize = 4; break;
					case ActiveAttribType.DoubleMat3: compType = EnumShaderPrimitiveType.Double; compSize = 9; break;
					case ActiveAttribType.DoubleMat4: compType = EnumShaderPrimitiveType.Double; compSize = 16; break;
					case ActiveAttribType.DoubleMat2x3: compType = EnumShaderPrimitiveType.Double; compSize = 6; break;
					case ActiveAttribType.DoubleMat2x4: compType = EnumShaderPrimitiveType.Double; compSize = 8; break;
					case ActiveAttribType.DoubleMat3x2: compType = EnumShaderPrimitiveType.Double; compSize = 6; break;
					case ActiveAttribType.DoubleMat3x4: compType = EnumShaderPrimitiveType.Double; compSize = 12; break;
					case ActiveAttribType.DoubleMat4x2: compType = EnumShaderPrimitiveType.Double; compSize = 8; break;
					case ActiveAttribType.DoubleMat4x3: compType = EnumShaderPrimitiveType.Double; compSize = 12; break;
					default: compType = EnumShaderPrimitiveType.Unknown; break;
				}

				string alias;
				if(aliasByName == null || !aliasByName.TryGetValue(name, out alias))
				{
					alias = null;
				}
				attributes[i] = new ShaderVertexAttribute(i, name, alias, compType, size * compSize);
			}
			Array.Sort(attributes, InputComparer.Instance);
			return attributes;
		}

		internal UniformPropertyHandle[] GetShaderUniforms(int handle, IReadOnlyDictionary<string, string> aliasByName = null)
		{
			GL.GetProgram(handle, GetProgramParameterName.ActiveUniforms, out int count);
			GL.GetProgram(handle, GetProgramParameterName.ActiveUniformMaxLength, out int maxNameLen);
			maxNameLen = (maxNameLen + 1) * 2;
			var uniforms = new UniformPropertyHandle[count];
			for(int i = 0; i < count; i++)
			{
				GL.GetActiveUniform(handle, i, maxNameLen, out _, out int size, out var type, out var name);
				EnumShaderPrimitiveType compType;
				EnumUniformStructType structType = EnumUniformStructType.Primitive;
				int compSize = 1;
				switch(type)
				{
					case ActiveUniformType.Bool: compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Int: compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedInt: compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Float: compType = EnumShaderPrimitiveType.Float; break;
					case ActiveUniformType.Double: compType = EnumShaderPrimitiveType.Double; break;
					case ActiveUniformType.IntVec2: structType = EnumUniformStructType.Vector; compType = EnumShaderPrimitiveType.Int; compSize = 2; break;
					case ActiveUniformType.IntVec3: structType = EnumUniformStructType.Vector; compType = EnumShaderPrimitiveType.Int; compSize = 3; break;
					case ActiveUniformType.IntVec4: structType = EnumUniformStructType.Vector; compType = EnumShaderPrimitiveType.Int; compSize = 4; break;
					case ActiveUniformType.UnsignedIntVec2: structType = EnumUniformStructType.Vector; compType = EnumShaderPrimitiveType.UInt; compSize = 2; break;
					case ActiveUniformType.UnsignedIntVec3: structType = EnumUniformStructType.Vector; compType = EnumShaderPrimitiveType.UInt; compSize = 3; break;
					case ActiveUniformType.UnsignedIntVec4: structType = EnumUniformStructType.Vector; compType = EnumShaderPrimitiveType.UInt; compSize = 4; break;
					case ActiveUniformType.BoolVec2: structType = EnumUniformStructType.Vector; compType = EnumShaderPrimitiveType.UInt; compSize = 2; break;
					case ActiveUniformType.BoolVec3: structType = EnumUniformStructType.Vector; compType = EnumShaderPrimitiveType.UInt; compSize = 3; break;
					case ActiveUniformType.BoolVec4: structType = EnumUniformStructType.Vector; compType = EnumShaderPrimitiveType.UInt; compSize = 4; break;
					case ActiveUniformType.FloatVec2: structType = EnumUniformStructType.Vector; compType = EnumShaderPrimitiveType.Float; compSize = 2; break;
					case ActiveUniformType.FloatVec3: structType = EnumUniformStructType.Vector; compType = EnumShaderPrimitiveType.Float; compSize = 3; break;
					case ActiveUniformType.FloatVec4: structType = EnumUniformStructType.Vector; compType = EnumShaderPrimitiveType.Float; compSize = 4; break;
					case ActiveUniformType.FloatMat2: structType = EnumUniformStructType.SquareMatrix; compType = EnumShaderPrimitiveType.Float; compSize = 4; break;
					case ActiveUniformType.FloatMat3: structType = EnumUniformStructType.SquareMatrix; compType = EnumShaderPrimitiveType.Float; compSize = 9; break;
					case ActiveUniformType.FloatMat4: structType = EnumUniformStructType.SquareMatrix; compType = EnumShaderPrimitiveType.Float; compSize = 16; break;
					case ActiveUniformType.FloatMat2x3: structType = EnumUniformStructType.RowMatrix; compType = EnumShaderPrimitiveType.Float; compSize = 6; break;
					case ActiveUniformType.FloatMat2x4: structType = EnumUniformStructType.RowMatrix; compType = EnumShaderPrimitiveType.Float; compSize = 8; break;
					case ActiveUniformType.FloatMat3x4: structType = EnumUniformStructType.RowMatrix; compType = EnumShaderPrimitiveType.Float; compSize = 12; break;
					case ActiveUniformType.FloatMat3x2: structType = EnumUniformStructType.ColumnMatrix; compType = EnumShaderPrimitiveType.Float; compSize = 6; break;
					case ActiveUniformType.FloatMat4x2: structType = EnumUniformStructType.ColumnMatrix; compType = EnumShaderPrimitiveType.Float; compSize = 8; break;
					case ActiveUniformType.FloatMat4x3: structType = EnumUniformStructType.ColumnMatrix; compType = EnumShaderPrimitiveType.Float; compSize = 12; break;
					case ActiveUniformType.DoubleVec2: structType = EnumUniformStructType.Vector; compType = EnumShaderPrimitiveType.Double; compSize = 2; break;
					case ActiveUniformType.DoubleVec3: structType = EnumUniformStructType.Vector; compType = EnumShaderPrimitiveType.Double; compSize = 3; break;
					case ActiveUniformType.DoubleVec4: structType = EnumUniformStructType.Vector; compType = EnumShaderPrimitiveType.Double; compSize = 4; break;

					case ActiveUniformType.Sampler1D: structType = EnumUniformStructType.Sampler1D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Sampler2D: structType = EnumUniformStructType.Sampler2D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Sampler3D: structType = EnumUniformStructType.Sampler3D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.SamplerCube: structType = EnumUniformStructType.SamplerCube; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Sampler1DShadow: structType = EnumUniformStructType.Sampler1DShadow; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Sampler2DShadow: structType = EnumUniformStructType.Sampler2DShadow; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Sampler2DRect: structType = EnumUniformStructType.Sampler2DRect; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Sampler2DRectShadow: structType = EnumUniformStructType.Sampler2DRectShadow; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Sampler1DArray: structType = EnumUniformStructType.Sampler1DArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Sampler2DArray: structType = EnumUniformStructType.Sampler2DArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.SamplerBuffer: structType = EnumUniformStructType.SamplerBuffer; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Sampler1DArrayShadow: structType = EnumUniformStructType.Sampler1DArrayShadow; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Sampler2DArrayShadow: structType = EnumUniformStructType.Sampler2DArrayShadow; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.SamplerCubeShadow: structType = EnumUniformStructType.SamplerCubeShadow; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntSampler1D: structType = EnumUniformStructType.IntSampler1D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntSampler2D: structType = EnumUniformStructType.IntSampler2D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntSampler3D: structType = EnumUniformStructType.IntSampler3D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntSamplerCube: structType = EnumUniformStructType.IntSamplerCube; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntSampler2DRect: structType = EnumUniformStructType.IntSampler2DRect; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntSampler1DArray: structType = EnumUniformStructType.IntSampler1DArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntSampler2DArray: structType = EnumUniformStructType.IntSampler2DArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntSamplerBuffer: structType = EnumUniformStructType.IntSamplerBuffer; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntSampler1D: structType = EnumUniformStructType.UnsignedIntSampler1D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntSampler2D: structType = EnumUniformStructType.UnsignedIntSampler2D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntSampler3D: structType = EnumUniformStructType.UnsignedIntSampler3D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntSamplerCube: structType = EnumUniformStructType.UnsignedIntSamplerCube; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntSampler2DRect: structType = EnumUniformStructType.UnsignedIntSampler2DRect; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntSampler1DArray: structType = EnumUniformStructType.UnsignedIntSampler1DArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntSampler2DArray: structType = EnumUniformStructType.UnsignedIntSampler2DArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntSamplerBuffer: structType = EnumUniformStructType.UnsignedIntSamplerBuffer; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.SamplerCubeMapArray: structType = EnumUniformStructType.SamplerCubeMapArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.SamplerCubeMapArrayShadow: structType = EnumUniformStructType.SamplerCubeMapArrayShadow; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntSamplerCubeMapArray: structType = EnumUniformStructType.IntSamplerCubeMapArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntSamplerCubeMapArray: structType = EnumUniformStructType.UnsignedIntSamplerCubeMapArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Image1D: structType = EnumUniformStructType.Image1D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Image2D: structType = EnumUniformStructType.Image2D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Image3D: structType = EnumUniformStructType.Image3D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Image2DRect: structType = EnumUniformStructType.Image2DRect; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.ImageCube: structType = EnumUniformStructType.ImageCube; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.ImageBuffer: structType = EnumUniformStructType.ImageBuffer; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Image1DArray: structType = EnumUniformStructType.Image1DArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Image2DArray: structType = EnumUniformStructType.Image2DArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.ImageCubeMapArray: structType = EnumUniformStructType.ImageCubeMapArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Image2DMultisample: structType = EnumUniformStructType.Image2DMultisample; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Image2DMultisampleArray: structType = EnumUniformStructType.Image2DMultisampleArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntImage1D: structType = EnumUniformStructType.IntImage1D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntImage2D: structType = EnumUniformStructType.IntImage2D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntImage3D: structType = EnumUniformStructType.IntImage3D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntImage2DRect: structType = EnumUniformStructType.IntImage2DRect; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntImageCube: structType = EnumUniformStructType.IntImageCube; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntImageBuffer: structType = EnumUniformStructType.IntImageBuffer; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntImage1DArray: structType = EnumUniformStructType.IntImage1DArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntImage2DArray: structType = EnumUniformStructType.IntImage2DArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntImageCubeMapArray: structType = EnumUniformStructType.IntImageCubeMapArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntImage2DMultisample: structType = EnumUniformStructType.IntImage2DMultisample; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntImage2DMultisampleArray: structType = EnumUniformStructType.IntImage2DMultisampleArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntImage1D: structType = EnumUniformStructType.UnsignedIntImage1D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntImage2D: structType = EnumUniformStructType.UnsignedIntImage2D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntImage3D: structType = EnumUniformStructType.UnsignedIntImage3D; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntImage2DRect: structType = EnumUniformStructType.UnsignedIntImage2DRect; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntImageCube: structType = EnumUniformStructType.UnsignedIntImageCube; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntImageBuffer: structType = EnumUniformStructType.UnsignedIntImageBuffer; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntImage1DArray: structType = EnumUniformStructType.UnsignedIntImage1DArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntImage2DArray: structType = EnumUniformStructType.UnsignedIntImage2DArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntImageCubeMapArray: structType = EnumUniformStructType.UnsignedIntImageCubeMapArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntImage2DMultisample: structType = EnumUniformStructType.UnsignedIntImage2DMultisample; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntImage2DMultisampleArray: structType = EnumUniformStructType.UnsignedIntImage2DMultisampleArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Sampler2DMultisample: structType = EnumUniformStructType.Sampler2DMultisample; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntSampler2DMultisample: structType = EnumUniformStructType.IntSampler2DMultisample; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntSampler2DMultisample: structType = EnumUniformStructType.UnsignedIntSampler2DMultisample; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.Sampler2DMultisampleArray: structType = EnumUniformStructType.Sampler2DMultisampleArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.IntSampler2DMultisampleArray: structType = EnumUniformStructType.IntSampler2DMultisampleArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntSampler2DMultisampleArray: structType = EnumUniformStructType.UnsignedIntSampler2DMultisampleArray; compType = EnumShaderPrimitiveType.UInt; break;
					case ActiveUniformType.UnsignedIntAtomicCounter: structType = EnumUniformStructType.UnsignedIntAtomicCounter; compType = EnumShaderPrimitiveType.UInt; break;
					default: compType = EnumShaderPrimitiveType.Unknown; break;
				}
				string alias;
				if(aliasByName == null || !aliasByName.TryGetValue(name, out alias))
				{
					alias = null;
				}
				uniforms[i] = new UniformPropertyHandle(i, name, alias, compType, structType, size * compSize, uniformHandlers.TryFindHandler(structType, compType, compSize));
			}
			Array.Sort(uniforms, UniformComparer.Instance);
			return uniforms;
		}

		private class InputComparer : IComparer<ShaderVertexAttribute>
		{
			public static readonly InputComparer Instance = new InputComparer();

			int IComparer<ShaderVertexAttribute>.Compare(ShaderVertexAttribute x, ShaderVertexAttribute y)
			{
				int c = x.Location.CompareTo(y.Location);
				if(c != 0) return c;
				return (x.Name ?? string.Empty).CompareTo(y.Name ?? string.Empty);
			}
		}

		private class UniformComparer : IComparer<UniformPropertyHandle>
		{
			public static readonly UniformComparer Instance = new UniformComparer();

			int IComparer<UniformPropertyHandle>.Compare(UniformPropertyHandle x, UniformPropertyHandle y)
			{
				int c = x.Location.CompareTo(y.Location);
				if(c != 0) return c;
				return (x.Name ?? string.Empty).CompareTo(y.Name ?? string.Empty);
			}
		}
	}
}