using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace DynLOD;

internal sealed class RangeRow : Border
{
    public readonly TextBox MinBox = new(), MaxBox = new();
    public readonly RangeTrack Track = new();
    public event Action? Edited;
    bool updating;
    public RangeRow(string title)
    {
        Background = Brushes.Transparent; BorderBrush = Brush("#2B2D31"); BorderThickness = new(0,1,0,0);
        Padding = new(0,1,0,1); Margin = new(0,0,0,1);
        var panel = new StackPanel(); Child = panel;
        panel.Children.Add(new TextBlock { Text = title, FontSize = 11, Foreground = Brush("#D8D8D8") });
        var inputs = new Grid { Height = 20 };
        inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        panel.Children.Add(inputs);
        foreach (var (label, box, column) in new[] { ("Min", MinBox, 0), ("Max", MaxBox, 2) })
        {
            box.MinWidth = 0; box.Padding = new(3,0,3,0); box.FontSize = 11; box.TextAlignment = TextAlignment.Center;
            box.VerticalContentAlignment = VerticalAlignment.Center; box.ToolTip = label;
            AutomationProperties.SetName(box, title + " " + label);
            Grid.SetColumn(box, column); inputs.Children.Add(box); box.TextChanged += (_, _) => FromText();
        }
        Grid.SetColumn(Track, 1); Track.Margin = new(3,0,3,0); inputs.Children.Add(Track);
        Track.SetNames(title);
        Track.Changed += () => { updating = true; MinBox.Text = Track.Minimum.ToString(); MaxBox.Text = Track.Maximum.ToString(); updating = false; FromText(); };
    }
    public void Load(string min, string max) { updating = true; MinBox.Text = min; MaxBox.Text = max; updating = false; FromText(); }
    void FromText()
    {
        if (updating) return;
        bool minOk = int.TryParse(MinBox.Text, out int min) && min >= 25 && min <= 500;
        bool maxOk = int.TryParse(MaxBox.Text, out int max) && max >= 25 && max <= 500;
        bool valid = minOk && maxOk && min <= max;
        MinBox.BorderBrush = MaxBox.BorderBrush = Brush(valid ? "#3A3D42" : "#EC3038");
        Track.IsEnabled = valid;
        if (valid) { Track.Minimum = min; Track.Maximum = max; Track.ArrangeHandles(); }
        Edited?.Invoke();
    }
    internal static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
}

internal sealed class RangeTrack : Canvas
{
    public int Minimum = 25, Maximum = 500;
    public int MinimumCeiling = 500, MaximumFloor = 25;
    public event Action? Changed;
    readonly Thumb low = MakeThumb(true), high = MakeThumb(false);
    double dragStartX, dragStartValue;
    double Span => Math.Max(1, ActualWidth - 24);
    double X(double value) => 12 + (value - 25) / 475 * Span;
    public RangeTrack()
    {
        Height = 20; Background = Brushes.Transparent;
        Children.Add(low); Children.Add(high);
        SizeChanged += (_, _) => ArrangeHandles();
        foreach (var thumb in new[] { low, high })
        {
            bool isLow = thumb == low;
            thumb.DragStarted += (_, _) => { dragStartX = Mouse.GetPosition(this).X; dragStartValue = isLow ? Minimum : Maximum; thumb.Focus(); };
            thumb.DragDelta += (_, _) => DragTo(isLow, Mouse.GetPosition(this).X);
            thumb.KeyDown += (_, e) =>
            {
                int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 25 : 1;
                int value = isLow ? Minimum : Maximum;
                if (e.Key == Key.Left || e.Key == Key.Down) value -= step;
                else if (e.Key == Key.Right || e.Key == Key.Up) value += step;
                else if (e.Key == Key.Home) value = 25;
                else if (e.Key == Key.End) value = 500;
                else return;
                Move(isLow, value); e.Handled = true;
            };
        }
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource != this) return;
            double pos = e.GetPosition(this).X;
            bool isLow = Minimum == Maximum ? pos < X(Minimum) : Math.Abs(pos - X(Minimum)) <= Math.Abs(pos - X(Maximum));
            Move(isLow, Snap(25 + (pos - 12) / Span * 475));
            (isLow ? low : high).Focus(); e.Handled = true;
        };
    }
    public void SetNames(string name) { AutomationProperties.SetName(low, name + " minimum handle"); AutomationProperties.SetName(high, name + " maximum handle"); }
    internal static int Snap(double value)
    {
        double nearest = Math.Round(value / 25) * 25;
        return (int)Math.Round(Math.Clamp(Math.Abs(value - nearest) <= 4 ? nearest : value, 25, 500));
    }
    internal void DragTo(bool minimum, double pointerX) => Move(minimum, Snap(dragStartValue + (pointerX - dragStartX) / Span * 475));
    internal void Move(bool isLow, int value)
    {
        if (isLow) Minimum = Math.Clamp(value, 25, Math.Min(Maximum,MinimumCeiling));
        else Maximum = Math.Clamp(value, Math.Max(Minimum,MaximumFloor), 500);
        ArrangeHandles(); Changed?.Invoke();
    }
    public void ArrangeHandles()
    {
        // Outside-facing halves meet as one split capsule when the values coincide.
        // Their hit regions never overlap, even for equal or nearly equal values.
        SetLeft(low, X(Minimum) - 12); SetLeft(high, X(Maximum));
        SetTop(low, -1); SetTop(high, -1);
        low.ToolTip = "Min " + Minimum + " · Arrow keys: 1 · Shift + arrows: 25";
        high.ToolTip = "Max " + Maximum + " · Arrow keys: 1 · Shift + arrows: 25";
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRoundedRectangle(RangeRow.Brush("#2B2D31"), null, new Rect(12,9,Span,4),2,2);
        dc.DrawRoundedRectangle(RangeRow.Brush(IsEnabled ? "#C90012" : "#45474B"), null, new Rect(X(Minimum),9,Math.Max(0,X(Maximum)-X(Minimum)),4),2,2);
        dc.DrawLine(new Pen(RangeRow.Brush("#9C9EA4"),1), new Point(X(100),5), new Point(X(100),17));
    }
    static Thumb MakeThumb(bool minimum) => new()
    {
        Width = 12, Height = 24, Focusable = true, Cursor = Cursors.Hand,
        Template = (ControlTemplate)XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Thumb">
        <Grid Background="Transparent"><Border x:Name="Grip" Width="7" Height="12" HorizontalAlignment="ALIGNMENT" Margin="MARGIN" Background="#E02A35" BorderBrush="#FF5960" BorderThickness="1" CornerRadius="CORNERS"/></Grid>
        <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Grip" Property="Background" Value="#FF5960"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Grip" Property="BorderBrush" Value="White"/><Setter TargetName="Grip" Property="BorderThickness" Value="2"/></Trigger></ControlTemplate.Triggers></ControlTemplate>
        """.Replace("ALIGNMENT", minimum ? "Right" : "Left")
            .Replace("MARGIN", minimum ? "0,0,1,0" : "1,0,0,0")
            .Replace("CORNERS", minimum ? "7,0,0,7" : "0,7,7,0"))
    };
}


