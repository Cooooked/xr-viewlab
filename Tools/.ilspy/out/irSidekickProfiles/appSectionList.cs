using System.Collections.Generic;
using System.IO;
using irSidekick;

namespace irSidekickProfiles;

public class appSectionList : Dictionary<string, appSection>
{
	public string tagPrefix = "";

	public appSectionList(string prefix)
	{
		tagPrefix = prefix;
	}

	public int CountSettings()
	{
		int num = 0;
		foreach (appSection value in base.Values)
		{
			num += value.Count;
		}
		return num;
	}

	public void CloneFrom(appSectionList source)
	{
		Clear();
		tagPrefix = source.tagPrefix;
		foreach (appSection value in source.Values)
		{
			Add(value.Name, value.Clone());
		}
	}

	public void Save(StreamWriter iniWriter, string exclusion)
	{
		using Enumerator enumerator = GetEnumerator();
		while (enumerator.MoveNext())
		{
			KeyValuePair<string, appSection> current = enumerator.Current;
			if (!exclusion.Contains(current.Key))
			{
				current.Value.Save(iniWriter);
			}
		}
	}

	public appSection addSection(appSection section)
	{
		Add(section.Name, section);
		return section;
	}

	public appSection addSection(string name)
	{
		appSection appSection2 = new appSection(tagPrefix, name);
		Add(name, appSection2);
		return appSection2;
	}

	public appSection getSection(string name)
	{
		if (TryGetValue(name, out var value))
		{
			return value;
		}
		return null;
	}

	public bool VRNormalize(string name)
	{
		iniDisplayMode iniDisplayMode2 = ProfileName.DisplayMode(name);
		int num = base.Count;
		switch (iniDisplayMode2)
		{
		case iniDisplayMode.Monitor:
			Remove("Oculus Rift");
			Remove("OpenVR");
			Remove("OpenXR");
			break;
		case iniDisplayMode.Oculus:
			Remove("OpenVR");
			Remove("OpenXR");
			break;
		case iniDisplayMode.OpenVR:
			Remove("Oculus Rift");
			Remove("OpenXR");
			break;
		case iniDisplayMode.OpenXR:
			Remove("Oculus Rift");
			Remove("OpenVR");
			break;
		default:
			return false;
		}
		int num2 = base.Count;
		return num != num2;
	}

	private void migrateAntiAlias(appSection section)
	{
		appSetting appSetting2 = section.ensureSetting(iniDX11.Graphic_AntiAliasMethod, "1");
		string text = "AASamples";
		if (section.ContainsKey(text))
		{
			appSetting setting = section.getSetting(text);
			section.Remove(text);
			if (setting.Value.IfNullOrEmpty("0").Equals("0"))
			{
				appSetting2.Value = "0";
			}
			else
			{
				appSetting2.Value = "1";
				section.ensureSetting(iniDX11.Graphic_MSAASamples, "1").Value = setting.Value.IfNullOrEmpty("4");
			}
		}
		string text2 = "FXAA";
		if (section.ContainsKey(text2))
		{
			appSetting setting2 = section.getSetting(text2);
			section.Remove(text2);
			if (setting2.Value.IfNullOrEmpty("0").Equals("1"))
			{
				appSetting2.Value = "2";
			}
		}
		string text3 = "SMAA";
		if (section.ContainsKey(text3))
		{
			appSetting setting3 = section.getSetting(text3);
			section.Remove(text3);
			if (setting3.Value.IfNullOrEmpty("0").Equals("1"))
			{
				appSetting2.Value = "3";
			}
		}
	}

