using System.Collections.Generic;

namespace irSidekickProfiles;

public class iniSettings : Dictionary<string, iniSetting>
{
	public void Add(iniSetting setting)
	{
		if (setting != null)
		{
			Add(setting.TagID, setting);
		}
	}

	public iniSetting Find(string tag)
	{
		iniSetting value = null;
		if (TryGetValue(tag, out value))
		{
			return value;
		}
		return new iniSetting(tag);
	}
}
