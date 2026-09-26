using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using MahApps.Metro.Controls;
using irSidekick;

namespace irSidekickProfiles;

public class winSettings : Window, IComponentConnector
{
	private bool ShowChanged;

	private bool ShowBackupProfileOriginal;

	private bool ShowMonitorProfileOriginal;

	private bool ShowOculusProfileOriginal;

	private bool ShowOpenVRProfileOriginal;

	private bool ShowOpenXRProfileOriginal;

	internal CheckBox chkConfirmActions;

	internal CheckBox chkShowBackupProfile;

	internal CheckBox chkShowMonitorProfile;

	internal CheckBox chkShowOculusProfile;

	internal CheckBox chkShowOpenVRProfile;

	internal CheckBox chkShowOpenXRProfile;

	internal CheckBox chkPreserveMainVolume;

	internal CheckBox chkAutoExitOnLaunch;

	internal CheckBox chkMismatchedMonitorKluge;

	internal CheckBox chkGSyncHack;

	private bool _contentLoaded;

	private winMain winMain => base.Owner as winMain;

	public winSettings()
	{
		InitializeComponent();
	}

	private void winClosed(object sender, EventArgs e)
	{
		SetProfileVisibility("Backup", winMain.ShowBackupProfile);
		SetProfileVisibility("", winMain.ShowMonitorProfile);
		SetProfileVisibility("Oculus", winMain.ShowOculusProfile);
		SetProfileVisibility("OpenVR", winMain.ShowOpenVRProfile);
		SetProfileVisibility("OpenXR", winMain.ShowOpenXRProfile);
		if (winMain.ProfileView.Tab.Visibility == Visibility.Hidden)
		{
			foreach (appProfile value in winMain.ProfileList.Values)
			{
				if (value.Tab.Visibility == Visibility.Visible)
				{
					value.Tab.IsSelected = true;
					break;
				}
			}
			return;
		}
		if (ShowChanged)
		{
			winMain.loadControlValues();
		}
		void SetProfileVisibility(string name, bool show)
		{
			try
			{
				if (winMain.ProfileList.ContainsKey(name))
				{
					appProfile appProfile2 = winMain.ProfileList[name];
					MetroTabItem tab = appProfile2.Tab;
					Visibility visibility = (appProfile2.Lab.Visibility = ((!show) ? Visibility.Hidden : Visibility.Visible));
					tab.Visibility = visibility;
				}
			}
			catch
			{
			}
		}
	}

	private void winLoaded(object sender, RoutedEventArgs e)
	{
		chkConfirmActions.IsChecked = winMain.ConfirmActions;
		chkShowBackupProfile.IsChecked = (ShowBackupProfileOriginal = winMain.ShowBackupProfile);
		chkShowMonitorProfile.IsChecked = (ShowMonitorProfileOriginal = winMain.ShowMonitorProfile);
		chkShowOculusProfile.IsChecked = (ShowOculusProfileOriginal = winMain.ShowOculusProfile);
		chkShowOpenVRProfile.IsChecked = (ShowOpenVRProfileOriginal = winMain.ShowOpenVRProfile);
		chkShowOpenXRProfile.IsChecked = (ShowOpenXRProfileOriginal = winMain.ShowOpenXRProfile);
		chkPreserveMainVolume.IsChecked = winMain.PreserveMainVolume;
		chkAutoExitOnLaunch.IsChecked = winMain.AutoExitOnLaunch;
		chkGSyncHack.IsChecked = winMain.GSyncHack;
		chkMismatchedMonitorKluge.IsChecked = winMain.MismatchedMonitorKluge;
	}

	private void winRendered(object sender, EventArgs e)
	{
		this.ScreenshotBinding();
	}

	private void chkConfirmActions_Click(object sender, RoutedEventArgs e)
	{
		winMain.ConfirmActions = chkConfirmActions.IsChecked.Value;
		Ini.WriteKey("ConfirmActions", winMain.ConfirmActions.ToString(), "Options");
	}

	private void chkShowBackupProfile_Click(object sender, RoutedEventArgs e)
	{
		ShowChanged = true;
		winMain.ShowBackupProfile = chkShowBackupProfile.IsChecked.Value;
		Ini.WriteKey("ShowBackupProfile", winMain.ShowBackupProfile.ToString(), "Options");
	}

