using System.Collections.Generic;

namespace irSidekickProfiles;

public class gfxSetting
{
	public string Tag { get; }

	public string Name { get; }

	public string Value { get; }

	public string Display { get; }

	public string Info { get; }

	public gfxSetting(string tag, string val, string show)
	{
		Tag = tag;
		Value = val;
		Display = show;
		switch (tag)
		{
		case "2.Graphics Options.SysMemToUseMB":
			Name = "System Memory";
			Info = "This setting does not control/limit memory used by iRacing, it tells Windows how much memory iRacing is likely to use and please avoid paging iRacing.";
			break;
		case "2.Graphics Options.VidMemToUseMB":
			Name = "Graphics Card Memory";
			Info = "Provide as much memory as possible to iRacing";
			break;
		case "2.Graphics Options.LimitFrameRate":
			Tag = "2.Graphics Options.SetFrameRateToRefreshRate";
			Name = "Limit Frame Rate";
			Value = "1";
			Info = "Unlimited saturates the rendering CPU core (stuttering) and the GPU render queue which increases (steering & pedal) input latency.\nEnsure monitor refresh rate is set in Monitor section";
			break;
		case "2.Graphics Options.DesiredFPSLimit":
			Tag = "2.Graphics Options.SetFrameRateToRefreshRate";
			Name = "Desired FPS Limit";
			Value = "";
			Info = "Set to 4 FPS below the monitor refresh rate\nUse a refresh rate your system can consistently achieve\nEnsure monitor refresh rate is configured in Monitor section";
			break;
		case "2.Graphics Options.LowQualityTrees":
			Name = "Low Quality Trees";
			Value = "1";
			Info = "Always on because the new 'speed trees' introduced with Hungaroring seriously hurt FPS";
			break;
		case "2.Graphics Options.EnableSwayTrees":
			Name = "Enable Sway Trees";
			Value = "0";
			Info = "Off when driving to save CPU/GPU workload but not a big issue, on for replays.";
			break;
		case "2.Graphics Options.MirrorDetail":
			Name = "Mirror Detail";
			Value = "0";
			Info = "Mirrors are high overhead, use low unless GPU is 3080 or better";
			break;
		case "2.Graphics Options.HeadlightsInMirrors":
			Name = "Headlights in Mirrors";
			Value = "0";
			Info = "When racing, don't have time to look at headlights illuminating the track surface in mirrors\nRemember, mirrors are high overhead";
			break;
		case "2.Graphics Options.MaxCockpitMirrors":
			Name = "Cockpit Mirror Count";
			Value = "2";
			Info = "Mirrors are high overhead, use 2 unless GPU is 3080 or better";
			break;
		case "2.Graphics Options.MaxCarsToDraw":
			Name = "Cars to Draw";
			switch (val.ToInt())
			{
			case 40:
			case 64:
				Value = "30";
				break;
			default:
				Value = "20";
				break;
			}
			Display = Value;
			Info = "Drawing too many cars around you is high overhead and pointless (can't see 40+ cars) but set higher for replays";
			break;
		case "2.Graphics Options.MaxCarsToDrawInMirrors":
			Name = "Cars in Mirrors";
			Value = "8";
			Display = Value;
			Info = "Mirrors are high overhead, can't actually see many cars in the mirrors\nUse 8 unless GPU is 3080 or better";
			break;
		case "2.Graphics Options.MaxPitObjsToDraw":
			Name = "Pits to Draw";
			switch (val.ToInt())
			{
			case 40:
			case 64:
				Value = "30";
				break;
			default:
				Value = "20";
				break;
			}
			Display = Value;
			Info = "Drawing too many pits around you is high overhead and pointless (can't see 40+ pits) but set higher for replays";
			break;
		case "2.Graphics Options.MaxPitObjsToDrawInMirrors":
			Name = "# pits in each mirror";
			Value = "8";
			Display = Value;
			Info = "Mirrors are high overhead, can't actually see many pits in the mirrors\nUse 8 unless GPU is 3080 or better";
			break;
		case "2.Graphics Options.NumFixedCubemaps":
			Name = "# fixed cube maps";
			Value = "0";
			Info = "High overhead, zero unless GPU is 3080 or better";
			break;
		case "2.Graphics Options.NumDynamicCubemaps":
			Name = "# dynamic cube maps";
			Value = "0";
			Info = "High overhead, zero unless GPU is 3080 or better";
			break;
		case "2.Graphics Options.AntiAliasMethod":
			Name = "Anti Aliasing Method";
			Info = "MSAA best, FXAA lower overhead, SMAA higher overhead";
			break;
		case "2.Graphics Options.MSAASamples":
			Name = "Anti Aliasing Samples";
			Info = "4 gives good performance and visuals at 1440p, high end PC's can use 8";
			break;
		case "2.Graphics Options.ShaderQuality":
			Name = "Shader Quality";
			Info = "Not much difference in visuals and performance between ultra and high.  Improved performance at medium.  Much more performance at low but significant visual impact.";
			break;
		case "2.Graphics Options.ObjectDetail":
			Name = "Object Population";
			Info = "Reducing this setting sometimes helps on tracks with high object count & detail\ne.g. The flowerbed around the fountain at Long Beach";
			break;
		case "2.Graphics Options.DNSMEnable":
			Name = "Night Shadow Maps";
			Info = "Shadow maps are high overhead, avoid unless your FPS is over 140 FPS.  Single monitor users can probably use shadow maps, triple monitor users need a high end GPU.";
			break;
		case "2.Graphics Options.ShadowMapType":
			Name = "Shadow Map Type";
			Info = "Shadow maps are high overhead, avoid unless your FPS is over 140 FPS.  Single monitor users can probably use shadow maps, triple monitor users need a high end GPU.";
			break;
		case "2.Graphics Options.DynamicShadowMaps":
			Name = "Dynamic Shadow Maps";
			Info = "This setting is very high overhead and can significantly impact FPS";
			break;
		case "2.Graphics Options.AllowTSOSelfShadows":
			Name = "Object Self Shadowing";
			Info = "Increased workload";
			break;
		case "2.Graphics Options.CompressTexturesSuits":
			Value = "1";
			Name = "Compress Textures: Suits";
			Info = "Since 2023 Q4 iRacing is using a lot of VRam on new tracks, it is essential to compress all textures to avoid blurry graphics";
			break;
		case "2.Graphics Options.CompressTexturesHelmets":
			Value = "1";
			Name = "Compress Textures: Helmets";
			Info = "Since 2023 Q4 iRacing is using a lot of VRam on new tracks, it is essential to compress all textures to avoid blurry graphics";
			break;
		case "2.Graphics Options.CompressTexturesCars":
			Value = "1";
			Name = "Compress Textures: Cars";
			Info = "Since 2023 Q4 iRacing is using a lot of VRam on new tracks, it is essential to compress all textures to avoid blurry graphics";
			break;
		case "2.Graphics Options.CompressedVertices":
			Value = "1";
			Name = "Compress: Vertices";
			Info = "Since 2023 Q4 iRacing is using a lot of VRam on new tracks, it is essential to compress all textures to avoid blurry graphics";
			break;
		}
	}

