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

public class winTripleCurved : MetroWindow, IComponentConnector
{
	private bool _contentLoaded;

	public winTripleCurved()
	{
		InitializeComponent();
	}

	private void winRendered(object sender, EventArgs e)
	{
		this.ScreenshotBinding();
	}

	private void CheckBox_Checked(object sender, RoutedEventArgs e)
	{
		WarningUpdate(ShowWarning: false);
	}

	private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
	{
		WarningUpdate(ShowWarning: true);
	}

	private void WarningUpdate(bool ShowWarning)
	{
		(base.Owner as winMain).ShowTripleCurvedWarning = ShowWarning;
		Ini.WriteKey("ShowTripleCurvedWarning", ShowWarning, "Options");
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/irSidekickProfiles;component/wintriplecurved.xaml", UriKind.Relative);
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
			((winTripleCurved)target).ContentRendered += winRendered;
			break;
		case 2:
			((CheckBox)target).Checked += CheckBox_Checked;
			((CheckBox)target).Unchecked += CheckBox_Unchecked;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
