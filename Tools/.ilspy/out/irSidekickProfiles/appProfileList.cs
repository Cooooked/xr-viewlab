#define TRACE
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Media;
using irSidekick;

namespace irSidekickProfiles;

public class appProfileList : Dictionary<string, appProfile>
{
	private const colourProfile Colours = colourProfile.Amber;

	private string tagValueLabels = "";

	private int inxColour = -1;

	public appProfile Backup
	{
		get
		{
			if (!ContainsKey("Backup"))
			{
				return null;
			}
			return base["Backup"];
		}
	}

	public appProfile Monitor
	{
		get
		{
			if (!ContainsKey(""))
			{
				return null;
			}
			return base[""];
		}
	}

	public appProfile Oculus
	{
		get
		{
			if (!ContainsKey("Oculus"))
			{
				return null;
			}
			return base["Oculus"];
		}
	}

	public appProfile OpenVR
	{
		get
		{
			if (!ContainsKey("OpenVR"))
			{
				return null;
			}
			return base["OpenVR"];
		}
	}

	public appProfile OpenXR
	{
		get
		{
			if (!ContainsKey("OpenXR"))
			{
				return null;
			}
			return base["OpenXR"];
		}
	}

	public void Discover()
	{
		TraceLog.Enter("appProfileList.Discover()");
		try
		{
			Clear();
			addProfile("");
			iniConfig ini = iniConfig.App;
			string[] profileNames = ini.getProfileNames();
			int num = 0;
			string[] array = profileNames;
			foreach (string name in array)
			{
				addProfile(name);
				if (num++ > 14)
				{
					break;
				}
			}
			addVRProfile(ini, "Oculus");
			addVRProfile(ini, "OpenVR");
			addVRProfile(ini, "OpenXR");
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "Exception encountered discovering profile ini files");
		}
		try
		{
			if (ContainsKey(""))
			{
				appProfile obj = base[""];
				obj.Tab.Height = 40.0;
				obj.Lab.Margin = new Thickness(2.0, 2.0, 2.0, 10.0);
			}
			if (ContainsKey("Backup"))
			{
				appProfile obj2 = base["Backup"];
				obj2.Tab.Height = 40.0;
				obj2.Lab.Margin = new Thickness(2.0, 2.0, 2.0, 10.0);
			}
		}
		catch
		{
		}
		TraceLog.Exit();
		void addVRProfile(iniConfig ini2, string text)
		{
			if (!ContainsKey(text) && File.Exists(Path.Combine(ini2.getLocation(), ini2.getDX11Name(text))))
			{
				string text2 = Path.Combine(ini2.getLocation(), "app.ini");
				string text3 = text2 + "." + text;
				if (!File.Exists(text3))
				{
					text2.SafeFileCopy(text3);
				}
				text2 = Path.Combine(ini2.getLocation(), "core.ini");
				text3 = text2 + "." + text;
				if (!File.Exists(text3))
				{
					text2.SafeFileCopy(text3);
				}
				addProfile(text);
			}
		}
	}

	public appProfile FindByName(string name)
	{
		using (Enumerator enumerator = GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				KeyValuePair<string, appProfile> current = enumerator.Current;
				if (current.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					return current.Value;
				}
			}
		}
		return null;
	}

	public bool ContainsName(string name)
	{
		using (Enumerator enumerator = GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				if (enumerator.Current.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		return false;
	}

	public void Load()
	{
		TraceLog.Enter("appProfileList.Load()");
		foreach (appProfile value in base.Values)
		{
			value.Load();
		}
		TraceLog.Exit();
	}

	public bool Save()
	{
		TraceLog.Enter("appProfileList.Save()");
		bool flag = false;
		bool flag2 = ExtnProcess.isIRacingRunning();
		using (Enumerator enumerator = GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				KeyValuePair<string, appProfile> current = enumerator.Current;
				if (current.Value.IsModified && (!flag2 || (!current.Value.IsMonitor && !current.Value.IsIRacingVR)))
				{
					flag = flag || current.Value.Save();
				}
			}
		}
		TraceLog.Exit();
		return flag;
	}

	public void Export(bool option)
	{
		string text = Ini.ReadKey("Name", "UserInfo", iniSidekick: true);
		if (text.Length == 0)
		{
			text = Environment.UserName;
		}
		for (int i = 0; i < text.Length; i++)
		{
			if (!((text[i] >= '0') & (text[i] <= '9')) && !((text[i] >= 'a') & (text[i] <= 'z')) && !((text[i] >= 'A') & (text[i] <= 'Z')))
			{
				text.Replace(text[i], '_');
			}
		}
		string text2 = Ini.ReadKey("MemberID", "UserInfo", iniSidekick: true);
		if (text2.Length == 0)
		{
			text2 = "000000";
		}
		string text3 = Path.Combine(iRacing.pathSidekick(), "irSidekick.Profiles." + text + "." + text2 + ".zip");
		if (File.Exists(text3))
		{
			File.Delete(text3);
		}
		ZipArchive val = ZipFile.Open(text3, (ZipArchiveMode)1);
		try
		{
			foreach (appProfile value in base.Values)
			{
				if (value.IsMonitor || value.IsBackup || option)
				{
					val.CreateEntryFromFile(value.irApp.Ini.getFullFileName(value.Name), value.irApp.Ini.getExportFileName(text, value.DisplayName), CompressionLevel.Fastest);
					val.CreateEntryFromFile(value.irCore.Ini.getFullFileName(value.Name), value.irCore.Ini.getExportFileName(text, value.DisplayName), CompressionLevel.Fastest);
					val.CreateEntryFromFile(value.irDX11.Ini.getFullFileName(value.Name), value.irDX11.Ini.getExportFileName(text, value.DisplayName), CompressionLevel.Fastest);
				}
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		text3.OpenExplorer();
	}

	public void Import()
	{
	}

	public bool IsModified()
	{
		foreach (appProfile value in base.Values)
		{
			if (value.IsModified)
			{
				return true;
			}
		}
		return false;
	}

	public void doBackup()
	{
		TraceLog.Enter("appProfileList.doBackup()");
		TraceLog.Info("Save iRacing profile");
		Monitor.Save();
		TraceLog.Info("Backup iRacing profile");
		Monitor.Backup();
		TraceLog.Info("Load Backup profile");
		Backup.Load();
		TraceLog.Exit();
	}

	public void doRestore()
	{
		TraceLog.Enter("appProfileList.doRestore()");
		TraceLog.Info("Save Backup profile");
		Backup.Save();
		TraceLog.Info("Restore iRacing profile");
		Monitor.Restore();
		TraceLog.Info("Load iRacing profile");
		Monitor.Load();
		TraceLog.Exit();
	}

	private int getColour()
	{
		return inxColour++;
	}

	public appProfile addProfile(string name)
	{
		TraceLog.Verbose("appProfileList.addProfile('" + name + "')");
		appProfile appProfile2 = null;
		try
		{
			appProfile2 = new appProfile(colourProfile.Amber.Pick(getColour(), name), name);
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "Exception encountered creating appProfile " + name);
			appProfile2 = new appProfile(colourProfile.Gray, name);
		}
		try
		{
			Add(name, appProfile2);
		}
		catch (Exception ex2)
		{
			TraceLog.Exception(ex2, "Exception encountered adding profile " + name + " to the profile list");
		}
		return appProfile2;
	}

	public void CreateBackupIfNone()
	{
		TraceLog.Enter("appProfileList.CreateBackupIfNone()");
		if (TryGetValue("Backup", out var value))
		{
			TraceLog.Exit("No action, backup already exists");
			return;
		}
		if (!TryGetValue("", out var value2))
		{
			TraceLog.Error("Could not find current profile, no app.ini ???");
			TraceLog.Exit();
			return;
		}
		value2.Backup();
		value = addProfile("Backup");
		value.Load();
		TraceLog.Exit();
	}

	public List<Brush> Compare(string profile, string tag)
	{
		List<Brush> brushes = new List<Brush>();
		if (!ContainsKey(profile))
		{
			return brushes;
		}
		appProfile appProfile2 = base[profile];
		using (Enumerator enumerator = GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				KeyValuePair<string, appProfile> current = enumerator.Current;
				if (current.Value.Name == appProfile2.Name || current.Value.Tab.Visibility != Visibility.Visible)
				{
					continue;
				}
				string text = current.Value.tagGetValue(tag, LogNotFound: false);
				string text2 = appProfile2.tagGetValue(tag, LogNotFound: false);
				try
				{
					double value = double.Parse(text, new CultureInfo("en-US"));
					double value2 = double.Parse(text2, new CultureInfo("en-US"));
					if (Math.Round(value, 6) != Math.Round(value2, 6))
					{
						AddBrush(current.Value);
					}
				}
				catch
				{
					if (text != text2)
					{
						AddBrush(current.Value);
					}
				}
			}
		}
		return brushes;
		void AddBrush(appProfile x)
		{
			brushes.Add(x.Colour.Brush());
		}
	}

	public void ValueLabelsShow(string tag)
	{
		using (Enumerator enumerator = GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				KeyValuePair<string, appProfile> current = enumerator.Current;
				current.Value.Lab.Content = current.Value.tagGetDisplay(tag);
			}
		}
		tagValueLabels = tag;
	}

	public void ValueLabelsClear()
	{
		if (tagValueLabels.Length == 0)
		{
			return;
		}
		tagValueLabels = "";
		using Enumerator enumerator = GetEnumerator();
		while (enumerator.MoveNext())
		{
			enumerator.Current.Value.Lab.Content = "";
		}
	}
}
