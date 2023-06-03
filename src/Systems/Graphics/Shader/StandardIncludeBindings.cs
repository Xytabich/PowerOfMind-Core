using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;

namespace PowerOfMind.Graphics.Shader
{
	public static class StandardIncludeBindings
	{
		public delegate Action GenBindingsDelegate(IExtendedShaderProgram shader);

		public static readonly KeyValuePair<string, GenBindingsDelegate>[] ShaderBindingFactoryPairs = new KeyValuePair<string, GenBindingsDelegate>[] {
			new KeyValuePair<string, GenBindingsDelegate>("fogandlight.fsh", GenFogAndLightFragmentBindings),
			new KeyValuePair<string, GenBindingsDelegate>("fogandlight.vsh", GenFogAndLightVertexBindings),
			new KeyValuePair<string, GenBindingsDelegate>("shadowcoords.vsh", GenShadowCoordsVertexBindings),
			new KeyValuePair<string, GenBindingsDelegate>("vertexwarp.vsh", GenVertexWarpVertexBindings),
			new KeyValuePair<string, GenBindingsDelegate>("skycolor.fsh", GenSkyColorFragmentBindings),
			new KeyValuePair<string, GenBindingsDelegate>("colormap.vsh", GenColorMapVertexBindings)
		};

		private static Action GenColorMapVertexBindings(IExtendedShaderProgram shader)
		{
			int _colorMapRects = shader.FindUniformIndex("colorMapRects");
			int _seasonRel = shader.FindUniformIndex("seasonRel");
			int _seaLevel = shader.FindUniformIndex("seaLevel");
			int _atlasHeight = shader.FindUniformIndex("atlasHeight");
			int _seasonTemperature = shader.FindUniformIndex("seasonTemperature");

			return () => {
				var uniforms = shader.Uniforms;
				var shUniforms = ScreenManager.Platform.ShaderUniforms;
				uniforms[_colorMapRects].SetValues(shUniforms.ColorMapRects4, 160);
				uniforms[_seasonRel].SetValue(shUniforms.SeasonRel);
				uniforms[_seaLevel].SetValue(shUniforms.SeaLevel);
				uniforms[_atlasHeight].SetValue(shUniforms.BlockAtlasHeight);
				uniforms[_seasonTemperature].SetValue(shUniforms.SeasonTemperature);
			};
		}

		private static Action GenSkyColorFragmentBindings(IExtendedShaderProgram shader)
		{
			int _fogWaveCounter = shader.FindUniformIndex("fogWaveCounter");
			int _sunsetMod = shader.FindUniformIndex("sunsetMod");
			int _ditherSeed = shader.FindUniformIndex("ditherSeed");
			int _horizontalResolution = shader.FindUniformIndex("horizontalResolution");
			int _playerToSealevelOffset = shader.FindUniformIndex("playerToSealevelOffset");

			shader.FindTextureBindings("sky", out var _sky, out var _skyTex);
			shader.FindTextureBindings("glow", out var _glow, out var _glowTex);

			return () => {
				var uniforms = shader.Uniforms;
				var shUniforms = ScreenManager.Platform.ShaderUniforms;
				shader.BindTexture(_sky, EnumTextureTarget.Texture2D, shUniforms.SkyTextureId, _skyTex);
				shader.BindTexture(_glow, EnumTextureTarget.Texture2D, shUniforms.GlowTextureId, _glowTex);
				uniforms[_fogWaveCounter].SetValue(shUniforms.FogWaveCounter);
				uniforms[_sunsetMod].SetValue(shUniforms.SunsetMod);
				uniforms[_ditherSeed].SetValue(shUniforms.DitherSeed);
				uniforms[_horizontalResolution].SetValue(shUniforms.FrameWidth);
				uniforms[_playerToSealevelOffset].SetValue(shUniforms.PlayerToSealevelOffset);
			};
		}

