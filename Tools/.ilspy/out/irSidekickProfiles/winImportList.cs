using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using MahApps.Metro.Controls;

namespace irSidekickProfiles;

public class winImportList : MetroWindow, IComponentConnector
{
	internal CheckBox chkAll;

	internal Label lblPrefix;

	internal ListBox lstProfiles;

	internal Button btnImport;

	internal Button btnCancel;

	private bool _contentLoaded;

	public ObservableCollection<SelectItem> SelectList { get; set; }

	public static string SelectProfiles(winMain winMain, string Prefix, List<string> ZipProfiles)
	{
		winImportList winImportList2 = new winImportList();
		winImportList2.Owner = winMain;
		winImportList2.lblPrefix.Content = "From: " + Prefix;
		winImportList2.LoadZipProfiles(ZipProfiles);
		if (winImportList2.ShowDialog().Value)
		{
			return winImportList2.getSelected();
		}
		return "";
	}

	public winImportList()
	{
		InitializeComponent();
		SelectList = new ObservableCollection<SelectItem>();
	}

	private void winClosed(object sender, EventArgs e)
	{
	}

	private void winLoaded(object sender, RoutedEventArgs e)
	{
	}

	private void LoadZipProfiles(List<string> ZipProfiles)
	{
		SelectList.Clear();
		chkAll.IsChecked = false;
		foreach (string ZipProfile in ZipProfiles)
		{
			SelectList.Add(new SelectItem(ZipProfile));
		}
		base.DataContext = this;
	}

	private string getSelected()
	{
		List<string> list = new List<string>();
		foreach (SelectItem select in SelectList)
		{
			if (select.IsSelected)
			{
				list.Add(select.ProfileName);
			}
		}
		return string.Join(",", list);
	}

	private void chkAll_Click(object sender, RoutedEventArgs e)
	{
		bool value = chkAll.IsChecked.Value;
		foreach (SelectItem select in SelectList)
		{
			select.IsSelected = value;
		}
		lstProfiles.Items.Refresh();
	}

	private void btnImport_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = true;
		Close();
	}

	private void btnCancel_Click(object sender, RoutedEventArgs e)
	{
		foreach (SelectItem select in SelectList)
		{
			select.IsSelected = false;
		}
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
			Uri resourceLocator = new Uri("/irSidekickProfiles;component/winimportlist.xaml", UriKind.Relative);
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
			((winImportList)target).Loaded += winLoaded;
			((winImportList)target).Closed += winClosed;
			break;
		case 2:
			chkAll = (CheckBox)target;
			chkAll.Click += chkAll_Click;
			break;
		case 3:
			lblPrefix = (Label)target;
			break;
		case 4:
			lstProfiles = (ListBox)target;
			break;
		case 5:
			btnImport = (Button)target;
			btnImport.Click += btnImport_Click;
			break;
		case 6:
			btnCancel = (Button)target;
			btnCancel.Click += btnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