	private void chkShowMonitorProfile_Click(object sender, RoutedEventArgs e)
	{
		ShowChanged = true;
		winMain.ShowMonitorProfile = chkShowMonitorProfile.IsChecked.Value;
		Ini.WriteKey("ShowMonitorProfile", winMain.ShowMonitorProfile.ToString(), "Options");
	}

	private void chkShowOculusProfile_Click(object sender, RoutedEventArgs e)
	{
		ShowChanged = true;
		winMain.ShowOculusProfile = chkShowOculusProfile.IsChecked.Value;
		Ini.WriteKey("ShowOculusProfile", winMain.ShowOculusProfile.ToString(), "Options");
	}

	private void chkShowOpenVRProfile_Click(object sender, RoutedEventArgs e)
	{
		ShowChanged = true;
		winMain.ShowOpenVRProfile = chkShowOpenVRProfile.IsChecked.Value;
		Ini.WriteKey("ShowOpenVRProfile", winMain.ShowOpenVRProfile.ToString(), "Options");
	}

	private void chkShowOpenXRProfile_Click(object sender, RoutedEventArgs e)
	{
		ShowChanged = true;
		winMain.ShowOpenXRProfile = chkShowOpenXRProfile.IsChecked.Value;
		Ini.WriteKey("ShowOpenXRProfile", winMain.ShowOpenXRProfile.ToString(), "Options");
	}

	private void chkAutoExitOnLaunch_Click(object sender, RoutedEventArgs e)
	{
		winMain.AutoExitOnLaunch = chkAutoExitOnLaunch.IsChecked.Value;
		Ini.WriteKey("AutoExitOnLaunch", winMain.AutoExitOnLaunch.ToString(), "Options");
	}

	private void chkPreserveMainVolume_Click(object sender, RoutedEventArgs e)
	{
		winMain.PreserveMainVolume = chkPreserveMainVolume.IsChecked.Value;
		Ini.WriteKey("PreserveMainVolume", winMain.PreserveMainVolume.ToString(), "Options");
	}

	private void chkGSyncHack_Click(object sender, RoutedEventArgs e)
	{
		winMain.GSyncHack = chkGSyncHack.IsChecked.Value;
		Ini.WriteKey("GSyncHack", winMain.GSyncHack.ToString(), "Options");
		winMain.Monitor_Click(null, null);
	}

	private void chkMismatchedMonitorKluge_Click(object sender, RoutedEventArgs e)
	{
		winMain.MismatchedMonitorKluge = chkMismatchedMonitorKluge.IsChecked.Value;
		Ini.WriteKey("MismatchedMonitorKludge", winMain.MismatchedMonitorKluge.ToString(), "Options");
		winMain.Monitor_Click(null, null);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/irSidekickProfiles;component/winsettings.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			((winSettings)target).Loaded += winLoaded;
			((winSettings)target).Closed += winClosed;
			((winSettings)target).ContentRendered += winRendered;
			break;
		case 2:
			chkConfirmActions = (CheckBox)target;
			chkConfirmActions.Click += chkConfirmActions_Click;
			break;
		case 3:
			chkShowBackupProfile = (CheckBox)target;
			chkShowBackupProfile.Click += chkShowBackupProfile_Click;
			break;
		case 4:
			chkShowMonitorProfile = (CheckBox)target;
			chkShowMonitorProfile.Click += chkShowMonitorProfile_Click;
			break;
		case 5:
			chkShowOculusProfile = (CheckBox)target;
			chkShowOculusProfile.Click += chkShowOculusProfile_Click;
			break;
		case 6:
			chkShowOpenVRProfile = (CheckBox)target;
			chkShowOpenVRProfile.Click += chkShowOpenVRProfile_Click;
			break;
		case 7:
			chkShowOpenXRProfile = (CheckBox)target;
			chkShowOpenXRProfile.Click += chkShowOpenXRProfile_Click;
			break;
		case 8:
			chkPreserveMainVolume = (CheckBox)target;
			chkPreserveMainVolume.Click += chkPreserveMainVolume_Click;
			break;
		case 9:
			chkAutoExitOnLaunch = (CheckBox)target;
			chkAutoExitOnLaunch.Click += chkAutoExitOnLaunch_Click;
			break;
		case 10:
			chkMismatchedMonitorKluge = (CheckBox)target;
			chkMismatchedMonitorKluge.Click += chkMismatchedMonitorKluge_Click;
			break;
		case 11:
			chkGSyncHack = (CheckBox)target;
			chkGSyncHack.Click += chkGSyncHack_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