		private static Action GenVertexWarpVertexBindings(IExtendedShaderProgram shader)
		{
			int _timeCounter = shader.FindUniformIndex("timeCounter");
			int _windWaveCounter = shader.FindUniformIndex("windWaveCounter");
			int _windWaveCounterHighFreq = shader.FindUniformIndex("windWaveCounterHighFreq");
			int _windSpeed = shader.FindUniformIndex("windSpeed");
			int _waterWaveCounter = shader.FindUniformIndex("waterWaveCounter");
			int _playerpos = shader.FindUniformIndex("playerpos");
			int _globalWarpIntensity = shader.FindUniformIndex("globalWarpIntensity");
			int _glitchStrength = shader.FindUniformIndex("glitchStrength");
			int _windWaveIntensity = shader.FindUniformIndex("windWaveIntensity");
			int _waterWaveIntensity = shader.FindUniformIndex("waterWaveIntensity");
			int _perceptionEffectId = shader.FindUniformIndex("perceptionEffectId");
			int _perceptionEffectIntensity = shader.FindUniformIndex("perceptionEffectIntensity");

			return () => {
				var uniforms = shader.Uniforms;
				var shUniforms = ScreenManager.Platform.ShaderUniforms;
				uniforms[_timeCounter].SetValue(shUniforms.TimeCounter);
				uniforms[_windWaveCounter].SetValue(shUniforms.WindWaveCounter);
				uniforms[_windWaveCounterHighFreq].SetValue(shUniforms.WindWaveCounterHighFreq);
				uniforms[_windSpeed].SetValue(shUniforms.WindSpeed);
				uniforms[_waterWaveCounter].SetValue(shUniforms.WaterWaveCounter);
				uniforms[_playerpos].SetValue(shUniforms.PlayerPos);
				uniforms[_globalWarpIntensity].SetValue(shUniforms.GlobalWorldWarp);
				uniforms[_glitchStrength].SetValue(shUniforms.GlitchStrength);
				uniforms[_windWaveIntensity].SetValue(shUniforms.WindWaveIntensity);
				uniforms[_waterWaveIntensity].SetValue(shUniforms.WaterWaveIntensity);
				uniforms[_perceptionEffectId].SetValue(shUniforms.PerceptionEffectId);
				uniforms[_perceptionEffectIntensity].SetValue(shUniforms.PerceptionEffectIntensity);
			};
		}

		private static Action GenShadowCoordsVertexBindings(IExtendedShaderProgram shader)
		{
			int _shadowRangeNear = shader.FindUniformIndex("shadowRangeNear");
			int _shadowRangeFar = shader.FindUniformIndex("shadowRangeFar");
			int _toShadowMapSpaceMatrixNear = shader.FindUniformIndex("toShadowMapSpaceMatrixNear");
			int _toShadowMapSpaceMatrixFar = shader.FindUniformIndex("toShadowMapSpaceMatrixFar");

			return () => {
				var uniforms = shader.Uniforms;
				var shUniforms = ScreenManager.Platform.ShaderUniforms;
				uniforms[_shadowRangeNear].SetValue(shUniforms.ShadowRangeNear);
				uniforms[_shadowRangeFar].SetValue(shUniforms.ShadowRangeFar);
				uniforms[_toShadowMapSpaceMatrixNear].SetValue(shUniforms.ToShadowMapSpaceMatrixNear);
				uniforms[_toShadowMapSpaceMatrixFar].SetValue(shUniforms.ToShadowMapSpaceMatrixFar);
			};
		}

