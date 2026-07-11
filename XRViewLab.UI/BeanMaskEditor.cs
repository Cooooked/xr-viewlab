using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
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
	private double _outerApexY;
	private double _innerLowerY;
	private double _innerBridgeWidth = 0.5;
	private double _innerBridgeRise = 0.0;
	private double _innerBridgePeakX = 0.5;
	private double _innerBridgeSteepness = 0.5;
	private bool _openInnerPreview;
	private double _cropHorizontal = 1.0;
	private double _cropVertical = 1.0;
	private DragTarget _dragTarget = DragTarget.None;

	// Inner-eye notch pins hidden: the notch looks right monocularly but turns translucent
	// under binocular fusion, so its controls are not exposed in the current UI.
	private static readonly bool ShowInnerShapePins = false;

	public BeanMaskEditor()
	{
		Focusable = true;
		IsHitTestVisible = true;
	}

	private enum DragTarget
	{
		None,
		OuterApex,
		InnerLower,
		InnerBridge,
		Size,
		Curve,
		InnerRise,
		InnerPeakX,
		InnerSteepness
	}

	public event EventHandler? ShapeChanged;

	public double Opening
	{
		get => Size;
		set => Size = value;
	}

	public double Size
	{
		get => _size;
		set => SetValue(ref _size, value, 0.10, 1.0);
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

	public double OuterApexY
	{
		get => _outerApexY;
		set => SetValue(ref _outerApexY, value, -0.5, 0.5);
	}

	public double InnerLowerY
	{
		get => _innerLowerY;
		set => SetValue(ref _innerLowerY, value, 0.0, 0.666);
	}

	public double InnerBridgeWidth
	{
		get => _innerBridgeWidth;
		set => SetValue(ref _innerBridgeWidth, value, 0.0, 1.0);
	}

	public double InnerBridgeRise
	{
		get => _innerBridgeRise;
		set => SetValue(ref _innerBridgeRise, value, -0.5, 1.0);
	}

	public double InnerBridgePeakX
	{
		get => _innerBridgePeakX;
		set => SetValue(ref _innerBridgePeakX, value, -1.0, 2.0);
	}

	public double InnerBridgeSteepness
	{
		get => _innerBridgeSteepness;
		set => SetValue(ref _innerBridgeSteepness, value, -1.0, 2.0);
	}

	// Post-crop render extents (fraction of the full uncropped render, 0.01..1).
	// View-only: they scale the preview rect so its aspect tracks the actual render area;
	// they do not alter mask geometry values and do not raise ShapeChanged.
	public double CropHorizontal
	{
		get => _cropHorizontal;
		set => SetViewValue(ref _cropHorizontal, value, 0.01, 1.0);
	}

	public double CropVertical
	{
		get => _cropVertical;
		set => SetViewValue(ref _cropVertical, value, 0.01, 1.0);
	}

	public bool OpenInnerPreview
	{
		get => _openInnerPreview;
		set
		{
			if (_openInnerPreview == value)
			{
				return;
			}
			_openInnerPreview = value;
			InvalidateVisual();
		}
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

		// The crop rect is the post-crop render area for BOTH eyes side by side; the visor is
		// drawn inside it so the preview aspect tracks the actual render as crop sliders move.
		Rect crop = CropRect(area);
		if (crop.Width <= 4.0 || crop.Height <= 4.0)
		{
			return;
		}
		dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1.0), crop);

		Rect leftEye = new(crop.Left, crop.Top, crop.Width * 0.5, crop.Height);
		Rect rightEye = new(crop.Left + crop.Width * 0.5, crop.Top, crop.Width * 0.5, crop.Height);
		StreamGeometry geometry = OpenInnerPreview
			? BuildBinocularOpenInnerGeometry(leftEye, rightEye)
			: BuildBinocularClosedGeometry(leftEye, rightEye);
		dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(224, 42, 53)), 4.0), geometry);
		dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1.0), geometry);

		DrawPins(dc, leftEye);
	}

	private Rect CropRect(Rect area)
	{
		double w = Math.Max(0.0, area.Width * CropHorizontal);
		double h = Math.Max(0.0, area.Height * CropVertical);
		return new Rect(area.Left + (area.Width - w) * 0.5, area.Top + (area.Height - h) * 0.5, w, h);
	}

	// Left-eye half of the crop rect — the coordinate space for pins and drags.
	private Rect LeftEyeArea()
	{
		Rect area = new(2.0, 2.0, Math.Max(0.0, ActualWidth - 4.0), Math.Max(0.0, ActualHeight - 4.0));
		Rect crop = CropRect(area);
		return new Rect(crop.Left, crop.Top, crop.Width * 0.5, crop.Height);
	}

	// A steep near-zero shoulder keeps the rectangle visually square while remaining continuous:
	// there is no separate geometry mode for the first non-zero slider step.
	internal static double CurveExponent(double curve) =>
		32.0 * Math.Pow(2.0 / 32.0, Math.Clamp(curve, 0.0, 1.0)) +
		1024.0 * Math.Exp(-50.0 * Math.Clamp(curve, 0.0, 1.0));

	private void AddClosedFigure(StreamGeometryContext ctx, Rect area)
	{
		// Size uniformly shrinks the exposed aperture without touching render crop/FOV.
		double halfW = area.Width * 0.5 * Math.Clamp(Size * WidthScale, 0.01, 1.0);
		double halfH = area.Height * 0.5 * Math.Clamp(Size * HeightScale, 0.01, 1.0);
		double centerX = area.Left + area.Width * 0.5 + (area.Width * 0.5 - halfW) * OffsetX;
		double centerY = area.Top + area.Height * 0.5 + (area.Height * 0.5 - halfH) * OffsetY;
		double exponent = CurveExponent(Curve);

		List<Point> points = new();
		const int count = 128;
		for (int i = 0; i < count; ++i)
		{
			double angle = Math.PI * 2.0 * i / count;
			double c = Math.Cos(angle);
			double s = Math.Sin(angle);
			double sx = Math.Sign(c) * Math.Pow(Math.Abs(c), 2.0 / exponent);
			double apexY = Math.Clamp(centerY + OuterApexY * halfH * 2.0, centerY - halfH + 0.001, centerY + halfH - 0.001);
			double syScale = s < 0.0 ? apexY - (centerY - halfH) : (centerY + halfH) - apexY;
			double sy = Math.Sign(s) * Math.Pow(Math.Abs(s), 2.0 / exponent) * syScale;
			double x = centerX + sx * halfW;
			double y = apexY + sy;
			points.Add(new Point(Math.Clamp(x, area.Left, area.Right), Math.Clamp(y, area.Top, area.Bottom)));
		}

		ctx.BeginFigure(points[0], isFilled: false, isClosed: true);
		ctx.PolyLineTo(points.GetRange(1, points.Count - 1), isStroked: true, isSmoothJoin: true);
	}

	private StreamGeometry BuildBinocularOpenInnerGeometry(Rect leftEye, Rect rightEye)
	{
		StreamGeometry geometry = new();
		using StreamGeometryContext ctx = geometry.Open();
		AddOpenInnerHalf(ctx, leftEye, outerLeft: true);
		AddOpenInnerHalf(ctx, rightEye, outerLeft: false);
		geometry.Freeze();
		return geometry;
	}

	private StreamGeometry BuildBinocularClosedGeometry(Rect leftEye, Rect rightEye)
	{
		StreamGeometry geometry = new();
		using StreamGeometryContext ctx = geometry.Open();
		AddClosedFigure(ctx, leftEye);
		AddClosedFigure(ctx, rightEye);
		geometry.Freeze();
		return geometry;
	}

	private void AddOpenInnerHalf(StreamGeometryContext ctx, Rect area, bool outerLeft)
	{
		if (area.Width <= 2.0 || area.Height <= 2.0)
		{
			return;
		}

		// Size uniformly shrinks the exposed aperture without touching render crop/FOV.
		double halfW = area.Width * 0.5 * Math.Clamp(Size * WidthScale, 0.01, 1.0);
		double halfH = area.Height * 0.5 * Math.Clamp(Size * HeightScale, 0.01, 1.0);
		double centerX = area.Left + area.Width * 0.5;
		double centerY = area.Top + area.Height * 0.5 + (area.Height * 0.5 - halfH) * OffsetY;
		double exponent = CurveExponent(Curve);
		double y0 = Math.Clamp(centerY - halfH, area.Top, area.Bottom);
		double y1 = Math.Clamp(centerY + halfH, area.Top, area.Bottom);
		double apexY = Math.Clamp(centerY + OuterApexY * (y1 - y0), y0 + 0.001, y1 - 0.001);
		if (y1 <= y0)
		{
			return;
		}

		const int count = 64;
		List<Point> points = new();
		double innerX = outerLeft ? area.Right : area.Left;
		points.Add(new Point(innerX, y0));
		for (int i = 0; i <= count; ++i)
		{
			double t = -0.5 * Math.PI + Math.PI * i / count;
			double c = Math.Max(0.0, Math.Cos(t));
			double s = Math.Sin(t);
			double cxOffset = Math.Pow(c, 2.0 / exponent) * halfW;
			double syScale = s < 0.0 ? apexY - y0 : y1 - apexY;
			double sy = Math.Sign(s) * Math.Pow(Math.Abs(s), 2.0 / exponent) * syScale;
			double x = outerLeft ? centerX - cxOffset : centerX + cxOffset;
			double y = apexY + sy;
			points.Add(new Point(Math.Clamp(x, area.Left, area.Right), Math.Clamp(y, area.Top, area.Bottom)));
		}
		if (InnerLowerY > 0.0)
		{
			double bandTopY = Math.Clamp(y1 - InnerLowerY * (y1 - y0), y0, y1);
			AddNoseBridgeCurve(points, centerX, y1, innerX, bandTopY, InnerLowerY, InnerBridgeWidth, area);
		}
		else
		{
			points.Add(new Point(innerX, y1));
		}

		ctx.BeginFigure(points[0], isFilled: false, isClosed: false);
		ctx.PolyLineTo(points.GetRange(1, points.Count - 1), isStroked: true, isSmoothJoin: true);
	}

	// Kidney-bean inner-edge curve: cubic Bézier with horizontal tangents at both ends
	// for a natural seal profile. P0->P1->P2->P3 where P1 and P2 align vertically
	// with their respective endpoints (horizontal tangents), and the horizontal
	// separation (bridgeWidth) controls how aggressively the rise/bridge transition happens.
	// New parameters (rise, peakX, steepness, curveExp) refine the shape.
	private void AddNoseBridgeCurve(
		List<Point> points, double startX, double startY, double endX, double endY,
		double bridgeHeight, double bridgeWidth, Rect area)
	{
		double dx = endX - startX;
		double dy = endY - startY;
		if (Math.Abs(dx) < 0.001 || Math.Abs(dy) < 0.001)
		{
			points.Add(new Point(Math.Clamp(startX, area.Left, area.Right), Math.Clamp(startY, area.Top, area.Bottom)));
			points.Add(new Point(Math.Clamp(endX, area.Left, area.Right), Math.Clamp(endY, area.Top, area.Bottom)));
			return;
		}

		double clampedBridgeWidth = Math.Clamp(bridgeWidth, 0.0, 1.0);
		double clampedSteepness = Math.Clamp(InnerBridgeSteepness, -1.0, 2.0);
		double clampedRise = Math.Clamp(InnerBridgeRise, -0.5, 1.0);
		double clampedPeakX = Math.Clamp(InnerBridgePeakX, -1.0, 2.0);

		// Handle length is proportional to the horizontal span, bridge width, and steepness.
		double baseHandleLength = Math.Abs(dx) * (0.1 + clampedBridgeWidth * 0.8);
		double handleLength = baseHandleLength * (0.5 + clampedSteepness * 0.5);

		// Control points: P1 and P2 sit at the SAME Y-level as their respective endpoints
		// so the tangent vectors at the start and end are purely horizontal.
		// PeakX shifts where the handles are positioned horizontally.
		double dir = dx > 0.0 ? 1.0 : -1.0;
		double p1x = startX + dir * handleLength * (0.5 + clampedPeakX * 0.5);
		double p1y = startY + clampedRise * dy;  // Horizontal tangent at start, with rise adjustment
		double p2x = endX - dir * handleLength * (1.0 - clampedPeakX * 0.5);
		double p2y = endY - clampedRise * dy;    // Horizontal tangent at end, with rise adjustment

		const int segCount = 32;
		for (int i = 0; i <= segCount; ++i)
		{
			double t = (double)i / segCount;
			double mt = 1.0 - t;
			double mt2 = mt * mt;
			double mt3 = mt2 * mt;
			double t2 = t * t;
			double t3 = t2 * t;

			double x = mt3 * startX + 3.0 * mt2 * t * p1x + 3.0 * mt * t2 * p2x + t3 * endX;
			double y = mt3 * startY + 3.0 * mt2 * t * p1y + 3.0 * mt * t2 * p2y + t3 * endY;

			points.Add(new Point(Math.Clamp(x, area.Left, area.Right), Math.Clamp(y, area.Top, area.Bottom)));
		}
	}

	private void DrawPins(DrawingContext dc, Rect area)
	{
		var pins = PinPositions(area);
		DrawPin(dc, pins.outerApex, Color.FromRgb(255, 96, 105));
		DrawPin(dc, pins.size, Color.FromRgb(180, 255, 120));
		DrawPin(dc, pins.curve, Color.FromRgb(210, 120, 255));
		if (ShowInnerShapePins)
		{
			DrawPin(dc, pins.innerLower, Color.FromRgb(255, 180, 90));
			DrawPin(dc, pins.innerRise, Color.FromRgb(100, 225, 220));
			DrawPin(dc, pins.innerPeakX, Color.FromRgb(80, 160, 255));
			DrawPin(dc, pins.innerSteepness, Color.FromRgb(255, 210, 100));
			if (InnerLowerY > 0.0)
			{
				DrawPin(dc, pins.innerBridge, Color.FromRgb(120, 200, 255));
			}
		}
	}

	private static void DrawPin(DrawingContext dc, Point p, Color color)
	{
		var fill = new SolidColorBrush(color);
		var stroke = new Pen(new SolidColorBrush(Color.FromRgb(12, 12, 13)), 1.5);
		dc.DrawEllipse(fill, stroke, p, 5.5, 5.5);
	}

	private (Point outerApex, Point innerLower, Point innerBridge, Point size, Point curve, Point innerRise, Point innerPeakX, Point innerSteepness, double y0, double y1, double bridgeStartX, double bridgeStartY, double bridgeEndX, double bridgeEndY) PinPositions(Rect area)
	{
		double halfW = area.Width * 0.5 * Math.Clamp(Size * WidthScale, 0.01, 1.0);
		double halfH = area.Height * 0.5 * Math.Clamp(Size * HeightScale, 0.01, 1.0);
		double centerX = area.Left + area.Width * 0.5;
		double centerY = area.Top + area.Height * 0.5 + (area.Height * 0.5 - halfH) * OffsetY;
		double y0 = Math.Clamp(centerY - halfH, area.Top, area.Bottom);
		double y1 = Math.Clamp(centerY + halfH, area.Top, area.Bottom);
		double outerY = Math.Clamp(centerY + OuterApexY * (y1 - y0), y0, y1);
		double innerY = Math.Clamp(y1 - InnerLowerY * (y1 - y0), y0, y1);
		double dx = area.Right - centerX;
		double dy = innerY - y1;
		double split = Math.Clamp(1.0 - InnerBridgeWidth, 0.0, 1.0);
		double bridgeX = centerX + dx * split;
		double bridgeY = y1 + dy * 0.5;
		return (
			new Point(centerX - halfW, outerY),
			new Point(area.Right, innerY),
			new Point(bridgeX, bridgeY),
			new Point(centerX - halfW, centerY),
			new Point(centerX - halfW * 0.7, y0),
			new Point(centerX + dx * 0.25, y1 + dy * 0.25),
			new Point(centerX + dx * Math.Clamp((InnerBridgePeakX + 1.0) / 3.0, 0.0, 1.0), y1 + dy * 0.5),
			new Point(centerX + dx * 0.75, y1 + dy * 0.75),
			y0, y1,
			centerX, y1, area.Right, innerY);
	}

	protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
	{
		Point p = hitTestParameters.HitPoint;
		return p.X >= 0.0 && p.X <= ActualWidth && p.Y >= 0.0 && p.Y <= ActualHeight
			? new PointHitTestResult(this, p)
			: null;
	}

	protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnPreviewMouseLeftButtonDown(e);
		Focus();
		_dragTarget = DragTarget.None;
		var area = LeftEyeArea();
		var pins = PinPositions(area);
		Point p = e.GetPosition(this);
		if (DistanceSquared(p, pins.size) <= 144.0)
			_dragTarget = DragTarget.Size;
		else if (DistanceSquared(p, pins.curve) <= 144.0)
			_dragTarget = DragTarget.Curve;
		else if (ShowInnerShapePins && DistanceSquared(p, pins.innerRise) <= 144.0)
			_dragTarget = DragTarget.InnerRise;
		else if (ShowInnerShapePins && DistanceSquared(p, pins.innerPeakX) <= 144.0)
			_dragTarget = DragTarget.InnerPeakX;
		else if (ShowInnerShapePins && DistanceSquared(p, pins.innerSteepness) <= 144.0)
			_dragTarget = DragTarget.InnerSteepness;
		else if (DistanceSquared(p, pins.outerApex) <= 144.0)
		{
			_dragTarget = DragTarget.OuterApex;
		}
		else if (ShowInnerShapePins && InnerLowerY > 0.0 && DistanceSquared(p, pins.innerBridge) <= 144.0)
		{
			_dragTarget = DragTarget.InnerBridge;
		}
		else if (ShowInnerShapePins && DistanceSquared(p, pins.innerLower) <= 144.0)
		{
			_dragTarget = DragTarget.InnerLower;
		}
		if (_dragTarget != DragTarget.None)
		{
			CaptureMouse();
			e.Handled = true;
		}
	}

	protected override void OnPreviewMouseMove(MouseEventArgs e)
	{
		base.OnPreviewMouseMove(e);
		if (_dragTarget == DragTarget.None || e.LeftButton != MouseButtonState.Pressed)
		{
			if (_dragTarget != DragTarget.None)
			{
				_dragTarget = DragTarget.None;
				if (IsMouseCaptured)
				{
					ReleaseMouseCapture();
				}
			}
			return;
		}

		var area = LeftEyeArea();
		var pins = PinPositions(area);
		Point mouse = e.GetPosition(this);
		double span = pins.y1 - pins.y0;

		if (_dragTarget == DragTarget.OuterApex)
		{
			double centerY = (pins.y0 + pins.y1) * 0.5;
			OuterApexY = Math.Clamp((mouse.Y - centerY) / span, -0.5, 0.5);
		}
		else if (_dragTarget == DragTarget.InnerLower)
		{
			InnerLowerY = Math.Clamp((pins.y1 - mouse.Y) / span, 0.0, 0.666);
		}
		else if (_dragTarget == DragTarget.InnerBridge)
		{
			double dxSpan = pins.bridgeEndX - pins.bridgeStartX;
			double split = Math.Abs(dxSpan) > 0.001 ? Math.Clamp((mouse.X - pins.bridgeStartX) / dxSpan, 0.0, 1.0) : 0.5;
			InnerBridgeWidth = Math.Clamp(1.0 - split, 0.0, 1.0);
		}
		else if (_dragTarget == DragTarget.Size)
		{
			double eyeCenterX = area.Left + area.Width * 0.5;
			Size = Math.Clamp(Math.Abs(mouse.X - eyeCenterX) / Math.Max(1.0, area.Width * 0.5), 0.1, 1.0);
		}
		else if (_dragTarget == DragTarget.Curve)
		{
			Curve = Math.Clamp((pins.y1 - mouse.Y) / Math.Max(1.0, span), 0.0, 1.0);
		}
		else if (_dragTarget == DragTarget.InnerRise)
		{
			InnerBridgeRise = Math.Clamp((mouse.Y - pins.y0) / Math.Max(1.0, span) * 1.5 - 0.5, -0.5, 1.0);
		}
		else if (_dragTarget == DragTarget.InnerPeakX)
		{
			InnerBridgePeakX = Math.Clamp((mouse.X - pins.bridgeStartX) / Math.Max(1.0, pins.bridgeEndX - pins.bridgeStartX) * 3.0 - 1.0, -1.0, 2.0);
		}
		else if (_dragTarget == DragTarget.InnerSteepness)
		{
			InnerBridgeSteepness = Math.Clamp((pins.y1 - mouse.Y) / Math.Max(1.0, span) * 3.0 - 1.0, -1.0, 2.0);
		}
		e.Handled = true;
	}

	protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
	{
		base.OnPreviewMouseLeftButtonUp(e);
		_dragTarget = DragTarget.None;
		if (IsMouseCaptured)
		{
			ReleaseMouseCapture();
		}
		e.Handled = true;
	}

	protected override void OnLostMouseCapture(MouseEventArgs e)
	{
		base.OnLostMouseCapture(e);
		_dragTarget = DragTarget.None;
	}

	private static double DistanceSquared(Point a, Point b)
	{
		double dx = a.X - b.X;
		double dy = a.Y - b.Y;
		return dx * dx + dy * dy;
	}

	// Like SetValue but does not raise ShapeChanged: crop extents describe the render
	// area the preview sits in, not an edit to the mask shape itself.
	private void SetViewValue(ref double field, double value, double min, double max)
	{
		double clamped = Math.Clamp(value, min, max);
		if (Math.Abs(field - clamped) < 0.0001)
		{
			return;
		}
		field = clamped;
		InvalidateVisual();
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
