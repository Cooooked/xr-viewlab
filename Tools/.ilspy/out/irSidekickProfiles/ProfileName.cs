using irSidekick;

namespace irSidekickProfiles;

public static class ProfileName
{
	public const string Monitor = "";

	public const string Backup = "Backup";

	public const string Oculus = "Oculus";

	public const string OpenVR = "OpenVR";

	public const string OpenXR = "OpenXR";

	public static string DisplayName(string name)
	{
		if (!name.IsNullOrEmpty())
		{
			return name;
		}
		return "Monitor";
	}

	public static string Suffix(string name)
	{
		if (!name.IsNullOrEmpty())
		{
			return "." + name;
		}
		return string.Empty;
	}

	public static bool IsMonitor(string name)
	{
		return name.IsNullOrEmpty();
	}

	public static bool IsiRacing(string name)
	{
		return ("." + "".ToLower() + "." + "Oculus".ToLower() + "." + "OpenVR".ToLower() + "." + "OpenXR".ToLower() + ".").Contains("." + name.ToLower() + ".");
	}

	public static bool IsiRacingVR(string name)
	{
		return ("." + "Oculus".ToLower() + "." + "OpenVR".ToLower() + "." + "OpenXR".ToLower() + ".").Contains("." + name.ToLower() + ".");
	}

	public static bool IsBackup(string name)
	{
		return name.Equals("Backup");
	}

	public static bool IsCustom(string name)
	{
		return !IsiRacing(name);
	}

	public static iniDisplayMode DisplayMode(string name)
	{
		if (IsMonitor(name))
		{
			return iniDisplayMode.Monitor;
		}
		if (name.Equals("Oculus"))
		{
			return iniDisplayMode.Oculus;
		}
		if (name.Equals("OpenVR"))
		{
			return iniDisplayMode.OpenVR;
		}
		if (name.Equals("OpenXR"))
		{
			return iniDisplayMode.OpenXR;
		}
		return iniDisplayMode.Custom;
	}
}
