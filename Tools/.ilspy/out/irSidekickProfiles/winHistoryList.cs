using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using MahApps.Metro.Controls;

namespace irSidekickProfiles;

public class winHistoryList : MetroWindow, IComponentConnector
{
	private HistoryList ProfileHistory;

	internal Label lblProfile;

	internal ListBox lstHistory;

	internal Button btnRestore;

	internal Button btnCancel;

	private bool _contentLoaded;

	public winMain winMain => base.Owner as winMain;

	public winHistoryList()
	{
		InitializeComponent();
	}

	public static bool SelectRestore(winMain winMain)
	{
		winHistoryList winHistoryList2 = new winHistoryList();
		winHistoryList2.Owner = winMain;
		winHistoryList2.ProfileHistory = appHistory.History.GetProfileHistory(winMain.ProfileView.DisplayName);
		winHistoryList2.lstHistory.ItemsSource = winHistoryList2.ProfileHistory;
		return winHistoryList2.ShowDialog().Value;
	}

	private void winClosed(object sender, EventArgs e)
	{
	}

	private void winLoaded(object sender, RoutedEventArgs e)
	{
		lblProfile.Content = "History of: " + winMain.ProfileView.DisplayName;
	}

	private void lstHistory_Select(object sender, SelectionChangedEventArgs e)
	{
		btnRestore.IsEnabled = lstHistory.SelectedItem != null;
	}

	private void btnRestore_Click(object sender, RoutedEventArgs e)
	{
		HistoryProfile historyProfile = lstHistory.SelectedItem as HistoryProfile;
		if (winMain.ConfirmAction("This action will retore the " + historyProfile.Name + " profile\nback to " + historyProfile.DateCreated + ".", "Confirm History Restore Action"))
		{
			appHistory.RestoreProfile(historyProfile);
			winMain.ProfileView.Load();
			winMain.loadControlValues();
			base.DialogResult = true;
			Close();
		}
	}

	private void btnCancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/irSidekickProfiles;component/winhistorylist.xaml", UriKind.Relative);
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
			((winHistoryList)target).Loaded += winLoaded;
			((winHistoryList)target).Closed += winClosed;
			break;
		case 2:
			lblProfile = (Label)target;
			break;
		case 3:
			lstHistory = (ListBox)target;
			lstHistory.SelectionChanged += lstHistory_Select;
			break;
		case 4:
			btnRestore = (Button)target;
			btnRestore.Click += btnRestore_Click;
			break;
		case 5:
			btnCancel = (Button)target;
			btnCancel.Click += btnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
