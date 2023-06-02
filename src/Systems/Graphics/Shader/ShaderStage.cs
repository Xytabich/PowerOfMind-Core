using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PowerOfMind.Graphics.Shader
{
	public class ShaderStage : IShader, IDisposable
	{
		public readonly EnumShaderType Type;

		EnumShaderType IShader.Type => Type;
		string IShader.Code { get { return code; } set { throw new InvalidOperationException(); } }
		string IShader.PrefixCode { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

		private readonly string filename;
		private readonly string code;
		private readonly string version;
		private readonly KeyValuePair<string, string>[] inputsAlias;
		private readonly KeyValuePair<string, string>[] uniformsAlias;

		private int handle = 0;
		private GraphicsSystem graphics;

		public ShaderStage(EnumShaderType type, string filename, string code, string version, KeyValuePair<string, string>[] inputsAlias, KeyValuePair<string, string>[] uniformsAlias)
		{
			Type = type;
			this.filename = filename;
			this.code = code;
			this.version = version;
			this.inputsAlias = inputsAlias;
			this.uniformsAlias = uniformsAlias;
		}

		public bool Compile(GraphicsSystem graphics, ILogger logger)
		{
			if(!graphics.TryCompileShaderStage(Type, code, out handle, out var error))
			{
				handle = 0;

				logger.Error("Shader compile error in {0} {1}", filename, error.TrimEnd());
				logger.VerboseDebug("{0}", code);
				return false;
			}
			this.graphics = graphics;
			return true;
		}

		public void Attach(int programHandle)
		{
			graphics.AttachStageToProgram(programHandle, handle);
		}

		public bool EnsureVersionSupported(ILogger logger)
		{
			try
			{
				graphics.EnsureShaderVersionSupported(version, filename);
			}
			catch(Exception e)
			{
				logger.Error("Shader version check error in {0}\n{1}", filename, e);
				return false;
			}
			return true;
		}

		public void CollectInputsAlias(IDictionary<string, string> outDictionary)
		{
			foreach(var pair in inputsAlias)
			{
				outDictionary[pair.Key] = pair.Value;
			}
		}

		public void CollectUniformsAlias(IDictionary<string, string> outDictionary)
		{
			foreach(var pair in uniformsAlias)
			{
				outDictionary[pair.Key] = pair.Value;
			}
		}

		public void Dispose()
		{
			if(handle != 0)
			{
				graphics.DeleteShaderStage(handle);
				handle = 0;
			}
		}
	}
}