	public appSection GraphicOptions()
	{
		string text = "Graphics Options";
		if (!TryGetValue(text, out var value))
		{
			value = addSection(text);
		}
		bool num = !value.ContainsKey("AntiAliasMethod");
		ensureGraphicOptions(value);
		if (num)
		{
			migrateAntiAlias(value);
		}
		value.ensureSetting(iniDX11.Graphic_FSRSharpness.AsAppSetting("4"));
		value.ensureSetting(iniDX11.Graphic_ZBuffer32Bits.AsAppSetting("1"));
		value.ensureSetting(iniDX11.Graphic_FoliageDetail.AsAppSetting("0"));
		value.ensureSetting(iniDX11.Graphic_HideCockpitObstructions.AsAppSetting("0"));
		string text2 = "MaxWorkingSetMB_64Bit";
		if (value.ContainsKey(text2))
		{
			appSetting setting = value.getSetting(text2);
			value.ensureSetting("SysMemToUseMB", setting.Value, "(64-bit) 1024 to 32768 MB");
			value.Remove(text2);
		}
		string text3 = "VidMemMB";
		if (value.ContainsKey(text3))
		{
			appSetting setting2 = value.getSetting(text3);
			value.ensureSetting("VidMemToUseMB", setting2.Value, "Maximum GPU video memory to consume (MB)");
			value.Remove(text3);
		}
		string text4 = "Reflections";
		if (value.ContainsKey(text4))
		{
			appSetting setting3 = value.getSetting(text4);
			value.ensureSetting("SSRLevel", (setting3.Value == "0") ? "0" : "2", "0=off, 1=lower res, 2=full res");
			value.ensureSetting("SSRRainOnly", "0", "(only if SSRLevel > 0) 0=SSR on always, 1=SSR only kicks in during 'wet rules'");
			value.Remove(text4);
		}
		else
		{
			value.ensureSetting(iniDX11.Graphic_SSRLevel.AsAppSetting("0"));
			value.ensureSetting(iniDX11.Graphic_SSRRainOnly.AsAppSetting("0"));
		}
		return value;
	}

	public appSection ReplayGraphics()
	{
		string text = "Replay Graphics";
		if (!TryGetValue(text, out var value))
		{
			value = addSection(text);
		}
		ensureReplayGraphics(value);
		value.ensureSetting(iniDX11.Replay_FoliageDetail.AsAppSetting("0"));
		value.ensureSetting(iniDX11.Replay_HideCockpitObstructions.AsAppSetting("0"));
		string text2 = "Reflections";
		if (value.ContainsKey(text2))
		{
			appSetting setting = value.getSetting(text2);
			value.ensureSetting("SSRLevel", (setting.Value == "0") ? "0" : "2", "0=off, 1=lower res, 2=full res");
			value.ensureSetting("SSRRainOnly", "0", "(only if SSRLevel > 0) 0=SSR on always, 1=SSR only kicks in during 'wet rules'");
			value.Remove(text2);
		}
		else
		{
			value.ensureSetting(iniDX11.Replay_SSRLevel.AsAppSetting("0"));
			value.ensureSetting(iniDX11.Replay_SSRRainOnly.AsAppSetting("0"));
		}
		return value;
	}

	public appSection Display()
	{
		string text = "Display";
		if (!TryGetValue(text, out var value))
		{
			value = addSection(text);
		}
		ensureDisplay(value);
		return value;
	}

	public appSection MonitorSetup()
	{
		string text = "MonitorSetup";
		if (!TryGetValue(text, out var value))
		{
			value = addSection(text);
		}
		ensureMonitorSetup(value);
		return value;
	}

	public appSection ensureOculus()
	{
		if (!TryGetValue("Oculus Rift", out var value))
		{
			value = addSection(iniDX11.OculusEnabled.Section);
		}
		value.ensureSetting(iniDX11.OculusEnabled, "1");
		value.ensureSetting(iniDX11.OculusMirrorViewVerticalShiftPct, "0");
		value.ensureSetting(iniDX11.OculusPixelsPerDisplayPixel, "100");
		value.ensureSetting(iniDX11.OculusUIScreenWidthCM, "130");
		value.ensureSetting(iniDX11.OculusUIScreenDistCM, "70");
		value.ensureSetting(iniDX11.OculusTwoStageAA, "1");
		value.ensureSetting(iniDX11.OculusFullyWaitForSync, "0");
		value.ensureSetting(iniDX11.OculusAutoSelect, "0");
		value.ensureSetting(iniDX11.OculusAutoCenter, "0");
		value.ensureSetting(iniDX11.OculusPrevVirtualMirrorWidth, "1112");
		value.ensureSetting(iniDX11.OculusPrevVirtualMirrorHeight, "212");
		return value;
	}

