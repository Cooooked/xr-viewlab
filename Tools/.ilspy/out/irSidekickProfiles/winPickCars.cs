using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using MahApps.Metro.Controls;
using irSidekick;

namespace irSidekickProfiles;

public class winPickCars : MetroWindow, IComponentConnector
{
	internal ListBox lstCars;

	internal Button btnApply;

	internal Button btnCancel;

	private bool _contentLoaded;

	public winPickCars()
	{
		InitializeComponent();
	}

	public static List<CarItem> PickCars(Window owner)
	{
		winPickCars winPickCars2 = new winPickCars();
		winPickCars2.Owner = owner;
		if (winPickCars2.ShowDialog() != true)
		{
			return null;
		}
		if (winPickCars2.lstCars.SelectedItems.Count == 0)
		{
			return null;
		}
		List<CarItem> list = new List<CarItem>();
		foreach (CarItem selectedItem in winPickCars2.lstCars.SelectedItems)
		{
			list.Add(selectedItem);
		}
		return list;
	}

	private void winLoaded(object sender, RoutedEventArgs e)
	{
		lstCars.ItemsSource = CarList.GlobalCarList.GetDisplayList(null);
	}

	private void btnApply_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = true;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/irSidekickProfiles;component/winpickcars.xaml", UriKind.Relative);
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
			((winPickCars)target).Loaded += winLoaded;
			break;
		case 2:
			lstCars = (ListBox)target;
			break;
		case 3:
			btnApply = (Button)target;
			btnApply.Click += btnApply_Click;
			break;
		case 4:
			btnCancel = (Button)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
