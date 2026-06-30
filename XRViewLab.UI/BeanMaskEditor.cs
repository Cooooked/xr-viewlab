using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace XRViewLab.UI;

public sealed class BeanMaskEditor : FrameworkElement
{
	private double _size = 1.0;
	private double _widthScale = 1.0;
	private double _heightScale = 1.0;
	private double _curve = 0.5;
	private double _offsetX;
	private double _offsetY;

	public event EventHandler? ShapeChanged;

	public double Opening
	{
		get => Size;
		set => Size = value;
	}

	public double Size
	{
		get => _size;
		set => SetValue(ref _size, value, 0.05, 1.0);
	}

	public double WidthScale
	{
		get => _widthScale;
		set => SetValue(ref _widthScale, value, 0.5, 1.5);
	}

	public double HeightScale
	{
		get => _heightScale;
		set => SetValue(ref _heightScale, value, 0.5, 1.5);
	}

	public double Roundness
	{
		get => Curve;
		set => Curve = value;
	}

	public double Curve
	{
		get => _curve;
		set => SetValue(ref _curve, value, 0.0, 1.0);
	}

	public double OffsetX
	{
		get => _offsetX;
		set => SetValue(ref _offsetX, value, -1.0, 1.0);
	}

	public double OffsetY
	{
		get => _offsetY;
		set => SetValue(ref _offsetY, value, -1.0, 1.0);
	}

	public double TopBias
	{
		get => OffsetY;
		set => OffsetY = value;
	}

	public double BottomBias
	{
		get => OffsetY;
		set => OffsetY = value;
	}

	public double LeftBias
	{
		get => OffsetX;
		set => OffsetX = value;
	}

	public double RightBias
	{
		get => OffsetX;
		set => OffsetX = value;
	}

	public double TopCurve
	{
		get => 0.0;
		set { }
	}

	public double BottomCurve
	{
		get => 0.0;
		set { }
	}

	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);

		Rect bounds = new(0.5, 0.5, Math.Max(0.0, ActualWidth - 1.0), Math.Max(0.0, ActualHeight - 1.0));
		dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(8, 9, 10)), new Pen(new SolidColorBrush(Color.FromRgb(43, 45, 49)), 1), bounds);

		Rect area = new(2.0, 2.0, Math.Max(0.0, ActualWidth - 4.0), Math.Max(0.0, ActualHeight - 4.0));
		if (area.Width <= 4.0 || area.Height <= 4.0)
		{
			return;
		}

		StreamGeometry geometry = BuildGeometry(area);
		dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(224, 42, 53)), 4.0), geometry);
		dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1.0), geometry);
	}

	private StreamGeometry BuildGeometry(Rect area)
	{
		double halfW = area.Width * 0.5 * Math.Clamp(Size * WidthScale, 0.01, 1.0);
		double halfH = area.Height * 0.5 * Math.Clamp(Size * HeightScale, 0.01, 1.0);
		double centerX = area.Left + area.Width * 0.5 + (area.Width * 0.5 - halfW) * OffsetX;
		double centerY = area.Top + area.Height * 0.5 + (area.Height * 0.5 - halfH) * OffsetY;
		double exponent = 32.0 - Math.Clamp(Curve, 0.0, 1.0) * 30.0;

		List<Point> points = new();
		const int count = 128;
		for (int i = 0; i < count; ++i)
		{
			double angle = Math.PI * 2.0 * i / count;
			double c = Math.Cos(angle);
			double s = Math.Sin(angle);
			double sx = Math.Sign(c) * Math.Pow(Math.Abs(c), 2.0 / exponent);
			double sy = Math.Sign(s) * Math.Pow(Math.Abs(s), 2.0 / exponent);
			double x = centerX + sx * halfW;
			double y = centerY + sy * halfH;
			points.Add(new Point(Math.Clamp(x, area.Left, area.Right), Math.Clamp(y, area.Top, area.Bottom)));
		}

		StreamGeometry geometry = new();
		using StreamGeometryContext ctx = geometry.Open();
		ctx.BeginFigure(points[0], isFilled: false, isClosed: true);
		ctx.PolyLineTo(points.GetRange(1, points.Count - 1), isStroked: true, isSmoothJoin: true);
		geometry.Freeze();
		return geometry;
	}

	private void SetValue(ref double field, double value, double min, double max)
	{
		double clamped = Math.Clamp(value, min, max);
		if (Math.Abs(field - clamped) < 0.0001)
		{
			return;
		}
		field = clamped;
		InvalidateVisual();
		ShapeChanged?.Invoke(this, EventArgs.Empty);
	}
}