	public appSection ensureOpenVR()
	{
		if (!TryGetValue("OpenVR", out var value))
		{
			value = addSection(iniDX11.OpenVREnabled.Section);
		}
		value.ensureSetting(iniDX11.OpenVREnabled, "1");
		value.ensureSetting(iniDX11.OpenVRUIScreenWidthCM, "130");
		value.ensureSetting(iniDX11.OpenVRUIScreenDistCM, "70");
		value.ensureSetting(iniDX11.OpenVRTwoStageAA, "1");
		value.ensureSetting(iniDX11.OpenVRAlignmentFix, "1");
		value.ensureSetting(iniDX11.OpenVRMirrorViewVerticalShiftPct, "0");
		value.ensureSetting(iniDX11.OpenVRPredictionMode, "1");
		value.ensureSetting(iniDX11.OpenVRFixGetProjectionRawBug, "0");
		value.ensureSetting(iniDX11.OpenVRFullyWaitForSync, "0");
		value.ensureSetting(iniDX11.OpenVRAutoSelect, "0");
		value.ensureSetting(iniDX11.OpenVRAutoCenter, "0");
		return value;
	}

	public appSection ensureOpenXR()
	{
		if (!TryGetValue("OpenXR", out var value))
		{
			value = addSection(iniDX11.OpenXREnabled.Section);
		}
		value.ensureSetting(iniDX11.OpenXREnabled, "1");
		value.ensureSetting(iniDX11.OpenXRUIScreenWidthCM, "130");
		value.ensureSetting(iniDX11.OpenXRUIScreenDistCM, "70");
		value.ensureSetting(iniDX11.OpenXRTwoStageAA, "1");
		value.ensureSetting(iniDX11.OpenXRMirrorViewVerticalShiftPct, "0");
		value.ensureSetting(iniDX11.OpenXRAutoSelect, "0");
		value.ensureSetting(iniDX11.OpenXRAutoCenter, "0");
		value.ensureSetting(iniDX11.OpenXRFoveatedInsetWidthPct, "40");
		value.ensureSetting(iniDX11.OpenXRFoveatedOuterPctRes, "35");
		value.ensureSetting(iniDX11.OpenXRUIScreenDistCM, "60");
		value.ensureSetting(iniDX11.OpenXRUIScreenWidthCM, "144");
		value.ensureSetting(iniDX11.OpenXRResolutionScalePct, "100");
		return value;
	}

	public appSection ensureSection(string name)
	{
		if (!TryGetValue(name, out var value))
		{
			return addSection(name);
		}
		return value;
	}

