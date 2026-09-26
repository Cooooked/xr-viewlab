using System.Windows.Input;

namespace irSidekickProfiles;

public static class appCommands
{
	public static readonly RoutedUICommand Run = new RoutedUICommand("Run iRacing", "Run", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand Forum = new RoutedUICommand("Profiles thread on the iRacing forum", "Forum", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand Discord = new RoutedUICommand("irSidekick Discord", "Discord", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand Help = new RoutedUICommand("YouTube Tutorials", "Help", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand Backup = new RoutedUICommand("Backup", "Backup", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand Restore = new RoutedUICommand("Restore", "Restore", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand History = new RoutedUICommand("History", "Restore from history", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand Export = new RoutedUICommand("Export", "Export", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand Import = new RoutedUICommand("Import", "Import", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand SaveAll = new RoutedUICommand("Save All", "SaveAll", typeof(appCommands), new InputGestureCollection
	{
		new KeyGesture(Key.S, ModifierKeys.Control)
	});

	public static readonly RoutedUICommand BackupIRacing = new RoutedUICommand("Copy from iRacing", "BackupIRacing", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand ApplyProfile = new RoutedUICommand("Apply Profile", "ApplyProfile", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand CopyProfile = new RoutedUICommand("Copy Profile", "CopyProfile", typeof(appCommands), new InputGestureCollection
	{
		new KeyGesture(Key.C, ModifierKeys.Control)
	});

	public static readonly RoutedUICommand RenameProfile = new RoutedUICommand("Rename Profile", "RenameProfile", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand DeleteProfile = new RoutedUICommand("Delete Profile", "DeleteProfile", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand Settings = new RoutedUICommand("Settings", "Settings", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand Update = new RoutedUICommand("Update", "Update", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand Exit = new RoutedUICommand("Exit Program", "Exit", typeof(appCommands), new InputGestureCollection
	{
		new KeyGesture(Key.F4, ModifierKeys.Alt)
	});

	public static readonly RoutedUICommand GFXLow = new RoutedUICommand("Low GFX Settings", "Low", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand GFXFaster = new RoutedUICommand("Faster (lower) GFX Settings", "Faster", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand GFXStandard = new RoutedUICommand("Standard GFX Settings", "Standard", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand GFXPretty = new RoutedUICommand("Pretty GFX Settings", "Pretty", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand GFXHigh = new RoutedUICommand("High GFX Settings", "High", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand GFXSync = new RoutedUICommand("Copy Graphic Settings to Replay", "Sync", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand GFXVRR = new RoutedUICommand("Variable Refresh Rate Settings", "VRR", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand GFXWizard = new RoutedUICommand("GFX Wizard", "GFXWizard", typeof(appCommands), new InputGestureCollection());

	public static readonly RoutedUICommand SettingSearch = new RoutedUICommand("Search Settings", "SettingSearch", typeof(appCommands), new InputGestureCollection
	{
		new KeyGesture(Key.F, ModifierKeys.Control)
	});

	public static readonly RoutedUICommand CopySettings = new RoutedUICommand("Copy settings to target profile", "Copy settings", typeof(appCommands), new InputGestureCollection());

	public static void InvalidateRequerySuggested()
	{
		CommandManager.InvalidateRequerySuggested();
	}
}
