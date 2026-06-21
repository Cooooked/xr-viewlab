using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace XRViewLab.UI;

public partial class ProfileWindow : Window
{
	public double TopValue { get; private set; }

	public double BottomValue { get; private set; }

	public double HorizontalValue { get; private set; }

	public bool UseGlobal { get; private set; }

	public ProfileWindow(string appName, double top, double bottom, double horizontal)
	{
		InitializeComponent();
		AppNameText.Text = "App: " + appName;
		TopBox.Text = FormatScale(top);
		BottomBox.Text = FormatScale(bottom);
		HorizontalBox.Text = FormatScale(horizontal);
		UpdateHints();
	}

	private static string FormatScale(double value)
	{
		return value.ToString("0.00", CultureInfo.InvariantCulture);
	}

	private static string FormatPercent(double value)
	{
		return $"{Math.Clamp(value, 0.0, 1.0) * 100.0:0}%";
	}

	private static bool TryRead(string text, out double value)
	{
		if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
		{
			value = 0.0;
			return false;
		}
		value = Math.Clamp(value, 0.0, 1.0);
		return true;
	}

	private void UpdateHints()
	{
		double value;
		bool flag = TryRead(TopBox.Text, out value);
		double value2;
		bool flag2 = TryRead(BottomBox.Text, out value2);
		double value3;
		bool flag3 = TryRead(HorizontalBox.Text, out value3);
		TopHint.Text = (flag ? (FormatPercent(value) + " top") : "Enter top value");
		BottomHint.Text = (flag2 ? (FormatPercent(value2) + " bottom") : "Enter bottom value");
		HorizontalHint.Text = (flag3 ? (FormatPercent(value3) + " horizontal") : "Enter horizontal width");
		CombinedHint.Text = ((flag && flag2) ? ("Combined: " + FormatPercent(value + value2) + " total render height") : "Combined: enter valid values");
	}

	private void Values_Changed(object sender, TextChangedEventArgs e)
	{
		UpdateHints();
	}

	private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		DragMove();
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		if (!TryRead(TopBox.Text, out var value) || !TryRead(BottomBox.Text, out var value2) || !TryRead(HorizontalBox.Text, out var value3) || value + value2 < 0.01 || value3 < 0.01)
		{
			MessageBox.Show(this, "Enter values from 0.00 to 1.00. Combined vertical and horizontal must be at least 0.01.", "XR ViewLab", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		TopValue = value;
		BottomValue = value2;
		HorizontalValue = value3;
		base.DialogResult = true;
	}

	private void UseGlobal_Click(object sender, RoutedEventArgs e)
	{
		UseGlobal = true;
		base.DialogResult = true;
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
	}
}
