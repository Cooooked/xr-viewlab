using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;

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
        AppNameText.Text = $"App: {appName}";
        TopBox.Text = FormatScale(top);
        BottomBox.Text = FormatScale(bottom);
        HorizontalBox.Text = FormatScale(horizontal);
        UpdateHints();
    }

    private static string FormatScale(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatPercent(double value) => $"{Math.Clamp(value, 0.0, 1.0) * 100.0:0}%";

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
        bool topOk = TryRead(TopBox.Text, out double top);
        bool bottomOk = TryRead(BottomBox.Text, out double bottom);
        bool horizontalOk = TryRead(HorizontalBox.Text, out double horizontal);
        TopHint.Text = topOk ? $"{FormatPercent(top)} top" : "Enter top value";
        BottomHint.Text = bottomOk ? $"{FormatPercent(bottom)} bottom" : "Enter bottom value";
        HorizontalHint.Text = horizontalOk ? $"{FormatPercent(horizontal)} horizontal" : "Enter horizontal width";
        CombinedHint.Text = topOk && bottomOk
            ? $"Combined: {FormatPercent(top + bottom)} total render height"
            : "Combined: enter valid values";
    }

    private void Values_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateHints();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(TopBox.Text, out double top) ||
            !TryRead(BottomBox.Text, out double bottom) ||
            !TryRead(HorizontalBox.Text, out double horizontal) ||
            top + bottom < 0.01 ||
            horizontal < 0.01)
        {
            MessageBox.Show(this, "Enter values from 0.00 to 1.00. Combined vertical and horizontal must be at least 0.01.", "XR ViewLab", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TopValue = top;
        BottomValue = bottom;
        HorizontalValue = horizontal;
        DialogResult = true;
    }

    private void UseGlobal_Click(object sender, RoutedEventArgs e)
    {
        UseGlobal = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
