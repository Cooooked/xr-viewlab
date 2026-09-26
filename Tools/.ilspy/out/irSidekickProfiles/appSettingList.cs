using System.Collections.Generic;
using System.IO;

namespace irSidekickProfiles;

public class appSettingList : Dictionary<string, appSetting>
{
	private string _tagPrefix;

	public string tagPrefix
	{
		get
		{
			return _tagPrefix;
		}
		set
		{
			_tagPrefix = value;
			foreach (appSetting value2 in base.Values)
			{
				value2.tagPrefix = _tagPrefix;
			}
		}
	}

	public bool HasModifiedValues
	{
		get
		{
			foreach (appSetting value in base.Values)
			{
				if (value.Modified)
				{
					return true;
				}
			}
			return false;
		}
	}

	public appSettingList(string prefix)
	{
		tagPrefix = prefix;
	}

	public void CloneFrom(appSettingList source)
	{
		Clear();
		_tagPrefix = source.tagPrefix;
		foreach (appSetting value in source.Values)
		{
			Add(value.Name, value.Clone());
		}
	}

	public virtual void Save(StreamWriter iniWriter)
	{
		using Enumerator enumerator = GetEnumerator();
		while (enumerator.MoveNext())
		{
			enumerator.Current.Value.Save(iniWriter);
		}
	}

	public appSetting addSetting(string name, string value, string comment = "")
	{
		appSetting appSetting2 = new appSetting(tagPrefix, name, value, comment);
		Add(name, appSetting2);
		return appSetting2;
	}

	public appSetting getSetting(string name)
	{
		try
		{
			return base[name];
		}
		catch (KeyNotFoundException innerException)
		{
			throw new KeyNotFoundException("Setting '" + name + "' not found", innerException);
		}
	}

	public bool existsSetting(string name)
	{
		appSetting value;
		return TryGetValue(name, out value);
	}

	public appSetting ensureSetting(string name, string value, string comment = "")
	{
		if (!TryGetValue(name, out var value2))
		{
			return addSetting(name, value, comment);
		}
		return value2;
	}

	public appSetting ensureSetting(iniSetting setting, string value)
	{
		return ensureSetting(setting.Name, value, setting.Comment);
	}

	public appSetting ensureSetting(appSetting setting)
	{
		return ensureSetting(setting.Name, setting.Value, setting.Comment);
	}
}
