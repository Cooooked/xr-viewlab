using System.Collections.Generic;

namespace irSidekickProfiles;

public static class iniDX11
{
	private static Dictionary<string, iniSetting> Settings = new Dictionary<string, iniSetting>();

	public static iniSetting AutoCfgCompleted = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "AutoCfg",
		Name = "AutoCfgCompleted"
	};

	public static iniSetting hideCarNum = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "User Options",
		Name = "hideCarNum"
	};

	public static iniSetting SessionUITransparency = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "User Options",
		Name = "SessionUITransparency"
	};

	public static iniSetting DriveUITransparency = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "User Options",
		Name = "DriveUITransparency"
	};

	public static iniSetting DriveUIFullScreen = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "User Options",
		Name = "DriveUIFullScreen"
	};

	public static iniSetting SessionUIFullScreen = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "User Options",
		Name = "SessionUIFullScreen"
	};

	public static iniSetting forceCrowdVisible = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "User Options",
		Name = "forceCrowdVisible"
	};

	public static iniSetting ForceVisibleWhenMove = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "User Options",
		Name = "ForceVisibleWhenMove"
	};

	public static iniSetting Oculus_FullyWaitForSync = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Oculus Rift",
		Name = "FullyWaitForSync"
	};

	public static iniSetting OpenVR_FullyWaitForSync = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenVR",
		Name = "FullyWaitForSync"
	};

	public static iniSetting Graphic_VRMode = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "VRMode",
		Comment = "0=normal  1=NV Single Pass Stereo  2=NV foveated quad view w/MVP"
	};

	public static iniSetting Graphic_EnableGPUParticles = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "EnableGPUParticles",
		Comment = "0=off, 1=on"
	};

	public static iniSetting Graphic_FSRSharpness = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "FSRSharpness",
		Comment = "Adjust sharpening amount for AMD FSR"
	};

	public static iniSetting Graphic_NvReflexMode = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "NvReflexMode"
	};

	public static iniSetting Graphic_UIScalePct = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "UIScalePct"
	};

	public static iniSetting Graphic_NumMultiGPUs = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "NumMultiGPUs"
	};

	public static iniSetting Graphic_GammaAdj = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "GammaAdj"
	};

	public static iniSetting Graphic_ContrastAdj = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "ContrastAdj"
	};

	public static iniSetting Graphic_BrightnessAdj = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "BrightnessAdj"
	};

	public static iniSetting Graphic_ResolutionScaling = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "ResolutionScaling"
	};

	public static iniSetting Graphic_LoadTexturesWhenDriving = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "LoadTexturesWhenDriving"
	};

	public static iniSetting Graphic_CompressedVertices = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "CompressedVertices"
	};

	public static iniSetting Graphic_CompressTexturesCars = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "CompressTexturesCars"
	};

	public static iniSetting Graphic_CompressTexturesSuits = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "CompressTexturesSuits"
	};

	public static iniSetting Graphic_CompressTexturesHelmets = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "CompressTexturesHelmets"
	};

	public static iniSetting Graphic_SkyRefreshRate = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "SkyRefreshRate"
	};

	public static iniSetting Graphic_CarDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "CarDetail"
	};

	public static iniSetting Graphic_PitObjectDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "PitObjectDetail"
	};

	public static iniSetting Graphic_WeekendDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "WeekendDetail"
	};

	public static iniSetting Graphic_GrandstandDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "GrandstandDetail"
	};

	public static iniSetting Graphic_CrowdDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "CrowdDetail"
	};

	public static iniSetting Graphic_ObjectDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "ObjectDetail"
	};

	public static iniSetting Graphic_FoliageDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "FoliageDetail"
	};

	public static iniSetting Graphic_ParticleDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "ParticleDetail"
	};

	public static iniSetting Graphic_ParticlesFullRes = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "ParticlesFullRes"
	};

	public static iniSetting Graphic_MaxCarsToDraw = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "MaxCarsToDraw"
	};

	public static iniSetting Graphic_MaxCarsToDrawInMirrors = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "MaxCarsToDrawInMirrors"
	};

	public static iniSetting Graphic_MaxPitObjsToDraw = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "MaxPitObjsToDraw"
	};

	public static iniSetting Graphic_MaxPitObjsToDrawInMirrors = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "MaxPitObjsToDrawInMirrors"
	};

	public static iniSetting Graphic_LODPctMax = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "LODPctMax"
	};

	public static iniSetting Graphic_LODPctMin = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "LODPctMin"
	};

	public static iniSetting Graphic_LODPctDynoMax = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "LODPctDynoMax"
	};

	public static iniSetting Graphic_LODPctDynoMin = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "LODPctDynoMin"
	};

	public static iniSetting Graphic_LODMinFPSTarget = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "LODMinFPSTarget"
	};

	public static iniSetting Graphic_DesiredFPSLimit = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "DesiredFPSLimit"
	};

	public static iniSetting Graphic_LimitFrameRate = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "LimitFrameRate"
	};

	public static iniSetting Graphic_AntiAliasMethod = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "AntiAliasMethod"
	};

	public static iniSetting Graphic_MSAASamples = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "MSAASamples"
	};

	public static iniSetting Graphic_MSAAUseFilter = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "MSAAUseFilter"
	};

	public static iniSetting Graphic_ShadowMapType = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "ShadowMapType"
	};

	public static iniSetting Graphic_AllowTSOSelfShadows = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "AllowTSOSelfShadows"
	};

	public static iniSetting Graphic_DynamicShadowMaps = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "DynamicShadowMaps"
	};

	public static iniSetting Graphic_DNSMEnable = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "DNSMEnable"
	};

	public static iniSetting Graphic_DNSMWallsCastShadows = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "DNSMWallsCastShadows"
	};

	public static iniSetting Graphic_DNSMNumLights = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "DNSMNumLights"
	};

	public static iniSetting Graphic_DNSMFilter = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "DNSMFilter"
	};

	public static iniSetting Graphic_NumDynamicCubemaps = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "NumDynamicCubemaps"
	};

	public static iniSetting Graphic_NumFixedCubemaps = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "NumFixedCubemaps"
	};

	public static iniSetting Graphic_ShaderQuality = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "ShaderQuality"
	};

	public static iniSetting Graphic_HideCockpitObstructions = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "HideCockpitObstructions"
	};

	public static iniSetting Graphic_DynamicShadowRes = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "DynamicShadowRes"
	};

	public static iniSetting Graphic_StaticShadowRes = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "StaticShadowRes"
	};

	public static iniSetting Graphic_StaticShadowCount = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "StaticShadowCount"
	};

	public static iniSetting Graphic_MaxPrerenderedFrames = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "MaxPrerenderedFrames"
	};

	public static iniSetting Graphic_VisibilityFrameDelay = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "VisibilityFrameDelay"
	};

	public static iniSetting Graphic_SteeringWheel = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "SteeringWheel"
	};

	public static iniSetting Graphic_DriverHands = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "DriverHands"
	};

	public static iniSetting Graphic_TwoPassTrees = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "TwoPassTrees"
	};

	public static iniSetting Graphic_LowQualityTrees = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "LowQualityTrees"
	};

	public static iniSetting Graphic_MaxCockpitMirrors = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "MaxCockpitMirrors"
	};

	public static iniSetting Graphic_MirrorDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "MirrorDetail"
	};

	public static iniSetting Graphic_HeadlightLevel = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "HeadlightLevel"
	};

	public static iniSetting Graphic_HeadlightsInMirrors = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "HeadlightsInMirrors"
	};

	public static iniSetting Graphic_VirtualMirrors = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "VirtualMirrors"
	};

	public static iniSetting Graphic_MotionBlurStrength = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "MotionBlurStrength"
	};

	public static iniSetting Graphic_HeatHaze = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "HeatHaze"
	};

	public static iniSetting Graphic_SSAO = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "SSAO"
	};

	public static iniSetting Graphic_FXAAQualitySubPix = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "FXAAQualitySubPix"
	};

	public static iniSetting Graphic_FXAAQualityEdgeThreshold = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "FXAAQualityEdgeThreshold"
	};

	public static iniSetting Graphic_SSRRainOnly = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "SSRRainOnly"
	};

	public static iniSetting Graphic_SSRLevel = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "SSRLevel"
	};

	public static iniSetting Graphic_Sharpening = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "Sharpening"
	};

	public static iniSetting Graphic_Distortion = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "Distortion"
	};

	public static iniSetting Graphic_EnableHDR = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "EnableHDR"
	};

	public static iniSetting Graphic_AutoExposure = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "AutoExposure"
	};

	public static iniSetting Graphic_CacheSwap3HighResCars = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "CacheSwap3HighResCars"
	};

	public static iniSetting Graphic_CarPaint2048x2048 = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "CarPaint2048x2048"
	};

	public static iniSetting Graphic_MonochromeHeadlights = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "MonochromeHeadlights"
	};

	public static iniSetting Graphic_ZBuffer32Bits = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "ZBuffer32Bits",
		Comment = "0=off, 1=on"
	};

	public static iniSetting Graphic_TrackDisplacementEnable = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "TrackDisplacementEnable"
	};

	public static iniSetting Graphic_ParallelSorting = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "ParallelSorting"
	};

	public static iniSetting Graphic_VidMemToUseMB = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "VidMemToUseMB"
	};

	public static iniSetting Graphic_SysMemToUseMB = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "SysMemToUseMB"
	};

	public static iniSetting Graphic_MipLODBias = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "MipLODBias"
	};

	public static iniSetting Graphic_SSR = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Graphics Options",
		Name = "SSR"
	};

	public static iniSetting Replay_SkyRefreshRate = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "SkyRefreshRate"
	};

	public static iniSetting Replay_CarDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "CarDetail"
	};

	public static iniSetting Replay_PitObjectDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "PitObjectDetail"
	};

	public static iniSetting Replay_WeekendDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "WeekendDetail"
	};

	public static iniSetting Replay_GrandstandDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "GrandstandDetail"
	};

	public static iniSetting Replay_CrowdDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "CrowdDetail"
	};

	public static iniSetting Replay_ObjectDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "ObjectDetail"
	};

	public static iniSetting Replay_FoliageDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "FoliageDetail"
	};

	public static iniSetting Replay_ParticleDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "ParticleDetail"
	};

	public static iniSetting Replay_ParticlesFullRes = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "ParticlesFullRes"
	};

	public static iniSetting Replay_ShadowMapType = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "ShadowMapType"
	};

	public static iniSetting Replay_AllowTSOSelfShadows = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "AllowTSOSelfShadows"
	};

	public static iniSetting Replay_DynamicShadowMaps = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "DynamicShadowMaps"
	};

	public static iniSetting Replay_DynamicShadowFilters = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "DynamicShadowFilters"
	};

	public static iniSetting Replay_DNSMEnable = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "DNSMEnable"
	};

	public static iniSetting Replay_DNSMWallsCastShadows = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "DNSMWallsCastShadows"
	};

	public static iniSetting Replay_DNSMNumLights = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "DNSMNumLights"
	};

	public static iniSetting Replay_DNSMFilter = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "DNSMFilter"
	};

	public static iniSetting Replay_NumDynamicCubemaps = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "NumDynamicCubemaps"
	};

	public static iniSetting Replay_NumFixedCubemaps = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "NumFixedCubemaps"
	};

	public static iniSetting Replay_HideCockpitObstructions = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "HideCockpitObstructions"
	};

	public static iniSetting Replay_DriverHands = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "DriverHands"
	};

	public static iniSetting Replay_TwoPassTrees = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "TwoPassTrees"
	};

	public static iniSetting Replay_MaxCockpitMirrors = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "MaxCockpitMirrors"
	};

	public static iniSetting Replay_MirrorDetail = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "MirrorDetail"
	};

	public static iniSetting Replay_MotionBlurStrength = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "MotionBlurStrength"
	};

	public static iniSetting Replay_MotionBlurDrivingCams = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "MotionBlurDrivingCams"
	};

	public static iniSetting Replay_MotionBlurBroadcastCams = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "MotionBlurBroadcastCams"
	};

	public static iniSetting Replay_HeatHaze = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "HeatHaze"
	};

	public static iniSetting Replay_SSAO = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "SSAO"
	};

	public static iniSetting Replay_FXAAQualitySubPix = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "FXAAQualitySubPix"
	};

	public static iniSetting Replay_FXAAQualityEdgeThreshold = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "FXAAQualityEdgeThreshold"
	};

	public static iniSetting Replay_SSRRainOnly = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "SSRRainOnly"
	};

	public static iniSetting Replay_SSRLevel = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "SSRLevel"
	};

	public static iniSetting Replay_Sharpening = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "Sharpening"
	};

	public static iniSetting Replay_Distortion = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "Distortion"
	};

	public static iniSetting Replay_DepthOfField = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "DepthOfField"
	};

	public static iniSetting Replay_ReplayRenderModes = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "ReplayRenderModes"
	};

	public static iniSetting Replay_ShaderQuality = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "ShaderQuality"
	};

	public static iniSetting Replay_SteeringWheel = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "SteeringWheel"
	};

	public static iniSetting Replay_MaxCarsToDraw = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "MaxCarsToDraw"
	};

	public static iniSetting Replay_MaxCarsToDrawInMirrors = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "MaxCarsToDrawInMirrors"
	};

	public static iniSetting Replay_MaxPitObjsToDraw = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "MaxPitObjsToDraw"
	};

	public static iniSetting Replay_MaxPitObjsToDrawInMirrors = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "MaxPitObjsToDrawInMirrors"
	};

	public static iniSetting Replay_LODPctMax = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "LODPctMax"
	};

	public static iniSetting Replay_LODPctMin = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "LODPctMin"
	};

	public static iniSetting Replay_LODPctDynoMax = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "LODPctDynoMax"
	};

	public static iniSetting Replay_LODPctDynoMin = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "LODPctDynoMin"
	};

	public static iniSetting Replay_LODMinFPSTarget = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "LODMinFPSTarget"
	};

	public static iniSetting Replay_SSR = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Replay Graphics",
		Name = "SSR"
	};

	public static iniSetting deviceIdx = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Display",
		Name = "deviceIdx"
	};

	public static iniSetting HDRFormat = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Display",
		Name = "HDRFormat"
	};

	public static iniSetting RefreshRate = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Display",
		Name = "RefreshRate"
	};

	public static iniSetting fullScreen = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Display",
		Name = "fullScreen"
	};

	public static iniSetting fullScreenDepth = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Display",
		Name = "fullScreenDepth"
	};

	public static iniSetting fullScreenWidth = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Display",
		Name = "fullScreenWidth"
	};

	public static iniSetting fullScreenHeight = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Display",
		Name = "fullScreenHeight"
	};

	public static iniSetting border = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Display",
		Name = "border"
	};

	public static iniSetting windowedWidth = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Display",
		Name = "windowedWidth"
	};

	public static iniSetting windowedHeight = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Display",
		Name = "windowedHeight"
	};

	public static iniSetting windowedXPos = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Display",
		Name = "windowedXPos"
	};

	public static iniSetting windowedYPos = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Display",
		Name = "windowedYPos"
	};

	public static iniSetting windowedAlignment = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Display",
		Name = "windowedAlignment"
	};

	public static iniSetting windowedMaximized = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Display",
		Name = "windowedMaximized"
	};

	public static iniSetting MonitorType = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "MonitorSetup",
		Name = "MonitorType"
	};

	public static iniSetting RadiusOfCurvature = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "MonitorSetup",
		Name = "RadiusOfCurvature"
	};

	public static iniSetting MonitorWidth = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "MonitorSetup",
		Name = "MonitorWidth"
	};

	public static iniSetting ScreenWidth = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "MonitorSetup",
		Name = "ScreenWidth"
	};

	public static iniSetting ViewingDist = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "MonitorSetup",
		Name = "ViewingDist"
	};

	public static iniSetting ScreenAngles = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "MonitorSetup",
		Name = "ScreenAngles"
	};

	public static iniSetting RenderViewPerMonitor = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "MonitorSetup",
		Name = "RenderViewPerMonitor"
	};

	public static iniSetting EnableSMPSurround = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "MonitorSetup",
		Name = "EnableSMPSurround"
	};

	public static iniSetting NumMonitors = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "MonitorSetup",
		Name = "NumMonitors"
	};

	public static iniSetting BezelProtectionPct = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "MonitorSetup",
		Name = "BezelProtectionPct"
	};

	public static iniSetting DiagonalWidth = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "MonitorSetup",
		Name = "DiagonalWidth"
	};

	public static iniSetting VirtualMirrorSize = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Drive Screen",
		Name = "VirtualMirrorSize"
	};

	public static iniSetting UIOffsetBottomPct = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Drive Screen",
		Name = "UIOffsetBottomPct"
	};

	public static iniSetting OculusEnabled = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Oculus Rift",
		Name = "RiftEnabled",
		Comment = "Enable Oculus Rift Support"
	};

	public static iniSetting OculusMirrorViewVerticalShiftPct = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Oculus Rift",
		Name = "MirrorViewVerticalShiftPct",
		Comment = "-30 to +30 percent - shifts the mirror image up/down (if clipped)"
	};

	public static iniSetting OculusPixelsPerDisplayPixel = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Oculus Rift",
		Name = "PixelsPerDisplayPixel",
		Comment = "(50% to 300%): 125%=1.25,  over 100% may hurt performance!"
	};

	public static iniSetting OculusUIScreenWidthCM = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Oculus Rift",
		Name = "UIScreenWidthCM",
		Comment = "The width (cm) of the user interface screens in 3D world space"
	};

	public static iniSetting OculusUIScreenDistCM = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Oculus Rift",
		Name = "UIScreenDistCM",
		Comment = "The depth (cm) where the user interface screens are rendered in 3D"
	};

	public static iniSetting OculusTwoStageAA = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Oculus Rift",
		Name = "TwoStageAA",
		Comment = "Repeat AA after the UI draws (note: consumes more vid mem)"
	};

	public static iniSetting OculusFullyWaitForSync = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Oculus Rift",
		Name = "FullyWaitForSync",
		Comment = "Improves timing/lag below refresh rate, but costs a little all of the time"
	};

	public static iniSetting OculusAutoSelect = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Oculus Rift",
		Name = "AutoSelect",
		Comment = "Use Rift, if detected, without prompting"
	};

	public static iniSetting OculusAutoCenter = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Oculus Rift",
		Name = "AutoCenter",
		Comment = "Re-center the HMD pose when health/safety warning disappears"
	};

	public static iniSetting OculusPrevVirtualMirrorWidth = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Oculus Rift",
		Name = "PrevVirtualMirrorWidth",
		Comment = "System use only -> do not edit..."
	};

	public static iniSetting OculusPrevVirtualMirrorHeight = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "Oculus Rift",
		Name = "PrevVirtualMirrorHeight",
		Comment = "System use only -> do not edit..."
	};

	public static iniSetting OpenVREnabled = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenVR",
		Name = "OpenVREnabled",
		Comment = "Enable OpenVR Support"
	};

	public static iniSetting OpenVRUIScreenWidthCM = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenVR",
		Name = "UIScreenWidthCM",
		Comment = "The width (cm) of the user interface screens in 3D world space"
	};

	public static iniSetting OpenVRUIScreenDistCM = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenVR",
		Name = "ScreenDistCM",
		Comment = "The depth (cm) where the user interface screens are rendered in 3D"
	};

	public static iniSetting OpenVRTwoStageAA = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenVR",
		Name = "TwoStageAA",
		Comment = "Repeat AA after the UI draws (note: consumes more vid mem)"
	};

	public static iniSetting OpenVRAlignmentFix = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenVR",
		Name = "AlignmentFix",
		Comment = "0=off, 1=simple (SPS), 2=simple (always), 3=advanced (SPS)"
	};

	public static iniSetting OpenVRMirrorViewVerticalShiftPct = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenVR",
		Name = "MirrorViewVerticalShiftPct",
		Comment = "-30 to +30 percent - shifts the mirror image up/down (if clipped)"
	};

	public static iniSetting OpenVRPredictionMode = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenVR",
		Name = "PredictionMode",
		Comment = "0=off, 1=dynamic, 2=fixed"
	};

	public static iniSetting OpenVRFixGetProjectionRawBug = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenVR",
		Name = "FixGetProjectionRawBug",
		Comment = "Might help fix vertical eye alignment issues"
	};

	public static iniSetting OpenVRFullyWaitForSync = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenVR",
		Name = "FullyWaitForSync",
		Comment = "Improves timing/lag below refresh rate, but costs a little all of the time"
	};

	public static iniSetting OpenVRAutoSelect = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenVR",
		Name = "AutoSelect",
		Comment = "Use OpenVR without prompting (note: Oculus has priority if enabled)"
	};

	public static iniSetting OpenVRAutoCenter = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenVR",
		Name = "AutoCenter",
		Comment = "Re-center the HMD pose when health/safety warning disappears"
	};

	public static iniSetting OpenXREnabled = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenXR",
		Name = "OpenXREnabled",
		Comment = "Enable OpenXR Support"
	};

	public static iniSetting OpenXRAutoSelect = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenXR",
		Name = "AutoSelect",
		Comment = "Use OpenXR without prompting"
	};

	public static iniSetting OpenXRAutoCenter = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenXR",
		Name = "AutoCenter",
		Comment = "Re-center the HMD pose when health/safety warning disappears"
	};

	public static iniSetting OpenXRTwoStageAA = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenXR",
		Name = "TwoStageAA",
		Comment = "Repeat AA after the UI draws (note: consumes more vid mem)"
	};

	public static iniSetting OpenXRUIScreenDistCM = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenXR",
		Name = "UIScreenDistCM",
		Comment = "The depth (cm) where the user interface screens are rendered in 3D"
	};

	public static iniSetting OpenXRUIScreenWidthCM = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenXR",
		Name = "UIScreenWidthCM",
		Comment = "The width (cm) of the user interface screens in 3D world space"
	};

	public static iniSetting OpenXRMirrorViewVerticalShiftPct = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenXR",
		Name = "MirrorViewVerticalShiftPct",
		Comment = "-30 to +30 percent - shifts the mirror image up/down (if clipped)"
	};

	public static iniSetting OpenXRFoveatedOuterPctRes = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenXR",
		Name = "FoveatedOuterPctRes",
		Comment = "Foveated: res to use for the peripheral part as a pct of the width and height of the image (25% to 50%)"
	};

	public static iniSetting OpenXRFoveatedInsetWidthPct = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenXR",
		Name = "FoveatedInsetWidthPct",
		Comment = "Foveated: the size of the hi-res inset as a pct of the width and height of the image (25% to 50%)"
	};

	public static iniSetting OpenXRResolutionScalePct = new iniSetting
	{
		Config = iniConfig.DX11,
		Section = "OpenXR",
		Name = "ResolutionScalePct",
		Comment = "Scale the recommended width/height resolution by this percentage (50% to 150%)"
	};

	private static void BuildDictionary()
	{
		AutoCfgCompleted.DictionaryAdd(Settings);
		hideCarNum.DictionaryAdd(Settings);
		SessionUITransparency.DictionaryAdd(Settings);
		DriveUITransparency.DictionaryAdd(Settings);
		DriveUIFullScreen.DictionaryAdd(Settings);
		SessionUITransparency.DictionaryAdd(Settings);
		SessionUIFullScreen.DictionaryAdd(Settings);
		ForceVisibleWhenMove.DictionaryAdd(Settings);
		forceCrowdVisible.DictionaryAdd(Settings);
		Graphic_VRMode.DictionaryAdd(Settings);
		Graphic_EnableGPUParticles.DictionaryAdd(Settings);
		Graphic_FSRSharpness.DictionaryAdd(Settings);
		Graphic_NvReflexMode.DictionaryAdd(Settings);
		Graphic_UIScalePct.DictionaryAdd(Settings);
		Graphic_NumMultiGPUs.DictionaryAdd(Settings);
		Graphic_GammaAdj.DictionaryAdd(Settings);
		Graphic_ContrastAdj.DictionaryAdd(Settings);
		Graphic_BrightnessAdj.DictionaryAdd(Settings);
		Graphic_ResolutionScaling.DictionaryAdd(Settings);
		Graphic_LoadTexturesWhenDriving.DictionaryAdd(Settings);
		Graphic_CompressedVertices.DictionaryAdd(Settings);
		Graphic_CompressTexturesCars.DictionaryAdd(Settings);
		Graphic_CompressTexturesSuits.DictionaryAdd(Settings);
		Graphic_CompressTexturesHelmets.DictionaryAdd(Settings);
		Graphic_SkyRefreshRate.DictionaryAdd(Settings);
		Graphic_CarDetail.DictionaryAdd(Settings);
		Graphic_PitObjectDetail.DictionaryAdd(Settings);
		Graphic_WeekendDetail.DictionaryAdd(Settings);
		Graphic_GrandstandDetail.DictionaryAdd(Settings);
		Graphic_CrowdDetail.DictionaryAdd(Settings);
		Graphic_ObjectDetail.DictionaryAdd(Settings);
		Graphic_FoliageDetail.DictionaryAdd(Settings);
		Graphic_ParticleDetail.DictionaryAdd(Settings);
		Graphic_ParticlesFullRes.DictionaryAdd(Settings);
		Graphic_MaxCarsToDraw.DictionaryAdd(Settings);
		Graphic_MaxCarsToDrawInMirrors.DictionaryAdd(Settings);
		Graphic_MaxPitObjsToDraw.DictionaryAdd(Settings);
		Graphic_MaxPitObjsToDrawInMirrors.DictionaryAdd(Settings);
		Graphic_LODPctMax.DictionaryAdd(Settings);
		Graphic_LODPctMin.DictionaryAdd(Settings);
		Graphic_LODPctDynoMax.DictionaryAdd(Settings);
		Graphic_LODPctDynoMin.DictionaryAdd(Settings);
		Graphic_LODMinFPSTarget.DictionaryAdd(Settings);
		Graphic_DesiredFPSLimit.DictionaryAdd(Settings);
		Graphic_LimitFrameRate.DictionaryAdd(Settings);
		Graphic_AntiAliasMethod.DictionaryAdd(Settings);
		Graphic_MSAASamples.DictionaryAdd(Settings);
		Graphic_MSAAUseFilter.DictionaryAdd(Settings);
		Graphic_ShadowMapType.DictionaryAdd(Settings);
		Graphic_AllowTSOSelfShadows.DictionaryAdd(Settings);
		Graphic_DynamicShadowMaps.DictionaryAdd(Settings);
		Graphic_DNSMEnable.DictionaryAdd(Settings);
		Graphic_DNSMWallsCastShadows.DictionaryAdd(Settings);
		Graphic_DNSMNumLights.DictionaryAdd(Settings);
		Graphic_DNSMFilter.DictionaryAdd(Settings);
		Graphic_NumDynamicCubemaps.DictionaryAdd(Settings);
		Graphic_NumFixedCubemaps.DictionaryAdd(Settings);
		Graphic_ShaderQuality.DictionaryAdd(Settings);
		Graphic_HideCockpitObstructions.DictionaryAdd(Settings);
		Graphic_DynamicShadowRes.DictionaryAdd(Settings);
		Graphic_StaticShadowRes.DictionaryAdd(Settings);
		Graphic_StaticShadowCount.DictionaryAdd(Settings);
		Graphic_MaxPrerenderedFrames.DictionaryAdd(Settings);
		Graphic_VisibilityFrameDelay.DictionaryAdd(Settings);
		Graphic_SteeringWheel.DictionaryAdd(Settings);
		Graphic_DriverHands.DictionaryAdd(Settings);
		Graphic_TwoPassTrees.DictionaryAdd(Settings);
		Graphic_LowQualityTrees.DictionaryAdd(Settings);
		Graphic_MaxCockpitMirrors.DictionaryAdd(Settings);
		Graphic_MirrorDetail.DictionaryAdd(Settings);
		Graphic_HeadlightLevel.DictionaryAdd(Settings);
		Graphic_HeadlightsInMirrors.DictionaryAdd(Settings);
		Graphic_VirtualMirrors.DictionaryAdd(Settings);
		Graphic_MotionBlurStrength.DictionaryAdd(Settings);
		Graphic_HeatHaze.DictionaryAdd(Settings);
		Graphic_SSAO.DictionaryAdd(Settings);
		Graphic_FXAAQualitySubPix.DictionaryAdd(Settings);
		Graphic_FXAAQualityEdgeThreshold.DictionaryAdd(Settings);
		Graphic_SSRRainOnly.DictionaryAdd(Settings);
		Graphic_SSRLevel.DictionaryAdd(Settings);
		Graphic_Sharpening.DictionaryAdd(Settings);
		Graphic_Distortion.DictionaryAdd(Settings);
		Graphic_EnableHDR.DictionaryAdd(Settings);
		Graphic_AutoExposure.DictionaryAdd(Settings);
		Graphic_CacheSwap3HighResCars.DictionaryAdd(Settings);
		Graphic_CarPaint2048x2048.DictionaryAdd(Settings);
		Graphic_MonochromeHeadlights.DictionaryAdd(Settings);
		Graphic_ZBuffer32Bits.DictionaryAdd(Settings);
		Graphic_TrackDisplacementEnable.DictionaryAdd(Settings);
		Graphic_ParallelSorting.DictionaryAdd(Settings);
		Graphic_VidMemToUseMB.DictionaryAdd(Settings);
		Graphic_SysMemToUseMB.DictionaryAdd(Settings);
		Graphic_SSR.DictionaryAdd(Settings);
		Replay_SkyRefreshRate.DictionaryAdd(Settings);
		Replay_CarDetail.DictionaryAdd(Settings);
		Replay_PitObjectDetail.DictionaryAdd(Settings);
		Replay_WeekendDetail.DictionaryAdd(Settings);
		Replay_GrandstandDetail.DictionaryAdd(Settings);
		Replay_CrowdDetail.DictionaryAdd(Settings);
		Replay_ObjectDetail.DictionaryAdd(Settings);
		Replay_FoliageDetail.DictionaryAdd(Settings);
		Replay_ParticleDetail.DictionaryAdd(Settings);
		Replay_ParticlesFullRes.DictionaryAdd(Settings);
		Replay_ShadowMapType.DictionaryAdd(Settings);
		Replay_AllowTSOSelfShadows.DictionaryAdd(Settings);
		Replay_DynamicShadowMaps.DictionaryAdd(Settings);
		Replay_DynamicShadowFilters.DictionaryAdd(Settings);
		Replay_DNSMEnable.DictionaryAdd(Settings);
		Replay_DNSMWallsCastShadows.DictionaryAdd(Settings);
		Replay_DNSMNumLights.DictionaryAdd(Settings);
		Replay_DNSMFilter.DictionaryAdd(Settings);
		Replay_NumDynamicCubemaps.DictionaryAdd(Settings);
		Replay_NumFixedCubemaps.DictionaryAdd(Settings);
		Replay_HideCockpitObstructions.DictionaryAdd(Settings);
		Replay_TwoPassTrees.DictionaryAdd(Settings);
		Replay_MaxCockpitMirrors.DictionaryAdd(Settings);
		Replay_MirrorDetail.DictionaryAdd(Settings);
		Replay_MotionBlurStrength.DictionaryAdd(Settings);
		Replay_MotionBlurDrivingCams.DictionaryAdd(Settings);
		Replay_MotionBlurBroadcastCams.DictionaryAdd(Settings);
		Replay_HeatHaze.DictionaryAdd(Settings);
		Replay_SSAO.DictionaryAdd(Settings);
		Replay_FXAAQualitySubPix.DictionaryAdd(Settings);
		Replay_FXAAQualityEdgeThreshold.DictionaryAdd(Settings);
		Replay_SSRRainOnly.DictionaryAdd(Settings);
		Replay_SSRLevel.DictionaryAdd(Settings);
		Replay_Sharpening.DictionaryAdd(Settings);
		Replay_Distortion.DictionaryAdd(Settings);
		Replay_DepthOfField.DictionaryAdd(Settings);
		Replay_ReplayRenderModes.DictionaryAdd(Settings);
		Replay_ShaderQuality.DictionaryAdd(Settings);
		Replay_SteeringWheel.DictionaryAdd(Settings);
		Replay_DriverHands.DictionaryAdd(Settings);
		Replay_MaxCarsToDraw.DictionaryAdd(Settings);
		Replay_MaxCarsToDrawInMirrors.DictionaryAdd(Settings);
		Replay_MaxPitObjsToDraw.DictionaryAdd(Settings);
		Replay_MaxPitObjsToDrawInMirrors.DictionaryAdd(Settings);
		Replay_LODPctMax.DictionaryAdd(Settings);
		Replay_LODPctMin.DictionaryAdd(Settings);
		Replay_LODPctDynoMax.DictionaryAdd(Settings);
		Replay_LODPctDynoMin.DictionaryAdd(Settings);
		Replay_LODMinFPSTarget.DictionaryAdd(Settings);
		Replay_SSR.DictionaryAdd(Settings);
		deviceIdx.DictionaryAdd(Settings);
		HDRFormat.DictionaryAdd(Settings);
		RefreshRate.DictionaryAdd(Settings);
		fullScreen.DictionaryAdd(Settings);
		fullScreenDepth.DictionaryAdd(Settings);
		fullScreenWidth.DictionaryAdd(Settings);
		fullScreenHeight.DictionaryAdd(Settings);
		border.DictionaryAdd(Settings);
		windowedWidth.DictionaryAdd(Settings);
		windowedHeight.DictionaryAdd(Settings);
		windowedXPos.DictionaryAdd(Settings);
		windowedYPos.DictionaryAdd(Settings);
		windowedAlignment.DictionaryAdd(Settings);
		windowedMaximized.DictionaryAdd(Settings);
		MonitorType.DictionaryAdd(Settings);
		RadiusOfCurvature.DictionaryAdd(Settings);
		MonitorWidth.DictionaryAdd(Settings);
		ScreenWidth.DictionaryAdd(Settings);
		ViewingDist.DictionaryAdd(Settings);
		ScreenAngles.DictionaryAdd(Settings);
		RenderViewPerMonitor.DictionaryAdd(Settings);
		EnableSMPSurround.DictionaryAdd(Settings);
		NumMonitors.DictionaryAdd(Settings);
		BezelProtectionPct.DictionaryAdd(Settings);
		DiagonalWidth.DictionaryAdd(Settings);
		VirtualMirrorSize.DictionaryAdd(Settings);
		UIOffsetBottomPct.DictionaryAdd(Settings);
		OculusEnabled.DictionaryAdd(Settings);
		OculusMirrorViewVerticalShiftPct.DictionaryAdd(Settings);
		OculusPixelsPerDisplayPixel.DictionaryAdd(Settings);
		OculusUIScreenWidthCM.DictionaryAdd(Settings);
		OculusUIScreenDistCM.DictionaryAdd(Settings);
		OculusTwoStageAA.DictionaryAdd(Settings);
		OculusFullyWaitForSync.DictionaryAdd(Settings);
		OculusAutoSelect.DictionaryAdd(Settings);
		OculusAutoCenter.DictionaryAdd(Settings);
		OculusPrevVirtualMirrorWidth.DictionaryAdd(Settings);
		OculusPrevVirtualMirrorHeight.DictionaryAdd(Settings);
		OpenVREnabled.DictionaryAdd(Settings);
		OpenVRUIScreenWidthCM.DictionaryAdd(Settings);
		OpenVRUIScreenDistCM.DictionaryAdd(Settings);
		OpenVRTwoStageAA.DictionaryAdd(Settings);
		OpenVRAlignmentFix.DictionaryAdd(Settings);
		OpenVRMirrorViewVerticalShiftPct.DictionaryAdd(Settings);
		OpenVRPredictionMode.DictionaryAdd(Settings);
		OpenVRFixGetProjectionRawBug.DictionaryAdd(Settings);
		OpenVRFullyWaitForSync.DictionaryAdd(Settings);
		OpenVRAutoSelect.DictionaryAdd(Settings);
		OpenVRAutoCenter.DictionaryAdd(Settings);
		OpenXREnabled.DictionaryAdd(Settings);
		OpenXRAutoSelect.DictionaryAdd(Settings);
		OpenXRAutoCenter.DictionaryAdd(Settings);
		OpenXRTwoStageAA.DictionaryAdd(Settings);
		OpenXRUIScreenDistCM.DictionaryAdd(Settings);
		OpenXRUIScreenWidthCM.DictionaryAdd(Settings);
		OpenXRMirrorViewVerticalShiftPct.DictionaryAdd(Settings);
		OpenXRFoveatedOuterPctRes.DictionaryAdd(Settings);
		OpenXRFoveatedInsetWidthPct.DictionaryAdd(Settings);
		OpenXRResolutionScalePct.DictionaryAdd(Settings);
	}

	public static iniSetting Find(string tag, bool NullIfNotFound = true)
	{
		if (Settings.Values.Count == 0)
		{
			BuildDictionary();
		}
		if (Settings.ContainsKey(tag))
		{
			return Settings[tag];
		}
		if (NullIfNotFound)
		{
			return null;
		}
		return new iniSetting(tag);
	}

	public static iniSetting Find(string tag, string comment)
	{
		if (Settings.Values.Count == 0)
		{
			BuildDictionary();
		}
		if (Settings.ContainsKey(tag))
		{
			return Settings[tag];
		}
		iniSetting iniSetting2 = new iniSetting(tag, comment);
		if (Settings.Values.Count > 0)
		{
			iniSetting2.DictionaryAdd(Settings);
		}
		return iniSetting2;
	}
}