	private void ensureGraphicOptions(appSection section)
	{
		createSetting("SSRRainOnly", "0", "(only if SSRLevel > 0) 0=SSR on always, 1=SSR only kicks in during 'wet rules'");
		createSetting("SSRLevel", "0", "0=off, 1=lower res, 2=full res");
		createSetting("FoliageDetail", "0", "foliage density 0=off, 1=low, 2=med, 3=high");
		createSetting("HideCockpitObstructions", "0", "0=hide nothing, 1=hide halo, 2=hide a-pillar/rockcage, 3=hide everything");
		createSetting("ZBuffer32Bits", "1", "0=off, 1=on");
		createSetting("AllowTSOSelfShadows", "1", "0=off, 1=more self-shadowing objects when shadow mapping");
		createSetting("DNSMFilter", "2", "0= none 1= Fetch4 2= PCF4 3= PCF4P 4= PCF8P 5= PCF16P");
		createSetting("DNSMShadowFadeTime", "25", "0 to # = time to fade in night shadows in 100ths of a sec (5 default)");
		createSetting("DNSMNumLights", "3", "0 to 128 = Max number of shadow mapped lights at night");
		createSetting("DNSMWallsCastShadows", "0", "0= off 1=track walls cast shadows");
		createSetting("DNSMTSOsCastShadows", "0", "0= off 1=track objects cast shadows");
		createSetting("DNSMDownsampleFirst", "0", "0=per-AA-sample shadows 1=per-pixel shadow");
		createSetting("DNSMEnable", "0", "0=off 1=dynamic night shadow maps");
		createSetting("AutoExposure", "1", "0=off, 1=on  (only functions when HDR is also enabled)");
		createSetting("TwoPassTrees", "0", "0=off, 1=render trees with higher quality in two passes");
		createSetting("NumFixedCubemaps", "0", "number of fixed cubemaps to render per frame(100 = 1/frame)");
		createSetting("NumDynamicCubemaps", "0", "number of dynamic cubemaps to render per frame(100 = 1/frame)");
		createSetting("ReplayRenderModes", "0", "0=off, 1=Replay Render Modes enabled");
		createSetting("Distortion", "0", "0=off, 1=Distortion enabled");
		createSetting("SharpeningClamp", "9", "sharpening clamp (0=min, 10=default, 100=max)");
		createSetting("SharpeningAmount", "125", "sharpening strength (10=min, 125=default, 300=max)");
		createSetting("FXAAQualityEdgeThreshold", "166", "333=too little(fast),250=lowqual,166=default,125=highqual,63=overkill(slow)");
		createSetting("FXAAQualitySubPix", "75", "aliasing amt (100=soft,75=default,50=sharp,25=low,0=0ff)");
		createSetting("Sharpening", "0", "0=off, 1=sharpening enabled");
		createSetting("SSAO", "0", "0=off, 1=screen space ambient occlusion enabled");
		createSetting("MotionBlurBroadcastCams", "1", "0=disabled, 1=enabled");
		createSetting("MotionBlurDrivingCams", "1", "0=disabled, 1=enabled");
		createSetting("MotionBlurStrength", "0", "motion blur strength: 0=off, 1=low, 2=med, 3=high, 4=ultra");
		createSetting("HeatHaze", "0", "0=off, 1=heat haze enabled");
		createSetting("DepthOfField", "0", "0=off, 1=depth of field blurs enabled");
		createSetting("ShadowMapType", "0", "map onto: 0=off, 1=on (Turn on on for clouds/overcast to work best)");
		createSetting("DynamicShadowMaps", "0", "0=off 1=Main 2=Main and Mirrors (Day only!)");
		createSetting("ShadowDetail", "0", "0=fewer shadows, 1=maximum shadows");
		createSetting("VirtualMirrors", "1", "0=off, 1=virtual mirrors enabled");
		createSetting("MaxCockpitMirrors", "2", "Maximum number of cockpit mirrors to enable (0 to 4)");
		createSetting("MirrorDetail", "0", "0=low detail, 1=high detail in mirrors");
		createSetting("ParticlesFullRes", "1", "full resolution particles: 0=off, 1=on");
		createSetting("ParticleDetail", "2", "particle detail: 0=low, 1=med, 2=high");
		createSetting("WeekendDetail", "2", "event detail: 0=low, 1=med, 2=high");
		createSetting("ObjectDetail", "2", "object population 0=low, 1=med, 2=high");
		createSetting("GrandstandDetail", "2", "0=low, 1=med, 2=high");
		createSetting("CrowdDetail", "2", "0=off, 1=low, 2=med, 3=high");
		createSetting("PitObjectDetail", "2", "0=off, 1=low, 2=med, 3=high");
		createSetting("CarDetail", "2", "0=low, 1=med, 2=high");
		createSetting("SkyRefreshRate", "1", "0=low update rate, 1=med update rate, 2=high update rate");
		createSetting("Trilinear", "1", "0=off, 1=improved texture quality");
		createSetting("LODPctDynoMirrorsMax", "100", "above 100% increases FPS and decreases LOD (25 to 500)");
		createSetting("LODPctDynoMirrorsMin", "25", "below 100% decreases FPS and increases LOD (25 to 500)");
		createSetting("LODPctDynoMax", "100", "above 100% increases FPS and decreases LOD (25 to 500)");
		createSetting("LODPctDynoMin", "25", "below 100% decreases FPS and increases LOD (25 to 500)");
		createSetting("LODPctMirrorsMax", "500", "above 100% increases FPS and decreases LOD (25 to 500)");
		createSetting("LODPctMirrorsMin", "100", "below 100% decreases FPS and increases LOD (25 to 500)");
		createSetting("LODPctMax", "400", "above 100% increases FPS and decreases LOD (25 to 500)");
		createSetting("LODPctMin", "100", "below 100% decreases FPS and increases LOD (25 to 500)");
		createSetting("LODMinFPSTarget", "100", "Reduce LODs (see LODPct* settings) when FPS is below target.");
		createSetting("MaxPitObjsToDrawInMirrors", "8", "0 to 192: Max number of pit objs to render per mirror camera");
		createSetting("MaxPitObjsToDraw", "20", "0 to 192: Max number of pit objs to render per camera");
		createSetting("MaxCarsToDrawInMirrors", "8", "4 to 64: Max number of cars to render per mirror camera");
		createSetting("MaxCarsToDraw", "20", "10 to 64: Max number of cars to render per camera");
		createSetting("DriverHands", "0", "Show driver hands? 0=no, 1=yes");
		createSetting("SteeringWheel", "3", "Show steering wheel? 0=no, 1=yes, 2=fixed, 3=show only if has display");
		createSetting("VRMode", "0", "0=normal  1=NV Single Pass Stereo  2=NV foveated quad view w/MVP");
		createSetting("EnableTireMarks", "1", "0=off 1=Use Single Pass Stereo VR (nvidia Pascal/Turing)");
		createSetting("DynamicCubemapType", "0", "0=RGB8, 1=RGBE, 2=RGB16");
		createSetting("EnableHDR", "0", "0=LDR rendering, 1=HDR rendering");
		createSetting("ResolutionScaling", "0", "Render to lower resolution and upscale (0=off, 1+ on)");
		createSetting("NvReflexMode", "0", "Low Sim to Render Latency Mode (0=off, 1=on, 2=on+boost)");
		createSetting("ContrastAdj", "0", "Adjusts scene contrast: -6 to +6 ");
		createSetting("BrightnessAdj", "0", "Adjusts scene intensity: -6 to +6 ");
		createSetting("GammaAdj", "0", "Adjusts gamma encoding: -6 to +6");
		createSetting("LowQualityTrees", "1", "0=off, 1=on");
		createSetting("EnableSwayTrees", "1", "0=normal trees, 1=trees sway with wind");
		createSetting("TrackDisplacementEnable", "1", "0=render without displacement, 1=render using track displacement shaders");
		createSetting("DNSMMaxLightsPerPass", "3", "0- 6 = Shadowing lights per-fullscreen pass");
		createSetting("DynamicShadowRes", "1", "(For 3 maps! So, x3) 0 = 512x512 1 = 1024x1024 2 = 2048x2048 3 = 4096x4096");
		createSetting("ShaderQuality", "2", "0=low, 1=med, 2=high, 3=max");
		createSetting("HeadlightLevel", "1", "0=low quality, 1=medium, 2=high quality. *** -1=disabled ***");
		createSetting("ParallelSorting", "1", "0=disabled 1=multithreaded scene sort");
		createSetting("MonochromeHeadlights", "0", "0=color headlights 1=all white (less blotches/banding)");
		createSetting("HeadlightsInMirrors", "0", "0=off 1= headlights illuminate track surface in mirrors");
		createSetting("LoadTexturesWhenDriving", "1", "0=only load when out of car");
		createSetting("NumMultiGPUs", "1", "Number of GPUs in Crossfire/SLI (1=off to 4). Set low as works.");
		createSetting("CompressTexturesSuits", "1", "0=uncompressed   1=block compress (recommended)");
		createSetting("CompressTexturesHelmets", "1", "0=uncompressed   1=block compress (recommended)");
		createSetting("CompressTexturesCars", "1", "0=uncompressed (warning! no!!)   1=block compress (highly recommended)");
		createSetting("CompressedVertices", "1", "0=off  1=Use compressed vertices");
		createSetting("ReduceCockpitFlicker", "1", "0=off  1=enabled");
		createSetting("CarPaint2048x2048", "1", "0=1024x1024 car textures res,  1=2048x2048 car texture res (max)");
		createSetting("CacheSwap3HighResCars", "0", "0=shrink to fit  1=cache swap higher res for nearest cars");
		createSetting("WorldNearPlaneDistance", "10", "Number of frames to wait before re-testing object visibility. 0 = no delay");
		createSetting("AntiAliasMethod", "1", "The type of Anti Aliasing method used: 0=None, 1=MSAA, 2=FXAA, 3=SMAA");
		createSetting("MSAASamples", "4", "The number of MSAA samples (if MSAA is in use): 2, 4 or 8");
		createSetting("MSAAUseFilter", "3", "The filter to use for MSAA resolve (if MSAA is in use): 0=soft, 1=neutral, 2=sharp, 3=simple (legacy)");
		createSetting("MipLODBias", "0", "% bias texture lookup 100 is a mip level, positive is blurry, negative sharp");
		createSetting("OcclusionCull", "1", "0=disable occlusion culling, 1=enabled (usually best)");
		createSetting("LimitFrameRate", "1", "0=no limit, 1=use DesiredFPSLimit");
		createSetting("DesiredFPSLimit", "116", "Enabled when LimitFrameRate=1 and on ext. power");
		createSetting("MaxPreRenderedFrames", "1", "1=normal  0=disabled/multi-gpu");
		createSetting("VerticalSync", "0", "0=allow tearing, 1=lock FPS to refresh rate");
		createSetting("TwoBackBuffers", "0", "0=1 back buffer, 1=try to create 2 back buffers");
		createSetting("SysMemToUseMB", "16384", "(64-bit) 1024 to 8192 MB - Lower to reduce page faults!");
		createSetting("VidMemToUseMB", "6880", "Maximum GPU video memory to consume (MB)");
		createSetting("VisibilityFrameDelay", "5", "Number of frames to wait before re-testing object visibility. 0 = no delay");
		createSetting("UIScalePct", "100", "User Interface Size");
		void createSetting(string name, string value, string comment)
		{
			section.ensureSetting(new appSetting("2", name, value, comment));
		}
	}

