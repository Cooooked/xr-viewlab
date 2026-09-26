using System.Collections.Generic;

namespace irSidekickProfiles;

public static class iniCore
{
	private static Dictionary<string, iniSetting> Settings = new Dictionary<string, iniSetting>();

	public static iniSetting connect_sockets = new iniSetting
	{
		Config = iniConfig.Core,
		Section = "Communications",
		Name = "connect_sockets",
		Comment = "connect() sockets?"
	};

	public static iniSetting max_num_default_worker_threads = new iniSetting
	{
		Config = iniConfig.Core,
		Section = "Task",
		Name = "max_num_default_worker_threads",
		Comment = "Maximum number of workers for the default thread pool"
	};

	public static iniSetting num_processors_to_use_for_new_damage = new iniSetting
	{
		Config = iniConfig.Core,
		Section = "Task",
		Name = "num_processors_to_use_for_new_damage",
		Comment = "Number of processors used to update new damage (choose 1 to 8, or 0 to have the system decide)"
	};

	public static iniSetting customTestSessionStallLocation = new iniSetting
	{
		Config = iniConfig.Core,
		Section = "Pit Lane",
		Name = "customTestSessionStallLocation",
		Comment = "In local test sessions you can use a pitstall other than the first (first stall = 1)"
	};

	private static void BuildDictionary()
	{
		connect_sockets.DictionaryAdd(Settings);
		max_num_default_worker_threads.DictionaryAdd(Settings);
		num_processors_to_use_for_new_damage.DictionaryAdd(Settings);
		customTestSessionStallLocation.DictionaryAdd(Settings);
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
