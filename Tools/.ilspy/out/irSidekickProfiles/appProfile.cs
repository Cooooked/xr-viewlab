#define TRACE
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using irSidekick;

namespace irSidekickProfiles;

public class appProfile
{
	public colourProfile Colour;

	public string Name = "";

	public appConfig irApp;

	public appConfig irCore;

	public appConfig irDX11;

	public appCameraList CarCameras;

	public DateTime LastChanged = new DateTime(2020, 1, 1);

	public MetroTabItem Tab = new MetroTabItem();

	public ComboBoxItem Cbo = new ComboBoxItem();

	public Label Lab = new Label();

	public List<string> Notes = new List<string>();

	public bool IsModified;

	public string DisplayName
	{
		get
		{
			if (!IsMonitor)
			{
				return Name;
			}
			return "Monitor";
		}
	}

	public bool IsBackup => Name.Equals("Backup");

	public bool IsMonitor => Name.IsNullOrEmpty();

	public bool IsOculus => Name.Equals("Oculus");

	public bool IsOpenVR => Name.Equals("OpenVR");

	public bool IsOpenXR => Name.Equals("OpenXR");

	public bool IsIRacingVR
	{
		get
		{
			if (!IsOculus && !IsOpenVR)
			{
				return IsOpenXR;
			}
			return true;
		}
	}

	public bool IsDisplayModeMonitor => DisplayMode == iniDisplayMode.Monitor;

	public bool IsDisplayModeVR => DisplayMode != iniDisplayMode.Monitor;

	public iniDisplayMode DisplayMode
	{
		get
		{
			iniDisplayMode iniDisplayMode2 = ProfileName.DisplayMode(Name);
			if (iniDisplayMode2 != iniDisplayMode.Custom)
			{
				return iniDisplayMode2;
			}
			return irDX11.DisplayMode;
		}
		set
		{
			if (irDX11.DisplayMode != value && ProfileName.DisplayMode(Name) == iniDisplayMode.Custom)
			{
				irDX11.DisplayMode = value;
				IsModified = true;
			}
		}
	}

	public string NoteToolTip(string theDefault = null)
	{
		if (Notes == null || Notes.Count == 0)
		{
			return theDefault.IfNullOrEmpty(string.Empty);
		}
		return Notes[0];
	}

	public appProfile(colourProfile colour, string name)
	{
		Colour = colour;
		Name = name;
		irApp = new appConfig(iniConfig.App);
		irCore = new appConfig(iniConfig.Core);
		irDX11 = new appConfig(iniConfig.DX11);
		CarCameras = new appCameraList(Name);
		Tab.Tag = name;
		Tab.Width = 220.0;
		Tab.Height = 32.0;
		Tab.IsTabStop = false;
		Tab.Header = DisplayName;
		Cbo.Content = DisplayName;
		Cbo.Tag = Name;
		Lab.Margin = new Thickness(2.0);
		Lab.Background = Colour.Brush();
		Lab.VerticalAlignment = VerticalAlignment.Stretch;
		Lab.HorizontalAlignment = HorizontalAlignment.Stretch;
		Lab.HorizontalContentAlignment = HorizontalAlignment.Right;
		UpdateToolTip();
	}

	public void UpdateToolTip()
	{
		string text = Ini.ReadKey(DisplayName, "Origin").IfNullOrEmpty("unknown");
		if (IsMonitor)
		{
			Tab.ToolTip = "iRacing Monitor profile (from '" + text + "')";
		}
		else if (ProfileName.IsiRacingVR(Name))
		{
			Tab.ToolTip = "iRacing VR profile (from '" + text + "')";
		}
		else if (IsBackup)
		{
			Tab.ToolTip = "A backup of the '" + text + "' profile";
		}
		else
		{
			Tab.ToolTip = "A custom user profile";
		}
	}

	public void CloneFrom(appProfile source)
	{
		if (this != source)
		{
			TraceLog.Info("Profile " + source.DisplayName + " applied to " + DisplayName);
			irApp.CloneFrom(source.irApp);
			irCore.CloneFrom(source.irCore);
			irDX11.CloneFrom(source.irDX11);
			if (!ProfileName.IsBackup(Name) && ProfileName.DisplayMode(Name) == iniDisplayMode.Custom)
			{
				irDX11.VRNormalize(source.Name);
			}
			CarCameras.CloneFrom(source.CarCameras);
			IsModified = true;
		}
	}

	public void Load()
	{
		TraceLog.Info("Load profile " + DisplayName);
		appHistory.logProfile(DisplayName);
		NotesLoad();
		irApp.Load(Name);
		irCore.Load(Name);
		irDX11.Load(Name);
		CarCameras.Load();
		IsModified = false;
		LastChanged = irDX11.LastChanged;
	}

	private string NoteFileName()
	{
		string text = (Name.IsNullOrEmpty() ? "" : ("." + Name));
		return Path.Combine(iRacing.pathRoot(), "Notes.txt" + text);
	}

