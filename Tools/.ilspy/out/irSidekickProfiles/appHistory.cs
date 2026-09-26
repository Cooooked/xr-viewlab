#define TRACE
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using irSidekick;

namespace irSidekickProfiles;

public static class appHistory
{
	public static bool HistoryLoaded = false;

	public static HistoryDict History = new HistoryDict();

	internal static string fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "iRacing\\sidekick\\" + Extension.getProgramBin(".history"));

	internal static FileStream fileStream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

	internal static StreamWriter fileWriter = new StreamWriter(fileStream);

	internal static Regex rxProfile = new Regex("\\x7BProfile\\x7C(?<ProfileName>.*)\\x7C(?<TimeStamp>\\d\\d\\d\\d-\\d\\d-\\d\\dT\\d\\d:\\d\\d:\\d\\d)\\x7D", RegexOptions.IgnoreCase);

	internal static Regex rxIniFile = new Regex("\\x28Ini\\x7C(?<FileName>.*)\\x7C(?<TimeStamp>\\d\\d\\d\\d-\\d\\d-\\d\\dT\\d\\d:\\d\\d:\\d\\d)\\x29", RegexOptions.IgnoreCase);

	internal static Regex rxIniType = new Regex("\\\\Documents\\\\iRacing\\\\(?<FileName>.*)\\.ini", RegexOptions.IgnoreCase);

	public static bool HistoryAvailable
	{
		get
		{
			if (HistoryLoaded)
			{
				return History.Count > 0;
			}
			return false;
		}
	}

	private static string TimeStamp()
	{
		return DateTime.Now.ToString("s");
	}

	public static void appStartup()
	{
		if (TrimHistory())
		{
			TraceLog.Info("History has been trimmed to 60 days");
		}
		fileStream.Position = fileStream.Length;
		fileWriter.WriteLine();
	}

	public static void appExit()
	{
		fileStream.Flush();
	}

	public static void logProfile(string ProfileName)
	{
		fileWriter.WriteLine("{Profile|" + ProfileName + "|" + TimeStamp() + "}");
	}

	public static void logIniFile(string FileName)
	{
		fileWriter.WriteLine("(Ini|" + FileName + "|" + TimeStamp() + ")");
	}

	public static void logIniLines(string[] lines)
	{
		for (int i = 0; i < lines.Length; i++)
		{
			if (i <= 0 || lines[i].Length <= 0 || !lines[i.Prev()].EndsWith(lines[i]))
			{
				logIniLine(lines[i]);
			}
		}
	}

	public static void logIniLine(string line)
	{
		fileWriter.WriteLine(line);
	}

	private static bool TrimHistory()
	{
		if (!File.Exists(fileName))
		{
			return false;
		}
		FileInfo fileInfo = new FileInfo(fileName);
		try
		{
			if (fileInfo.Length > 1048576)
			{
				return TrimOnDisk();
			}
			return TrimInMemory();
		}
		catch (ETrimAbort)
		{
			fileWriter.BaseStream.Position = fileWriter.BaseStream.Length;
			fileWriter.WriteLine();
			return false;
		}
	}

	private static bool TrimOnDisk()
	{
		string text = Path.ChangeExtension(fileName, ".history.tmp");
		text.SafeFileDelete();
		HistoryTrimmer historyTrimmer = new HistoryTrimmer();
		fileStream.Position = 0L;
		StreamReader streamReader = new StreamReader(fileStream);
		using (StreamWriter streamWriter = new StreamWriter(text))
		{
			string text2;
			while ((text2 = streamReader.ReadLine()) != null)
			{
				if (historyTrimmer.Include(text2))
				{
					streamWriter.WriteLine(text2);
				}
			}
		}
		fileStream.Position = 0L;
		using (StreamReader streamReader2 = new StreamReader(text))
		{
			while (!streamReader2.EndOfStream)
			{
				fileWriter.WriteLine(streamReader2.ReadLine());
			}
		}
		text.SafeFileDelete();
		fileWriter.WriteLine();
		fileStream.SetLength(fileStream.Position);
		return true;
	}

	private static bool TrimInMemory()
	{
		List<string> list = new List<string>();
		HistoryTrimmer historyTrimmer = new HistoryTrimmer();
		fileStream.Position = 0L;
		StreamReader streamReader = new StreamReader(fileStream);
		while (!streamReader.EndOfStream)
		{
			string text = streamReader.ReadLine();
			if (historyTrimmer.Include(text))
			{
				list.Add(text);
			}
		}
		fileStream.Position = 0L;
		foreach (string item in list)
		{
			fileWriter.WriteLine(item);
		}
		fileWriter.WriteLine();
		fileStream.SetLength(fileStream.Position);
		return true;
	}

	public static void GetHistory(Action NotifyMethod)
	{
		History.Clear();
		HistoryLoaded = false;
		Task.Run(delegate
		{
			ReadHistory();
		}).ContinueWith(delegate(Task t)
		{
			if (t.IsFaulted)
			{
				TraceLog.Error("appHistory.ReadHistory failed", t.Exception.InnerException);
			}
			else if (t.IsCompleted)
			{
				TraceLog.Info($"appHistory.ReadHistory complete, {History.Count} history profiles found");
				HistoryLoaded = true;
				SafeExec(NotifyMethod);
			}
		}, TaskContinuationOptions.OnlyOnFaulted);
	}

	public static void ReadHistory()
	{
		long num = 0L;
		HistoryProfile historyProfile = null;
		using FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
		using StreamReader streamReader = new StreamReader(stream);
		while (!streamReader.EndOfStream)
		{
			num++;
			string text = streamReader.ReadLine();
			if (text.Length == 0)
			{
				continue;
			}
			Match match;
			if (text[0] == '{')
			{
				match = rxProfile.Match(text);
				if (match.Success)
				{
					historyProfile = new HistoryProfile(num, match);
					continue;
				}
			}
			if (text[0] != '(')
			{
				continue;
			}
			match = rxIniFile.Match(text);
			if (match.Success)
			{
				historyProfile.IniMatch(num, match);
				if (historyProfile.IsValid)
				{
					History.AddProfile(historyProfile);
				}
			}
		}
	}

	public static void RestoreProfile(HistoryProfile profile)
	{
		if (!profile.IsValid)
		{
			return;
		}
		TraceLog.Info("Restore profile " + profile.Name + " to " + profile.DateCreated);
		long num = 0L;
		using FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
		using StreamReader streamReader = new StreamReader(stream);
		while (!streamReader.EndOfStream)
		{
			num++;
			string iniLine = streamReader.ReadLine();
			if (num == profile.lineAPP)
			{
				RestoreINI(profile, streamReader, iniLine);
				break;
			}
		}
	}

	private static void RestoreINI(HistoryProfile profile, StreamReader fileReader, string iniLine)
	{
		Match match = rxIniFile.Match(iniLine);
		if (!match.Success)
		{
			return;
		}
		string value = match.Groups[1].Value;
		if (!profile.IsiRacing && !value.Contains(profile.Name))
		{
			return;
		}
		List<string> list = new List<string>();
		while (!fileReader.EndOfStream)
		{
			string text = fileReader.ReadLine();
			if (text.Length == 0)
			{
				list.Add("");
				continue;
			}
			switch (text[0])
			{
			case '{':
				WriteIni(value, list);
				return;
			case '(':
				WriteIni(value, list);
				RestoreINI(profile, fileReader, text);
				return;
			default:
				list.Add(text);
				break;
			}
		}
		if (list.Count > 0)
		{
			WriteIni(value, list);
		}
	}

	private static void WriteIni(string iniFileName, List<string> iniLines)
	{
		if (iniLines.Count == 0)
		{
			return;
		}
		iniFileName = iniFileName.ToLower().Replace("rendererDX11.ini", "rendererDX11Monitor.ini");
		try
		{
			if (File.Exists(iniFileName))
			{
				File.Delete(iniFileName);
			}
			File.WriteAllLines(iniFileName, iniLines.ToArray());
			TraceLog.Info("History restore ini " + iniFileName);
			iniLines.Clear();
		}
		catch (Exception ex)
		{
			TraceLog.Error("History restore failed: " + iniFileName, ex);
		}
	}

	private static void SafeExec(Action ExecMethod)
	{
		try
		{
			Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				ExecMethod();
			});
		}
		catch
		{
		}
	}
}
