using System;
using System.IO;
using System.Text.RegularExpressions;

namespace irSidekickProfiles;

public class HistoryProfile : IComparable
{
	public DateTime TimeStamp { get; set; }

	public string Name { get; set; }

	public long Line { get; set; }

	public long lineAPP { get; set; }

	public long lineCORE { get; set; }

	public long lineDX11 { get; set; }

	public string Key => GetKey(TimeStamp, Name);

	public bool IsValid
	{
		get
		{
			if (lineAPP > 0 && lineCORE > 0 && lineDX11 > 0 && Name.Length > 0)
			{
				return TimeStamp.Date.Year > 2010;
			}
			return false;
		}
	}

	public bool IsiRacing => Name.Equals("iRacing", StringComparison.CurrentCultureIgnoreCase);

	public string DateCreated => TimeStamp.ToString("yyyy-MM-dd ddd h:mm tt");

	public HistoryProfile(long lineNum, Match rxMatch)
	{
		Line = lineNum;
		Name = rxMatch.Groups[1].Value;
		TimeStamp = DateTime.Parse(rxMatch.Groups[2].Value);
	}

	public void IniMatch(long LineNum, Match rxFileMatch)
	{
		switch (Path.GetFileName(rxFileMatch.Groups[1].Value).IniType())
		{
		case "app":
			lineAPP = LineNum;
			break;
		case "core":
			lineCORE = LineNum;
			break;
		case "rendererdx11monitor":
			lineDX11 = LineNum;
			break;
		}
	}

	public static string GetKey(DateTime timeStamp, string ProfileName)
	{
		return timeStamp.Date.ToString("s").Substring(0, 10) + ":" + ProfileName;
	}

	public int CompareTo(object obj)
	{
		return -TimeStamp.CompareTo((obj as HistoryProfile).TimeStamp);
	}
}
