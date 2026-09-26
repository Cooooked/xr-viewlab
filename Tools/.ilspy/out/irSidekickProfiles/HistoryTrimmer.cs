using System;
using System.Text.RegularExpressions;
using irSidekick;

namespace irSidekickProfiles;

internal class HistoryTrimmer
{
	private bool exclude = true;

	private bool FirstLine = true;

	private DateTime Today = DateTime.Today;

	public bool Include(string line)
	{
		if (exclude && line.Length > 0 && line[0] == '{')
		{
			Match match = appHistory.rxProfile.Match(line);
			if (match.Success)
			{
				DateTime value = DateTime.Parse(match.Groups[2].Value);
				exclude = Today.Subtract(value).TotalDays > 62.0;
			}
		}
		if (FirstLine && !exclude)
		{
			throw new ETrimAbort();
		}
		FirstLine = false;
		return !exclude;
	}
}
