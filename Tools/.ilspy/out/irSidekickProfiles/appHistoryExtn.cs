using System.IO;

namespace irSidekickProfiles;

public static class appHistoryExtn
{
	public static string IniType(this string value)
	{
		string[] array = Path.GetFileName(value).Split('.');
		if (array.Length != 0)
		{
			return array[0].ToLower();
		}
		return "";
	}

	public static string IniProfile(this string value)
	{
		string[] array = Path.GetFileName(value).Split('.');
		if (array.Length == 3)
		{
			return array[2].ToLower();
		}
		return "";
	}
}
