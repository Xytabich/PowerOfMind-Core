using OpenTK.Graphics.OpenGL;
using System;

namespace PowerOfMind.Systems.Graphics
{
	using GLTextureWrapMode = OpenTK.Graphics.OpenGL.TextureWrapMode;
	using GLTextureMinFilter = OpenTK.Graphics.OpenGL.TextureMinFilter;
	using GLTextureMagFilter = OpenTK.Graphics.OpenGL.TextureMagFilter;
	public class TextureSampler : IDisposable, ITextureSampler
	{
		private static readonly All[] texCompareFuncs = new All[] {
			All.Less,
			All.Equal,
			All.Lequal,
			All.Greater,
			All.Notequal,
			All.Gequal,
			All.Always
		};

		private static readonly GLTextureWrapMode[] texWrapModes = new GLTextureWrapMode[] {
			GLTextureWrapMode.Repeat,
			GLTextureWrapMode.ClampToBorder,
			GLTextureWrapMode.ClampToBorderArb,
			GLTextureWrapMode.ClampToBorderNv,
			GLTextureWrapMode.ClampToBorderSgis,
			GLTextureWrapMode.ClampToEdge,
			GLTextureWrapMode.ClampToEdgeSgis,
			GLTextureWrapMode.MirroredRepeat,
		};

		private static readonly GLTextureMinFilter[] texMinFilters = new GLTextureMinFilter[] {
			GLTextureMinFilter.Nearest,
			GLTextureMinFilter.Linear,
			GLTextureMinFilter.NearestMipmapNearest,
			GLTextureMinFilter.LinearMipmapNearest,
			GLTextureMinFilter.NearestMipmapLinear,
			GLTextureMinFilter.LinearMipmapLinear
		};

		private int handle;
		private int allocations = 0;

		public TextureSampler()
		{
			handle = GL.GenSampler();
		}

		public TextureSampler SetBorderColor()
		{
			return this;
		}

		public TextureSampler SetMagFilter(TextureMagFilter filter)
		{
			GL.SamplerParameter(handle, SamplerParameterName.TextureWrapS, (int)(filter == TextureMagFilter.Nearest ? GLTextureMagFilter.Nearest : GLTextureMagFilter.Linear));
			return this;
		}

		public TextureSampler SetMinFilter(TextureMinFilter filter)
		{
			GL.SamplerParameter(handle, SamplerParameterName.TextureWrapS, (int)texMinFilters[(int)filter]);
			return this;
		}

		public TextureSampler SetWrapS(TextureWrapMode mode)
		{
			GL.SamplerParameter(handle, SamplerParameterName.TextureWrapS, (int)texWrapModes[(int)mode]);
			return this;
		}

		public TextureSampler SetWrapT(TextureWrapMode mode)
		{
			GL.SamplerParameter(handle, SamplerParameterName.TextureWrapT, (int)texWrapModes[(int)mode]);
			return this;
		}

		public TextureSampler SetWrapR(TextureWrapMode mode)
		{
			GL.SamplerParameter(handle, SamplerParameterName.TextureWrapR, (int)texWrapModes[(int)mode]);
			return this;
		}

		public TextureSampler SetCompareMode(bool compareRefToTexture)
		{
			GL.SamplerParameter(handle, SamplerParameterName.TextureCompareMode, compareRefToTexture ? (int)TextureCompareMode.CompareRefToTexture : 0);
			return this;
		}

		public TextureSampler SetCompareFunc(TextureCompareFunc func)
		{
			GL.SamplerParameter(handle, SamplerParameterName.TextureCompareFunc, (int)texCompareFuncs[(int)func]);
			return this;
		}

		public void Dispose()
		{
			if(handle == 0) return;
			allocations = 0;

			GL.DeleteSampler(handle);
			handle = 0;
		}

		void ITextureSampler.Bind(int textureNumber)
		{
			GL.BindSampler(textureNumber, handle);
		}

		void ITextureSampler.Unbind(int textureNumber)
		{
			GL.BindSampler(textureNumber, 0);
		}

		void ITextureSampler.Allocate()
		{
			allocations++;
		}

		void ITextureSampler.Release()
		{
			allocations--;
			if(allocations == 0) Dispose();
		}
	}
}