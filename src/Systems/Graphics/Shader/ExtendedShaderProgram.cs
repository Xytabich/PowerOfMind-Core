using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace PowerOfMind.Systems.Graphics.Shader
{
	//https://registry.khronos.org/OpenGL-Refpages/gl4/html/glGetActiveUniform.xhtml
	//https://registry.khronos.org/OpenGL-Refpages/gl4/html/glGetProgram.xhtml
	public class ExtendedShaderProgram : IShaderProgram
	{
		public int PassId { get; }
		public string PassName { get; }
		public bool ClampTexturesToEdge { get; set; }
		public IShader VertexShader { get; set; }
		public IShader FragmentShader { get; set; }
		public IShader GeometryShader { get; set; }
		public bool Disposed { get; }
		public bool LoadError { get; }

		public bool Compile()
		{
			throw new System.NotImplementedException();
		}

		public void Use()
		{
			throw new System.NotImplementedException();
		}

		public void Stop()
		{
			throw new System.NotImplementedException();
		}

		public void Dispose()
		{
			throw new System.NotImplementedException();
		}

		public void BindTexture2D(string samplerName, int textureId, int textureNumber)
		{
			throw new System.NotImplementedException();
		}

		public void BindTextureCube(string samplerName, int textureId, int textureNumber)
		{
			throw new System.NotImplementedException();
		}

		public void Uniform(string uniformName, float value)
		{
			throw new System.NotImplementedException();
		}

		public void Uniform(string uniformName, int value)
		{
			throw new System.NotImplementedException();
		}

		public void Uniform(string uniformName, Vec2f value)
		{
			throw new System.NotImplementedException();
		}

		public void Uniform(string uniformName, Vec3f value)
		{
			throw new System.NotImplementedException();
		}

		public void Uniform(string uniformName, Vec4f value)
		{
			throw new System.NotImplementedException();
		}

		public void Uniforms4(string uniformName, int count, float[] values)
		{
			throw new System.NotImplementedException();
		}

		public void UniformMatrices(string uniformName, int count, float[] matrix)
		{
			throw new System.NotImplementedException();
		}

		public void UniformMatrices4x3(string uniformName, int count, float[] matrix)
		{
			throw new System.NotImplementedException();
		}

		public void UniformMatrix(string uniformName, float[] matrix)
		{
			throw new System.NotImplementedException();
		}
	}
}