	private void NotesLoad()
	{
		Notes = new List<string>();
		string path = NoteFileName();
		if (!File.Exists(path))
		{
			return;
		}
		try
		{
			string[] collection = File.ReadAllLines(path, Encoding.UTF8);
			Notes.AddRange(collection);
			if (!IsBackup && !IsMonitor)
			{
				string theDefault = (IsIRacingVR ? "An iRacing VR profile" : "A custom user profile");
				string toolTip = NoteToolTip(theDefault);
				Tab.ToolTip = toolTip;
			}
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "Error loading notes for profile " + DisplayName);
		}
	}

	public bool Save()
	{
		TraceLog.Info("Save profile " + DisplayName);
		bool result = Validations();
		NotesSave();
		irApp.Save(Name);
		irCore.Save(Name);
		irDX11.Save(Name);
		IsModified = false;
		return result;
	}

	public bool Validations()
	{
		return false;
	}

	private void NotesSave()
	{
		string path = NoteFileName();
		if (Notes == null || Notes.Count == 0)
		{
			if (File.Exists(path))
			{
				try
				{
					File.Delete(path);
					return;
				}
				catch (Exception ex)
				{
					TraceLog.Exception(ex, "Error deleting notes for profile " + DisplayName);
					return;
				}
			}
			return;
		}
		try
		{
			File.WriteAllLines(path, Notes, Encoding.UTF8);
		}
		catch (Exception ex2)
		{
			TraceLog.Exception(ex2, "Error saving notes for profile " + DisplayName);
		}
	}

	private void NotesDelete()
	{
		string path = NoteFileName();
		if (File.Exists(path))
		{
			try
			{
				File.Delete(path);
			}
			catch (Exception ex)
			{
				TraceLog.Exception(ex, "Error deleting notes for profile " + DisplayName);
			}
		}
	}

	public void NotesUpdate(List<string> notes)
	{
		Notes.Clear();
		Notes.AddRange(notes);
		if (!IsBackup && !IsMonitor)
		{
			Tab.ToolTip = NoteToolTip(IsIRacingVR ? "An iRacing VR profile" : "A custom user profile");
		}
	}

	public void Backup()
	{
		if (IsMonitor)
		{
			TraceLog.Info("Backup profile " + DisplayName);
			irApp.Backup();
			irCore.Backup();
			irDX11.Backup();
		}
	}

	public void Restore()
	{
		if (IsMonitor)
		{
			TraceLog.Info("Restore profile " + DisplayName);
			irApp.Restore();
			irCore.Restore();
			irDX11.Restore();
		}
	}

	public void Delete()
	{
		if (!IsMonitor && !IsBackup)
		{
			TraceLog.Info("Delete profile " + DisplayName);
			irApp.Delete(Name);
			irCore.Delete(Name);
			irDX11.Delete(Name);
			CarCameras.Delete();
			NotesDelete();
		}
	}

	public bool IsDifferent()
	{
		Task<bool>[] array = new Task<bool>[3]
		{
			Task.Run(() => IsDifferentConfig(Name, irApp)),
			Task.Run(() => IsDifferentConfig(Name, irCore)),
			Task.Run(() => IsDifferentConfig(Name, irDX11))
		};
		Task[] tasks = array;
		Task.WaitAll(tasks);
		if (!array[0].Result && !array[1].Result)
		{
			return array[2].Result;
		}
		return true;
	}

	private bool IsDifferentConfig(string name, appConfig iniMemory)
	{
		appConfig obj = new appConfig(iniMemory.Ini);
		obj.Load(Name, Fixup: false);
		return obj.IsDifferent(name, iniMemory);
	}

	public string tagGetValue(iniSetting setting, bool LogNotFound = true)
	{
		return tagGetValue(setting.TagID, LogNotFound);
	}

	public string tagGetValue(string tag, bool LogNotFound = true)
	{
		if (tag.Length == 0)
		{
			throw new Exception("tagGetValue called with no tag value");
		}
		string[] array = tag.Split('.');
		if (array.Length != 3)
		{
			throw new Exception("tagGetValue called with incorrectly formatted tag: " + tag);
		}
		try
		{
			string text = array[0].IfNullOrEmpty("");
			switch (text)
			{
			default:
				if (text.Length != 0)
				{
					break;
				}
				throw new Exception("tagGetValue called with no tag prefix: '" + tag + "'");
			case "0":
				return irApp.tagGetValue(array[1], array[2]);
			case "1":
				return irCore.tagGetValue(array[1], array[2]);
			case "2":
				return irDX11.tagGetValue(array[1], array[2]);
			case null:
				break;
			}
			throw new Exception("tagGetValue called with invalid tag prefix: " + tag);
		}
		catch (KeyNotFoundException)
		{
			if (LogNotFound)
			{
				TraceLog.Error("Profile '" + DisplayName + "' tagGetValue(" + tag + "): Tag not found");
			}
			return string.Empty;
		}
	}

	public void CopyGFX2Replay()
	{
		appSection appSection2 = irDX11["Graphics Options"];
		foreach (appSetting value in irDX11["Replay Graphics"].Values)
		{
			if (!"DepthOfField,ReplayRenderModes".Contains(value.Name) && appSection2.existsSetting(value.Name))
			{
				tagSetValue(value.tagFull, appSection2[value.Name].Value);
			}
		}
	}

	public bool tagSetValue(iniSetting setting, string value)
	{
		return tagSetValue(setting.TagID, value);
	}

	public bool tagSetValue(string tag, string value)
	{
		if (tag.Length == 0)
		{
			throw new Exception("tagSetValue called with no tag value");
		}
		string[] array = tag.Split('.');
		if (array.Length != 3)
		{
			throw new Exception("tagSetValue called with incorrectly formatted tag: " + tag);
		}
		try
		{
			if (array[2].Equals("SetFrameRateToRefreshRate"))
			{
				irDX11.tagSetValue("Graphics Options", "LimitFrameRate", "1");
				int num = irDX11.tagGetValue("Display", "RefreshRate").IfNullOrEmpty("64").ToInt() - 4;
				irDX11.tagSetValue("Graphics Options", "DesiredFPSLimit", num.ToString("D"));
				return true;
			}
			string text = array[0].IfNullOrEmpty("");
			string text2;
			switch (text)
			{
			default:
				if (text.Length != 0)
				{
					goto case null;
				}
				throw new Exception("tagSetValue called with no tag prefix: '" + tag + "'");
			case "0":
				text2 = irApp.tagGetValue(array[1], array[2]);
				break;
			case "1":
				text2 = irCore.tagGetValue(array[1], array[2]);
				break;
			case "2":
				text2 = irDX11.tagGetValue(array[1], array[2]);
				break;
			case null:
				throw new Exception("tagSetValue called with invalid tag prefix: " + tag);
			}
			if (text2 == value)
			{
				return false;
			}
			IsModified = true;
			TraceLog.Info("Profile '" + DisplayName + "' Tag " + tag + " value changed from '" + text2 + "' to '" + value + "'");
			text = array[0].IfNullOrEmpty("");
			switch (text)
			{
			default:
				if (text.Length != 0)
				{
					goto case null;
				}
				throw new Exception("tagSetValue called with no tag prefix: '" + tag + "'");
			case "0":
				irApp.tagSetValue(array[1], array[2], value);
				break;
			case "1":
				irCore.tagSetValue(array[1], array[2], value);
				break;
			case "2":
				irDX11.tagSetValue(array[1], array[2], value);
				break;
			case null:
				throw new Exception("tagSetValue called with invalid tag prefix: " + tag);
			}
			return true;
		}
		catch (KeyNotFoundException)
		{
			TraceLog.Error("Profile '" + DisplayName + "' tagSetValue(" + tag + "): Tag not found");
			return false;
		}
	}

	public string tagGetDisplay(string tag, string input = null)
	{
		if (tag.Length == 0)
		{
			throw new Exception("tagGetDisplay called with no tag value");
		}
		string[] array = tag.Split('.');
		if (array.Length != 3)
		{
			throw new Exception("tagGetDisplay called with incorrectly formatted tag: " + tag);
		}
		try
		{
			string text = array[0].IfNullOrEmpty("");
			switch (text)
			{
			default:
				if (text.Length != 0)
				{
					break;
				}
				throw new Exception("tagGetDisplay called with no tag prefix: " + tag);
			case "0":
				return irApp.tagGetDisplay(array[1], array[2], input);
			case "1":
				return irCore.tagGetDisplay(array[1], array[2], input);
			case "2":
				return irDX11.tagGetDisplay(array[1], array[2], input);
			case null:
				break;
			}
			throw new Exception("tagGetDisplay called with invalid tag prefix: " + tag);
		}
		catch (KeyNotFoundException)
		{
			TraceLog.Error("Profile '" + DisplayName + "' tagGetDisplay(" + tag + "): Tag not found");
			return "Null";
		}
	}

	public string tagGetGFXPreset(gfxPreset preset, string tag)
	{
		if (tag.Length == 0)
		{
			throw new Exception("tagGetGFXPreset called with no tag value");
		}
		string[] array = tag.Split('.');
		if (array.Length != 3)
		{
			throw new Exception("tagGetGFXPreset called with incorrectly formatted tag: " + tag);
		}
		try
		{
			string text = array[2];
			if (!(text == "DesiredFPSLimit"))
			{
				if (text == "LODMinFPSTarget")
				{
					return Math.Round((double)tagGetValue("2.Display.RefreshRate").ToInt().Max(240) * 0.85).ToIntStr();
				}
				return irDX11.tagGetGFXPreset(preset, array[2]);
			}
			return (tagGetValue("2.Display.RefreshRate").ToInt().Max(240) - 4).ToIntStr();
		}
		catch (KeyNotFoundException)
		{
			TraceLog.Error("Profile '" + DisplayName + "' tagGetGFXPreset(" + tag + "): Tag not found");
			return "";
		}
	}

	public Rect getDisplayBounds()
	{
		return irDX11.getDisplayBounds("Display");
	}
}
