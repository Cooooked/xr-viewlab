using System;
using System.Collections.Generic;
using System.Globalization;
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
	private double _noseSpreadX;
	private bool _openInnerPreview;
	private double _cropHorizontal = 1.0;
	private double _cropVertical = 1.0;
	private double _cropTopScale = 1.0;
	private double _cropBottomScale = 1.0;
	private bool _useCircularEyeGuides = true;
	private bool _usePerEyeFrameGuides;
	private bool _useOpticalPreviewCentre;
	private double _previewIpdMillimetres = Quest3PreviewGeometry.DefaultIpdMillimetres;
	private bool _visorVisible;
	private DragTarget _dragTarget = DragTarget.None;
	private DragTarget _hoverTarget = DragTarget.None;
	private double _viewZoom = 1.0;
	private Vector _viewPan;
	private bool _panning;
	private CrosshairSettings? _crosshair;
	private bool _crosshairVisible;
	private double _crosshairOffsetX,_crosshairOffsetY;
	private readonly List<OverlayPreviewItem> _overlayPreviews = new();
	private string? _overlayDragId;
	private OverlayPreviewHandle _overlayDragHandle;
	private string? _overlayHoverId;
	private OverlayPreviewHandle _overlayHoverHandle;
	private OverlayPreviewItem _overlayDragStartItem;
	private Point _overlayDragStartMouse;
	private Point _overlayScaleOrigin;
	private double _overlayScaleStartDistance;
	private Point _panStart;
	private Vector _panOrigin;
	private Point? _inspectorPoint;

	// Measured desktop-preview-to-HMD vertical correction for widget replicas (full-lens fraction).
	private const double WidgetPreviewShimY = 0.077;

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

	private enum OverlayPreviewHandle { None, Move, Scale }
	private readonly record struct OverlayPreviewGeometry(OverlayPreviewItem Item, Rect Rect, Point MovePin, Point ScalePin);

	public event EventHandler? ShapeChanged;
	internal event EventHandler<OverlayPreviewChangedEventArgs>? OverlayPreviewChanged;

	internal void SetCrosshair(CrosshairSettings settings, bool visible = true, double offsetX = 0, double offsetY = 0)
	{
		_crosshair = settings;
		_crosshairVisible = visible;
		_crosshairOffsetX=Math.Clamp(offsetX,-1,1);_crosshairOffsetY=Math.Clamp(offsetY,-1,1);
		InvalidateVisual();
	}

	internal void SetOverlayPreviews(IEnumerable<OverlayPreviewItem> items)
	{
		_overlayPreviews.Clear(); _overlayPreviews.AddRange(items); InvalidateVisual();
	}

	internal void SetVisorVisible(bool visible)
	{
		if(_visorVisible==visible)return;_visorVisible=visible;InvalidateVisual();
	}

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

	public double NoseSpreadX
	{
		get => _noseSpreadX;
		set => SetValue(ref _noseSpreadX, value, 0.0, 0.5);
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
		set
		{
			double vertical = Math.Clamp(value, 0.01, 1.0);
			SetCropVerticalState(vertical, vertical);
		}
	}

	public void SetCropVertical(double top, double bottom)
	{
		SetCropVerticalState(Math.Clamp(top, 0.0, 1.0), Math.Clamp(bottom, 0.0, 1.0));
	}

	private void SetCropVerticalState(double topScale, double bottomScale)
	{
		topScale = Math.Clamp(topScale, 0.0, 1.0);
		bottomScale = Math.Clamp(bottomScale, 0.0, 1.0);
		double vertical = Math.Clamp(topScale + bottomScale, 0.01, 2.0) * 0.5;
		if (Math.Abs(_cropVertical - vertical) < 0.0001 && Math.Abs(_cropTopScale - topScale) < 0.0001 && Math.Abs(_cropBottomScale - bottomScale) < 0.0001) return;
		_cropVertical = vertical;
		_cropTopScale = topScale;
		_cropBottomScale = bottomScale;
		InvalidateVisual();
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

	// Fixed centred binocular box; never stretch it to the host control.
	protected override Size MeasureOverride(Size availableSize)
	{
		return Quest3PreviewGeometry.DesiredSize(availableSize.Width);
	}

	public bool UseCircularEyeGuides
	{
		get => _useCircularEyeGuides;
		set
		{
			if (_useCircularEyeGuides == value) return;
			_useCircularEyeGuides = value;
			InvalidateVisual();
		}
	}

	public bool UsePerEyeFrameGuides
	{
		get => _usePerEyeFrameGuides;
		set
		{
			if (_usePerEyeFrameGuides == value) return;
			_usePerEyeFrameGuides = value;
			InvalidateVisual();
		}
	}

	public double PreviewIpdMillimetres
	{
		get => _previewIpdMillimetres;
		set => SetViewValue(ref _previewIpdMillimetres, value,
			Quest3PreviewGeometry.MinimumIpdMillimetres, Quest3PreviewGeometry.MaximumIpdMillimetres);
	}

	public bool UseOpticalPreviewCentre
	{
		get => _useOpticalPreviewCentre;
		set { if (_useOpticalPreviewCentre == value) return; _useOpticalPreviewCentre = value; InvalidateVisual(); }
	}

	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);

		Rect bounds = new(0.5, 0.5, Math.Max(0.0, ActualWidth - 1.0), Math.Max(0.0, ActualHeight - 1.0));
		dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(8, 9, 10)), new Pen(new SolidColorBrush(Color.FromRgb(43, 45, 49)), 1), bounds);

		Rect area = PreviewFullArea();
		if (area.Width <= 4.0 || area.Height <= 4.0)
		{
			return;
		}

		Rect frame = FrameArea();
		Rect crop = PreviewCropRect(area);
		bool cropSupportsMaskGeometry=crop.Width>4.0&&crop.Height>4.0;
		dc.PushClip(new RectangleGeometry(bounds));
		Point centre = new(ActualWidth * 0.5, ActualHeight * 0.5);
		dc.PushTransform(new TranslateTransform(_viewPan.X, _viewPan.Y));
		dc.PushTransform(new ScaleTransform(_viewZoom, _viewZoom, centre.X, centre.Y));
		dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(34, 155, 160, 168)), new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 1.0/_viewZoom), crop);
		var fullReferencePen=new Pen(new SolidColorBrush(Color.FromArgb(155,190,195,202)),1.25/_viewZoom)
		{
			DashStyle=new DashStyle(new[]{2.5/_viewZoom,3.5/_viewZoom},0)
		};
		// Frame and periphery guides use the FIXED frame so the outer preview never pans.
		if (UsePerEyeFrameGuides)
		{
			Quest3PreviewGeometry.EyeFrames frames = Quest3PreviewGeometry.FullEyeFrames(frame, PreviewIpdMillimetres);
			dc.DrawRectangle(null, fullReferencePen, frames.Left);
			dc.DrawRectangle(null, fullReferencePen, frames.Right);
		}
		else
		{
			dc.DrawRectangle(null,fullReferencePen,frame);
		}
		DrawPreviewLabel(dc,$"QUEST 3 {PreviewIpdMillimetres:0.0} mm  FULL BINOCULAR",new Point(frame.Left+5/_viewZoom,frame.Top+5/_viewZoom),Color.FromRgb(175,180,188),180);
		var eyeGuidePen=new Pen(new SolidColorBrush(Color.FromArgb(135,155,160,168)),1.1/_viewZoom)
		{
			DashStyle=new DashStyle(new[]{6/_viewZoom,4/_viewZoom},0)
		};
		if (UseCircularEyeGuides)
		{
			Quest3PreviewGeometry.EyeGuides fullEyes = Quest3PreviewGeometry.FullEyeGuides(frame, PreviewIpdMillimetres);
			DrawEyeGuide(dc, fullEyes.Left, eyeGuidePen);
			DrawEyeGuide(dc, fullEyes.Right, eyeGuidePen);
		}
		else
		{
			Rect oval = Quest3PreviewGeometry.FullBinocularOval(frame);
			dc.DrawEllipse(null, eyeGuidePen,
				new Point(oval.Left + oval.Width * 0.5, oval.Top + oval.Height * 0.5),
				oval.Width * 0.5, oval.Height * 0.5);
		}
		Rect leftEye = new(crop.Left, crop.Top, crop.Width * 0.5, crop.Height);
		if(cropSupportsMaskGeometry)
		{
			Rect rightEye = new(crop.Left + crop.Width * 0.5, crop.Top, crop.Width * 0.5, crop.Height);
			StreamGeometry geometry = OpenInnerPreview
				? BuildBinocularOpenInnerGeometry(leftEye, rightEye)
				: BuildBinocularClosedGeometry(leftEye, rightEye);
			// Geometry remains in model space, but the pen is compensated inside the zoom transform so
			// its on-screen width stays inspectable instead of becoming hairline/thick with zoom.
			double screenStroke = (_visorVisible?4.0:2.0) / _viewZoom;
			Color visorColour=_visorVisible?Color.FromRgb(224,42,53):Color.FromArgb(42,170,174,180);
			dc.DrawGeometry(null, new Pen(new SolidColorBrush(visorColour), screenStroke), geometry);
			if(_visorVisible)dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1.0 / _viewZoom), geometry);
		}
		DrawOverlayPreviews(dc, area);
		// The crosshair converges at the centre of the post-crop visible region (the crop/visor centre),
		// not the full-lens box, so it fuses over the same point the visor frames. It follows the optical
		// content shift because crop is derived from the shifted content area.
		DrawCrosshair(dc, cropSupportsMaskGeometry ? crop : area);

		if(cropSupportsMaskGeometry)DrawPins(dc, leftEye);
		DrawCoordinateMarker(dc, area);
		dc.Pop();
		dc.Pop();
		DrawCoordinateInspector(dc, area);
	}

	private void DrawCoordinateMarker(DrawingContext dc, Rect area)
	{
		if (_inspectorPoint is not Point point || !area.Contains(point)) return;
		double radius = 4.0 / _viewZoom;
		var pen = new Pen(new SolidColorBrush(Color.FromArgb(190, 255, 214, 72)), 1.0 / _viewZoom);
		dc.DrawLine(pen, new Point(point.X - radius, point.Y), new Point(point.X + radius, point.Y));
		dc.DrawLine(pen, new Point(point.X, point.Y - radius), new Point(point.X, point.Y + radius));
	}

	private void DrawCoordinateInspector(DrawingContext dc, Rect area)
	{
		if (_inspectorPoint is not Point point || !area.Contains(point)) return;
		Point full = Quest3PreviewGeometry.InvertFullLens(area, point);
		Rect crop = PreviewCropRect(area);
		Point cropped = Quest3PreviewGeometry.InvertFullLens(crop, point);
		string value = $"FULL  X {full.X:0.000}  Y {full.Y:0.000}    CROP  X {cropped.X:0.000}  Y {cropped.Y:0.000}";
		var text = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
			new Typeface("Consolas"), 10.0, new SolidColorBrush(Color.FromRgb(255, 222, 96)), 1.0);
		double left = Math.Max(5.0, ActualWidth - text.Width - 13.0);
		double top = Math.Max(5.0, ActualHeight - text.Height - 9.0);
		dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(220, 12, 13, 15)),
			new Pen(new SolidColorBrush(Color.FromArgb(180, 92, 84, 48)), 1.0), new Rect(left - 5.0, top - 3.0, text.Width + 10.0, text.Height + 6.0), 3.0, 3.0);
		dc.DrawText(text, new Point(left, top));
	}

	private static void DrawEyeGuide(DrawingContext dc, Rect eye, Pen pen)
	{
		double radius = eye.Width * 0.5;
		dc.DrawEllipse(null, pen, new Point(eye.Left + radius, eye.Top + radius), radius, radius);
	}

	private void DrawCrosshair(DrawingContext dc, Rect sizeReference)
	{
		if (_crosshair is null || !_crosshairVisible) return;
		// One uniform reference-pixel-to-screen factor for BOTH axes so the preview crosshair stays
		// square and centred, matching the native renderer's uniform scale x eyeHeight/1080 mapping.
		// Separate X/Y factors previously stretched it because the 55:48 preview area aspect differs
		// from the eye tangent aspect. Position still uses the shared centred offset.
		double unit=Quest3PreviewGeometry.TangentReferencePixelsUniform(sizeReference,2.0*_crosshair.VrScale);
		double armX=Math.Round(_crosshair.Size*unit),armY=armX;
		double thickX=Math.Max(1,Math.Round(_crosshair.Thickness*unit)),thickY=thickX;
		double gapX=Math.Round(_crosshair.Gap*unit),gapY=gapX;
		double outlineX=_crosshair.Outline?Math.Max(1,Math.Round(_crosshair.OutlineThickness*unit)):0;
		double outlineY=outlineX;
		Point centre=Quest3PreviewGeometry.ResolveCentredOffset(sizeReference,_crosshairOffsetX,_crosshairOffsetY);
		double cx=Math.Floor(centre.X)+.5,cy=Math.Floor(centre.Y)+.5,halfX=thickX/2,halfY=thickY/2,innerX=gapX+halfX,innerY=gapY+halfY;
		var arms=new List<Rect>{new(cx-innerX-armX,cy-halfY,armX,thickY),new(cx+innerX,cy-halfY,armX,thickY),new(cx-halfX,cy+innerY,thickX,armY)};
		if(!_crosshair.TStyle) arms.Add(new(cx-halfX,cy-innerY-armY,thickX,armY));
		byte a=(byte)Math.Round(Math.Clamp(_crosshair.Alpha,0,1)*255); var fg=new SolidColorBrush(Color.FromArgb(a,_crosshair.R,_crosshair.G,_crosshair.B)); var bg=new SolidColorBrush(Color.FromArgb(a,0,0,0));
		if(outlineX>0||outlineY>0) foreach(var r in arms) dc.DrawRectangle(bg,null,new Rect(r.X-outlineX,r.Y-outlineY,r.Width+2*outlineX,r.Height+2*outlineY));
		foreach(var r in arms) dc.DrawRectangle(fg,null,r);
		if(_crosshair.Dot){if(outlineX>0||outlineY>0)dc.DrawRectangle(bg,null,new Rect(cx-halfX-outlineX,cy-halfY-outlineY,thickX+2*outlineX,thickY+2*outlineY));dc.DrawRectangle(fg,null,new Rect(cx-halfX,cy-halfY,thickX,thickY));}
	}

	private void DrawOverlayPreviews(DrawingContext dc, Rect area)
	{
		foreach (OverlayPreviewGeometry geometry in OverlayPreviewGeometries(area))
		{
			OverlayPreviewItem item = geometry.Item;
			byte alpha=(byte)Math.Round(Math.Clamp(item.Opacity,.18,1)*210);
			Color accent=item.Style switch { OverlayPreviewStyle.Hud=>Color.FromRgb(52,220,135),OverlayPreviewStyle.Trace=>Color.FromRgb(52,196,240),OverlayPreviewStyle.Clock=>Color.FromRgb(210,210,205),OverlayPreviewStyle.Notification=>Color.FromRgb(70,196,165),OverlayPreviewStyle.Sticky=>item.Theme switch{1=>Color.FromRgb(240,145,160),2=>Color.FromRgb(145,220,170),3=>Color.FromRgb(135,190,235),4=>Color.FromRgb(225,215,185),_=>Color.FromRgb(238,195,82)},_=>Color.FromRgb(235,82,82)};
			Rect rect=geometry.Rect;
			if(item.Anchor is OverlayPreviewAnchor.Edge or OverlayPreviewAnchor.RenderEdge or OverlayPreviewAnchor.RecordingRenderEdge)
			{
				var pen=new Pen(new SolidColorBrush(Color.FromArgb(alpha,accent.R,accent.G,accent.B)),Math.Max(1,2/_viewZoom));dc.DrawRectangle(null,pen,rect);
				Point labelOrigin=item.Anchor==OverlayPreviewAnchor.RecordingRenderEdge
					?new Point(rect.Left+7/_viewZoom,rect.Bottom-15/_viewZoom)
					:new Point(rect.Left+7/_viewZoom,rect.Top+6/_viewZoom);
				DrawPreviewLabel(dc,item.Label,labelOrigin,accent,alpha);continue;
			}
			dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb((byte)Math.Min(150,(int)alpha),18,20,23)),new Pen(new SolidColorBrush(Color.FromArgb(alpha,accent.R,accent.G,accent.B)),Math.Max(1,1/_viewZoom)),rect,item.Style==OverlayPreviewStyle.Sticky?1:3,item.Style==OverlayPreviewStyle.Sticky?1:3);
			DrawPreviewLabel(dc,item.Label,new Point(rect.Left+4/_viewZoom,rect.Top+Math.Max(1/_viewZoom,(rect.Height-10/_viewZoom)/2)),accent,alpha);
			if(item.Editable)
			{
				DrawOverlayPin(dc,geometry.MovePin,Color.FromRgb(96,215,255),item.Id,OverlayPreviewHandle.Move);
				DrawOverlayPin(dc,geometry.ScalePin,Color.FromRgb(255,190,82),item.Id,OverlayPreviewHandle.Scale);
			}
		}
	}

	private void DrawPreviewLabel(DrawingContext dc,string label,Point origin,Color colour,byte alpha)
	{
		var text=new FormattedText(label,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI Semibold"),8/_viewZoom,new SolidColorBrush(Color.FromArgb(alpha,colour.R,colour.G,colour.B)),1.0);
		dc.DrawText(text,origin);
	}

	private IEnumerable<OverlayPreviewGeometry> OverlayPreviewGeometries(Rect area)
	{
		foreach(OverlayPreviewItem item in _overlayPreviews)
		{
			if(item.Anchor==OverlayPreviewAnchor.RecordingRenderEdge)
			{
				Rect crop=PreviewCropRect(area);
				yield return new OverlayPreviewGeometry(item,crop,default,default);continue;
			}
			if(item.Anchor==OverlayPreviewAnchor.RenderEdge)
			{
				Rect crop=PreviewCropRect(area);
				Rect edge=new(crop.Left+3,crop.Top+3,Math.Max(0,crop.Width-6),Math.Max(0,crop.Height-6));
				yield return new OverlayPreviewGeometry(item,edge,default,default);continue;
			}
			if(item.Anchor==OverlayPreviewAnchor.Edge)
			{
				Rect edge=new(area.Left+3,area.Top+3,Math.Max(0,area.Width-6),Math.Max(0,area.Height-6));
				yield return new OverlayPreviewGeometry(item,edge,default,default);continue;
			}
			Size footprint=OverlayPreviewReplicaLayout.ResolveSize(item,area);double w=footprint.Width,h=footprint.Height;
			// Widget preview shim: draw all widgets one fixed full-lens fraction lower so their
			// on-screen position matches the HMD. Drawing/pins/hit-testing all flow from this rect;
			// dragging is delta-based so saved X/Y are never changed by the shim.
			Point anchor=Quest3PreviewGeometry.ResolveFullLens(area,item.X,item.Y+WidgetPreviewShimY);double x=anchor.X,y=anchor.Y;
			if(item.Anchor==OverlayPreviewAnchor.Centre){x-=w/2;y-=h/2;}else if(item.Anchor==OverlayPreviewAnchor.BottomRight){x-=w;y-=h;}
			Rect rect=new(x,y,w,h);
			yield return new OverlayPreviewGeometry(item,rect,new Point(rect.Left+rect.Width*.5,rect.Top),rect.TopRight);
		}
	}

	private void DrawOverlayPin(DrawingContext dc,Point point,Color colour,string id,OverlayPreviewHandle handle)
	{
		double radius=3.75/_viewZoom;
		bool active=_overlayDragId==id&&_overlayDragHandle==handle;
		bool hover=_overlayHoverId==id&&_overlayHoverHandle==handle;
		double outline=(active?2.75:hover?2.0:1.25)/_viewZoom;
		dc.DrawEllipse(new SolidColorBrush(colour),new Pen(new SolidColorBrush(Color.FromRgb(10,11,13)),outline),point,radius,radius);
	}

	private (OverlayPreviewGeometry geometry,OverlayPreviewHandle handle)? HitOverlayHandle(Point point,Rect area)
	{
		double hit=9/_viewZoom,hitSquared=hit*hit;
		foreach(OverlayPreviewGeometry geometry in OverlayPreviewGeometries(area))
		{
			if(!geometry.Item.Editable)continue;
			if(DistanceSquared(point,geometry.ScalePin)<=hitSquared)return(geometry,OverlayPreviewHandle.Scale);
			if(DistanceSquared(point,geometry.MovePin)<=hitSquared)return(geometry,OverlayPreviewHandle.Move);
		}
		return null;
	}

	// Optional Optical-centred content transform: shift the HEADSET CONTENT (crop, visor, crosshair,
	// widgets) upward by this normalized full-lens Y fraction. It is content-only — the outer preview
	// frame, eye guides, labels, pan, zoom and inspector chrome never move (those use FrameArea()).
	// This is deliberately NOT a viewport refit; it does not pan the whole preview.
	private const double OpticalContentShiftY = 0.077;

	// The FIXED preview viewport: the fitted 55:48 box that never moves. Frame, eye/periphery guides
	// and labels are drawn against this so toggling Optical-centred cannot pan the preview.
	private Rect FrameArea() => Quest3PreviewGeometry.FitArea(RenderSize);

	// The CONTENT area that crop, visor, crosshair and widgets share. Equal to FrameArea(), plus the
	// optional Optical-centred upward shim when the checkbox is on. Saved coordinates are unchanged;
	// this only translates where headset content is drawn/hit-tested within the fixed viewport.
	private Rect PreviewFullArea()
	{
		Rect area = Quest3PreviewGeometry.FitArea(RenderSize);
		if (_useOpticalPreviewCentre)
			area = new Rect(area.Left, area.Top - OpticalContentShiftY * area.Height, area.Width, area.Height);
		return area;
	}

	// Reproduce native ApplyXRViewLabFov in normalized full-tangent space. The resulting crop,
	// saved FullLens widget coordinates and drag inverse all use this same translated area.
	private Rect PreviewCropRect(Rect area) =>
		Quest3PreviewGeometry.RuntimeCropRect(area, CropHorizontal, _cropTopScale, _cropBottomScale);

	// Left-eye half of the crop rect — the coordinate space for pins and drags.
	private Rect LeftEyeArea()
	{
		Rect area = PreviewFullArea();
		Rect crop = PreviewCropRect(area);
		return new Rect(crop.Left, crop.Top, crop.Width * 0.5, crop.Height);
	}

	private Point ScenePoint(Point screen)
	{
		Point centre = new(ActualWidth * 0.5, ActualHeight * 0.5);
		return new Point((screen.X - _viewPan.X - centre.X) / _viewZoom + centre.X,
			(screen.Y - _viewPan.Y - centre.Y) / _viewZoom + centre.Y);
	}

	private bool IsOverPreviewWidget(Point scenePoint)
	{
		foreach(OverlayPreviewGeometry geometry in OverlayPreviewGeometries(PreviewFullArea()))
			if(geometry.Item.Editable&&geometry.Rect.Contains(scenePoint))return true;
		return false;
	}

	private void ResetViewToStartupFit()
	{
		// Startup draws Quest3PreviewGeometry.FitArea at identity. Reset to that exact
		// same path instead of calculating a second fitted rectangle here.
		_viewZoom=1.0;_viewPan=new Vector();_panning=false;InvalidateVisual();
	}

	protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
	{
		base.OnPreviewMouseRightButtonDown(e);
		if(IsOverPreviewWidget(ScenePoint(e.GetPosition(this))))return;
		ResetViewToStartupFit();e.Handled=true;
	}

	protected override void OnMouseWheel(MouseWheelEventArgs e)
	{
		base.OnMouseWheel(e);
		Point cursor = e.GetPosition(this);
		double before = _viewZoom;
		_viewZoom = Math.Clamp(_viewZoom * (e.Delta > 0 ? 1.15 : 1.0 / 1.15), 1.0, 8.0);
		// Hold the scene point beneath the cursor while zooming.
		Point centre = new(ActualWidth * 0.5, ActualHeight * 0.5);
		_viewPan += new Vector(cursor.X - centre.X, cursor.Y - centre.Y) * (1.0 / before - 1.0 / _viewZoom) * _viewZoom;
		InvalidateVisual(); e.Handled = true;
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
			double noseStartX = Math.Clamp(centerX + (outerLeft ? -1.0 : 1.0) * NoseSpreadX * area.Width, area.Left, area.Right);
			AddNoseBridgeCurve(points, noseStartX, y1, innerX, bandTopY, InnerLowerY, InnerBridgeWidth, area);
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

	private const double PinPixelRadius = 5.5;
	private const double PinHitPixelRadius = 12.0;

	private void DrawPins(DrawingContext dc, Rect area)
	{
		var pins = PinPositions(area);
		DrawPin(dc, pins.outerApex, Color.FromRgb(255, 96, 105), DragTarget.OuterApex);
		DrawPin(dc, pins.size, Color.FromRgb(180, 255, 120), DragTarget.Size);
		DrawPin(dc, pins.curve, Color.FromRgb(210, 120, 255), DragTarget.Curve);
		if (ShowInnerShapePins)
		{
			DrawPin(dc, pins.innerLower, Color.FromRgb(255, 180, 90), DragTarget.InnerLower);
			DrawPin(dc, pins.innerRise, Color.FromRgb(100, 225, 220), DragTarget.InnerRise);
			DrawPin(dc, pins.innerPeakX, Color.FromRgb(80, 160, 255), DragTarget.InnerPeakX);
			DrawPin(dc, pins.innerSteepness, Color.FromRgb(255, 210, 100), DragTarget.InnerSteepness);
			if (InnerLowerY > 0.0)
			{
				DrawPin(dc, pins.innerBridge, Color.FromRgb(120, 200, 255), DragTarget.InnerBridge);
			}
		}
	}

	private void DrawPin(DrawingContext dc, Point p, Color color, DragTarget target)
	{
		double modelRadius = PinPixelRadius / _viewZoom;
		double outlinePixels = target == _dragTarget ? 3.0 : target == _hoverTarget ? 2.25 : 1.5;
		var fill = new SolidColorBrush(color);
		var stroke = new Pen(new SolidColorBrush(Color.FromRgb(12, 12, 13)), outlinePixels / _viewZoom);
		dc.DrawEllipse(fill, stroke, p, modelRadius, modelRadius);
	}

	private DragTarget HitTarget(Point point, Rect area)
	{
		var pins = PinPositions(area);
		double modelHitRadius = PinHitPixelRadius / _viewZoom;
		double hitRadiusSquared = modelHitRadius * modelHitRadius;
		if (DistanceSquared(point, pins.size) <= hitRadiusSquared) return DragTarget.Size;
		if (DistanceSquared(point, pins.curve) <= hitRadiusSquared) return DragTarget.Curve;
		if (ShowInnerShapePins && DistanceSquared(point, pins.innerRise) <= hitRadiusSquared) return DragTarget.InnerRise;
		if (ShowInnerShapePins && DistanceSquared(point, pins.innerPeakX) <= hitRadiusSquared) return DragTarget.InnerPeakX;
		if (ShowInnerShapePins && DistanceSquared(point, pins.innerSteepness) <= hitRadiusSquared) return DragTarget.InnerSteepness;
		if (DistanceSquared(point, pins.outerApex) <= hitRadiusSquared) return DragTarget.OuterApex;
		if (ShowInnerShapePins && InnerLowerY > 0.0 && DistanceSquared(point, pins.innerBridge) <= hitRadiusSquared) return DragTarget.InnerBridge;
		if (ShowInnerShapePins && DistanceSquared(point, pins.innerLower) <= hitRadiusSquared) return DragTarget.InnerLower;
		return DragTarget.None;
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
		Point sceneMouse=ScenePoint(e.GetPosition(this));
		var overlayHit=HitOverlayHandle(sceneMouse,PreviewFullArea());
		if(overlayHit is { } hit)
		{
			_overlayDragId=hit.geometry.Item.Id;_overlayDragHandle=hit.handle;
			_overlayHoverId=_overlayDragId;_overlayHoverHandle=_overlayDragHandle;
			_overlayDragStartItem=hit.geometry.Item;_overlayDragStartMouse=sceneMouse;
			_overlayScaleOrigin=hit.geometry.Rect.TopLeft+new Vector(hit.geometry.Rect.Width*.5,hit.geometry.Rect.Height*.5);
			_overlayScaleStartDistance=Math.Max(1,(sceneMouse-_overlayScaleOrigin).Length);
			CaptureMouse();InvalidateVisual();e.Handled=true;return;
		}
		var area = LeftEyeArea();
		_dragTarget = HitTarget(sceneMouse, area);
		_hoverTarget = _dragTarget;
		InvalidateVisual();
		if (_dragTarget != DragTarget.None)
		{
			CaptureMouse();
			e.Handled = true;
		}
		else
		{
			_panning = true; _panStart = e.GetPosition(this); _panOrigin = _viewPan; CaptureMouse(); e.Handled = true;
		}
	}

	protected override void OnPreviewMouseMove(MouseEventArgs e)
	{
		base.OnPreviewMouseMove(e);
		Point inspectorPoint = ScenePoint(e.GetPosition(this));
		if (_inspectorPoint != inspectorPoint) { _inspectorPoint = inspectorPoint; InvalidateVisual(); }
		if(_overlayDragId!=null&&_overlayDragHandle!=OverlayPreviewHandle.None&&e.LeftButton==MouseButtonState.Pressed)
		{
			Point overlayMouse=ScenePoint(e.GetPosition(this));Rect overlayArea=PreviewFullArea();
			if(_overlayDragHandle==OverlayPreviewHandle.Move)
			{
				Point normalized=Quest3PreviewGeometry.ApplyFullLensDrag(overlayArea,
					new Point(_overlayDragStartItem.X,_overlayDragStartItem.Y),overlayMouse-_overlayDragStartMouse);
				OverlayPreviewChanged?.Invoke(this,new OverlayPreviewChangedEventArgs(_overlayDragId,OverlayPreviewEditKind.Position,normalized.X,normalized.Y,_overlayDragStartItem.Scale));
			}
			else
			{
				double distance=Math.Max(1,(overlayMouse-_overlayScaleOrigin).Length);
				double scale=Math.Clamp(_overlayDragStartItem.Scale*distance/_overlayScaleStartDistance,_overlayDragStartItem.MinScale,_overlayDragStartItem.MaxScale);
				OverlayPreviewChanged?.Invoke(this,new OverlayPreviewChangedEventArgs(_overlayDragId,OverlayPreviewEditKind.Scale,_overlayDragStartItem.X,_overlayDragStartItem.Y,scale));
			}
			e.Handled=true;return;
		}
		if (_panning && e.LeftButton == MouseButtonState.Pressed)
		{
			_viewPan = _panOrigin + (e.GetPosition(this) - _panStart); InvalidateVisual(); e.Handled = true; return;
		}
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
			Point scene=ScenePoint(e.GetPosition(this));var overlayHover=HitOverlayHandle(scene,PreviewFullArea());
			string? hoverId=overlayHover?.geometry.Item.Id;OverlayPreviewHandle hoverHandle=overlayHover?.handle??OverlayPreviewHandle.None;
			if(_overlayHoverId!=hoverId||_overlayHoverHandle!=hoverHandle){_overlayHoverId=hoverId;_overlayHoverHandle=hoverHandle;InvalidateVisual();}
			Cursor=hoverHandle switch{OverlayPreviewHandle.Move=>Cursors.SizeAll,OverlayPreviewHandle.Scale=>Cursors.SizeNWSE,_=>Cursors.Arrow};
			DragTarget hover = overlayHover==null?HitTarget(scene, LeftEyeArea()):DragTarget.None;
			if (_hoverTarget != hover)
			{
				_hoverTarget = hover;
				InvalidateVisual();
			}
			return;
		}

		var area = LeftEyeArea();
		var pins = PinPositions(area);
		Point mouse = ScenePoint(e.GetPosition(this));
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
		_overlayDragId=null;_overlayDragHandle=OverlayPreviewHandle.None;
		var overlayHover=HitOverlayHandle(ScenePoint(e.GetPosition(this)),PreviewFullArea());
		_overlayHoverId=overlayHover?.geometry.Item.Id;_overlayHoverHandle=overlayHover?.handle??OverlayPreviewHandle.None;
		Cursor=_overlayHoverHandle switch{OverlayPreviewHandle.Move=>Cursors.SizeAll,OverlayPreviewHandle.Scale=>Cursors.SizeNWSE,_=>Cursors.Arrow};
		_dragTarget = DragTarget.None;
		_hoverTarget = overlayHover==null?HitTarget(ScenePoint(e.GetPosition(this)), LeftEyeArea()):DragTarget.None;
		_panning = false;
		InvalidateVisual();
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
		_overlayDragId=null;_overlayDragHandle=OverlayPreviewHandle.None;
		_overlayHoverId=null;_overlayHoverHandle=OverlayPreviewHandle.None;Cursor=Cursors.Arrow;
		_panning = false;
		InvalidateVisual();
	}

	protected override void OnMouseLeave(MouseEventArgs e)
	{
		base.OnMouseLeave(e);
		_inspectorPoint = null;
		if (_dragTarget == DragTarget.None && (_hoverTarget != DragTarget.None||_overlayHoverHandle!=OverlayPreviewHandle.None))
		{
			_hoverTarget = DragTarget.None;_overlayHoverId=null;_overlayHoverHandle=OverlayPreviewHandle.None;Cursor=Cursors.Arrow;
		}
		InvalidateVisual();
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