	private void ensureReplayGraphics(appSection section)
	{
		createSetting("SSRRainOnly", "0", "(only if SSRLevel > 0) 0=SSR on always, 1=SSR only kicks in during 'wet rules'");
		createSetting("SSRLevel", "0", "0=off, 1=lower res, 2=full res");
		createSetting("FoliageDetail", "0", "foliage density 0=off, 1=low, 2=med, 3=high");
		createSetting("HideCockpitObstructions", "0", "0=hide nothing, 1=hide halo, 2=hide a-pillar/rockcage, 3=hide everything");
		createSetting("AllowTSOSelfShadows", "1", "0=off, 1=more self-shadowing objects when shadow mapping");
		createSetting("DNSMFilter", "2", "0= none 1= Fetch4 2= PCF4 3= PCF4P 4= PCF8P 5= PCF16P");
		createSetting("DNSMShadowFadeTime", "25", "0 to # = time to fade in night shadows in 100ths of a sec (5 default)");
		createSetting("DNSMNumLights", "3", "0 to 128 = Max number of shadow mapped lights at night");
		createSetting("DNSMWallsCastShadows", "0", "0= off 1=track walls cast shadows");
		createSetting("DNSMTSOsCastShadows", "0", "0= off 1=track objects cast shadows");
		createSetting("DNSMDownsampleFirst", "0", "0=per-AA-sample shadows 1=per-pixel shadow");
		createSetting("DNSMEnable", "0", "0=off 1=dynamic night shadow maps");
		createSetting("AutoExposure", "1", "0=off, 1=on  (only functions when HDR is also enabled)");
		createSetting("TwoPassTrees", "0", "0=off, 1=render trees with higher quality in two passes");
		createSetting("NumFixedCubemaps", "0", "number of fixed cubemaps to render per frame(100 = 1/frame)");
		createSetting("NumDynamicCubemaps", "0", "number of dynamic cubemaps to render per frame(100 = 1/frame)");
		createSetting("ReplayRenderModes", "0", "0=off, 1=Replay Render Modes enabled");
		createSetting("Distortion", "0", "0=off, 1=Distortion enabled");
		createSetting("SharpeningClamp", "9", "sharpening clamp (0=min, 10=default, 100=max)");
		createSetting("SharpeningAmount", "125", "sharpening strength (10=min, 125=default, 300=max)");
		createSetting("FXAAQualityEdgeThreshold", "166", "333=too little(fast),250=lowqual,166=default,125=highqual,63=overkill(slow)");
		createSetting("FXAAQualitySubPix", "75", "aliasing amt (100=soft,75=default,50=sharp,25=low,0=0ff)");
		createSetting("Sharpening", "0", "0=off, 1=sharpening enabled");
		createSetting("SSAO", "0", "0=off, 1=screen space ambient occlusion enabled");
		createSetting("MotionBlurBroadcastCams", "0", "0=disabled, 1=enabled");
		createSetting("MotionBlurDrivingCams", "0", "0=disabled, 1=enabled");
		createSetting("MotionBlurStrength", "0", "motion blur strength: 0=off, 1=low, 2=med, 3=high, 4=Ultra");
		createSetting("HeatHaze", "0", "0=off, 1=heat haze enabled");
		createSetting("DepthOfField", "0", "0=off, 1=depth of field blurs enabled");
		createSetting("ShadowMapType", "1", "map onto: 0=off, 1=on (Turn on on for clouds/overcast to work best)");
		createSetting("DynamicShadowMaps", "0", "0=off 1=Main 2=Main and Mirrors (Day only!)");
		createSetting("ShadowDetail", "0", "0=fewer shadows, 1=maximum shadows");
		createSetting("VirtualMirrors", "1", "0=off, 1=virtual mirrors enabled");
		createSetting("MaxCockpitMirrors", "2", "Maximum number of cockpit mirrors to enable (0 to 4)");
		createSetting("MirrorDetail", "0", "0=low detail, 1=high detail in mirrors");
		createSetting("ParticlesFullRes", "1", "full resolution particles: 0=off, 1=on");
		createSetting("ParticleDetail", "2", "particle detail: 0=low, 1=med, 2=high");
		createSetting("WeekendDetail", "2", "event detail: 0=low, 1=med, 2=high");
		createSetting("ObjectDetail", "2", "object population 0=low, 1=med, 2=high");
		createSetting("GrandstandDetail", "2", "0=low, 1=med, 2=high");
		createSetting("CrowdDetail", "2", "0=off, 1=low, 2=med, 3=high");
		createSetting("PitObjectDetail", "3", "0=off, 1=low, 2=med, 3=high");
		createSetting("CarDetail", "2", "0=low, 1=med, 2=high");
		createSetting("SkyRefreshRate", "1", "0=low update rate, 1=med update rate, 2=high update rate");
		createSetting("Trilinear", "1", "0=off, 1=improved texture quality");
		createSetting("LODPctDynoMirrorsMax", "100", "above 100% increases FPS and decreases LOD (25 to 500)");
		createSetting("LODPctDynoMirrorsMin", "25", "below 100% decreases FPS and increases LOD (25 to 500)");
		createSetting("LODPctDynoMax", "100", "above 100% increases FPS and decreases LOD (25 to 500)");
		createSetting("LODPctDynoMin", "25", "below 100% decreases FPS and increases LOD (25 to 500)");
		createSetting("LODPctMirrorsMax", "500", "above 100% increases FPS and decreases LOD (25 to 500)");
		createSetting("LODPctMirrorsMin", "100", "below 100% decreases FPS and increases LOD (25 to 500)");
		createSetting("LODPctMax", "400", "above 100% increases FPS and decreases LOD (25 to 500)");
		createSetting("LODPctMin", "100", "below 100% decreases FPS and increases LOD (25 to 500)");
		createSetting("LODMinFPSTarget", "100", "Reduce LODs (see LODPct* settings) when FPS is below target.");
		createSetting("MaxPitObjsToDrawInMirrors", "20", "0 to 192: Max number of pit objs to render per mirror camera");
		createSetting("MaxPitObjsToDraw", "40", "0 to 192: Max number of pit objs to render per camera");
		createSetting("MaxCarsToDrawInMirrors", "6", "4 to 64: Max number of cars to render per mirror camera");
		createSetting("MaxCarsToDraw", "40", "10 to 64: Max number of cars to render per camera");
		createSetting("DriverHands", "1", "Show driver hands? 0=no, 1=yes");
		createSetting("SteeringWheel", "1", "Show steering wheel? 0=no, 1=yes, 2=fixed, 3=show only if has display");
		void createSetting(string name, string value, string comment)
		{
			section.ensureSetting(new appSetting("2", name, value, comment));
		}
	}

