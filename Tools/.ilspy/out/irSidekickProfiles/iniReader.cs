#define TRACE
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using irSidekick;

namespace irSidekickProfiles;

public class iniReader
{
	private appSection SectionItem;

	private MatchCollection rxMatches;

	private string[] iniLines;

	private const string regexSECTION = "^\\s*\\[(?<name>[^\\]]*)\\]\\s*";

	private Regex rxSection = new Regex("^\\s*\\[(?<name>[^\\]]*)\\]\\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private const string regexSETTING = "^(?<name>[^=]+)=(?<value>[^;\\x09]*)\\s*;?(?<comment>.*)?";

	private Regex rxSetting = new Regex("^(?<name>[^=]+)=(?<value>[^;\\x09]*)\\s*;?(?<comment>.*)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static Encoding _IniEncoding;

	public appSectionList SectionList { get; private set; }

	public static Encoding InIEncoding
	{
		get
		{
			if (_IniEncoding == null)
			{
				_IniEncoding = GetEncoding1252();
			}
			return _IniEncoding;
		}
	}

	private static Encoding GetEncoding1252()
	{
		try
		{
			Encoding encoding = Encoding.GetEncoding(1252);
			if (encoding == null || encoding.CodePage != 1252)
			{
				throw new Exception("GenEncoding failed");
			}
			return encoding;
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "GetEncoding failed to provide Windows code page 1252");
			return GetEncodingUTF8();
		}
	}

	private static Encoding GetEncodingUTF8()
	{
		try
		{
			return Encoding.UTF8;
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "GetEncoding failed to provide UTF-8");
			return Encoding.Default;
		}
	}

	public iniReader(appSectionList target, string fileName, bool iniLog)
	{
		SectionList = target;
		iniLines = File.ReadAllLines(fileName, InIEncoding);
		if (iniLog)
		{
			appHistory.logIniFile(fileName);
			appHistory.logIniLines(iniLines);
		}
	}

	public int ReadLoop()
	{
		iniState iniState2 = iniState.findSection;
		int num = -1;
		while (num < iniLines.Length.Prev())
		{
			num++;
			iniLines[num] = iniLines[num].TrimEnd();
			if (iniLines[num].Length == 0)
			{
				continue;
			}
			if (num > 0 && iniLines[num.Prev()].EndsWith(iniLines[num]))
			{
				TraceLog.Enter("Corrupt INI");
				TraceLog.Warn("INVALID: End of previous line appears on next line");
				TraceLog.Warn(iniLines[num.Prev()]);
				TraceLog.Warn(iniLines[num]);
				TraceLog.Exit();
				continue;
			}
			switch (iniState2)
			{
			case iniState.findSection:
				if (ReadSectionHeader(ref iniLines[num]))
				{
					iniState2 = iniState.readSetting;
				}
				break;
			case iniState.readSetting:
				if (!ReadSetting(ref iniLines[num]))
				{
					iniState2 = iniState.findSection;
					num--;
				}
				break;
			}
		}
		return iniLines.Length;
	}

	private bool ReadSectionHeader(ref string iniText)
	{
		rxMatches = rxSection.Matches(iniText);
		if (rxMatches.Count == 0)
		{
			return false;
		}
		if (rxMatches.Count > 1)
		{
			TraceLog.Warn("Multiple section headers on one line?\n" + iniText);
		}
		if (rxMatches[0].Groups.Count == 2)
		{
			SectionItem = SectionList.ensureSection(rxMatches[0].Groups["name"].Value);
			return true;
		}
		throw new Exception("\nSection header format not expected\n" + iniText + "\n");
	}

	private bool ReadSetting(ref string iniText)
	{
		rxMatches = rxSetting.Matches(iniText);
		if (rxMatches.Count == 0)
		{
			return false;
		}
		if (rxMatches.Count > 1)
		{
			TraceLog.Warn("Multiple settings on one line?\n" + iniText);
		}
		if (rxMatches[0].Groups.Count < 3)
		{
			TraceLog.Error("Unexpected setting definition\n" + iniText);
			return false;
		}
		string name = rxMatches[0].Groups["name"].Value.Trim();
		string value = rxMatches[0].Groups["value"].Value.Trim();
		string comment = rxMatches[0].Groups["comment"].Value.Trim();
		appSetting obj = SectionItem.ensureSetting(name, value, comment);
		obj.Value = value;
		obj.Comment = comment;
		return true;
	}
}
