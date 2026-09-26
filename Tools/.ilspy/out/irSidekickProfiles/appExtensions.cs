#define TRACE
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using irSidekick;

namespace irSidekickProfiles;

public static class appExtensions
{
	public static string getIRacingEXE()
	{
		string text = (string)Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\Classes\\iracing\\shell\\open\\command", "", "");
		if (text.IsNullOrEmpty())
		{
			return "";
		}
		MatchCollection matchCollection = new Regex("\"(?<Path>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled).Matches(text);
		if (matchCollection.Count == 0 || matchCollection[0].Groups.Count == 0)
		{
			return "";
		}
		return matchCollection[0].Groups["Path"].Value;
	}

	public static string getIRacingFolder()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "iRacing");
	}

	public static string[] getIRacingSpotters()
	{
		string text = string.Empty;
		try
		{
			text = Path.GetDirectoryName(getIRacingEXE());
			string[] directories = Directory.GetDirectories(Path.Combine(text, "..\\sound\\spcc"));
			for (int i = 0; i < directories.Length; i++)
			{
				directories[i] = new DirectoryInfo(directories[i]).Name;
			}
			return directories;
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "Unable to locate the spotter voice pack folders at '" + text + "': " + ex.Message);
			return new string[0];
		}
	}

	public static string GetTag(this object obj)
	{
		return (obj as Control).GetTag();
	}

	public static string GetTag(this Control ctrl)
	{
		if (ctrl.Tag == null)
		{
			return "";
		}
		return ctrl.Tag.ToString();
	}

	public static int Max(this colourProfile profile)
	{
		return Enum.GetValues(typeof(colourProfile)).Cast<int>().Max();
	}

	public static int Count(this colourProfile profile)
	{
		return Enum.GetValues(typeof(colourProfile)).Length;
	}

	public static SolidColorBrush Brush(this colourProfile colour)
	{
		return (SolidColorBrush)new BrushConverter().ConvertFrom(colour.RGB());
	}

	public static string RGB(this colourProfile colour)
	{
		return colour switch
		{
			colourProfile.Black => "#000000", 
			colourProfile.Gray => "#9E9E9E", 
			colourProfile.Pink => "#E91E63", 
			colourProfile.Indigo => "#3F51B5", 
			colourProfile.Red => "#D50000", 
			colourProfile.Purple => "#9C27B0", 
			colourProfile.Green => "#4CAF50", 
			colourProfile.Cyan => "#00BCD4", 
			colourProfile.Orange => "#FF9800", 
			colourProfile.Blue => "#2196F3", 
			colourProfile.Teal => "#009688", 
			colourProfile.Brown => "#795548", 
			colourProfile.Yellow => "#CDDC39", 
			colourProfile.Amber => "#FFC107", 
			colourProfile.DarkRed => "#D92626", 
			colourProfile.Olive => "#BF9A40", 
			colourProfile.SoftGreen => "#89B24D", 
			colourProfile.DullBlue => "#407CBF", 
			colourProfile.DeepPurple => "#5E40BF", 
			colourProfile.DullPurple => "#BF409A", 
			colourProfile.Crayon => "#B24D71", 
			_ => "#000000", 
		};
	}

	public static colourProfile Pick(this colourProfile colour, int index, string name)
	{
		string text = name.ToLower(CultureInfo.CurrentCulture).IfNullOrEmpty("monitor");
		if (!(text == "monitor"))
		{
			if (text == "backup")
			{
				return colourProfile.Red;
			}
			return index switch
			{
				0 => colourProfile.Black, 
				1 => colourProfile.Indigo, 
				2 => colourProfile.Purple, 
				3 => colourProfile.Orange, 
				4 => colourProfile.Blue, 
				5 => colourProfile.Pink, 
				6 => colourProfile.Teal, 
				7 => colourProfile.Brown, 
				8 => colourProfile.Yellow, 
				9 => colourProfile.Amber, 
				10 => colourProfile.Olive, 
				11 => colourProfile.DarkRed, 
				13 => colourProfile.Crayon, 
				14 => colourProfile.Cyan, 
				15 => colourProfile.Gray, 
				16 => colourProfile.SoftGreen, 
				17 => colourProfile.DullBlue, 
				18 => colourProfile.DullPurple, 
				19 => colourProfile.DeepPurple, 
				_ => throw new Exception("Ran out of colours"), 
			};
		}
		return colourProfile.Green;
	}

	public static string TagID(this iniConfig ini)
	{
		return ini.ToString("D");
	}

	public static string TagName(this iniConfig ini)
	{
		return ini switch
		{
			iniConfig.App => "App", 
			iniConfig.Core => "Core", 
			iniConfig.DX11 => "DX11", 
			_ => "Unknown", 
		};
	}

	public static string getDX11Name(this iniConfig ini, string profile)
	{
		switch (profile.ToLower())
		{
		case "oculus":
		case "openvr":
		case "openxr":
			return "rendererDX11" + profile + ".ini";
		default:
			return "rendererDX11Monitor.ini." + profile;
		}
	}

	public static string[] getProfileNames(this iniConfig ini)
	{
		string[] files = Directory.GetFiles(ini.getLocation(), ini.iniName() + ".*");
		List<string> list = new List<string>();
		TraceLog.Enter("getProfileNames");
		string[] array = files;
		foreach (string text in array)
		{
			if (text.IsNullOrEmpty())
			{
				continue;
			}
			string fileName = Path.GetFileName(text);
			TraceLog.Verbose("Checking file '" + fileName + "'");
			string[] array2 = fileName.Split('.');
			if (array2.Length != 3)
			{
				TraceLog.Verbose("- Discarded because not a profile");
				continue;
			}
			if (array2[2].IsNullOrEmpty())
			{
				TraceLog.Verbose("- Discarded due to no profile name");
				continue;
			}
			if (!File.Exists(ini.getLocation() + "\\core.ini." + array2[2]))
			{
				TraceLog.Verbose("- core.ini not found for this profile");
				continue;
			}
			if (!File.Exists(ini.getLocation() + "\\" + ini.getDX11Name(array2[2])))
			{
				TraceLog.Verbose("- rendererDX11 not found for this profile");
				continue;
			}
			try
			{
				list.Add(array2[2]);
			}
			catch (Exception ex)
			{
				TraceLog.Exception(ex, "- ProfileList.Add(" + array2[2] + ") failed for file " + fileName);
			}
		}
		TraceLog.Exit();
		list.Sort();
		return list.ToArray();
	}

	public static string getFullFileName(this iniConfig ini, string profile)
	{
		return Path.Combine(ini.getLocation(), ini.getProfileFileName(profile));
	}

	public static string getProfileFileName(this iniConfig ini, string profile)
	{
		if (profile.IsNullOrEmpty())
		{
			return ini.iniName();
		}
		if (ProfileName.IsiRacingVR(profile) && ini == iniConfig.DX11)
		{
			return ini.getDX11Name(profile);
		}
		return ini.iniName() + "." + profile;
	}

	public static string getExportFileName(this iniConfig ini, string MemberName, string profile)
	{
		if (profile.IsNullOrEmpty())
		{
			return ini.iniName() + "." + MemberName + "!";
		}
		if (profile.Contains('!'))
		{
			return ini.iniName() + "." + profile;
		}
		if (ProfileName.IsiRacingVR(profile))
		{
			if (ini != iniConfig.DX11)
			{
				return ini.iniName() + "." + MemberName + "!";
			}
			return "rendererDX11" + profile + ".ini." + MemberName + "!";
		}
		return ini.iniName() + "." + MemberName + "!" + profile;
	}

	public static string getLocation(this iniConfig ini)
	{
		return getIRacingFolder();
	}

	public static string iniName(this iniConfig ini)
	{
		return ini switch
		{
			iniConfig.App => "app.ini", 
			iniConfig.Core => "core.ini", 
			iniConfig.DX11 => "rendererDX11Monitor.ini", 
			_ => "", 
		};
	}

	public static string ToDB(this int value)
	{
		return value.ToString("N0") + "db";
	}

	public static string ToHz(this int value)
	{
		return value.ToString("N0") + "hz";
	}

	public static string ToPercent(this int value)
	{
		return value.ToString("N0") + "%";
	}

	public static string ToPercent(this double value)
	{
		if (value <= 1.0)
		{
			return (value * 100.0).ToString("N2") + "%";
		}
		return value.ToString("N2") + "%";
	}

	public static string ToGB(this int value)
	{
		if (value < 1024)
		{
			return value + "mb";
		}
		return ((double)value / 1024.0).Round1().ToString("N2") + "gb";
	}

	public static int ToInt(this double value)
	{
		return (int)Math.Round(value, 0);
	}

	public static int ToInt(this string value)
	{
		try
		{
			return (int)double.Parse(value, new CultureInfo("en-US"));
		}
		catch
		{
			return 0;
		}
	}

	public static bool ToBool(this string value)
	{
		try
		{
			return int.Parse(value) != 0;
		}
		catch
		{
			return false;
		}
	}

	public static string ToText(this bool? value)
	{
		if (!value.Value)
		{
			return "0";
		}
		return "1";
	}

	public static double ToDouble(this string value)
	{
		try
		{
			return double.Parse(value, new CultureInfo("en-US"));
		}
		catch
		{
			return 0.0;
		}
	}

	public static double Round1(this double value)
	{
		return Math.Round(value, 2);
	}

	public static double Round2(this double value)
	{
		return Math.Round(value, 2);
	}

	public static string ToIntStr(this int value)
	{
		return value.ToString("D", new CultureInfo("en-US"));
	}

	public static string ToIntStr(this double value)
	{
		return value.ToInt().ToString("D", new CultureInfo("en-US"));
	}

	public static string ToString4Storage(this double value, int places = 6)
	{
		return value.ToString($"F{places}", CultureInfo.InvariantCulture);
	}

	public static string ToString4Display(this double value, int places = 6)
	{
		return value.ToString($"N{places}", new CultureInfo("en-US"));
	}
}
