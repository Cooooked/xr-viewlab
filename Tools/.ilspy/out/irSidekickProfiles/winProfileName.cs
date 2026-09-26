using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using MahApps.Metro.Controls;
using irSidekick;

namespace irSidekickProfiles;

public class winProfileName : MetroWindow, IComponentConnector
{
	private appProfileList ProfileList;

	private static readonly Regex rxFileName = new Regex("[^0-9^a-z^A-Z_\\-\\[\\]{}()]+");

	internal TextBox txtName;

	internal Button btnApply;

	internal Button btnCancel;

	private bool _contentLoaded;

	public winProfileName()
	{
		InitializeComponent();
		txtName.Focus();
	}

	public static string getName(Window owner, appProfileList list)
	{
		winProfileName winProfileName2 = new winProfileName();
		winProfileName2.Owner = owner;
		winProfileName2.ProfileList = list;
		if (winProfileName2.ShowDialog() != true)
		{
			return "";
		}
		return winProfileName2.txtName.Text.Trim();
	}

	private void btnApply_Click(object sender, RoutedEventArgs e)
	{
		string text = txtName.Text.Trim();
		if (text.IsNullOrEmpty())
		{
			MessageBox.Show("Please enter a profile name", "Empty profile name");
			return;
		}
		if (ProfileName.IsiRacing(text))
		{
			MessageBox.Show("That profile name is reserved, please enter a different profile name", "Reserved profile name");
			return;
		}
		if (ProfileList.ContainsName(text))
		{
			MessageBox.Show("A profile with that name already exists, please provide a unique name", "Duplicate profile name");
			return;
		}
		base.DialogResult = true;
		Close();
	}

	private void btnCancel_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void FileNameInputFilter(object sender, TextCompositionEventArgs e)
	{
		e.Handled = rxFileName.IsMatch(e.Text);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/irSidekickProfiles;component/winprofilename.xaml", UriKind.Relative);
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
			txtName = (TextBox)target;
			txtName.PreviewTextInput += FileNameInputFilter;
			break;
		case 2:
			btnApply = (Button)target;
			btnApply.Click += btnApply_Click;
			break;
		case 3:
			btnCancel = (Button)target;
			btnCancel.Click += btnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