		private static Action GenFogAndLightVertexBindings(IExtendedShaderProgram shader)
		{
			int _pointLightQuantity = shader.FindUniformIndex("pointLightQuantity");
			int _pointLights = shader.FindUniformIndex("pointLights");
			int _pointLightColors = shader.FindUniformIndex("pointLightColors");
			int _flatFogDensity = shader.FindUniformIndex("flatFogDensity");
			int _flatFogStart = shader.FindUniformIndex("flatFogStart");
			int _glitchStrengthFL = shader.FindUniformIndex("glitchStrengthFL");
			int _viewDistance = shader.FindUniformIndex("viewDistance");
			int _viewDistanceLod0 = shader.FindUniformIndex("viewDistanceLod0");
			int _nightVisonStrength = shader.FindUniformIndex("nightVisonStrength");

			return () => {
				var uniforms = shader.Uniforms;
				var shUniforms = ScreenManager.Platform.ShaderUniforms;
				int cnt = shUniforms.PointLightsCount;
				uniforms[_pointLightQuantity].SetValue(cnt);
				uniforms[_pointLights].SetValues(shUniforms.PointLights3, cnt);
				uniforms[_pointLightColors].SetValues(shUniforms.PointLightColors3, cnt);
				uniforms[_flatFogDensity].SetValue(shUniforms.FlagFogDensity);
				uniforms[_flatFogStart].SetValue(shUniforms.FlatFogStartYPos - shUniforms.PlayerPos.Y);
				uniforms[_glitchStrengthFL].SetValue(shUniforms.GlitchStrength);
				uniforms[_viewDistance].SetValue((float)ClientSettings.ViewDistance);
				uniforms[_viewDistanceLod0].SetValue((float)Math.Min(640, ClientSettings.ViewDistance) * ClientSettings.LodBias);
				uniforms[_nightVisonStrength].SetValue(shUniforms.NightVisonStrength);
			};
		}

		private static Action GenFogAndLightFragmentBindings(IExtendedShaderProgram shader)
		{
			int _zNear = shader.FindUniformIndex("zNear");
			int _zFar = shader.FindUniformIndex("zFar");
			int _lightPosition = shader.FindUniformIndex("lightPosition");
			int _shadowIntensity = shader.FindUniformIndex("shadowIntensity");

			int _shadowMapWidthInv = shader.FindUniformIndex("shadowMapWidthInv");
			int _shadowMapHeightInv = shader.FindUniformIndex("shadowMapHeightInv");
			int _viewDistance = shader.FindUniformIndex("viewDistance");
			int _viewDistanceLod0 = shader.FindUniformIndex("viewDistanceLod0");

			shader.FindTextureBindings("shadowMapFar", out var _shadowMapFar, out var _shadowMapFarTex);
			shader.FindTextureBindings("shadowMapNear", out var _shadowMapNear, out var _shadowMapNearTex);

			return () => {
				var uniforms = shader.Uniforms;
				var shUniforms = ScreenManager.Platform.ShaderUniforms;
				uniforms[_zNear].SetValue(shUniforms.ZNear);
				uniforms[_zFar].SetValue(shUniforms.ZFar);
				uniforms[_lightPosition].SetValue(shUniforms.LightPosition3D);
				uniforms[_shadowIntensity].SetValue(shUniforms.DropShadowIntensity);
				if(ShaderProgramBase.shadowmapQuality > 0)
				{
					FrameBufferRef farFb = ScreenManager.Platform.FrameBuffers[11];
					FrameBufferRef nearFb = ScreenManager.Platform.FrameBuffers[12];
					shader.BindTexture(_shadowMapFar, EnumTextureTarget.Texture2D, farFb.DepthTextureId, _shadowMapFarTex);
					shader.BindTexture(_shadowMapNear, EnumTextureTarget.Texture2D, nearFb.DepthTextureId, _shadowMapNearTex);
					uniforms[_shadowMapWidthInv].SetValue(1f / farFb.Width);
					uniforms[_shadowMapHeightInv].SetValue(1f / farFb.Height);
					uniforms[_viewDistance].SetValue((float)ClientSettings.ViewDistance);
					uniforms[_viewDistanceLod0].SetValue((float)Math.Min(640, ClientSettings.ViewDistance) * ClientSettings.LodBias);
				}
			};
		}
	}
}