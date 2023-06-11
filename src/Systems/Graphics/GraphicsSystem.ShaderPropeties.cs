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
				attributes[i] = new ShaderVertexAttribute(GL.GetAttribLocation(handle, name), name, alias, compType, size * compSize);
			}
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

					case ActiveUniformType.Sampler1D: structType = EnumUniformStructType.Sampler1D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Sampler2D: structType = EnumUniformStructType.Sampler2D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Sampler3D: structType = EnumUniformStructType.Sampler3D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.SamplerCube: structType = EnumUniformStructType.SamplerCube; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Sampler1DShadow: structType = EnumUniformStructType.Sampler1DShadow; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Sampler2DShadow: structType = EnumUniformStructType.Sampler2DShadow; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Sampler2DRect: structType = EnumUniformStructType.Sampler2DRect; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Sampler2DRectShadow: structType = EnumUniformStructType.Sampler2DRectShadow; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Sampler1DArray: structType = EnumUniformStructType.Sampler1DArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Sampler2DArray: structType = EnumUniformStructType.Sampler2DArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.SamplerBuffer: structType = EnumUniformStructType.SamplerBuffer; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Sampler1DArrayShadow: structType = EnumUniformStructType.Sampler1DArrayShadow; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Sampler2DArrayShadow: structType = EnumUniformStructType.Sampler2DArrayShadow; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.SamplerCubeShadow: structType = EnumUniformStructType.SamplerCubeShadow; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntSampler1D: structType = EnumUniformStructType.IntSampler1D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntSampler2D: structType = EnumUniformStructType.IntSampler2D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntSampler3D: structType = EnumUniformStructType.IntSampler3D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntSamplerCube: structType = EnumUniformStructType.IntSamplerCube; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntSampler2DRect: structType = EnumUniformStructType.IntSampler2DRect; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntSampler1DArray: structType = EnumUniformStructType.IntSampler1DArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntSampler2DArray: structType = EnumUniformStructType.IntSampler2DArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntSamplerBuffer: structType = EnumUniformStructType.IntSamplerBuffer; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntSampler1D: structType = EnumUniformStructType.UnsignedIntSampler1D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntSampler2D: structType = EnumUniformStructType.UnsignedIntSampler2D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntSampler3D: structType = EnumUniformStructType.UnsignedIntSampler3D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntSamplerCube: structType = EnumUniformStructType.UnsignedIntSamplerCube; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntSampler2DRect: structType = EnumUniformStructType.UnsignedIntSampler2DRect; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntSampler1DArray: structType = EnumUniformStructType.UnsignedIntSampler1DArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntSampler2DArray: structType = EnumUniformStructType.UnsignedIntSampler2DArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntSamplerBuffer: structType = EnumUniformStructType.UnsignedIntSamplerBuffer; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.SamplerCubeMapArray: structType = EnumUniformStructType.SamplerCubeMapArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.SamplerCubeMapArrayShadow: structType = EnumUniformStructType.SamplerCubeMapArrayShadow; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntSamplerCubeMapArray: structType = EnumUniformStructType.IntSamplerCubeMapArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntSamplerCubeMapArray: structType = EnumUniformStructType.UnsignedIntSamplerCubeMapArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Image1D: structType = EnumUniformStructType.Image1D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Image2D: structType = EnumUniformStructType.Image2D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Image3D: structType = EnumUniformStructType.Image3D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Image2DRect: structType = EnumUniformStructType.Image2DRect; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.ImageCube: structType = EnumUniformStructType.ImageCube; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.ImageBuffer: structType = EnumUniformStructType.ImageBuffer; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Image1DArray: structType = EnumUniformStructType.Image1DArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Image2DArray: structType = EnumUniformStructType.Image2DArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.ImageCubeMapArray: structType = EnumUniformStructType.ImageCubeMapArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Image2DMultisample: structType = EnumUniformStructType.Image2DMultisample; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Image2DMultisampleArray: structType = EnumUniformStructType.Image2DMultisampleArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntImage1D: structType = EnumUniformStructType.IntImage1D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntImage2D: structType = EnumUniformStructType.IntImage2D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntImage3D: structType = EnumUniformStructType.IntImage3D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntImage2DRect: structType = EnumUniformStructType.IntImage2DRect; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntImageCube: structType = EnumUniformStructType.IntImageCube; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntImageBuffer: structType = EnumUniformStructType.IntImageBuffer; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntImage1DArray: structType = EnumUniformStructType.IntImage1DArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntImage2DArray: structType = EnumUniformStructType.IntImage2DArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntImageCubeMapArray: structType = EnumUniformStructType.IntImageCubeMapArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntImage2DMultisample: structType = EnumUniformStructType.IntImage2DMultisample; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntImage2DMultisampleArray: structType = EnumUniformStructType.IntImage2DMultisampleArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntImage1D: structType = EnumUniformStructType.UnsignedIntImage1D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntImage2D: structType = EnumUniformStructType.UnsignedIntImage2D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntImage3D: structType = EnumUniformStructType.UnsignedIntImage3D; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntImage2DRect: structType = EnumUniformStructType.UnsignedIntImage2DRect; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntImageCube: structType = EnumUniformStructType.UnsignedIntImageCube; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntImageBuffer: structType = EnumUniformStructType.UnsignedIntImageBuffer; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntImage1DArray: structType = EnumUniformStructType.UnsignedIntImage1DArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntImage2DArray: structType = EnumUniformStructType.UnsignedIntImage2DArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntImageCubeMapArray: structType = EnumUniformStructType.UnsignedIntImageCubeMapArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntImage2DMultisample: structType = EnumUniformStructType.UnsignedIntImage2DMultisample; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntImage2DMultisampleArray: structType = EnumUniformStructType.UnsignedIntImage2DMultisampleArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Sampler2DMultisample: structType = EnumUniformStructType.Sampler2DMultisample; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntSampler2DMultisample: structType = EnumUniformStructType.IntSampler2DMultisample; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntSampler2DMultisample: structType = EnumUniformStructType.UnsignedIntSampler2DMultisample; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.Sampler2DMultisampleArray: structType = EnumUniformStructType.Sampler2DMultisampleArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.IntSampler2DMultisampleArray: structType = EnumUniformStructType.IntSampler2DMultisampleArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntSampler2DMultisampleArray: structType = EnumUniformStructType.UnsignedIntSampler2DMultisampleArray; compType = EnumShaderPrimitiveType.Int; break;
					case ActiveUniformType.UnsignedIntAtomicCounter: structType = EnumUniformStructType.UnsignedIntAtomicCounter; compType = EnumShaderPrimitiveType.Int; break;
					default: compType = EnumShaderPrimitiveType.Unknown; break;
				}
				string alias;
				if(aliasByName == null || !aliasByName.TryGetValue(name, out alias))
				{
					alias = null;
				}
				uniforms[i] = new UniformPropertyHandle(GL.GetUniformLocation(handle, name), name, alias,
					compType, structType, size * compSize, compSize, uniformHandlers.TryFindHandler(structType, compType, compSize));
			}
			Array.Sort(uniforms, UniformComparer.Instance);//Sort by type to create proper order for textures, e.g. shadows don't work on texture unit 0 for some reason
			int slotCounter = 0;
			for(int i = 0; i < uniforms.Length; i++)
			{
				switch(uniforms[i].StructType)
				{
					case EnumUniformStructType.Sampler1D:
					case EnumUniformStructType.Sampler2D:
					case EnumUniformStructType.Sampler3D:
					case EnumUniformStructType.SamplerCube:
					case EnumUniformStructType.Sampler1DShadow:
					case EnumUniformStructType.Sampler2DShadow:
					case EnumUniformStructType.Sampler2DRect:
					case EnumUniformStructType.Sampler2DRectShadow:
					case EnumUniformStructType.Sampler1DArray:
					case EnumUniformStructType.Sampler2DArray:
					case EnumUniformStructType.SamplerBuffer:
					case EnumUniformStructType.Sampler1DArrayShadow:
					case EnumUniformStructType.Sampler2DArrayShadow:
					case EnumUniformStructType.SamplerCubeShadow:
					case EnumUniformStructType.IntSampler1D:
					case EnumUniformStructType.IntSampler2D:
					case EnumUniformStructType.IntSampler3D:
					case EnumUniformStructType.IntSamplerCube:
					case EnumUniformStructType.IntSampler2DRect:
					case EnumUniformStructType.IntSampler1DArray:
					case EnumUniformStructType.IntSampler2DArray:
					case EnumUniformStructType.IntSamplerBuffer:
					case EnumUniformStructType.UnsignedIntSampler1D:
					case EnumUniformStructType.UnsignedIntSampler2D:
					case EnumUniformStructType.UnsignedIntSampler3D:
					case EnumUniformStructType.UnsignedIntSamplerCube:
					case EnumUniformStructType.UnsignedIntSampler2DRect:
					case EnumUniformStructType.UnsignedIntSampler1DArray:
					case EnumUniformStructType.UnsignedIntSampler2DArray:
					case EnumUniformStructType.UnsignedIntSamplerBuffer:
					case EnumUniformStructType.SamplerCubeMapArray:
					case EnumUniformStructType.SamplerCubeMapArrayShadow:
					case EnumUniformStructType.IntSamplerCubeMapArray:
					case EnumUniformStructType.UnsignedIntSamplerCubeMapArray:
					case EnumUniformStructType.Sampler2DMultisample:
					case EnumUniformStructType.IntSampler2DMultisample:
					case EnumUniformStructType.UnsignedIntSampler2DMultisample:
					case EnumUniformStructType.Sampler2DMultisampleArray:
					case EnumUniformStructType.IntSampler2DMultisampleArray:
					case EnumUniformStructType.UnsignedIntSampler2DMultisampleArray:
						uniforms[i] = uniforms[i].WithReferenceSlot(slotCounter);
						slotCounter++;
						break;
				}
			}
			return uniforms;
		}

		private class UniformComparer : IComparer<UniformPropertyHandle>
		{
			public static readonly UniformComparer Instance = new UniformComparer();

			int IComparer<UniformPropertyHandle>.Compare(UniformPropertyHandle x, UniformPropertyHandle y)
			{
				int c = ((int)x.StructType).CompareTo((int)y.StructType);
				if(c != 0) return c;
				c = ((int)x.Type).CompareTo((int)y.Type);
				if(c != 0) return c;
				return x.Location.CompareTo(y.Location);
			}
		}
	}
}