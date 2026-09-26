#define TRACE
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using irSidekick;

namespace irSidekickProfiles;

public class appConfig : appSectionList
{
	public bool Invalid;

	public iniConfig Ini;

	public int inLineCount;

	public DateTime LastChanged;

	private const int BUFFER_SIZE = 8192;

	public bool IsVR
	{
		get
		{
			if (Ini != iniConfig.DX11)
			{
				throw new InvalidDisplayModeActionException();
			}
			if (!ContainsKey("Oculus Rift") && !ContainsKey("OpenVR"))
			{
				return ContainsKey("OpenXR");
			}
			return true;
		}
	}

	public bool IsOculus
	{
		get
		{
			if (Ini != iniConfig.DX11)
			{
				throw new InvalidDisplayModeActionException();
			}
			return ContainsKey("Oculus Rift");
		}
	}

	public bool IsOpenVR
	{
		get
		{
			if (Ini != iniConfig.DX11)
			{
				throw new InvalidDisplayModeActionException();
			}
			return ContainsKey("OpenVR");
		}
	}

	public bool IsOpenXR
	{
		get
		{
			if (Ini != iniConfig.DX11)
			{
				throw new InvalidDisplayModeActionException();
			}
			return ContainsKey("OpenXR");
		}
	}

	public iniDisplayMode DisplayMode
	{
		get
		{
			if (Ini != iniConfig.DX11)
			{
				throw new InvalidDisplayModeActionException();
			}
			if (IsOpenXR)
			{
				return iniDisplayMode.OpenXR;
			}
			if (IsOpenVR)
			{
				return iniDisplayMode.OpenVR;
			}
			if (IsOculus)
			{
				return iniDisplayMode.Oculus;
			}
			return iniDisplayMode.Monitor;
		}
		set
		{
			if (Ini != iniConfig.DX11)
			{
				throw new InvalidDisplayModeActionException();
			}
			switch (value)
			{
			case iniDisplayMode.Monitor:
				Remove("Oculus Rift");
				Remove("OpenVR");
				Remove("OpenXR");
				break;
			case iniDisplayMode.Oculus:
				Remove("OpenVR");
				Remove("OpenXR");
				ensureOculus();
				break;
			case iniDisplayMode.OpenVR:
				Remove("Oculus Rift");
				Remove("OpenXR");
				ensureOpenVR();
				break;
			case iniDisplayMode.OpenXR:
				Remove("Oculus Rift");
				Remove("OpenVR");
				ensureOpenXR();
				break;
			default:
				throw new Exception("appConfig.DisplayMode set to an unknown value");
			}
		}
	}

	public appConfig(iniConfig ini)
		: base(ini.TagID())
	{
		Ini = ini;
		Invalid = false;
		inLineCount = 0;
		LastChanged = new DateTime(2020, 1, 1);
	}

	public appConfig Clone()
	{
		appConfig appConfig2 = new appConfig(Ini);
		appConfig2.Invalid = Invalid;
		appConfig2.inLineCount = inLineCount;
		appConfig2.LastChanged = LastChanged;
		foreach (appSection value in base.Values)
		{
			Add(value.Name, value.Clone());
		}
		return appConfig2;
	}

	public void CloneFrom(appConfig source)
	{
		if (this != source)
		{
			Ini = source.Ini;
			inLineCount = source.inLineCount;
			CloneFrom((appSectionList)source);
		}
	}

	public bool IsDifferent(string name, appConfig other)
	{
		if (CountSettings() != other.CountSettings())
		{
			List<string> list = new List<string>();
			foreach (appSection value in base.Values)
			{
				if (value.Name.Equals("Adaptive", StringComparison.InvariantCultureIgnoreCase) && !SaveAdaptive())
				{
					continue;
				}
				if (!other.ContainsKey(value.Name))
				{
					list.Add("Section [" + value.Name + "] not found");
					continue;
				}
				appSection appSection2 = other[value.Name];
				if (value.Count == appSection2.Count)
				{
					continue;
				}
				foreach (appSetting value2 in value.Values)
				{
					if (!appSection2.ContainsKey(value2.Name))
					{
						list.Add(value2.tagFull);
					}
				}
				foreach (appSetting value3 in appSection2.Values)
				{
					if (!value.ContainsKey(value3.Name))
					{
						list.Add(value3.tagFull);
					}
				}
			}
			if (list.Count > 0)
			{
				TraceLog.Info("IsDifferent: Profile '" + name + "' " + Ini.iniName() + " Setting count variance");
				TraceLog.Info("IsDifferent: " + Ini.getProfileFileName(name) + " Setting variances = " + string.Join(", ", list));
				return true;
			}
		}
		foreach (appSection value4 in base.Values)
		{
			foreach (appSetting value5 in value4.Values)
			{
				try
				{
					if (tagGetValue(value4.Name, value5.Name) != other.tagGetValue(value4.Name, value5.Name))
					{
						TraceLog.Info("IsDifferent: Profile " + name + " " + value5.tagFull + "): " + tagGetValue(value4.Name, value5.Name) + " != " + other.tagGetValue(value4.Name, value5.Name));
						return true;
					}
				}
				catch
				{
				}
			}
		}
		return false;
	}

	public void allSetGFXPreset(gfxPreset preset)
	{
		tagSetGFXPreset(preset, "SkyRefreshRate");
		tagSetGFXPreset(preset, "CarDetail");
		tagSetGFXPreset(preset, "PitObjectDetail");
		tagSetGFXPreset(preset, "WeekendDetail");
		tagSetGFXPreset(preset, "GrandstandDetail");
		tagSetGFXPreset(preset, "CrowdDetail");
		tagSetGFXPreset(preset, "ObjectDetail");
		tagSetGFXPreset(preset, "ParticleDetail");
		tagSetGFXPreset(preset, "ParticlesFullRes");
		tagSetGFXPreset(preset, "MSAASamples");
		tagSetGFXPreset(preset, "MaxCarsToDraw");
		tagSetGFXPreset(preset, "MaxCarsToDrawInMirrors");
		tagSetGFXPreset(preset, "MaxPitObjsToDraw");
		tagSetGFXPreset(preset, "MaxPitObjsToDrawInMirrors");
		tagSetGFXPreset(preset, "ShadowDetail");
		tagSetGFXPreset(preset, "DynamicShadowRes");
		tagSetGFXPreset(preset, "ShadowMapType");
		tagSetGFXPreset(preset, "AllowTSOSelfShadows");
		tagSetGFXPreset(preset, "DynamicShadowMaps");
		tagSetGFXPreset(preset, "MonochromeHeadlights");
		tagSetGFXPreset(preset, "DNSMEnable");
		tagSetGFXPreset(preset, "DNSMWallsCastShadows");
		tagSetGFXPreset(preset, "DNSMNumLights");
		tagSetGFXPreset(preset, "DNSMFilter");
		tagSetGFXPreset(preset, "DNSMShadowFadeTime");
		tagSetGFXPreset(preset, "DNSMTSOsCastShadows");
		tagSetGFXPreset(preset, "DNSMDownsampleFirst");
		tagSetGFXPreset(preset, "NumDynamicCubemaps");
		tagSetGFXPreset(preset, "NumFixedCubemaps");
		tagSetGFXPreset(preset, "AntiAliasMethod");
		tagSetGFXPreset(preset, "MSAASamples");
		tagSetGFXPreset(preset, "MSAAUseFilter");
		tagSetGFXPreset(preset, "ShaderQuality");
		tagSetGFXPreset(preset, "Trilinear");
		tagSetGFXPreset(preset, "TwoPassTrees");
		tagSetGFXPreset(preset, "LowQualityTrees");
		tagSetGFXPreset(preset, "MaxCockpitMirrors");
		tagSetGFXPreset(preset, "MirrorDetail");
		tagSetGFXPreset(preset, "HeadlightLevel");
		tagSetGFXPreset(preset, "HeadlightsInMirrors");
		tagSetGFXPreset(preset, "MotionBlurStrength");
		tagSetGFXPreset(preset, "HeatHaze");
		tagSetGFXPreset(preset, "FXAAQualitySubPix");
		tagSetGFXPreset(preset, "FXAAQualityEdgeThreshold");
		tagSetGFXPreset(preset, "SSAO");
		tagSetGFXPreset(preset, "SSR");
		tagSetGFXPreset(preset, "Sharpening");
		tagSetGFXPreset(preset, "SharpeningClamp");
		tagSetGFXPreset(preset, "SharpeningAmount");
		tagSetGFXPreset(preset, "Distortion");
		tagSetGFXPreset(preset, "EnableHDR");
		tagSetGFXPreset(preset, "AutoExposure");
		tagSetGFXPreset(preset, "CarPaint2048x2048");
		tagSetGFXPreset(preset, "CacheSwap3HighResCars");
		tagSetGFXPreset(preset, "MipLODBias");
		tagSetGFXPreset(preset, "LODMinFPSTarget");
		tagSetGFXPreset(preset, "LODPctMin");
		tagSetGFXPreset(preset, "LODPctMax");
		tagSetGFXPreset(preset, "LODPctDynoMin");
		tagSetGFXPreset(preset, "LODPctDynoMax");
	}