	private void ensureDisplay(appSection section)
	{
		createSetting("windowedYPos", "0", "Window top left corner in windowed mode");
		createSetting("windowedXPos", "0", "Window top left corner in windowed mode");
		createSetting("windowedWidth", "1920", "Window mode width");
		createSetting("windowedHeight", "1080", "Window mode height");
		createSetting("windowedMaximized", "1", "Window mode is maximized?");
		createSetting("windowedAlignment", "0", "windowed mode alignment: 0 - none, 1 - center, 2 - top left");
		createSetting("border", "0", "window border?");
		createSetting("deviceIdx", "0", "which adapter");
		createSetting("fullScreen", "1", "fullscreen mode?");
		createSetting("fullScreenDepth", "32", "Color depth");
		createSetting("displayRotateMode", "1", "0 - auto, 1 - landscale, 2 - landscape inv, 3 - portrait, 4 - portrait inv");
		createSetting("fullScreenWidth", "1920", "full screen Window's width");
		createSetting("fullScreenHeight", "1440", "full screen Window's height");
		void createSetting(string name, string value, string comment)
		{
			section.ensureSetting(new appSetting("2", name, value, comment));
		}
	}

	private void ensureMonitorSetup(appSection section)
	{
		createSetting("MonitorType", "0", "0=flat or 1=curved");
		createSetting("NumMonitors", "1", "1 or 3");
		createSetting("ViewingDist", "500", "(mm) Distance from eyes to center of monitor");
		createSetting("EnableSMPSurround", "1", "0=off 1=Enable Simultaneous Multi-Projection via GPU");
		createSetting("RenderViewPerMonitor", "1", "0 = off 1 = separate view on each monitor(less distortion)");
		createSetting("RadiusOfCurvature", "1500", "(mm)Radius of screen's curvature (ignored if type is flat)");
		createSetting("ViewingDist", "600", "(mm)Distance from eyes to center of monitor");
		createSetting("MonitorWidth", "610", "(mm)total width of each monitor(screen +bezels)");
		createSetting("ScreenWidth", "600", "(mm)usable width of each screen(no bezels)");
		createSetting("ScreenAngles", "60", "(deg)side monitor angle, 10 = slight, 65 = max");
		void createSetting(string name, string value, string comment)
		{
			section.ensureSetting(new appSetting("2", name, value, comment));
		}
	}
}
