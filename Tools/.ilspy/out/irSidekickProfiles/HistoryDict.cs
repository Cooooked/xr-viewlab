using System.Collections.Generic;

namespace irSidekickProfiles;

public class HistoryDict : Dictionary<string, HistoryProfile>
{
	public void AddProfile(HistoryProfile profile)
	{
		if (profile != null && profile.IsValid)
		{
			string key = profile.Key;
			if (!ContainsKey(key))
			{
				Add(key, profile);
			}
		}
	}

	public HistoryList GetProfileHistory(string ProfileName)
	{
		HistoryList historyList = new HistoryList();
		foreach (HistoryProfile value in base.Values)
		{
			if (value.Name.Equals(ProfileName))
			{
				historyList.Add(value);
			}
		}
		historyList.Sort();
		return historyList;
	}
}
