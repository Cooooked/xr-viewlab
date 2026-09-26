#define TRACE
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using irSidekick;
using irSidekick.Monitor;

namespace irSidekickProfiles;

public class clsMonitorItem : FrameworkElement, IComparable<clsMonitorItem>
{
	private Brush brushBorder;

	private Brush brushNumber;

	private Brush brushMonitor;

	private Brush brushSelected;

	private Pen strokeMonitor;

	public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent("Click", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(clsMonitorItem));

	public Rect PixelBounds { get; set; }

	public string Device { get; private set; }

	public bool Selected { get; set; }

	public bool IsPrimary { get; private set; }

	public int MonitorNum { get; set; }

	public int RefreshRate { get; set; }

	public string GraphicCard { get; set; }

	public string AspectRatio
	{
		get
		{
			double num = PixelBounds.Width / PixelBounds.Height;
			for (int i = 2; i < 13; i++)
			{
				double num2 = num * (double)i;
				if (num2 % 1.0 == 0.0 || num2 % 1.0 == 0.5)
				{
					return " " + num2.ToString("G0") + ":" + i.ToString("G0");
				}
			}
			return "";
		}
	}

	public string Resolution => PixelBounds.Width.ToString("F0") + "x" + PixelBounds.Height.ToString("F0") + AspectRatio;

	public event RoutedEventHandler Click
	{
		add
		{
			AddHandler(ClickEvent, value);
		}
		remove
		{
			RemoveHandler(ClickEvent, value);
		}
	}

	public int CompareTo(clsMonitorItem other)
	{
		if (other == null)
		{
			return 1;
		}
		return PixelBounds.Left.CompareTo(other.PixelBounds.Left);
	}

	public bool NotSameRow(clsMonitorItem other)
	{
		return PixelBounds.NotSameRow(other.PixelBounds);
	}

	public clsMonitorItem(int num, WpfScreen screen, hwMonitor monitor)
	{
		if (!screen.DeviceName.IsNullOrEmpty())
		{
			base.Name = screen.DeviceName.Replace("\\", "").Replace(".", "");
		}
		Device = screen.DeviceName;
		Selected = false;
		MonitorNum = num;
		IsPrimary = screen.Primary;
		PixelBounds = screen.PixelBounds;
		try
		{
			if (Device.Equals(monitor.Name))
			{
				MonitorNum = (int)monitor.ID;
				RefreshRate = monitor.RefreshRate;
				GraphicCard = monitor.GraphicsCard;
				PixelBounds = monitor.Bounds;
			}
		}
		catch
		{
		}
		TraceLog.Info(string.Format("Monitor{0}:\t{1}\tTop={2}\tLeft={3}\tWidth={4}\tHeight={5}", IsPrimary ? "*" : " ", Device, PixelBounds.Top.ToString().PadRight(6), PixelBounds.Left.ToString().PadRight(6), PixelBounds.Width, PixelBounds.Height));
		brushBorder = new SolidColorBrush(Color.FromRgb(192, 192, 192));
		brushNumber = new SolidColorBrush(Color.FromRgb(192, 192, 192));
		brushMonitor = new SolidColorBrush(Color.FromRgb(46, 46, 46));
		brushSelected = new SolidColorBrush(Color.FromRgb(19, 119, 167));
		strokeMonitor = new Pen(brushBorder, 4.0);
		base.PreviewMouseLeftButtonUp += delegate
		{
			DoClick();
		};
	}

	private void DoClick()
	{
		DoSelect(!Selected);
		RoutedEventArgs e = new RoutedEventArgs(ClickEvent);
		RaiseEvent(e);
	}

	private void DoSelect(bool select)
	{
		Selected = select;
		InvalidateVisual();
	}

	public void SetSelected(Rect irBounds)
	{
		DoSelect(WithinBounds(irBounds));
	}

	public bool WithinBounds(Rect irBounds)
	{
		if (PixelBounds.Left < irBounds.Left)
		{
			return false;
		}
		if (PixelBounds.Right > irBounds.Right)
		{
			return false;
		}
		if (PixelBounds.Top >= irBounds.Bottom)
		{
			return false;
		}
		if (PixelBounds.Bottom <= irBounds.Top)
		{
			return false;
		}
		return true;
	}

	protected override void OnRender(DrawingContext dc)
	{
		if (base.ActualWidth == 0.0 || base.ActualHeight == 0.0)
		{
			return;
		}
		Typeface typeface = new Typeface("Segoe UI");
		dc.DrawRectangle(rectangle: new Rect(4.0, 4.0, base.ActualWidth - 8.0, base.ActualHeight - 8.0), brush: Selected ? brushSelected : brushMonitor, pen: strokeMonitor);
		FormattedText formattedText = new FormattedText("Left: " + PixelBounds.Left.ToString("F0"), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, 12.0, brushNumber, 1.0);
		if (formattedText.Width < base.Width / 2.5)
		{
			dc.DrawText(formattedText, new Point(10.0, 8.0));
		}
		FormattedText formattedText2 = new FormattedText("Top: " + PixelBounds.Top.ToString("F0"), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, 12.0, brushNumber, 1.0);
		if (formattedText2.Width < base.Width / 2.5)
		{
			dc.DrawText(formattedText2, new Point(base.ActualWidth - formattedText2.Width - 10.0, 8.0));
		}
		if (IsPrimary)
		{
			if (!GraphicCard.IsNullOrEmpty())
			{
				FormattedText formattedText3 = new FormattedText(GraphicCard, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, 12.0, brushNumber, 1.0);
				dc.DrawText(formattedText3, new Point(base.ActualWidth / 2.0 - formattedText3.Width / 2.0, 8.0));
			}
			if (RefreshRate > 0)
			{
				FormattedText formattedText4 = new FormattedText(RefreshRate.ToString("D0") + "hz", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, 12.0, brushNumber, 1.0);
				dc.DrawText(formattedText4, new Point(10.0, base.ActualHeight - formattedText4.Height - 8.0));
			}
		}
		FormattedText formattedText5 = new FormattedText(Resolution, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, 16.0, brushNumber, 1.0);
		dc.DrawText(formattedText5, new Point(base.ActualWidth / 2.0 - formattedText5.Width / 2.0, base.ActualHeight - formattedText5.Height - 8.0));
		int num = 98;
		double num2 = base.ActualHeight - formattedText5.Height - 8.0;
		new TextDecorationCollection().Add(TextDecorations.Underline);
		string textToFormat = (char)(MonitorNum + 65) + (IsPrimary ? "*" : string.Empty);
		FormattedText formattedText6 = new FormattedText(textToFormat, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, num, brushNumber, 1.0);
		do
		{
			num -= 2;
			formattedText6 = new FormattedText(textToFormat, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), num, brushNumber, 1.0);
		}
		while (num >= 14 && formattedText6.Height > num2);
		if (IsPrimary)
		{
			formattedText6.SetFontSize(42.666666666666664, 1, 1);
		}
		dc.DrawText(formattedText6, new Point(base.ActualWidth / 2.0 - formattedText6.Width / 2.0, base.ActualHeight / 2.0 - formattedText6.Height / 2.0));
	}
}