	public static bool tagValueGTPreset(string tag, string value, string preset)
	{
		switch (tag)
		{
		case "2.Graphics Options.LimitFrameRate":
			return value == "0";
		case "2.Graphics Options.VidMemToUseMB":
		case "2.Graphics Options.SysMemToUseMB":
			return value.ToInt() < preset.ToInt();
		case "2.Graphics Options.CompressTexturesHelmets":
		case "2.Graphics Options.CompressTexturesSuits":
		case "2.Graphics Options.CompressTexturesCars":
		case "2.Graphics Options.CompressedVertices":
			return value.ToInt() < preset.ToInt();
		default:
			return value.ToInt() > preset.ToInt();
		}
	}

	public static List<string> tagSettings()
	{
		return new List<string>
		{
			iniDX11.Graphic_VidMemToUseMB.TagID,
			iniDX11.Graphic_SysMemToUseMB.TagID,
			iniDX11.Graphic_LimitFrameRate.TagID,
			iniDX11.Graphic_DesiredFPSLimit.TagID,
			iniDX11.Graphic_LowQualityTrees.TagID,
			iniDX11.Graphic_MirrorDetail.TagID,
			iniDX11.Graphic_HeadlightsInMirrors.TagID,
			iniDX11.Graphic_MaxCockpitMirrors.TagID,
			iniDX11.Graphic_MaxCarsToDraw.TagID,
			iniDX11.Graphic_MaxCarsToDrawInMirrors.TagID,
			iniDX11.Graphic_MaxPitObjsToDraw.TagID,
			iniDX11.Graphic_MaxPitObjsToDrawInMirrors.TagID,
			iniDX11.Graphic_NumFixedCubemaps.TagID,
			iniDX11.Graphic_NumDynamicCubemaps.TagID,
			iniDX11.Graphic_AntiAliasMethod.TagID,
			iniDX11.Graphic_MSAASamples.TagID,
			iniDX11.Graphic_ShaderQuality.TagID,
			iniDX11.Graphic_ObjectDetail.TagID,
			iniDX11.Graphic_DNSMEnable.TagID,
			iniDX11.Graphic_SSR.TagID,
			iniDX11.Graphic_ShadowMapType.TagID,
			iniDX11.Graphic_DynamicShadowMaps.TagID,
			iniDX11.Graphic_AllowTSOSelfShadows.TagID,
			iniDX11.Graphic_CompressTexturesSuits.TagID,
			iniDX11.Graphic_CompressTexturesHelmets.TagID,
			iniDX11.Graphic_CompressTexturesCars.TagID,
			iniDX11.Graphic_CompressedVertices.TagID
		};
	}
}
