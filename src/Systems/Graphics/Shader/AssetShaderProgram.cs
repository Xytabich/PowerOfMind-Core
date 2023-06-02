using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;

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

		private delegate Action GenBindingsDelegate(IExtendedShaderProgram shader);
		private static readonly KeyValuePair<string, GenBindingsDelegate>[] includeBindings = new KeyValuePair<string, GenBindingsDelegate>[] {
			new KeyValuePair<string, GenBindingsDelegate>("fogandlight.fsh", GenFogAndLightFragmentBindings),
			new KeyValuePair<string, GenBindingsDelegate>("fogandlight.vsh", GenFogAndLightVertexBindings),
			new KeyValuePair<string, GenBindingsDelegate>("shadowcoords.vsh", GenShadowCoordsVertexBindings),
			new KeyValuePair<string, GenBindingsDelegate>("vertexwarp.vsh", GenVertexWarpVertexBindings),
			new KeyValuePair<string, GenBindingsDelegate>("skycolor.fsh", GenSkyColorFragmentBindings),
			new KeyValuePair<string, GenBindingsDelegate>("colormap.vsh", GenColorMapVertexBindings)
		};

		private readonly AssetLocation location;

		public AssetShaderProgram(GraphicsSystem graphics, string passName, AssetLocation location) : base(graphics, passName, null)
		{
			this.location = location;
		}

		public override bool Compile()
		{
			var baseLoc = location.Clone().WithPathPrefixOnce(SHADERS_LOC);
			var stages = new List<ShaderStage>();
			HashSet<string> includes = null;
			foreach(var pair in shaderTypes)
			{
				var assetLoc = baseLoc.WithPathAppendix(pair.Value);
				var asset = graphics.Api.Assets.TryGet(assetLoc, loadAsset: true);
				if(asset != null)
				{
					try
					{
						var mainCode = asset.ToText();
						var code = graphics.ShaderPreprocessor.PreprocessShaderAsset(pair.Key, assetLoc, asset, out var version);
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
								if(include.Value == 0 || include.Key.Domain != "game")
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
					foreach(var pair in includeBindings)
					{
						if(includes.Contains(pair.Key))
						{
							bindings.Add(pair.Value(this));
						}
					}
				}
				this.useBindings = bindings?.ToArray();
				return true;
			}
			return false;
		}

		private static Action GenColorMapVertexBindings(IExtendedShaderProgram shader)
		{
			int _colorMapRects = shader.FindUniformLocation("colorMapRects");
			int _seasonRel = shader.FindUniformLocation("seasonRel");
			int _seaLevel = shader.FindUniformLocation("seaLevel");
			int _atlasHeight = shader.FindUniformLocation("atlasHeight");
			int _seasonTemperature = shader.FindUniformLocation("seasonTemperature");

			return () => {
				var uniformProperties = shader.Uniforms.Properties;
				var shUniforms = ScreenManager.Platform.ShaderUniforms;
				uniformProperties[_colorMapRects].SetValues(shUniforms.ColorMapRects4, 160);
				uniformProperties[_seasonRel].SetValue(shUniforms.SeasonRel);
				uniformProperties[_seaLevel].SetValue(shUniforms.SeaLevel);
				uniformProperties[_atlasHeight].SetValue(shUniforms.BlockAtlasHeight);
				uniformProperties[_seasonTemperature].SetValue(shUniforms.SeasonTemperature);
			};
		}

		private static Action GenSkyColorFragmentBindings(IExtendedShaderProgram shader)
		{
			int _fogWaveCounter = shader.FindUniformLocation("fogWaveCounter");
			int _sunsetMod = shader.FindUniformLocation("sunsetMod");
			int _ditherSeed = shader.FindUniformLocation("ditherSeed");
			int _horizontalResolution = shader.FindUniformLocation("horizontalResolution");
			int _playerToSealevelOffset = shader.FindUniformLocation("playerToSealevelOffset");

			int _sky = shader.FindUniformLocation("sky");
			int _skyTex = shader.Uniforms.LocationToTextureUnit[_sky];
			int _glow = shader.FindUniformLocation("glow");
			int _glowTex = shader.Uniforms.LocationToTextureUnit[_glow];

			return () => {
				var uniformProperties = shader.Uniforms.Properties;
				var shUniforms = ScreenManager.Platform.ShaderUniforms;
				shader.BindTexture(_sky, EnumTextureTarget.Texture2D, shUniforms.SkyTextureId, _skyTex);
				shader.BindTexture(_glow, EnumTextureTarget.Texture2D, shUniforms.GlowTextureId, _glowTex);
				uniformProperties[_fogWaveCounter].SetValue(shUniforms.FogWaveCounter);
				uniformProperties[_sunsetMod].SetValue(shUniforms.SunsetMod);
				uniformProperties[_ditherSeed].SetValue(shUniforms.DitherSeed);
				uniformProperties[_horizontalResolution].SetValue(shUniforms.FrameWidth);
				uniformProperties[_playerToSealevelOffset].SetValue(shUniforms.PlayerToSealevelOffset);
			};
		}

		private static Action GenVertexWarpVertexBindings(IExtendedShaderProgram shader)
		{
			int _timeCounter = shader.FindUniformLocation("timeCounter");
			int _windWaveCounter = shader.FindUniformLocation("windWaveCounter");
			int _windWaveCounterHighFreq = shader.FindUniformLocation("windWaveCounterHighFreq");
			int _windSpeed = shader.FindUniformLocation("windSpeed");
			int _waterWaveCounter = shader.FindUniformLocation("waterWaveCounter");
			int _playerpos = shader.FindUniformLocation("playerpos");
			int _globalWarpIntensity = shader.FindUniformLocation("globalWarpIntensity");
			int _glitchStrength = shader.FindUniformLocation("glitchStrength");
			int _windWaveIntensity = shader.FindUniformLocation("windWaveIntensity");
			int _waterWaveIntensity = shader.FindUniformLocation("waterWaveIntensity");
			int _perceptionEffectId = shader.FindUniformLocation("perceptionEffectId");
			int _perceptionEffectIntensity = shader.FindUniformLocation("perceptionEffectIntensity");

			return () => {
				var uniformProperties = shader.Uniforms.Properties;
				var shUniforms = ScreenManager.Platform.ShaderUniforms;
				uniformProperties[_timeCounter].SetValue(shUniforms.TimeCounter);
				uniformProperties[_windWaveCounter].SetValue(shUniforms.WindWaveCounter);
				uniformProperties[_windWaveCounterHighFreq].SetValue(shUniforms.WindWaveCounterHighFreq);
				uniformProperties[_windSpeed].SetValue(shUniforms.WindSpeed);
				uniformProperties[_waterWaveCounter].SetValue(shUniforms.WaterWaveCounter);
				uniformProperties[_playerpos].SetValue(shUniforms.PlayerPos);
				uniformProperties[_globalWarpIntensity].SetValue(shUniforms.GlobalWorldWarp);
				uniformProperties[_glitchStrength].SetValue(shUniforms.GlitchStrength);
				uniformProperties[_windWaveIntensity].SetValue(shUniforms.WindWaveIntensity);
				uniformProperties[_waterWaveIntensity].SetValue(shUniforms.WaterWaveIntensity);
				uniformProperties[_perceptionEffectId].SetValue(shUniforms.PerceptionEffectId);
				uniformProperties[_perceptionEffectIntensity].SetValue(shUniforms.PerceptionEffectIntensity);
			};
		}

		private static Action GenShadowCoordsVertexBindings(IExtendedShaderProgram shader)
		{
			int _shadowRangeNear = shader.FindUniformLocation("shadowRangeNear");
			int _shadowRangeFar = shader.FindUniformLocation("shadowRangeFar");
			int _toShadowMapSpaceMatrixNear = shader.FindUniformLocation("toShadowMapSpaceMatrixNear");
			int _toShadowMapSpaceMatrixFar = shader.FindUniformLocation("toShadowMapSpaceMatrixFar");

			return () => {
				var uniformProperties = shader.Uniforms.Properties;
				var shUniforms = ScreenManager.Platform.ShaderUniforms;
				uniformProperties[_shadowRangeNear].SetValue(shUniforms.ShadowRangeNear);
				uniformProperties[_shadowRangeFar].SetValue(shUniforms.ShadowRangeFar);
				uniformProperties[_toShadowMapSpaceMatrixNear].SetValue(shUniforms.ToShadowMapSpaceMatrixNear);
				uniformProperties[_toShadowMapSpaceMatrixFar].SetValue(shUniforms.ToShadowMapSpaceMatrixFar);
			};
		}

		private static Action GenFogAndLightVertexBindings(IExtendedShaderProgram shader)
		{
			int _pointLightQuantity = shader.FindUniformLocation("pointLightQuantity");
			int _pointLights = shader.FindUniformLocation("pointLights");
			int _pointLightColors = shader.FindUniformLocation("pointLightColors");
			int _flatFogDensity = shader.FindUniformLocation("flatFogDensity");
			int _flatFogStart = shader.FindUniformLocation("flatFogStart");
			int _glitchStrengthFL = shader.FindUniformLocation("glitchStrengthFL");
			int _viewDistance = shader.FindUniformLocation("viewDistance");
			int _viewDistanceLod0 = shader.FindUniformLocation("viewDistanceLod0");
			int _nightVisonStrength = shader.FindUniformLocation("nightVisonStrength");

			return () => {
				var uniformProperties = shader.Uniforms.Properties;
				var shUniforms = ScreenManager.Platform.ShaderUniforms;
				int cnt = shUniforms.PointLightsCount;
				uniformProperties[_pointLightQuantity].SetValue(cnt);
				uniformProperties[_pointLights].SetValues(shUniforms.PointLights3, cnt);
				uniformProperties[_pointLightColors].SetValues(shUniforms.PointLightColors3, cnt);
				uniformProperties[_flatFogDensity].SetValue(shUniforms.FlagFogDensity);
				uniformProperties[_flatFogStart].SetValue(shUniforms.FlatFogStartYPos - shUniforms.PlayerPos.Y);
				uniformProperties[_glitchStrengthFL].SetValue(shUniforms.GlitchStrength);
				uniformProperties[_viewDistance].SetValue((float)ClientSettings.ViewDistance);
				uniformProperties[_viewDistanceLod0].SetValue((float)Math.Min(640, ClientSettings.ViewDistance) * ClientSettings.LodBias);
				uniformProperties[_nightVisonStrength].SetValue(shUniforms.NightVisonStrength);
			};
		}

		private static Action GenFogAndLightFragmentBindings(IExtendedShaderProgram shader)
		{
			int _zNear = shader.FindUniformLocation("zNear");
			int _zFar = shader.FindUniformLocation("zFar");
			int _lightPosition = shader.FindUniformLocation("lightPosition");
			int _shadowIntensity = shader.FindUniformLocation("shadowIntensity");

			int _shadowMapWidthInv = shader.FindUniformLocation("shadowMapWidthInv");
			int _shadowMapHeightInv = shader.FindUniformLocation("shadowMapHeightInv");
			int _viewDistance = shader.FindUniformLocation("viewDistance");
			int _viewDistanceLod0 = shader.FindUniformLocation("viewDistanceLod0");

			int _shadowMapFar = shader.FindUniformLocation("shadowMapFar");
			int _shadowMapNear = shader.FindUniformLocation("shadowMapNear");

			return () => {
				var uniformProperties = shader.Uniforms.Properties;
				var shUniforms = ScreenManager.Platform.ShaderUniforms;
				uniformProperties[_zNear].SetValue(shUniforms.ZNear);
				uniformProperties[_zFar].SetValue(shUniforms.ZFar);
				uniformProperties[_lightPosition].SetValue(shUniforms.LightPosition3D);
				uniformProperties[_shadowIntensity].SetValue(shUniforms.DropShadowIntensity);
				if(ShaderProgramBase.shadowmapQuality > 0)
				{
					FrameBufferRef farFb = ScreenManager.Platform.FrameBuffers[11];
					FrameBufferRef nearFb = ScreenManager.Platform.FrameBuffers[12];
					shader.BindTexture(_shadowMapFar, EnumTextureTarget.Texture2D, farFb.DepthTextureId);
					shader.BindTexture(_shadowMapNear, EnumTextureTarget.Texture2D, nearFb.DepthTextureId);
					uniformProperties[_shadowMapWidthInv].SetValue(1f / farFb.Width);
					uniformProperties[_shadowMapHeightInv].SetValue(1f / farFb.Height);
					uniformProperties[_viewDistance].SetValue((float)ClientSettings.ViewDistance);
					uniformProperties[_viewDistanceLod0].SetValue((float)Math.Min(640, ClientSettings.ViewDistance) * ClientSettings.LodBias);
				}
			};
		}
	}
}