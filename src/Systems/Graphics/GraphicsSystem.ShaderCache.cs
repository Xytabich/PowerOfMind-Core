using Newtonsoft.Json;
using OpenTK.Graphics.OpenGL;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace PowerOfMind.Graphics
{
	public partial class GraphicsSystem
	{
		private const byte SHADER_CACHE_VERSION = 1;

		private Dictionary<AssetLocation, string> cachedShaders = null;
		private bool shaderCacheDirty = false;
		private HashSet<ShaderHashInfo> tmpShaderHashSet = null;

		private bool? arbGetProgramBinary = null;

		public void SaveShadersCache()
		{
			if(cachedShaders != null && shaderCacheDirty)
			{
				shaderCacheDirty = false;
				try
				{
					var path = Path.Combine(GamePaths.Cache, "pomshadercache");
					if(!Directory.Exists(path))
					{
						Directory.CreateDirectory(path);
					}
					path = Path.Combine(path, "entries.json");
					File.WriteAllText(path, JsonConvert.SerializeObject(cachedShaders));
				}
				catch { }
			}
		}

		bool IGraphicsSystemInternal.HasCachedShaderProgram(AssetLocation shaderKey)
		{
			if(cachedShaders == null) LoadShadersCache();
			return cachedShaders.ContainsKey(shaderKey);
		}

		unsafe bool IGraphicsSystemInternal.TryLoadCachedShaderProgram<TEnumerable>(AssetLocation shaderKey, TEnumerable stagesHash, out int handle, out string error)
		{
			if(!HasProgramBinaryArb())
			{
				handle = 0;
				error = "Not supported";
				return false;
			}

			if(cachedShaders == null) LoadShadersCache();

			handle = 0;
			error = null;
			if(cachedShaders.TryGetValue(shaderKey, out var fileName))
			{
				string path = null;
				try
				{
					path = Path.Combine(GamePaths.Cache, "pomshadercache", fileName.Substring(0, 2), fileName);

					if(File.Exists(path))
					{
						using(var stream = File.OpenRead(path))
						{
							if(stream.ReadByte() == SHADER_CACHE_VERSION)
							{
								if(tmpShaderHashSet == null) tmpShaderHashSet = new HashSet<ShaderHashInfo>();
								else tmpShaderHashSet.Clear();
								tmpShaderHashSet.UnionWith(stagesHash);

								int stageCount = stream.ReadByte();
								if(stageCount == tmpShaderHashSet.Count)
								{
									ulong tmpLong;
									var buffSpan = new Span<byte>(&tmpLong, sizeof(ulong));
									for(int i = 0; i < stageCount; i++)
									{
										var type = EnumShaderType.FragmentShader + stream.ReadByte();

										stream.Read(buffSpan.Slice(0, sizeof(int)));
										int size = *(int*)&tmpLong;

										stream.Read(buffSpan);
										tmpShaderHashSet.Add(new ShaderHashInfo(type, tmpLong, size));
									}

									// Since the size are the same, no new elements were added, so the shader probably hasn’t changed
									if(stageCount == tmpShaderHashSet.Count)
									{
										stream.Read(buffSpan);
										int size = ((int*)&tmpLong)[0];
										int format = ((int*)&tmpLong)[1];

										handle = GL.CreateProgram();

										var bytes = ArrayPool<byte>.Shared.Rent(size);
										stream.Read(bytes, 0, size);
										GL.ProgramBinary(handle, (BinaryFormat)format, bytes, size);
										ArrayPool<byte>.Shared.Return(bytes);

										var errno = GL.GetError();
										if(errno == ErrorCode.NoError)
										{
											GL.GetProgram(handle, GetProgramParameterName.LinkStatus, out var status);
											if(status == (int)All.True)
											{
												return true;
											}
										}

										error = errno.ToString();
									}
								}
							}
						}
					}
				}
				catch(Exception e)
				{
					error = e.Message;
				}

				if(handle != 0)
				{
					GL.DeleteProgram(handle);
					handle = 0;
				}

				try
				{
					if(!string.IsNullOrEmpty(path) && File.Exists(path))
					{
						File.Delete(path);
					}
				}
				catch { }
				cachedShaders.Remove(shaderKey);
			}

			return false;
		}

		unsafe void IGraphicsSystemInternal.SaveShaderProgramToCache<TEnumerable>(AssetLocation shaderKey, int handle, TEnumerable stagesHash)
		{
			if(!HasProgramBinaryArb()) return;

			if(tmpShaderHashSet == null) tmpShaderHashSet = new HashSet<ShaderHashInfo>();
			else tmpShaderHashSet.Clear();
			tmpShaderHashSet.UnionWith(stagesHash);
			if(tmpShaderHashSet.Count == 0) return;

			GL.GetProgram(handle, GetProgramParameterName.ProgramBinaryRetrievableHint, out var state);
			if(GL.GetError() != ErrorCode.NoError || state != (int)All.True) return;
			GL.GetProgram(handle, GetProgramParameterName.ProgramBinaryLength, out var size);
			if(GL.GetError() != ErrorCode.NoError || size == 0) return;

			if(cachedShaders == null) LoadShadersCache();
			if(!cachedShaders.TryGetValue(shaderKey, out var fileName))
			{
				fileName = Guid.NewGuid().ToString("N");
				cachedShaders[shaderKey] = fileName;
			}

			try
			{
				var bytes = ArrayPool<byte>.Shared.Rent(size);
				GL.GetProgramBinary(handle, size, out size, out BinaryFormat format, bytes);
				if(GL.GetError() != ErrorCode.NoError)
				{
					ArrayPool<byte>.Shared.Return(bytes);
					return;
				}

				var path = Path.Combine(GamePaths.Cache, "pomshadercache", fileName.Substring(0, 2));
				if(!Directory.Exists(path)) Directory.CreateDirectory(path);
				path = Path.Combine(path, fileName);
				using(var stream = File.OpenWrite(path))
				{
					stream.SetLength(0);
					stream.WriteByte(SHADER_CACHE_VERSION);

					ulong tmpLong;
					var buffSpan = new Span<byte>(&tmpLong, sizeof(ulong));

					stream.WriteByte((byte)tmpShaderHashSet.Count);
					foreach(var info in tmpShaderHashSet)
					{
						stream.WriteByte((byte)(info.Type - EnumShaderType.FragmentShader));

						*(int*)&tmpLong = info.Size;
						stream.Write(buffSpan.Slice(0, sizeof(int)));

						tmpLong = info.Hash;
						stream.Write(buffSpan);
					}

					((int*)&tmpLong)[0] = size;
					((int*)&tmpLong)[1] = (int)format;
					stream.Write(buffSpan);

					stream.Write(bytes, 0, size);

					shaderCacheDirty = true;
				}
				ArrayPool<byte>.Shared.Return(bytes);
			}
			catch { }
		}

		private unsafe bool HasProgramBinaryArb()
		{
			if(!arbGetProgramBinary.HasValue)
			{
				int num = GL.GetInteger(GetPName.NumProgramBinaryFormats);
				arbGetProgramBinary = GL.GetError() == ErrorCode.NoError && num > 0;
			}
			return arbGetProgramBinary.Value;
		}

		private void LoadShadersCache()
		{
			var path = Path.Combine(GamePaths.Cache, "pomshadercache", "entries.json");
			if(File.Exists(path))
			{
				try
				{
					cachedShaders = JsonConvert.DeserializeObject<Dictionary<AssetLocation, string>>(File.ReadAllText(path));
				}
				catch { }
			}
			if(cachedShaders == null) cachedShaders = new Dictionary<AssetLocation, string>();
		}
	}
}