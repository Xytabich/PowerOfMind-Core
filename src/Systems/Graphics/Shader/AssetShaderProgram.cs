using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PowerOfMind.Graphics.Shader
{
	public class AssetShaderProgram : ExtendedShaderProgram
	{
		public static readonly string SHADERS_LOC = AssetCategory.shaders.Code + "/";
		public static readonly string SHADERINCLUDES_LOC = AssetCategory.shaderincludes.Code + "/";

		private static readonly KeyValuePair<EnumShaderType, string>[] shaderTypes = new KeyValuePair<EnumShaderType, string>[] {
			new KeyValuePair<EnumShaderType, string>(EnumShaderType.VertexShader, ".vsh"),
			new KeyValuePair<EnumShaderType, string>(EnumShaderType.FragmentShader, ".fsh"),
			new KeyValuePair<EnumShaderType, string>(EnumShaderType.GeometryShader, ".gsh")
		};

		private readonly AssetLocation location;
		private readonly System.Func<EnumShaderType, string> shaderDefinitionsProvider;
		private readonly System.Func<IExtendedShaderProgram, Action> createUseBindings;

		public AssetShaderProgram(GraphicsSystem graphics, string passName, AssetLocation location,
			System.Func<EnumShaderType, string> shaderDefinitionsProvider,
			System.Func<IExtendedShaderProgram, Action> createUseBindings)
			: base(graphics, passName, null)
		{
			this.location = location;
			this.shaderDefinitionsProvider = shaderDefinitionsProvider;
			this.createUseBindings = createUseBindings;
		}

		public override bool Compile()
		{
			if(handle != 0) return true;

			var baseLoc = location.Clone().WithPathPrefixOnce(SHADERS_LOC);
			var stages = new List<ShaderStage>();
			HashSet<string> includes = null;
			foreach(var pair in shaderTypes)
			{
				var assetLoc = baseLoc.Clone().WithPathAppendix(pair.Value);
				var asset = graphics.Api.Assets.TryGet(assetLoc, loadAsset: true);
				if(asset != null)
				{
					try
					{
						var mainCode = asset.ToText();
						var code = graphics.ShaderPreprocessor.PreprocessShaderAsset(pair.Key, assetLoc, asset, shaderDefinitionsProvider, out var version);
						var inputAliasMap = Array.Empty<KeyValuePair<string, string>>();
						if(pair.Key == EnumShaderType.VertexShader)
						{
							inputAliasMap = graphics.ShaderPreprocessor.inputAliasMap.ToArray();
						}
						stages.Add(new ShaderStage(pair.Key, assetLoc.ToString(), code, version, inputAliasMap, graphics.ShaderPreprocessor.uniformAliasMap.ToArray()));
						if(graphics.ShaderPreprocessor.locationToId.Count > 1)
						{
							foreach(var include in graphics.ShaderPreprocessor.locationToId)
							{
								if(include.Value != 0 && include.Key.Domain == "game")
								{
									if(includes == null) includes = new HashSet<string>();
									includes.Add(Path.GetFileName(include.Key.Path));
								}
							}
						}
					}
					catch(Exception e)
					{
						graphics.Logger.Error("Exception while trying to preprocess shader asset '{0}': {1}", assetLoc.ToString(), e);
						break;
					}
				}
			}
			this.stages = stages.ToArray();
			if(base.Compile())
			{
				List<Action> bindings = null;
				if(includes != null)
				{
					foreach(var pair in StandardIncludeBindings.ShaderBindingFactoryPairs)
					{
						if(includes.Contains(pair.Key))
						{
							if(bindings == null) bindings = new List<Action>();
							bindings.Add(pair.Value(this));
						}
					}
				}
				if(createUseBindings != null)
				{
					var action = createUseBindings(this);
					if(action != null)
					{
						if(bindings == null) bindings = new List<Action>();
						bindings.Add(action);
					}
				}
				this.useBindings = bindings?.ToArray();
				return true;
			}
			return false;
		}
	}
}