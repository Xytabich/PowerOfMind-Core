using OpenTK.Graphics.OpenGL;
using System;

namespace PowerOfMind.Systems.Graphics
{
	using GLTextureMagFilter = OpenTK.Graphics.OpenGL.TextureMagFilter;
	using GLTextureMinFilter = OpenTK.Graphics.OpenGL.TextureMinFilter;
	using GLTextureWrapMode = OpenTK.Graphics.OpenGL.TextureWrapMode;
	public class TextureSampler : IDisposable, ITextureSampler
	{
		private const GLTextureMagFilter MAGFILTER_DEFAULT = GLTextureMagFilter.Linear;
		private const GLTextureMinFilter MINFILTER_DEFAULT = GLTextureMinFilter.NearestMipmapLinear;
		private const GLTextureWrapMode WRAP_DEFAULT = GLTextureWrapMode.Repeat;

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

		private int handle = 0;
		private int allocations = 0;

		private GLTextureMagFilter magFilter = MAGFILTER_DEFAULT;
		private GLTextureMinFilter minFilter = MINFILTER_DEFAULT;
		private GLTextureWrapMode wrapS = WRAP_DEFAULT, wrapT = WRAP_DEFAULT, wrapR = WRAP_DEFAULT;
		private TextureCompareMode compareMode = 0;
		private All compareFunc = All.Less;

		public TextureSampler SetMagFilter(TextureMagFilter filter)
		{
			magFilter = filter == TextureMagFilter.Nearest ? GLTextureMagFilter.Nearest : GLTextureMagFilter.Linear;
			if(handle != 0) GL.SamplerParameter(handle, SamplerParameterName.TextureMagFilter, (int)magFilter);
			return this;
		}

		public TextureSampler SetMinFilter(TextureMinFilter filter)
		{
			minFilter = texMinFilters[(int)filter];
			if(handle != 0) GL.SamplerParameter(handle, SamplerParameterName.TextureMinFilter, (int)minFilter);
			return this;
		}

		public TextureSampler SetWrapS(TextureWrapMode mode)
		{
			wrapS = texWrapModes[(int)mode];
			if(handle != 0) GL.SamplerParameter(handle, SamplerParameterName.TextureWrapS, (int)wrapS);
			return this;
		}

		public TextureSampler SetWrapT(TextureWrapMode mode)
		{
			wrapT = texWrapModes[(int)mode];
			if(handle != 0) GL.SamplerParameter(handle, SamplerParameterName.TextureWrapT, (int)wrapT);
			return this;
		}

		public TextureSampler SetWrapR(TextureWrapMode mode)
		{
			wrapR = texWrapModes[(int)mode];
			if(handle != 0) GL.SamplerParameter(handle, SamplerParameterName.TextureWrapR, (int)wrapR);
			return this;
		}

		public TextureSampler SetCompareMode(bool compareRefToTexture)
		{
			compareMode = compareRefToTexture ? TextureCompareMode.CompareRefToTexture : 0;
			if(handle != 0) GL.SamplerParameter(handle, SamplerParameterName.TextureCompareMode, (int)compareMode);
			return this;
		}

		public TextureSampler SetCompareFunc(TextureCompareFunc func)
		{
			compareFunc = texCompareFuncs[(int)func];
			if(handle != 0) GL.SamplerParameter(handle, SamplerParameterName.TextureCompareFunc, (int)compareFunc);
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
			if(handle == 0)
			{
				handle = GL.GenSampler();
				if(magFilter != MAGFILTER_DEFAULT) GL.SamplerParameter(handle, SamplerParameterName.TextureMagFilter, (int)magFilter);
				if(minFilter != MINFILTER_DEFAULT) GL.SamplerParameter(handle, SamplerParameterName.TextureMinFilter, (int)minFilter);
				if(wrapS != WRAP_DEFAULT) GL.SamplerParameter(handle, SamplerParameterName.TextureWrapS, (int)wrapS);
				if(wrapT != WRAP_DEFAULT) GL.SamplerParameter(handle, SamplerParameterName.TextureWrapT, (int)wrapT);
				if(wrapR != WRAP_DEFAULT) GL.SamplerParameter(handle, SamplerParameterName.TextureWrapR, (int)wrapR);
				if(compareMode != 0)
				{
					GL.SamplerParameter(handle, SamplerParameterName.TextureCompareMode, (int)compareMode);
					GL.SamplerParameter(handle, SamplerParameterName.TextureCompareFunc, (int)compareFunc);
				}
			}
		}

		void ITextureSampler.Release()
		{
			allocations--;
			if(allocations == 0) Dispose();
		}
	}
}