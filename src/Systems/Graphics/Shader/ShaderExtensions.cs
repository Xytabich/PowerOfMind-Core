﻿using PowerOfMind.Collections;
using PowerOfMind.Graphics.Shader;
using System;
using System.Collections.Generic;

namespace PowerOfMind.Graphics
{
	public static class ShaderExtensions
	{
		public static void FindTextureBindings(this IExtendedShaderProgram shader, string uniformName, out int uniformIndex, out int textureNumber)
		{
			uniformIndex = shader.FindUniformIndex(uniformName);
			if(uniformIndex < 0 || !shader.Uniforms.IndexToTextureUnit.TryGetValue(uniformIndex, out textureNumber))
			{
				textureNumber = -1;
			}
		}

		public static void FindTextureBindingsByAlias(this IExtendedShaderProgram shader, string uniformAlias, out int uniformIndex, out int textureNumber)
		{
			uniformIndex = shader.FindUniformIndexByAlias(uniformAlias);
			if(uniformIndex < 0 || !shader.Uniforms.IndexToTextureUnit.TryGetValue(uniformIndex, out textureNumber))
			{
				textureNumber = -1;
			}
		}

		public static void BindTexture2D(this IExtendedShaderProgram shader, string samplerName, int textureId)
		{
			shader.BindTexture(shader.FindUniformIndex(samplerName), EnumTextureTarget.Texture2D, textureId);
		}

		public static void BindTexture2D(this IExtendedShaderProgram shader, string samplerName, EnumTextureTarget target, int textureId)
		{
			shader.BindTexture(shader.FindUniformIndex(samplerName), target, textureId);
		}

		public static void BindTexture2D(this IExtendedShaderProgram shader, string samplerName, EnumTextureTarget target, int textureId, int textureNumber)
		{
			shader.BindTexture(shader.FindUniformIndex(samplerName), target, textureId, textureNumber);
		}

		public static void MapDeclarationByIdentifier(this IExtendedShaderProgram shader, UniformsDeclaration declaration, IDictionary<int, int> outMap)
		{
			var uniforms = shader.Uniforms.Properties;
			int uniformsCount = uniforms.Length;
			for(int i = declaration.Properties.Length - 1; i >= 0; i--)
			{
				string name = declaration.Properties[i].Name;
				string alias = declaration.Properties[i].Alias;
				bool checkAlias = !string.IsNullOrEmpty(alias);
				for(int j = 0; j < uniformsCount; j++)
				{
					if(checkAlias && uniforms[j].Alias == alias || uniforms[j].Name == name)
					{
						outMap[i] = j;
						break;
					}
				}
			}
		}

		/// <summary>
		/// Creates a declaration that matches the shader, removing unsuitable attributes from the input declaration.
		/// It will also assign such parameters as <see cref="VertexAttribute.Location"/> and <see cref="VertexAttribute.IntegerTarget"/>
		/// </summary>
		public static VertexDeclaration MapDeclaration(this IExtendedShaderProgram shader, VertexDeclaration declaration)
		{
			var attribs = new RefList<VertexAttribute>();
			MapDeclaration(shader, declaration, attribs);
			return new VertexDeclaration(attribs.ToArray());
		}

		/// <summary>
		/// Creates a declaration that matches the shader, removing unsuitable attributes from the input declaration.
		/// It will also assign such parameters as <see cref="VertexAttribute.Location"/> and <see cref="VertexAttribute.IntegerTarget"/>
		/// </summary>
		public static void MapDeclaration(this IExtendedShaderProgram shader, VertexDeclaration declaration, RefList<VertexAttribute> outAttributesList)
		{
			var inputs = shader.Inputs.Attributes;
			int inputsCount = inputs.Length;
			for(int i = declaration.Attributes.Length - 1; i >= 0; i--)
			{
				string name = declaration.Attributes[i].Name;
				string alias = declaration.Attributes[i].Alias;
				bool checkAlias = !string.IsNullOrEmpty(alias);
				for(int j = 0; j < inputsCount; j++)
				{
					if(checkAlias && inputs[j].Alias == alias || inputs[j].Name == name)
					{
						AddMappedAttribute(declaration.Attributes, i, inputs, j, outAttributesList);
						break;
					}
				}
			}
		}

		private static void AddMappedAttribute(VertexAttribute[] attributes, int attribIndex, ShaderVertexAttribute[] inputs, int inputIndex, RefList<VertexAttribute> outList)
		{
			ref readonly var attrib = ref attributes[attribIndex];
			ref readonly var input = ref inputs[inputIndex];
			if(attrib.Size > input.Size) throw new Exception("Attribute data size too big for the shader");
			outList.Add(new VertexAttribute(
				input.Name,
				input.Alias ?? attrib.Alias,
				input.Location,
				attrib.Stride,
				attrib.Offset,
				attrib.InstanceDivisor,
				attrib.Size,
				attrib.Type,
				attrib.Normalized,
				IsInteger(input.Type)
			));
		}

		private static bool IsInteger(EnumShaderPrimitiveType type)
		{
			switch(type)
			{
				case EnumShaderPrimitiveType.Half:
				case EnumShaderPrimitiveType.Float:
				case EnumShaderPrimitiveType.Double:
					return false;
				default:
					return true;
			}
		}
	}
}