	public void tagSetGFXPreset(gfxPreset preset, string setting)
	{
		appSection appSection2 = null;
		if (ContainsKey("Replay Graphics"))
		{
			appSection2 = base["Replay Graphics"];
		}
		try
		{
			string value = tagGetGFXPreset(preset, setting);
			tagSetValue("Graphics Options", setting, value);
			if (appSection2 != null && appSection2.existsSetting(setting))
			{
				tagSetValue("Replay Graphics", setting, value);
			}
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "SetGFXPreset failed on: " + setting);
		}
	}

	public string tagGetGFXPreset(gfxPreset preset, string setting)
	{
		return preset switch
		{
			gfxPreset.Low => tagGetGFXLow(setting), 
			gfxPreset.Faster => tagGetGFXFaster(setting), 
			gfxPreset.Standard => tagGetGFXStandard(setting), 
			gfxPreset.Pretty => tagGetGFXPretty(setting), 
			gfxPreset.High => tagGetGFXHigh(setting), 
			_ => tagGetValue("Graphics Options", setting), 
		};
	}

	public string tagGetGFXLow(string setting)
	{
		return setting switch
		{
			"AntiAliasMethod" => "0", 
			"MSAASamples" => "2", 
			"MSAAUseFilter" => "3", 
			"FSRSharpness" => "5", 
			"LimitFrameRate" => "1", 
			"SkyRefreshRate" => "0", 
			"CarDetail" => "1", 
			"PitObjectDetail" => "0", 
			"WeekendDetail" => "0", 
			"GrandstandDetail" => "0", 
			"CrowdDetail" => "0", 
			"ObjectDetail" => "0", 
			"FoliageDetail" => "0", 
			"ParticleDetail" => "0", 
			"ParticlesFullRes" => "0", 
			"MaxCarsToDraw" => "10", 
			"MaxCarsToDrawInMirrors" => "4", 
			"MaxPitObjsToDraw" => "4", 
			"MaxPitObjsToDrawInMirrors" => "0", 
			"ShadowDetail" => "0", 
			"DynamicShadowRes" => "0", 
			"StaticShadowRes" => "0", 
			"StaticShadowNumber" => "3", 
			"ShadowMapType" => "0", 
			"AllowTSOSelfShadows" => "0", 
			"DynamicShadowMaps" => "0", 
			"MonochromeHeadlights" => "1", 
			"DNSMEnable" => "0", 
			"DNSMWallsCastShadows" => "0", 
			"DNSMNumLights" => "2", 
			"DNSMFilter" => "2", 
			"DNSMShadowFadeTime" => "5", 
			"DNSMTSOsCastShadows" => "0", 
			"DNSMDownsampleFirst" => "0", 
			"NumDynamicCubemaps" => "0", 
			"NumFixedCubemaps" => "0", 
			"ShaderQuality" => "0", 
			"Trilinear" => "0", 
			"TwoPassTrees" => "0", 
			"LowQualityTrees" => "1", 
			"MaxCockpitMirrors" => "0", 
			"MirrorDetail" => "0", 
			"HeadlightLevel" => "0", 
			"HeadlightsInMirrors" => "0", 
			"MotionBlurStrength" => "0", 
			"HeatHaze" => "0", 
			"SSR" => "0.0", 
			"FXAAQualitySubPix" => "0", 
			"FXAAQualityEdgeThreshold" => "333", 
			"SSAO" => "0", 
			"Sharpening" => "0", 
			"SharpeningClamp" => "10", 
			"SharpeningAmount" => "125", 
			"Distortion" => "0", 
			"EnableHDR" => "0", 
			"AutoExposure" => "0", 
			"CarPaint2048x2048" => "0", 
			"CacheSwap3HighResCars" => "1", 
			"CompressTexturesHelmets" => "1", 
			"CompressTexturesSuits" => "1", 
			"CompressTexturesCars" => "1", 
			"CompressedVertices" => "1", 
			"MipLODBias" => "0", 
			"LODMinFPSTarget" => "100", 
			"LODPctMin" => "100", 
			"LODPctMax" => "400", 
			"LODPctDynoMin" => "100", 
			"LODPctDynoMax" => "400", 
			_ => tagGetValue("Graphics Options", setting), 
		};
	}

	public string tagGetGFXFaster(string setting)
	{
		return setting switch
		{
			"AntiAliasMethod" => "2", 
			"MSAASamples" => "2", 
			"MSAAUseFilter" => "3", 
			"FSRSharpness" => "4", 
			"LimitFrameRate" => "1", 
			"SkyRefreshRate" => "0", 
			"CarDetail" => "1", 
			"PitObjectDetail" => "3", 
			"WeekendDetail" => "2", 
			"GrandstandDetail" => "2", 
			"CrowdDetail" => "2", 
			"ObjectDetail" => "1", 
			"FoliageDetail" => "0", 
			"ParticleDetail" => "0", 
			"ParticlesFullRes" => "0", 
			"MaxCarsToDraw" => "20", 
			"MaxCarsToDrawInMirrors" => "4", 
			"MaxPitObjsToDraw" => "20", 
			"MaxPitObjsToDrawInMirrors" => "4", 
			"ShadowDetail" => "0", 
			"DynamicShadowRes" => "1", 
			"StaticShadowRes" => "1", 
			"StaticShadowNumber" => "3", 
			"ShadowMapType" => "0", 
			"AllowTSOSelfShadows" => "0", 
			"DynamicShadowMaps" => "0", 
			"MonochromeHeadlights" => "1", 
			"DNSMEnable" => "0", 
			"DNSMWallsCastShadows" => "0", 
			"DNSMNumLights" => "3", 
			"DNSMFilter" => "2", 
			"DNSMShadowFadeTime" => "5", 
			"DNSMTSOsCastShadows" => "0", 
			"DNSMDownsampleFirst" => "0", 
			"NumDynamicCubemaps" => "0", 
			"NumFixedCubemaps" => "0", 
			"ShaderQuality" => "1", 
			"Trilinear" => "0", 
			"TwoPassTrees" => "0", 
			"LowQualityTrees" => "1", 
			"MaxCockpitMirrors" => "2", 
			"MirrorDetail" => "0", 
			"HeadlightLevel" => "0", 
			"HeadlightsInMirrors" => "0", 
			"MotionBlurStrength" => "0", 
			"HeatHaze" => "0", 
			"SSR" => "0.0", 
			"FXAAQualitySubPix" => "0", 
			"FXAAQualityEdgeThreshold" => "250", 
			"SSAO" => "0", 
			"Sharpening" => "0", 
			"SharpeningClamp" => "10", 
			"SharpeningAmount" => "125", 
			"Distortion" => "0", 
			"EnableHDR" => "0", 
			"AutoExposure" => "0", 
			"CarPaint2048x2048" => "1", 
			"CacheSwap3HighResCars" => "1", 
			"CompressTexturesHelmets" => "1", 
			"CompressTexturesSuits" => "1", 
			"CompressTexturesCars" => "1", 
			"CompressedVertices" => "1", 
			"MipLODBias" => "0", 
			"LODMinFPSTarget" => "120", 
			"LODPctDynoMin" => "100", 
			"LODPctDynoMax" => "400", 
			"LODPctMin" => "100", 
			"LODPctMax" => "400", 
			_ => tagGetValue("Graphics Options", setting), 
		};
	}

	public string tagGetGFXStandard(string setting)
	{
		return setting switch
		{
			"AntiAliasMethod" => "1", 
			"MSAASamples" => "4", 
			"MSAAUseFilter" => "3", 
			"FSRSharpness" => "4", 
			"LimitFrameRate" => "1", 
			"SkyRefreshRate" => "1", 
			"CarDetail" => "2", 
			"PitObjectDetail" => "3", 
			"WeekendDetail" => "2", 
			"GrandstandDetail" => "2", 
			"CrowdDetail" => "2", 
			"ObjectDetail" => "2", 
			"FoliageDetail" => "1", 
			"ParticleDetail" => "2", 
			"ParticlesFullRes" => "1", 
			"MaxCarsToDraw" => "20", 
			"MaxCarsToDrawInMirrors" => "8", 
			"MaxPitObjsToDraw" => "20", 
			"MaxPitObjsToDrawInMirrors" => "8", 
			"ShadowDetail" => "0", 
			"DynamicShadowRes" => "2", 
			"StaticShadowRes" => "2", 
			"StaticShadowNumber" => "4", 
			"ShadowMapType" => "0", 
			"AllowTSOSelfShadows" => "0", 
			"DynamicShadowMaps" => "0", 
			"MonochromeHeadlights" => "1", 
			"DNSMEnable" => "0", 
			"DNSMWallsCastShadows" => "0", 
			"DNSMNumLights" => "2", 
			"DNSMFilter" => "2", 
			"DNSMShadowFadeTime" => "5", 
			"DNSMTSOsCastShadows" => "0", 
			"DNSMDownsampleFirst" => "0", 
			"NumDynamicCubemaps" => "0", 
			"NumFixedCubemaps" => "0", 
			"ShaderQuality" => "2", 
			"Trilinear" => "1", 
			"TwoPassTrees" => "1", 
			"LowQualityTrees" => "1", 
			"MaxCockpitMirrors" => "2", 
			"MirrorDetail" => "0", 
			"HeadlightLevel" => "0", 
			"HeadlightsInMirrors" => "0", 
			"MotionBlurStrength" => "0", 
			"HeatHaze" => "0", 
			"SSR" => "0.0", 
			"FXAAQualitySubPix" => "75", 
			"FXAAQualityEdgeThreshold" => "166", 
			"SSAO" => "0", 
			"Sharpening" => "1", 
			"SharpeningClamp" => "9", 
			"SharpeningAmount" => "125", 
			"Distortion" => "0", 
			"EnableHDR" => "0", 
			"AutoExposure" => "0", 
			"CarPaint2048x2048" => "1", 
			"CacheSwap3HighResCars" => "1", 
			"CompressTexturesHelmets" => "1", 
			"CompressTexturesSuits" => "1", 
			"CompressTexturesCars" => "1", 
			"CompressedVertices" => "1", 
			"MipLODBias" => "38", 
			"LODMinFPSTarget" => "100", 
			"LODPctDynoMin" => "75", 
			"LODPctDynoMax" => "200", 
			"LODPctMin" => "75", 
			"LODPctMax" => "200", 
			_ => tagGetValue("Graphics Options", setting), 
		};
	}

	public string tagGetGFXPretty(string setting)
	{
		return setting switch
		{
			"AntiAliasMethod" => "1", 
			"MSAASamples" => "4", 
			"MSAAUseFilter" => "1", 
			"FSRSharpness" => "3", 
			"LimitFrameRate" => "1", 
			"SkyRefreshRate" => "1", 
			"CarDetail" => "2", 
			"PitObjectDetail" => "3", 
			"WeekendDetail" => "2", 
			"GrandstandDetail" => "2", 
			"CrowdDetail" => "2", 
			"ObjectDetail" => "2", 
			"FoliageDetail" => "2", 
			"ParticleDetail" => "2", 
			"ParticlesFullRes" => "1", 
			"MaxCarsToDraw" => "30", 
			"MaxCarsToDrawInMirrors" => "8", 
			"MaxPitObjsToDraw" => "30", 
			"MaxPitObjsToDrawInMirrors" => "8", 
			"ShadowDetail" => "0", 
			"DynamicShadowRes" => "3", 
			"StaticShadowRes" => "3", 
			"StaticShadowNumber" => "5", 
			"ShadowMapType" => "1", 
			"AllowTSOSelfShadows" => "0", 
			"DynamicShadowMaps" => "0", 
			"MonochromeHeadlights" => "1", 
			"DNSMEnable" => "0", 
			"DNSMWallsCastShadows" => "0", 
			"DNSMNumLights" => "3", 
			"DNSMFilter" => "2", 
			"DNSMShadowFadeTime" => "15", 
			"DNSMTSOsCastShadows" => "0", 
			"DNSMDownsampleFirst" => "0", 
			"NumDynamicCubemaps" => "0", 
			"NumFixedCubemaps" => "0", 
			"ShaderQuality" => "3", 
			"Trilinear" => "1", 
			"TwoPassTrees" => "1", 
			"LowQualityTrees" => "1", 
			"MaxCockpitMirrors" => "2", 
			"MirrorDetail" => "0", 
			"HeadlightLevel" => "1", 
			"HeadlightsInMirrors" => "0", 
			"MotionBlurStrength" => "0", 
			"HeatHaze" => "0", 
			"SSR" => "0.1", 
			"FXAAQualitySubPix" => "50", 
			"FXAAQualityEdgeThreshold" => "125", 
			"SSAO" => "0", 
			"Sharpening" => "1", 
			"SharpeningClamp" => "9", 
			"SharpeningAmount" => "150", 
			"Distortion" => "0", 
			"EnableHDR" => "1", 
			"AutoExposure" => "0", 
			"CarPaint2048x2048" => "1", 
			"CacheSwap3HighResCars" => "1", 
			"CompressTexturesHelmets" => "1", 
			"CompressTexturesSuits" => "1", 
			"CompressTexturesCars" => "1", 
			"CompressedVertices" => "1", 
			"MipLODBias" => "38", 
			"LODMinFPSTarget" => "100", 
			"LODPctDynoMin" => "50", 
			"LODPctDynoMax" => "300", 
			"LODPctMin" => "50", 
			"LODPctMax" => "300", 
			_ => tagGetValue("Graphics Options", setting), 
		};
	}

	public string tagGetGFXHigh(string setting)
	{
		return setting switch
		{
			"AntiAliasMethod" => "1", 
			"MSAASamples" => "8", 
			"MSAAUseFilter" => "2", 
			"FSRSharpness" => "3", 
			"LimitFrameRate" => "1", 
			"SkyRefreshRate" => "2", 
			"CarDetail" => "2", 
			"PitObjectDetail" => "3", 
			"WeekendDetail" => "2", 
			"GrandstandDetail" => "2", 
			"CrowdDetail" => "2", 
			"ObjectDetail" => "2", 
			"FoliageDetail" => "3", 
			"ParticleDetail" => "2", 
			"ParticlesFullRes" => "1", 
			"MaxCarsToDraw" => "40", 
			"MaxCarsToDrawInMirrors" => "12", 
			"MaxPitObjsToDraw" => "40", 
			"MaxPitObjsToDrawInMirrors" => "12", 
			"ShadowDetail" => "1", 
			"DynamicShadowRes" => "4", 
			"StaticShadowRes" => "4", 
			"StaticShadowNumber" => "8", 
			"ShadowMapType" => "1", 
			"AllowTSOSelfShadows" => "0", 
			"DynamicShadowMaps" => "0", 
			"MonochromeHeadlights" => "1", 
			"DNSMEnable" => "1", 
			"DNSMWallsCastShadows" => "0", 
			"DNSMNumLights" => "3", 
			"DNSMFilter" => "2", 
			"DNSMShadowFadeTime" => "25", 
			"DNSMTSOsCastShadows" => "1", 
			"DNSMDownsampleFirst" => "1", 
			"NumDynamicCubemaps" => "0", 
			"NumFixedCubemaps" => "0", 
			"ShaderQuality" => "3", 
			"Trilinear" => "1", 
			"TwoPassTrees" => "1", 
			"LowQualityTrees" => "0", 
			"MaxCockpitMirrors" => "3", 
			"MirrorDetail" => "1", 
			"HeadlightLevel" => "2", 
			"HeadlightsInMirrors" => "1", 
			"MotionBlurStrength" => "0", 
			"HeatHaze" => "1", 
			"SSR" => "0.2", 
			"FXAAQualitySubPix" => "50", 
			"FXAAQualityEdgeThreshold" => "100", 
			"SSAO" => "0", 
			"Sharpening" => "1", 
			"SharpeningClamp" => "9", 
			"SharpeningAmount" => "175", 
			"Distortion" => "0", 
			"EnableHDR" => "1", 
			"AutoExposure" => "1", 
			"CarPaint2048x2048" => "1", 
			"CacheSwap3HighResCars" => "1", 
			"CompressTexturesHelmets" => "1", 
			"CompressTexturesSuits" => "1", 
			"CompressTexturesCars" => "0", 
			"CompressedVertices" => "1", 
			"MipLODBias" => "40", 
			"LODMinFPSTarget" => "120", 
			"LODPctDynoMin" => "25", 
			"LODPctDynoMax" => "400", 
			"LODPctMin" => "25", 
			"LODPctMax" => "400", 
			_ => tagGetValue("Graphics Options", setting), 
		};
	}

	public string tagGetValue(iniSetting setting)
	{
		return tagGetValue(setting.Section, setting.Name);
	}

	public string tagGetValue(string section, string setting)
	{
		try
		{
			switch (setting)
			{
			case "cockpitLookAngle":
			case "cockpitLookUpAngle":
			case "cockpitLookDownAngle":
				return ValueToNumber(base[section][setting].Value);
			case "raceLineWidth":
				return base[section][setting].Value.ToDouble().ToString4Storage(1);
			case "autoForceFactor":
				return base[section][setting].Value.ToDouble().ToString4Storage(2);
			case "NumFixedCubemaps":
			case "NumDynamicCubemaps":
				return (base[section][setting].Value.ParseUSInt() / 100).FormatUSInt();
			case "virtualMirrorFOV":
				return base[section][setting].Value.ToDouble().ToString4Storage(0);
			case "drivingCamFOV":
				return base[section][setting].Value.ToDouble().ToString4Storage(0);
			case "trueForceDamperPct":
				return Value2IntPercent(base[section][setting].Value);
			case "RefreshRate":
				return ValueToNumber(base[section][setting].Value);
			case "Resolution":
				return getDisplayResolution(section);
			case "display":
				return getSPCCDisplay(section);
			case "DiagonalWidth":
				return getDiagonalWidth();
			case "HighQualityTrees":
				return (base[section]["LowQualityTrees"].Value == "0") ? "1" : "0";
			case "LODPct":
				return base[section]["LODPctMax"].Value + "." + base[section]["LODPctMin"].Value;
			case "LODPctDyno":
				return base[section]["LODPctDynoMax"].Value + "." + base[section]["LODPctDynoMin"].Value;
			case "SSR":
				return base[section]["SSRRainOnly"].Value + "." + base[section]["SSRLevel"].Value;
			default:
				return base[section][setting].Value;
			}
		}
		catch (KeyNotFoundException innerException)
		{
			throw new KeyNotFoundException("Section '" + section + "' or setting '" + setting + "' not found", innerException);
		}
	}

	public void tagSetValue(iniSetting setting, string value)
	{
		tagSetValue(setting.Section, setting.Name, value);
	}

	public void tagSetValue(string section, string setting, string value)
	{
		switch (setting)
		{
		case "trueForceDamperPct":
			value = (value.ToDouble() / 100.0).ToString4Storage();
			break;
		case "raceLineWidth":
			value = value.ToDouble().ToString("F1", CultureInfo.InvariantCulture);
			break;
		case "NumFixedCubemaps":
		case "NumDynamicCubemaps":
			value = (value.ToInt() * 100).FormatUSInt();
			break;
		case "cockpitLookAngle":
		case "cockpitLookUpAngle":
		case "cockpitLookDownAngle":
			value = ValueToNumber(value);
			break;
		case "autoForceFactor":
			value = value.ToDouble().ToString4Storage();
			break;
		case "virtualMirrorFOV":
		case "drivingCamFOV":
			value = value.ToDouble().ToString4Storage(0);
			break;
		case "RefreshRate":
			value = ValueToNumber(value);
			break;
		case "display":
			switch (value)
			{
			default:
				tagSetValue(section, "voice", "1");
				tagSetValue(section, "text", "1");
				break;
			case "1":
				tagSetValue(section, "voice", "1");
				tagSetValue(section, "text", "0");
				break;
			case "2":
				tagSetValue(section, "voice", "0");
				tagSetValue(section, "text", "1");
				break;
			}
			return;
		case "DiagonalWidth":
			setDiagonalWidth(value);
			break;
		case "HighQualityTrees":
			tagSetValue(section, "LowQualityTrees", (value == "0") ? "1" : "0");
			return;
		case "Resolution":
			return;
		case "LODPct":
			switch (value)
			{
			case "400.25":
				tagSetValue(section, "LODPctMirrorsMax", "400");
				tagSetValue(section, "LODPctMirrorsMin", "25");
				tagSetValue(section, "LODPctMax", "400");
				tagSetValue(section, "LODPctMin", "25");
				break;
			case "300.50":
				tagSetValue(section, "LODPctMirrorsMax", "300");
				tagSetValue(section, "LODPctMirrorsMin", "50");
				tagSetValue(section, "LODPctMax", "300");
				tagSetValue(section, "LODPctMin", "50");
				break;
			case "200.75":
				tagSetValue(section, "LODPctMirrorsMax", "200");
				tagSetValue(section, "LODPctMirrorsMin", "75");
				tagSetValue(section, "LODPctMax", "200");
				tagSetValue(section, "LODPctMin", "75");
				break;
			case "400.100":
				tagSetValue(section, "LODPctMirrorsMax", "500");
				tagSetValue(section, "LODPctMirrorsMin", "100");
				tagSetValue(section, "LODPctMax", "400");
				tagSetValue(section, "LODPctMin", "100");
				break;
			case "100.25":
				tagSetValue(section, "LODPctMirrorsMax", "100");
				tagSetValue(section, "LODPctMirrorsMin", "25");
				tagSetValue(section, "LODPctMax", "100");
				tagSetValue(section, "LODPctMin", "25");
				break;
			case "50.50":
				tagSetValue(section, "LODPctMirrorsMax", "200");
				tagSetValue(section, "LODPctMirrorsMin", "200");
				tagSetValue(section, "LODPctMax", "50");
				tagSetValue(section, "LODPctMin", "50");
				break;
			case "100.100":
				tagSetValue(section, "LODPctMirrorsMax", "100");
				tagSetValue(section, "LODPctMirrorsMin", "100");
				tagSetValue(section, "LODPctMax", "100");
				tagSetValue(section, "LODPctMin", "100");
				break;
			}
			return;
		case "LODPctDyno":
			switch (value)
			{
			case "400.25":
				tagSetValue(section, "LODPctDynoMirrorsMax", "500");
				tagSetValue(section, "LODPctDynoMirrorsMin", "25");
				tagSetValue(section, "LODPctDynoMax", "400");
				tagSetValue(section, "LODPctDynoMin", "25");
				break;
			case "300.50":
				tagSetValue(section, "LODPctDynoMirrorsMax", "300");
				tagSetValue(section, "LODPctDynoMirrorsMin", "50");
				tagSetValue(section, "LODPctDynoMax", "300");
				tagSetValue(section, "LODPctDynoMin", "50");
				break;
			case "200.75":
				tagSetValue(section, "LODPctDynoMirrorsMax", "200");
				tagSetValue(section, "LODPctDynoMirrorsMin", "75");
				tagSetValue(section, "LODPctDynoMax", "200");
				tagSetValue(section, "LODPctDynoMin", "75");
				break;
			case "400.100":
				tagSetValue(section, "LODPctDynoMirrorsMax", "500");
				tagSetValue(section, "LODPctDynoMirrorsMin", "100");
				tagSetValue(section, "LODPctDynoMax", "400");
				tagSetValue(section, "LODPctDynoMin", "100");
				break;
			case "100.25":
				tagSetValue(section, "LODPctDynoMirrorsMax", "100");
				tagSetValue(section, "LODPctDynoMirrorsMin", "25");
				tagSetValue(section, "LODPctDynoMax", "100");
				tagSetValue(section, "LODPctDynoMin", "25");
				break;
			case "50.50":
				tagSetValue(section, "LODPctDynoMirrorsMax", "200");
				tagSetValue(section, "LODPctDynoMirrorsMin", "200");
				tagSetValue(section, "LODPctDynoMax", "50");
				tagSetValue(section, "LODPctDynoMin", "50");
				break;
			case "100.100":
				tagSetValue(section, "LODPctDynoMirrorsMax", "100");
				tagSetValue(section, "LODPctDynoMirrorsMin", "100");
				tagSetValue(section, "LODPctDynoMax", "100");
				tagSetValue(section, "LODPctDynoMin", "100");
				break;
			}
			return;
		case "SSR":
			switch (value)
			{
			case "0.0":
				tagSetValue(section, "SSRRainOnly", "0");
				tagSetValue(section, "SSRLevel", "0");
				break;
			case "0.1":
				tagSetValue(section, "SSRRainOnly", "0");
				tagSetValue(section, "SSRLevel", "1");
				break;
			case "0.2":
				tagSetValue(section, "SSRRainOnly", "0");
				tagSetValue(section, "SSRLevel", "2");
				break;
			case "1.1":
				tagSetValue(section, "SSRRainOnly", "1");
				tagSetValue(section, "SSRLevel", "1");
				break;
			case "1.2":
				tagSetValue(section, "SSRRainOnly", "1");
				tagSetValue(section, "SSRLevel", "2");
				break;
			}
			return;
		}
		try
		{
			base[section][setting].Value = value;
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "appConfig.tagSetValue(" + section + ", " + setting + ", " + value + ") encountered an error");
			return;
		}
		if (section.Equals("Graphics Options") && (setting == "VirtualMirrors" || setting == "LowQualityTrees"))
		{
			try
			{
				base["Replay Graphics"][setting].Value = value;
			}
			catch
			{
			}
		}
	}

	public string tagGetDisplay(string section, string setting, string input = null)
	{
		string text = ((input == null) ? tagGetValue(section, setting) : input);
		switch (setting)
		{
		case "VRMode":
			return Value2VRMode(text);
		case "RiftEnabled":
			return Value2Bool(text);
		case "OpenVREnabled":
			return Value2Bool(text);
		case "OpenXREnabled":
			return Value2Bool(text);
		case "FullyWaitForSync":
			return Value2Bool(text);
		case "ShowSpotterUIForSpectators":
			return Value2Bool(text);
		case "soundUiControlsInDb":
			return ValueUiDb(text);
		case "pauseReplayOnExit":
			return Value2Bool(text);
		case "AutoExposure":
			return Value2Bool(text);
		case "AutoCfgCompleted":
			return Value2Bool(text);
		case "loadAsetecAPI":
			return Value2Bool(text);
		case "loadConspitAPI":
			return Value2Bool(text);
		case "loadFanatecAPI":
			return Value2Bool(text);
		case "loadMozaAPI":
			return Value2Bool(text);
		case "loadSimagicAPI":
			return Value2Bool(text);
		case "loadSimucubeAPI":
			return Value2Bool(text);
		case "loadTrueForceAPI":
			return Value2Bool(text);
		case "enableTrueForceVibe":
			return Value2Bool(text);
		case "trueForceVibePhysics":
			return Value2Bool(text);
		case "trueForceDamperPct":
			return text + "%";
		case "enableFFB360HzInterpolated":
			return Value2Bool(text);
		case "enableWheelDisplay":
			return Value2Bool(text);
		case "enableWheelDisplayBlink":
			return Value2Bool(text);
		case "joyEnableVibrateWheelWithPedal":
			return Value2Bool(text);
		case "hushDuration":
			return ValueToNumber(text);
		case "reportNewLeaderEnabled":
		case "reportLapsLeaderLapTimeEnabled":
		case "reportGainingLosingEnabled":
		case "reportFasterCarBehindEnabled":
		case "reportPitboxCount":
		case "reportCompetitorPitEnabled":
		case "reportLapsNewBestEnabled":
		case "reportLapsNewBestLapTimeEnabled":
		case "reportLapsNewPersonalBestEnabled":
		case "reportLapsNewPersonalBestInRaceEnabled":
			return Value2Bool(text);
		case "LoadTexturesWhenDriving":
			return Value2Bool(text);
		case "StaticShadowNumber":
			return ValueToNumber(text);
		case "smoothingFilterType":
			return ValueToFFBSmoothingFilterType(text);
		case "ambientMusicDisabled":
			return Value2Bool(text);
		case "ZBuffer32Bits":
			return Value2Bool(text);
		case "reportFuelData":
			return Value2Bool(text);
		case "autoFuelDefaultEnable":
			return Value2Bool(text);
		case "autoFuelDefaultMarginLaps":
			return text.ToDouble().ToString4Display(1);
		case "drivingCamFOV":
			return ValueToNumber(text);
		case "raceLineWidth":
			return text.ToDouble().ToString4Display(1);
		case "carLowHiPadding":
			return text.ToDouble().ToString4Display(2);
		case "driverHeightAdj":
			return text.ToDouble().ToString4Display(3);
		case "DrivingVanishY":
			return text.ToDouble().ToString4Display(3);
		case "cockpitLookInstant":
			return Value2LookTransition(text);
		case "cockpitLookAngle":
			return ValueToNumber(text);
		case "cockpitLookUpAngle":
			return ValueToNumber(text);
		case "cockpitLookDownAngle":
			return ValueToNumber(text);
		case "enableLFEBKAmpCut":
			return Value2Bool(text);
		case "EnableTicker":
			return Value2Bool(text);
		case "highContrastCursor":
			return Value2Bool(text);
		case "pitLineAlwaysVisible":
			return Value2Bool(text);
		case "CompressedVertices":
			return Value2Bool(text);
		case "CompressTexturesCars":
			return Value2Bool(text);
		case "CompressTexturesSuits":
			return Value2Bool(text);
		case "CompressTexturesHelmets":
			return Value2Bool(text);
		case "NvReflexMode":
			return Value2Reflex(text);
		case "Resolution":
			return getDisplayResolution(section);
		case "SkyRefreshRate":
			return ValueToLowMediumHigh(text);
		case "CarDetail":
			return ValueToLowMediumHigh(text);
		case "PitObjectDetail":
			return ValueToOffLowMediumHigh(text);
		case "WeekendDetail":
			return ValueToLowMediumHigh(text);
		case "GrandstandDetail":
			return ValueToLowMediumHigh(text);
		case "CrowdDetail":
			return ValueToOffLowMediumHigh(text);
		case "ObjectDetail":
			return ValueToOffLowHigh(text);
		case "ParticleDetail":
			return ValueToLowMediumHigh(text);
		case "ParticlesFullRes":
			return Value2Bool(text);
		case "VerticalSync":
			return Value2Bool(text);
		case "connect_sockets":
			return Value2Bool(text);
		case "irsdkEnableMem":
			return Value2Bool(text);
		case "AntiAliasMethod":
			return ValueToAntiAliasMethod(text);
		case "MSAASamples":
			return ValueToMSAASamples(text);
		case "MSAAUseFilter":
			return ValueToMSAAUseFilter(text);
		case "ShadowMapType":
			return ValueToShadowMap(text);
		case "AllowTSOSelfShadows":
			return Value2Bool(text);
		case "DynamicShadowMaps":
			return ValueToDynamicShadowMaps(text);
		case "DNSMEnable":
			return Value2Bool(text);
		case "DNSMWallsCastShadows":
			return Value2Bool(text);
		case "DNSMHeadlightsCastShadows":
			return Value2Bool(text);
		case "DSNMNumLights":
			return ValueToNumber(text);
		case "DNSMFilter":
			return ValueToDNSMFilter(text);
		case "ShaderQuality":
			return ValueToShader(text);
		case "TwoPassTrees":
			return Value2Bool(text);
		case "LowQualityTrees":
			return Value2Bool(text);
		case "HighQualityTrees":
			return Value2Bool(text);
		case "MaxCockpitMirrors":
			return ValueToNumber(text);
		case "MirrorDetail":
			return ValueToLowHigh(text);
		case "HeadlightLevel":
			return ValueToHeadlights(text);
		case "HeadlightsInMirrors":
			return Value2Bool(text);
		case "virtualMirrorFOV":
			return ValueToNumber(text);
		case "VirtualMirrors":
			return Value2Bool(text);
		case "VirtualMirrorSize":
			return ValueToVMirrorSize(text);
		case "MotionBlurStrength":
			return ValueToOffLowMediumHigh(text);
		case "MotionBlurDrivingCams":
			return Value2Bool(text);
		case "MotionBlurBroadcastCams":
			return Value2Bool(text);
		case "HeatHaze":
			return Value2Bool(text);
		case "SSAO":
			return Value2Bool(text);
		case "Sharpening":
			return Value2Bool(text);
		case "Distortion":
			return Value2Bool(text);
		case "EnableHDR":
			return Value2Bool(text);
		case "CacheSwap3HighResCars":
			return Value2Bool(text);
		case "CarPaint2048x2048":
			return Value2Bool(text);
		case "hideCarNum":
			return Value2Bool(text);
		case "MonochromeHeadlights":
			return Value2Bool(text);
		case "DepthOfField":
			return Value2Bool(text);
		case "ReplayRenderModes":
			return Value2Bool(text);
		case "UIScalePct":
			return ValueToNumber(text);
		case "HDRFormat":
			return Value2Bool(text);
		case "EnableSPS":
			return Value2Bool(text);
		case "windowedAlignment":
			return ValueToAlign(text);
		case "GammaAdj":
			return ValueToNumber(text);
		case "BrightnessAdj":
			return ValueToNumber(text);
		case "Border":
			return Value2Bool(text);
		case "fullScreen":
			return Value2Bool(text);
		case "windowedWidth":
			return ValueToNumber(text);
		case "windowedHeight":
			return ValueToNumber(text);
		case "RefreshRate":
			return ValueToNumber(text);
		case "MonitorWidth":
			return ValueToNumber(text);
		case "ScreenWidth":
			return ValueToNumber(text);
		case "ScreenAngles":
			return ValueToNumber(text);
		case "NumMonitors":
			return getDisplayNumMonitors(section);
		case "EnableSMPSurround":
			return Value2Bool(text);
		case "RenderViewPerMonitor":
			return Value2Bool(text);
		case "NumMultiGPUs":
			return ValueToNumber(text);
		case "autoForceFactor":
			return text.ToDouble().ToString4Display(2);
		case "SessionUITransparency":
			return text.ToDouble().ToString4Display(2);
		case "DriveUITransparency":
			return text.ToDouble().ToString4Display(2);
		case "ghostCarTransp":
			return text.ToDouble().ToString4Display(2);
		case "ghostCarOffsetSec":
			return text.ToDouble().ToString4Display(2);
		case "spoolingEnabled":
			return Value2Bool(text);
		case "askToSaveOnQuit":
			return Value2Bool(text);
		case "autoResetFastRepair":
			return Value2Bool(text);
		case "autoResetPitBox":
			return Value2Bool(text);
		case "vidCaptureEnable":
			return Value2Bool(text);
		case "disableAtRaceStart":
			return Value2Bool(text);
		case "fadeGhostCarWhenClose":
			return Value2Bool(text);
		case "screenshotFileFormat":
			return ValueToScreenshotFormat(text);
		case "videoFileFrmt":
			return ValueToVideoFormat(text);
		case "videoFramerate":
			return ValueToVideoFramerate(text);
		case "videoImgSize":
			return ValueToVideoResolution(text);
		case "radioScriptsEnabled":
			return Value2Bool(text);
		case "showJoinLeave":
			return Value2Bool(text);
		case "showSysMessagesWhileDriving":
			return Value2Bool(text);
		case "showUserMessagesWhileDriving":
			return Value2Bool(text);
		case "showIncidentMessagesWhileDriving":
			return Value2Bool(text);
		case "LimitFrameRate":
			return Value2Bool(text);
		case "dimensions":
			return ValueToDimensions(text);
		case "voiceChatNotificationStyle":
			return ValueToNotification(text);
		case "enabled":
			return Value2Bool(text);
		case "muteSpotterIfLive":
			return Value2Bool(text);
		case "reduceVerbosityIfLive":
			return Value2Bool(text);
		case "carLowHiAtStart":
			return Value2Bool(text);
		case "LFEEnabled":
			return Value2Bool(text);
		case "voiceChatMuted":
			return Value2Bool(text);
		case "voiceChatEnabled":
			return Value2Bool(text);
		case "rotateWithHeadset":
			return Value2Bool(text);
		case "reportLapsEnabled":
			return Value2Bool(text);
		case "voiceChatEnabledWhileDriving":
			return Value2Bool(text);
		case "downshiftProtectionNoiseEnabled":
			return Value2Bool(text);
		case "reportLapsMode_n":
			return ValueToReportLapsMode(text);
		case "HideCockpitObstructions":
			return ValueToObstructions(text);
		case "FoliageDetail":
			return ValueToFoliageDetail(text);
		case "verbosity":
			return ValueToLowMediumHigh(text);
		case "display":
			return ValueToSPCCDisplay(text);
		case "reportLapsMinute":
			return Value2Bool(text);
		case "forceCrowdVisible":
			return Value2Bool(text);
		case "ForceVisibleWhenMove":
			return Value2Bool(text);
		case "TrackDisplacementEnable":
			return Value2Bool(text);
		case "ParallelSorting":
			return Value2Bool(text);
		case "blackBox":
			return ValueToDriveBlackBox(text);
		case "blackBoxPitStop":
			return ValueToPitBlackBox(text);
		case "DriveUIFullScreen":
			return Value2Bool(text);
		case "SysMemToUseMB":
			return ValueToGB(text);
		case "VidMemToUseMB":
			return ValueToGB(text);
		case "MaxCarsToDraw":
			return Value2DrawCars(text, tagGetValue(section, "MaxCarsToDrawInMirrors"));
		case "MaxCarsToDrawInMirrors":
			return Value2DrawCars(tagGetValue(section, "MaxCarsToDraw"), text);
		case "MaxPitObjsToDraw":
			return Value2DrawPits(text, tagGetValue(section, "MaxPitObjsToDrawInMirrors"));
		case "MaxPitObjsToDrawInMirrors":
			return Value2DrawPits(tagGetValue(section, "MaxPitObjsToDraw"), text);
		case "LODPct":
		case "LODPctDyno":
			return Value2LOD(text);
		case "DynamicShadowRes":
			return Value2ShadowRes(text);
		case "StaticShadowRes":
			return Value2ShadowRes(text);
		case "FXAAQualityEdgeThreshold":
			return Value2FXAAEdge(text);
		case "FXAAQualitySubPix":
			return Value2FXAASubPix(text);
		case "NumFixedCubemaps":
		case "NumDynamicCubemaps":
			return tagGetValue(section, setting);
		case "DriverHands":
			return Value2SteeringWheel(tagGetValue(section, "SteeringWheel"), text);
		case "SteeringWheel":
			return Value2SteeringWheel(text, tagGetValue(section, "DriverHands"));
		case "ResolutionScaling":
			return ValueToResScale(text);
		case "SSR":
			return ValueToSSR(text);
		case "clutchLaunchMode":
			return ValueToClutchLaunchMode(text);
		case "earProtection":
			return ValueToEarProtection(text);
		case "compressorReplay":
			return ValueToCompressorReplay(text);
		default:
			return text;
		}
	}

	internal Rect getDisplayBounds(string section)
	{
		Rect result = default(Rect);
		string text = tagGetValue(section, "fullScreen");
		if (!(text == "0"))
		{
			if (text == "1")
			{
				result.Y = 0.0;
				result.X = 0.0;
				result.Width = tagGetValue(section, "fullScreenWidth").ToInt();
				result.Height = tagGetValue(section, "fullScreenHeight").ToInt();
				return result;
			}
			return result;
		}
		result.Y = tagGetValue(section, "windowedYPos").ToInt();
		result.X = tagGetValue(section, "windowedXPos").ToInt();
		result.Width = tagGetValue(section, "windowedWidth").ToInt();
		result.Height = tagGetValue(section, "windowedHeight").ToInt();
		return result;
	}

	private string getDisplayNumMonitors(string section)
	{
		return (tagGetValue(section, "NumMonitors") + "." + tagGetValue(section, "MonitorType")) switch
		{
			"1.0" => "1 Single flat", 
			"3.0" => "3 Triple flat", 
			"1.1" => "1 Single curved", 
			"3.1" => "3 Triple curved", 
			_ => "", 
		};
	}

	private string getDiagonalWidth()
	{
		if (tagGetValue("MonitorSetup", "NumMonitors").ToInt() < 3)
		{
			return "0";
		}
		int num = tagGetValue("MonitorSetup", "MonitorWidth").ToInt();
		if (num == 0)
		{
			return "0";
		}
		double num2 = tagGetValue("MonitorSetup", "ScreenAngles").ToDouble();
		if (num2 == 0.0)
		{
			return "0";
		}
		try
		{
			double num3 = 2.0 * (double)(num * num);
			double d = (180.0 - num2) * Math.PI / 180.0;
			return Math.Sqrt(num3 - num3 * Math.Cos(d)).Round1().ToIntStr();
		}
		catch
		{
			return "0";
		}
	}

	private void setDiagonalWidth(string value)
	{
		double num = value.ToDouble();
		if (num == 0.0 || tagGetValue("MonitorSetup", "NumMonitors").ToInt() < 3)
		{
			return;
		}
		int num2 = tagGetValue("MonitorSetup", "MonitorWidth").ToInt();
		if (num2 == 0)
		{
			return;
		}
		try
		{
			double num3 = 2.0 * (double)num2 * (double)num2;
			double num4 = num * num;
			double num5 = Math.Acos((num3 - num4) / num3);
			double value2 = 180.0 - num5 * (180.0 / Math.PI);
			tagSetValue("MonitorSetup", "ScreenAngles", value2.Round1().ToIntStr());
		}
		catch
		{
		}
	}

	private string getDisplayResolution(string section)
	{
		string text = tagGetValue(section, "fullScreen");
		string text2;
		string text3;
		if (!(text == "0"))
		{
			if (!(text == "1"))
			{
				return "";
			}
			text2 = tagGetValue(section, "fullScreenWidth");
			text3 = tagGetValue(section, "fullScreenHeight");
		}
		else
		{
			text2 = tagGetValue(section, "windowedWidth");
			text3 = tagGetValue(section, "windowedHeight");
		}
		return text2 + "*" + text3;
	}

	private string getSPCCDisplay(string section)
	{
		return (tagGetValue(section, "voice") + "." + tagGetValue(section, "text")) switch
		{
			"1.0" => "1", 
			"0.1" => "2", 
			_ => "0", 
		};
	}

	private string ValueToClutchLaunchMode(string value)
	{
		if (!(value == "0"))
		{
			if (value == "1")
			{
				return "Hold on to activate";
			}
			return value.IfNullOrEmpty("Null");
		}
		return "Release to activate";
	}

	private string ValueToSSR(string value)
	{
		return value switch
		{
			"0.0" => "SSR Off", 
			"1.1" => "SSR Low Rain Only", 
			"1.2" => "SSR High Rain Only", 
			"0.1" => "SSR Low Always", 
			"0.2" => "SSR High Always", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToDriveBlackBox(string value)
	{
		return value switch
		{
			"-1" => "None", 
			"0" => "Timing", 
			"1" => "Standing", 
			"2" => "Relative", 
			"3" => "Fuel", 
			"4" => "Graphic Adjust", 
			"5" => "Radio", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToPitBlackBox(string value)
	{
		return value switch
		{
			"-1" => "None", 
			"0" => "Fuel", 
			"1" => "Tyres", 
			"2" => "Tyre Info", 
			"3" => "Pit Adjust", 
			"4" => "Car Adjust", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToDimensions(string value)
	{
		return value switch
		{
			"1" => "Mono", 
			"2" => "Stereo", 
			"3" => "Surround", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToNotification(string value)
	{
		return value switch
		{
			"0" => "None", 
			"1" => "Small", 
			"2" => "Large", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string Value2SteeringWheel(string wheel, string hands)
	{
		string text = wheel + "." + hands;
		return text switch
		{
			"0.0" => "Hide Steering Wheel", 
			"1.1" => "Show Driver Arms", 
			"1.0" => "Show Steering Wheel", 
			"2.0" => "Static Steering Wheel", 
			"3.0" => "Show Wheel if has display", 
			_ => text.IfNullOrEmpty("Null"), 
		};
	}

	private string Value2ShadowRes(string value)
	{
		return value switch
		{
			"0" => "512x512", 
			"1" => "1024x1024", 
			"2" => "2048x2048", 
			"3" => "4096x4096", 
			"4" => "8192x8192", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string Value2FXAAEdge(string value)
	{
		return value switch
		{
			"333" => "333 Too little (Fast)", 
			"250" => "250 Low Quality", 
			"166" => "166 Default", 
			"125" => "125 High Quality", 
			"63" => "63 Overkill (Slow)", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string Value2FXAASubPix(string value)
	{
		return value switch
		{
			"100" => "100 Soft", 
			"75" => "75 Default", 
			"50" => "50 Sharp", 
			"25" => "25 Low", 
			"0" => "0 Off", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string Value2DrawCars(string cars, string mirrors)
	{
		string text = cars + "." + mirrors;
		if (!(text == "64.64"))
		{
			if (text == "10.4")
			{
				return "Draw Min Cars";
			}
			return "Draw " + cars + " (" + mirrors + ") Cars";
		}
		return "Draw All Cars";
	}

	private string Value2DrawPits(string cars, string mirrors)
	{
		return (cars + "." + mirrors) switch
		{
			"64.64" => "Draw All Pits", 
			"10.4" => "Draw Min Pits", 
			"0.0" => "Only my pit box", 
			_ => "Draw " + cars + " (" + mirrors + ") Pits", 
		};
	}

	private string Value2LOD(string value)
	{
		return value switch
		{
			"400.25" => "Maximum", 
			"300.50" => "Medium", 
			"200.75" => "Minimum", 
			"400.100" => "Decrease", 
			"100.25" => "Increase", 
			"50.50" => "Stable", 
			"100.100" => "Off", 
			_ => "Custom (" + value + ")", 
		};
	}

	private string ValueToSPCCDisplay(string value)
	{
		return value switch
		{
			"0" => "Voice & Text", 
			"1" => "Voice", 
			"2" => "Text", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToFoliageDetail(string value)
	{
		return value switch
		{
			"0" => "Off", 
			"1" => "Low Detail", 
			"2" => "Medium Detail", 
			"3" => "High Detail", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToObstructions(string value)
	{
		return value switch
		{
			"0" => "None", 
			"1" => "Cockpit Halo", 
			"2" => "Pillar/Rockcage", 
			"3" => "Hide All", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToDynamicShadowMaps(string value)
	{
		return value switch
		{
			"0" => "Off", 
			"1" => "Main", 
			"2" => "Main & Mirrors", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToReportLapsMode(string value)
	{
		if (!(value == "0"))
		{
			if (value == "1")
			{
				return "Avg Speed";
			}
			return value.IfNullOrEmpty("Null");
		}
		return "Time";
	}

	private string ValueToGB(string value)
	{
		return value.ToInt().ToGB();
	}

	private string ValueToAlign(string value)
	{
		return value switch
		{
			"0" => "None", 
			"1" => "Center", 
			"2" => "Top Left", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToAniso(string value)
	{
		if (value == "1")
		{
			return "Off";
		}
		return value.IfNullOrEmpty("Null");
	}

	private string ValueToShader(string value)
	{
		return value switch
		{
			"0" => "Low", 
			"1" => "Meduim", 
			"2" => "High", 
			"3" => "Max", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToVideoFormat(string value)
	{
		return value switch
		{
			"0" => "MP4", 
			"1" => "WMV", 
			"2" => "AVI2", 
			"3" => "AVI", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToVideoFramerate(string value)
	{
		if (!(value == "0"))
		{
			if (value == "1")
			{
				return "30 fps";
			}
			return value.IfNullOrEmpty("Null");
		}
		return "60 fps";
	}

	private string ValueToVideoResolution(string value)
	{
		return value switch
		{
			"0" => "AUTO", 
			"1" => "1080p", 
			"2" => "720p", 
			"3" => "480p", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToScreenshotFormat(string value)
	{
		return value switch
		{
			"0" => "PNG", 
			"1" => "JPG", 
			"2" => "BMP", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToVMirrorSize(string value)
	{
		return value switch
		{
			"0" => "Large", 
			"1" => "Medium", 
			"2" => "Small", 
			"3" => "Extra Small", 
			"4" => "Even Smaller", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToHeadlights(string value)
	{
		return value switch
		{
			"-1" => "Disabled", 
			"0" => "Low", 
			"1" => "Medium", 
			"2" => "High", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToDNSMFilter(string value)
	{
		return value switch
		{
			"0" => "None", 
			"1" => "Simple", 
			"2" => "PCF4", 
			"3" => "PCF4P", 
			"4" => "PCF8P", 
			"5" => "PCF16P", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToAntiAliasMethod(string value)
	{
		return value switch
		{
			"0" => "Off", 
			"1" => "MSAA", 
			"2" => "FXAA", 
			"3" => "SMAA", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToMSAASamples(string value)
	{
		return value.IfNullOrEmpty("Null");
	}

	private string ValueToMSAAUseFilter(string value)
	{
		return value switch
		{
			"0" => "Soft", 
			"1" => "Neutral", 
			"2" => "Sharp", 
			"3" => "Simple", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToShadowMap(string value)
	{
		switch (value)
		{
		case "0":
			return "Off";
		case "1":
		case "2":
		case "3":
			return "On";
		default:
			return value.IfNullOrEmpty("Null");
		}
	}

	private string Value2Reflex(string value)
	{
		return value switch
		{
			"0" => "Off", 
			"1" => "On", 
			"2" => "On + Boost", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string Value2LookTransition(string value)
	{
		if (!(value == "1"))
		{
			if (value == "0")
			{
				return "Smooth";
			}
			return value.IfNullOrEmpty("Null");
		}
		return "Instant";
	}

	private string Value2VRMode(string value)
	{
		return value switch
		{
			"0" => "Normal", 
			"1" => "NV Single pass stereo", 
			"2" => "NV Foveated quad view", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string Value2Bool(string value)
	{
		if (value.ToBool())
		{
			return "On";
		}
		return "Off";
	}

	private string ValueUiDb(string value)
	{
		if (value.ToBool())
		{
			return "Db";
		}
		return "0 to 100";
	}

	private string ValueToNumber(string value)
	{
		return value.ToInt().ToString("N0");
	}

	private string ValueToResScale(string value)
	{
		return value switch
		{
			"0" => "None", 
			"1" => "AMD FSR Ultra", 
			"2" => "AMD FSR Quality", 
			"3" => "AMD FSR Balanced", 
			"4" => "AMD FSR Performance", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToLowHigh(string value)
	{
		if (!(value == "0"))
		{
			if (value == "1")
			{
				return "High";
			}
			return value.IfNullOrEmpty("Null");
		}
		return "Low";
	}

	private string ValueToFFBSmoothingFilterType(string value)
	{
		if (!(value == "0"))
		{
			if (value == "1")
			{
				return "Boxcar";
			}
			return value.IfNullOrEmpty("Null");
		}
		return "Slew Rate Limited";
	}

	private string ValueToLowMediumHigh(string value)
	{
		return value switch
		{
			"0" => "Low", 
			"1" => "Medium", 
			"2" => "High", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToOffLowHigh(string value)
	{
		return value switch
		{
			"0" => "Off", 
			"1" => "Low", 
			"2" => "High", 
			_ => value, 
		};
	}

	private string ValueToOffLowMediumHigh(string value)
	{
		return value switch
		{
			"0" => "Off", 
			"1" => "Low", 
			"2" => "Medium", 
			"3" => "High", 
			"4" => "Ultra", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToEarProtection(string value)
	{
		return value switch
		{
			"0" => "None", 
			"1" => "Open helmet", 
			"2" => "Ear plugs", 
			"3" => "Closed helmet", 
			"4" => "Ear muffs", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string ValueToCompressorReplay(string value)
	{
		return value switch
		{
			"0" => "None", 
			"1" => "Mild", 
			"2" => "Heavy", 
			_ => value.IfNullOrEmpty("Null"), 
		};
	}

	private string Value2IntPercent(string value)
	{
		if (double.TryParse(value, out var result))
		{
			return ((int)(result * 100.0)).FormatUSInt();
		}
		return "Null";
	}

	public void Load(string nameProfile, bool Fixup = true)
	{
		string fullFileName = Ini.getFullFileName(nameProfile);
		if (!File.Exists(fullFileName))
		{
			TraceLog.Warn(Ini.iniName() + ": Load(" + nameProfile + ") called but file does not exist: " + fullFileName);
			return;
		}
		LastChanged = new FileInfo(fullFileName).LastWriteTime;
		if (Ini == iniConfig.DX11 && ProfileName.IsMonitor(nameProfile))
		{
			string text = fullFileName.Replace("Monitor.ini", ".ini");
			if (!File.Exists(fullFileName) && File.Exists(text))
			{
				try
				{
					File.Copy(text, fullFileName);
				}
				catch
				{
				}
			}
		}
		try
		{
			iniReader iniReader2 = new iniReader(this, fullFileName, iniLog: true);
			inLineCount = iniReader2.ReadLoop();
		}
		catch (Exception ex)
		{
			TraceLog.Error(string.Format("Exception encountered loading profile {0}.{1}: {2}", nameProfile.IfNullOrEmpty("Monitor"), Ini, ex.Message));
		}
		if (!Fixup)
		{
			return;
		}
		appConfig original = new appConfig(Ini);
		original.CloneFrom(this);
		switch (Ini)
		{
		case iniConfig.App:
		{
			ensureSection("SPCC");
			appSection obj3 = ensureSection("Audio");
			obj3.ensureSetting(iniApp.UiControlsInDb, "1");
			obj3.ensureSetting(iniApp.downshiftProtectionAlert, "0");
			obj3.ensureSetting(iniApp.earProtection, "0");
			obj3.ensureSetting(iniApp.compressorReplay, "0");
			obj3.ensureSetting("loudnessRain", "0", "Volume adjustment for rain water in dB");
			obj3.ensureSetting("ambientMusicDisabled", "0", "Disable music playing in the environment (PA speakers, etc)");
			ensureSection("SplitsDeltas").ensureSetting("comparisonLapFileName", "", "User specified split delta file used for comparison");
			appSection appSection2 = ensureSection("Adaptive");
			appSection2.ensureSetting("highContrastCursor", "0", "Set to 1 to use a high contrast mouse cursor");
			appSection2.ensureSetting("pitLineAlwaysVisible", "0", "Force the pitline to always be visible, not just when there is a pit exit line rule in place");
			appSection2.ensureSetting("raceLineWidth", "0.6", "Width of the race line in meters");
			appSection2.ensureSetting("pitlineColor", "#308CFF");
			appSection2.ensureSetting("racelineFastColor", "#63E84F");
			appSection2.ensureSetting("racelineSameColor", "#E8E8E8");
			appSection2.ensureSetting("racelineSlowColor", "#F90825");
			if (appSection2["raceLineWidth"].Value.Contains(','))
			{
				appSection2["raceLineWidth"].Value = "0.6";
			}
			appSection obj4 = ensureSection("TrueForce");
			obj4.ensureSetting(iniApp.loadTrueForceAPI.AsAppSetting("0"));
			obj4.ensureSetting(iniApp.enableTrueForceVibe.AsAppSetting("1"));
			obj4.ensureSetting(iniApp.trueForceVibePhysics.AsAppSetting("1"));
			obj4.ensureSetting(iniApp.trueForceDamper.AsAppSetting("0.050000"));
			obj4.ensureSetting(iniApp.volTrueForcePhysMaster.AsAppSetting("0.000000"));
			obj4.ensureSetting(iniApp.volTrueForcePhysCarBody.AsAppSetting("0.000000"));
			obj4.ensureSetting(iniApp.volTrueForcePhysDriveShaft.AsAppSetting("0.000000"));
			obj4.ensureSetting(iniApp.volTrueForcePhysEngineRPM.AsAppSetting("-9.000000"));
			obj4.ensureSetting(iniApp.volTrueForcePhysGearChange.AsAppSetting("3.000000"));
			obj4.ensureSetting(iniApp.volTrueForcePhysRevLimit.AsAppSetting("0.000000"));
			obj4.ensureSetting(iniApp.volTrueForcePhysRoadTexture.AsAppSetting("0.000000"));
			obj4.ensureSetting(iniApp.volTrueForcePhysRumbleStrip.AsAppSetting("-3.000000"));
			obj4.ensureSetting(iniApp.volTrueForcePhysWheelSlip.AsAppSetting("6.000000"));
			appSection obj5 = ensureSection("Force Feedback");
			obj5.ensureSetting(iniApp.loadAsetekAPI.AsAppSetting("0"));
			obj5.ensureSetting(iniApp.loadConspitAPI.AsAppSetting("0"));
			obj5.ensureSetting(iniApp.loadFanatecAPI.AsAppSetting("0"));
			obj5.ensureSetting(iniApp.loadMozaAPI.AsAppSetting("0"));
			obj5.ensureSetting(iniApp.loadSimagicAPI.AsAppSetting("0"));
			obj5.ensureSetting(iniApp.loadSimucubeAPI.AsAppSetting("0"));
			obj5.ensureSetting(iniApp.loadVRSAPI.AsAppSetting("0"));
			obj5.ensureSetting(iniApp.EnableWheelDisplay.AsAppSetting("1"));
			obj5.ensureSetting(iniApp.enableWheelDisplayBlink.AsAppSetting("1"));
			obj5.ensureSetting(iniApp.VibratePedal.AsAppSetting("0"));
			obj5.ensureSetting(iniApp.autoForceFactor.AsAppSetting("0"));
			break;
		}
		case iniConfig.Core:
		{
			appSection obj2 = ensureSection("Task");
			obj2.ensureSetting("max_num_default_worker_threads", "10", "Number of (Enki) default worker threads to launch");
			obj2.ensureSetting("num_processors_to_use_for_new_damage", "0", "Number of processors used to update new damage (choose 1 to 8, or 0 to have the system decide)");
			ensureSection("Pit Lane").ensureSetting(iniCore.customTestSessionStallLocation.AsAppSetting("10"));
			break;
		}
		case iniConfig.DX11:
			GraphicOptions();
			ReplayGraphics();
			Display();
			MonitorSetup();
			switch (ProfileName.DisplayMode(nameProfile))
			{
			case iniDisplayMode.Oculus:
				ensureOculus();
				break;
			case iniDisplayMode.OpenVR:
				ensureOpenVR();
				break;
			case iniDisplayMode.OpenXR:
				ensureOpenXR();
				break;
			default:
				if (IsOculus)
				{
					ensureOculus();
				}
				if (IsOpenVR)
				{
					ensureOpenXR();
				}
				if (IsOpenXR)
				{
					ensureOpenXR();
				}
				break;
			case iniDisplayMode.Monitor:
				break;
			}
			break;
		}
		Task.Run(delegate
		{
			try
			{
				if (IsDifferent(ProfileName.DisplayName(nameProfile), original))
				{
					TraceLog.Info("Profile " + ProfileName.DisplayName(nameProfile) + " saved due to differences created by updates performed during loading");
					Save(nameProfile);
				}
			}
			catch (Exception ex2)
			{
				TraceLog.Exception(ex2, "appConfig.Load encountered an exception saving profile " + ProfileName.DisplayName(nameProfile));
			}
		});
	}

	public void Save(string nameProfile)
	{
		string fullFileName = Ini.getFullFileName(nameProfile);
		string text = fullFileName + ".snapshot";
		if (File.Exists(text))
		{
			File.Delete(text);
		}
		if (File.Exists(fullFileName))
		{
			File.Move(fullFileName, text);
		}
		try
		{
			using (StreamWriter streamWriter = new StreamWriter(fullFileName, append: false, iniReader.InIEncoding, 8192))
			{
				streamWriter.NewLine = "\n";
				streamWriter.WriteLine("");
				Save(streamWriter, SaveAdaptive() ? "" : "Adaptive,");
			}
			File.Delete(text);
			MirrorDX11(nameProfile, fullFileName);
		}
		catch (Exception ex)
		{
			TraceLog.Error(string.Format("Exception encountered saving profile {0}.{1}: {2}", nameProfile.IfNullOrEmpty("Monitor"), Ini, ex.Message));
			if (File.Exists(text))
			{
				File.Delete(fullFileName);
				File.Move(text, fullFileName);
			}
		}
		LastChanged = DateTime.Now;
	}

	private void MirrorDX11(string nameProfile, string filenameProfile)
	{
		if (!nameProfile.IsNullOrEmpty() || Ini != iniConfig.DX11 || !nameProfile.IsNullOrEmpty())
		{
			return;
		}
		string text = filenameProfile.Replace("Monitor.ini", ".ini");
		try
		{
			File.Delete(text);
			File.Copy(filenameProfile, text);
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "Error while copying rendererDX11Monitor.ini to rendererDX11.ini");
		}
	}

	private bool SaveAdaptive()
	{
		if (Ini != iniConfig.App || !ContainsKey("Adaptive"))
		{
			return false;
		}
		appSection appSection2 = base["Adaptive"];
		if (!appSection2.hasSetting("highContrastCursor", "0") && !appSection2.hasSetting("pitLineAlwaysVisible", "0") && !appSection2.hasSetting("raceLineWidth", "0.6") && !appSection2.hasSetting("pitlineColor", "#308CFF") && !appSection2.hasSetting("racelineFastColor", "#63E84F") && !appSection2.hasSetting("racelineSameColor", "#E8E8E8"))
		{
			return appSection2.hasSetting("racelineSlowColor", "#F90825");
		}
		return true;
	}

	public void Backup()
	{
		TraceLog.Enter("appConfig.Backup(" + Ini.iniName() + ")");
		string fullFileName = Ini.getFullFileName("");
		string destFileName = fullFileName + ".Backup";
		try
		{
			File.Copy(fullFileName, destFileName, overwrite: true);
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "appConfig.Backup() encountered an exception");
		}
		TraceLog.Exit();
	}

	public void Restore()
	{
		TraceLog.Enter("appConfig.Restore(" + Ini.iniName() + ")");
		string fullFileName = Ini.getFullFileName("");
		string sourceFileName = fullFileName + ".Backup";
		try
		{
			File.Copy(sourceFileName, fullFileName, overwrite: true);
			MirrorDX11(null, fullFileName);
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "appConfig.Restore() encountered an exception");
		}
		TraceLog.Exit();
	}

	public void Delete(string nameProfile)
	{
		string fullFileName = Ini.getFullFileName(nameProfile);
		TraceLog.Enter("appConfig.Delete(" + fullFileName + ")");
		try
		{
			File.Delete(fullFileName);
		}
		catch (Exception ex)
		{
			TraceLog.Error("appConfig.Delete() Exception " + ex.Message);
		}
		TraceLog.Exit();
	}
}
