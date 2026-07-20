using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XRViewLab.UI;

public partial class ProfileWindow : Window
{
	public double TopValue { get; private set; }

	public double BottomValue { get; private set; }

	public double HorizontalValue { get; private set; }

	public bool UseGlobalValues { get; private set; }
	public bool UseGlobalVisor { get; private set; }

	public bool HiddenValue { get; private set; }

	public bool HiddenChanged { get; private set; }

	public string DisplayName { get; private set; } = "";

	// Render-resolution multiplier (1.0 = 100%). Stored/edited as a percent in the UI.
	public double RenderScaleValue { get; private set; } = 1.0;

	public bool MaskEnabledValue { get; private set; }

	public double MaskVerticalValue { get; private set; } = 1.0;

	public double MaskHorizontalValue { get; private set; } = 1.0;

	public bool MaskRoundedValue { get; private set; }

	public double MaskCornerValue { get; private set; } = 1.0;

	public double MaskTopBiasValue { get; private set; }

	public double MaskBottomBiasValue { get; private set; }

	public double MaskLeftBiasValue { get; private set; }

	public double MaskRightBiasValue { get; private set; }

	public double MaskTopCurveValue { get; private set; }

	public double MaskBottomCurveValue { get; private set; }

	private readonly bool _globalMaskEnabled;
	private readonly double _globalMaskVertical;
	private readonly double _globalMaskHorizontal;
	private readonly double _globalMaskCorner;
	private readonly double _globalOffsetX;
	private readonly double _globalOffsetY;
	private readonly double _globalVisorOuterApexY;
	private readonly double _globalVisorInnerLowerY;
	private readonly double _globalVisorInnerBridgeWidth;
	private readonly double _globalVisorInnerBridgeRise;
	private readonly double _globalVisorInnerBridgePeakX;
	private readonly double _globalVisorSize;
	private readonly double _globalVisorWidth;
	private readonly double _globalVisorHeight;
	private readonly double _globalVisorInnerBridgeSteepness;
	private readonly double _globalVisorNoseSpreadX;
	private readonly bool _customMaskEnabled;
	private readonly double _customMaskVertical;
	private readonly double _customMaskHorizontal;
	private readonly double _customMaskCorner;
	private readonly double _customOffsetX;
	private readonly double _customOffsetY;
	private readonly double _customVisorOuterApexY;
	private readonly double _customVisorInnerLowerY;
	private readonly double _customVisorInnerBridgeWidth;
	private readonly double _customVisorInnerBridgeRise;
	private readonly double _customVisorInnerBridgePeakX;
	private readonly double _customVisorSize;
	private readonly double _customVisorWidth;
	private readonly double _customVisorHeight;
	private readonly double _customVisorInnerBridgeSteepness;
	private readonly double _customVisorNoseSpreadX;
	private readonly bool _useCircularEyeGuides;
	private readonly bool _usePerEyeFrameGuides;
	private readonly bool _useOpticalPreviewCentre;
	private readonly double _previewIpdMillimetres;
	private readonly List<OverlayPreviewItem> _globalOverlayPreviews;
	private readonly CrosshairSettings _globalCrosshair;
	private readonly bool _globalCrosshairVisible;
	private readonly double _globalCrosshairX;
	private readonly double _globalCrosshairY;
	private readonly Dictionary<string, OverlayPlacementOverride> _overlayPlacements;
	private readonly OverlayProfileOverrides _globalOverlaySettings;
	private readonly OverlayProfileOverrides _overlayOverrides;
	private readonly ObservableCollection<HudWidgetOption> _profileHudWidgets = new();
	private readonly ObservableCollection<StickyNoteOption> _profileStickyNotes = new();
	public IReadOnlyList<string> StickyNoteThemes { get; } = new[] { "Graphite", "Paper", "OLED", "Amber", "Mint" };

	// In-memory boxes feed the legacy mask_vertical/horizontal calculation (not shown in UI).
	private readonly TextBox MaskVerticalBox = new() { Text = "1" };
	private readonly TextBox MaskHorizontalBox = new() { Text = "1" };

	// Guards event handlers that fire via WPF property coercion during InitializeComponent.
	private bool _initialized;
	private bool _syncingControls;

	public double VisorSizeValue { get; private set; } = 0.82;
	public double VisorWidthValue { get; private set; } = 1.0;
	public double VisorHeightValue { get; private set; } = 1.0;
	public double VisorCurveValue { get; private set; } = 0.75;
	public double VisorOuterApexYValue { get; private set; }
	public double VisorInnerLowerYValue { get; private set; }
	public double VisorInnerBridgeWidthValue { get; private set; } = 0.5;
	public double VisorInnerBridgeRiseValue { get; private set; }
	public double VisorInnerBridgePeakXValue { get; private set; } = 0.5;
	public double VisorInnerBridgeSteepnessValue { get; private set; } = 0.5;
	public double VisorNoseSpreadXValue { get; private set; }
	public IReadOnlyDictionary<string, OverlayPlacementOverride> OverlayPlacements => _overlayPlacements;
	public OverlayProfileOverrides OverlayOverrides => _overlayOverrides;

	internal ProfileWindow(string appName, string exeName, bool hidden, double top, double bottom, double horizontal, double renderScale, bool maskEnabled, double maskVertical, double maskHorizontal, bool maskRounded, double maskCorner, double maskTopBias, double maskBottomBias, double maskLeftBias, double maskRightBias, double maskTopCurve, double maskBottomCurve, bool globalMaskEnabled, double globalMaskVertical, double globalMaskHorizontal, double globalMaskCorner, double globalOffsetX, double globalOffsetY, double visorSize, double visorWidth, double visorHeight, double visorOuterApexY, double visorInnerLowerY, double visorInnerBridgeWidth, double visorInnerBridgeRise, double visorInnerBridgePeakX, double visorInnerBridgeSteepness, double visorNoseSpreadX, double globalVisorSize, double globalVisorWidth, double globalVisorHeight, double globalVisorOuterApexY, double globalVisorInnerLowerY, double globalVisorInnerBridgeWidth, double globalVisorInnerBridgeRise, double globalVisorInnerBridgePeakX, double globalVisorInnerBridgeSteepness, double globalVisorNoseSpreadX, bool useCircularEyeGuides, bool usePerEyeFrameGuides, bool useOpticalPreviewCentre, double previewIpdMillimetres, bool globalStencilOuterEdges, IReadOnlyList<OverlayPreviewItem> globalOverlayPreviews, IReadOnlyDictionary<string, OverlayPlacementOverride> overlayPlacements, OverlayProfileOverrides globalOverlaySettings, OverlayProfileOverrides overlayOverrides, IReadOnlyList<HudWidgetOption> globalHudWidgets, IReadOnlyList<StickyNoteOption> globalStickyNotes, CrosshairSettings globalCrosshair, bool globalCrosshairVisible, double globalCrosshairX, double globalCrosshairY)
	{
		InitializeComponent();
		ConfigureProfileHotkeys();
		_globalMaskEnabled = globalMaskEnabled;
		_globalMaskVertical = globalMaskVertical;
		_globalMaskHorizontal = globalMaskHorizontal;
		_globalMaskCorner = globalMaskCorner;
		_globalOffsetX = globalOffsetX;
		_globalOffsetY = globalOffsetY;
		_globalVisorSize = Math.Clamp(globalVisorSize, 0.05, 1.0);
		_globalVisorWidth = Math.Clamp(globalVisorWidth, 0.25, 2.0);
		_globalVisorHeight = Math.Clamp(globalVisorHeight, 0.25, 2.0);
		_globalVisorOuterApexY = globalVisorOuterApexY;
		_globalVisorInnerLowerY = globalVisorInnerLowerY;
		_globalVisorInnerBridgeWidth = globalVisorInnerBridgeWidth;
		_globalVisorInnerBridgeRise = globalVisorInnerBridgeRise;
		_globalVisorInnerBridgePeakX = globalVisorInnerBridgePeakX;
		_globalVisorInnerBridgeSteepness = globalVisorInnerBridgeSteepness;
		_globalVisorNoseSpreadX = Math.Clamp(globalVisorNoseSpreadX, 0.0, 0.5);
		bool hasCustomVisor = visorSize > 0.0;
		_customMaskEnabled = hasCustomVisor ? maskEnabled : globalMaskEnabled;
		_customMaskVertical = hasCustomVisor ? maskVertical : globalMaskVertical;
		_customMaskHorizontal = hasCustomVisor ? maskHorizontal : globalMaskHorizontal;
		_customMaskCorner = hasCustomVisor ? maskCorner : globalMaskCorner;
		_customOffsetX = hasCustomVisor ? Math.Clamp((maskLeftBias + maskRightBias) * 0.5, -1.0, 1.0) : 0.0;
		_customOffsetY = hasCustomVisor ? Math.Clamp((maskTopBias + maskBottomBias) * 0.5, -1.0, 1.0) : 0.0;
		_customVisorSize = hasCustomVisor ? Math.Clamp(visorSize, 0.1, 1.0) : _globalVisorSize;
		_customVisorWidth = hasCustomVisor ? Math.Clamp(visorWidth, 0.25, 2.0) : _globalVisorWidth;
		_customVisorHeight = hasCustomVisor ? Math.Clamp(visorHeight, 0.25, 2.0) : _globalVisorHeight;
		_customVisorOuterApexY = hasCustomVisor ? Math.Clamp(visorOuterApexY, -0.5, 0.5) : _globalVisorOuterApexY;
		_customVisorInnerLowerY = hasCustomVisor ? Math.Clamp(visorInnerLowerY, 0.0, 0.666) : _globalVisorInnerLowerY;
		_customVisorInnerBridgeWidth = hasCustomVisor ? Math.Clamp(visorInnerBridgeWidth, 0.0, 1.0) : _globalVisorInnerBridgeWidth;
		_customVisorInnerBridgeRise = hasCustomVisor ? Math.Clamp(visorInnerBridgeRise, -0.5, 1.0) : _globalVisorInnerBridgeRise;
		_customVisorInnerBridgePeakX = hasCustomVisor ? Math.Clamp(visorInnerBridgePeakX, -1.0, 2.0) : _globalVisorInnerBridgePeakX;
		_customVisorInnerBridgeSteepness = hasCustomVisor ? Math.Clamp(visorInnerBridgeSteepness, -1.0, 2.0) : _globalVisorInnerBridgeSteepness;
		_customVisorNoseSpreadX = hasCustomVisor ? Math.Clamp(visorNoseSpreadX, 0.0, 0.5) : _globalVisorNoseSpreadX;
		DisplayName = appName;
		_useCircularEyeGuides = useCircularEyeGuides;
		_usePerEyeFrameGuides = usePerEyeFrameGuides;
		_useOpticalPreviewCentre = useOpticalPreviewCentre;
		_previewIpdMillimetres = Math.Round(Math.Clamp(previewIpdMillimetres,
			Quest3PreviewGeometry.MinimumIpdMillimetres, Quest3PreviewGeometry.MaximumIpdMillimetres), 1);
		_globalOverlayPreviews = globalOverlayPreviews.ToList();
		_overlayPlacements = new Dictionary<string, OverlayPlacementOverride>(overlayPlacements);
		_globalOverlaySettings = globalOverlaySettings.Clone();
		_overlayOverrides = overlayOverrides.Clone();
		foreach (HudWidgetOption widget in globalHudWidgets.Select((widget, index) => (widget, index)).OrderBy(pair => OverlayDouble("hud", $"hud_widget_{pair.widget.Id}_order", pair.index)).Select(pair => pair.widget)) _profileHudWidgets.Add(CloneHudWidget(widget));
		foreach (StickyNoteOption note in globalStickyNotes) _profileStickyNotes.Add(CloneStickyNote(note));
		int desiredNotes = Math.Clamp((int)OverlayDouble("sticky", "sticky_note_count", _profileStickyNotes.Count), 0, StickyNoteLiveStateService.MaxNotes);
		while (_profileStickyNotes.Count > desiredNotes) _profileStickyNotes.RemoveAt(_profileStickyNotes.Count - 1);
		while (_profileStickyNotes.Count < desiredNotes) _profileStickyNotes.Add(CloneStickyNote(new StickyNoteOption { Number = _profileStickyNotes.Count + 1 }));
		ProfileHudWidgetList.ItemsSource = _profileHudWidgets;
		ProfileStickyNotesList.ItemsSource = _profileStickyNotes;
		_globalCrosshair = globalCrosshair.Clone();
		_globalCrosshairVisible = globalCrosshairVisible;
		_globalCrosshairX = globalCrosshairX;
		_globalCrosshairY = globalCrosshairY;
		HiddenValue = hidden;
		NameBox.Text = appName;
		ExeNameText.Text = exeName;
		TopBox.Text = FormatScale(Math.Clamp(top * 2.0, 0.0, 1.0));
		BottomBox.Text = FormatScale(Math.Clamp(bottom * 2.0, 0.0, 1.0));
		HorizontalBox.Text = FormatScale(horizontal);
		RenderScaleValue = renderScale;
		RenderBox.Text = (renderScale * 100.0).ToString("0.####", CultureInfo.InvariantCulture);
		MaskEnabledValue = maskEnabled;
		MaskVerticalValue = maskVertical;
		MaskHorizontalValue = maskHorizontal;
		MaskVerticalBox.Text = FormatScale(maskVertical);
		MaskHorizontalBox.Text = FormatScale(maskHorizontal);
		MaskRoundedValue = maskRounded;
		MaskCornerValue = maskCorner;
		MaskTopBiasValue = maskTopBias;
		MaskBottomBiasValue = maskBottomBias;
		MaskLeftBiasValue = maskLeftBias;
		MaskRightBiasValue = maskRightBias;
		MaskTopCurveValue = maskTopCurve;
		MaskBottomCurveValue = maskBottomCurve;

		bool useGlobal = visorSize <= 0;
		UseGlobalVisorCheck.IsChecked = useGlobal;
		if (useGlobal) LoadGlobalVisorValues();
		else LoadCustomVisorValues();
		SetVisorSlidersEnabled(!useGlobal);
		SyncMaskEditorFromSliders();
		LoadOverlayControls();
		InitInheritCheckboxes();
		ApplyOverlayPreviewState();
		UpdateHideShowButton();
		UpdateHints();
		_initialized = true;
	}

	private static bool TryReadPercent(string text, out double multiplier)
	{
		if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double pct))
		{
			multiplier = Math.Clamp(pct / 100.0, 0.1, 3.0);
			return true;
		}
		multiplier = 1.0;
		return false;
	}

	private static string FormatScale(double value)
	{
		return value.ToString("0.###", CultureInfo.InvariantCulture);
	}

	private static string FormatPercent(double value)
	{
		double pct = Math.Clamp(value, 0.0, 1.0) * 100.0;
		return pct.ToString("0.#####", CultureInfo.InvariantCulture) + "%";
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

	private double CurrentVerticalCrop()
	{
		if (TryRead(TopBox.Text, out var top) && TryRead(BottomBox.Text, out var bottom))
		{
			return Math.Clamp((top + bottom) * 0.5, 0.01, 1.0);
		}
		return 1.0;
	}

	private double CurrentHorizontalCrop()
	{
		return TryRead(HorizontalBox.Text, out var horizontal) ? Math.Clamp(horizontal, 0.01, 1.0) : 1.0;
	}

	private void SyncMaskEditorFromSliders()
	{
		if (MaskBeanEditor == null)
		{
			return;
		}
		MaskBeanEditor.Size = VisorSizeSlider?.Value ?? 1.0;
		MaskBeanEditor.WidthScale = VisorWidthSlider?.Value ?? 1.0;
		MaskBeanEditor.HeightScale = VisorHeightSlider?.Value ?? 1.0;
		MaskBeanEditor.Curve = VisorCurveSlider?.Value ?? 0.5;
		MaskBeanEditor.OuterApexY = VisorApexYSlider?.Value ?? 0.0;
		MaskBeanEditor.InnerLowerY = VisorInnerLowerSlider?.Value ?? 0.0;
		MaskBeanEditor.InnerBridgeWidth = 0.5;
		MaskBeanEditor.InnerBridgeRise = 0.0;
		MaskBeanEditor.InnerBridgePeakX = 0.5;
		MaskBeanEditor.InnerBridgeSteepness = 0.5;
		MaskBeanEditor.NoseSpreadX = VisorNoseSpreadXSlider?.Value ?? 0.0;
		MaskBeanEditor.UseCircularEyeGuides = _useCircularEyeGuides;
		MaskBeanEditor.UsePerEyeFrameGuides = _usePerEyeFrameGuides;
		MaskBeanEditor.UseOpticalPreviewCentre = _useOpticalPreviewCentre;
		MaskBeanEditor.PreviewIpdMillimetres = _previewIpdMillimetres;
		MaskBeanEditor.OffsetX = 0.0;
		MaskBeanEditor.OffsetY = 0.0;
		MaskBeanEditor.SetVisorVisible(VisorEnabledCheck?.IsChecked == true);
		MaskBeanEditor.OpenInnerPreview = true; // Stencil outer edges only is permanently enabled
		// Preview rect tracks the post-crop render area so the mask aspect is WYSIWYG.
		if (TryRead(TopBox.Text, out double previewTop) && TryRead(BottomBox.Text, out double previewBottom))
			MaskBeanEditor.SetCropVertical(previewTop, previewBottom);
		else
			MaskBeanEditor.CropVertical = CurrentVerticalCrop();
		MaskBeanEditor.CropHorizontal = CurrentHorizontalCrop();
	}

	private void SetVisorSlidersEnabled(bool enabled)
	{
		VisorEnabledCheck.IsEnabled = enabled;
		VisorSizeSlider.IsEnabled = enabled;
		VisorWidthSlider.IsEnabled = enabled;
		VisorHeightSlider.IsEnabled = enabled;
		VisorCurveSlider.IsEnabled = enabled;
		VisorApexYSlider.IsEnabled = enabled;
		VisorInnerLowerSlider.IsEnabled = enabled;
		VisorNoseSpreadXSlider.IsEnabled = enabled;
		MaskBeanEditor.IsEnabled = enabled;
	}

	private void ApplyOverlayPreviewState()
	{
		List<OverlayPreviewItem> items = _globalOverlayPreviews.Select(item =>
		{
			string feature = item.Id.StartsWith("sticky:", StringComparison.Ordinal) ? "sticky" : item.Id;
			OverlayPreviewItem resolved = item;
			if (_overlayPlacements.TryGetValue(item.Id, out OverlayPlacementOverride placement)) resolved = resolved with { X = placement.X, Y = placement.Y, Scale = placement.Scale };
			// Match native precedence exactly: compatibility overlay_layout_* values load first,
			// then the canonical feature settings win when both exist.
			if (OverlaySettingsCatalog.All.TryGetValue(feature, out OverlaySettingsDefinition? definition))
				resolved = resolved with
				{
					X = OverlayDouble(feature, definition.XKey, resolved.X),
					Y = OverlayDouble(feature, definition.YKey, resolved.Y),
					Scale = OverlayDouble(feature, definition.ScaleKey, resolved.Scale)
				};
			resolved = feature switch
			{
				"clock" => resolved with { Opacity = OverlayDouble(feature, "clock_widget_opacity", resolved.Opacity), Theme = (int)OverlayDouble(feature, "clock_widget_theme", resolved.Theme) },
				"hud" => resolved with { ReferenceWidth = Math.Max(1, _profileHudWidgets.Count(widget => widget.Enabled)), Opacity = OverlayDouble(feature, "hud_opacity", resolved.Opacity) },
				"trace" => resolved with { ReferenceWidth = OverlayDouble(feature, "hud_trace_width", resolved.ReferenceWidth), Opacity = OverlayDouble(feature, "hud_trace_opacity", resolved.Opacity) },
				"notifications" => resolved with { Opacity = OverlayDouble(feature, "notify_opacity", resolved.Opacity), Theme = (int)OverlayDouble(feature, "notify_theme", resolved.Theme) },
				_ => resolved
			};
			return resolved;
		}).Where(item => OverlayBool(item.Id.StartsWith("sticky:", StringComparison.Ordinal) ? "sticky" : item.Id,
			OverlaySettingsCatalog.All[item.Id.StartsWith("sticky:", StringComparison.Ordinal) ? "sticky" : item.Id].EnabledKey, false)).ToList();
		items.RemoveAll(item => item.Id.StartsWith("sticky:", StringComparison.Ordinal));
		if (OverlayBool("sticky", "sticky_note_enabled"))
		{
			for (int index = 0; index < _profileStickyNotes.Count; index++)
			{
				StickyNoteOption note = _profileStickyNotes[index]; if (!note.Enabled || string.IsNullOrWhiteSpace(note.Text)) continue;
				OverlayPlacementOverride placement = _overlayPlacements.TryGetValue($"sticky:{index}", out var customSticky)
					? customSticky : new OverlayPlacementOverride(note.X, note.Y, note.Scale);
				items.Add(new OverlayPreviewItem($"sticky:{index}", $"NOTE {index + 1}", placement.X, placement.Y, .12, .12, placement.Scale, .5, 2.5,
					note.Opacity, OverlayPreviewAnchor.Centre, OverlayPreviewStyle.Sticky, note.Theme));
			}
		}
		if (OverlayBool("obs", "obs_indicator_enabled"))
		{
			double opacity = double.TryParse(_globalOverlaySettings.Get("obs", "obs_indicator_opacity"),
				NumberStyles.Float, CultureInfo.InvariantCulture, out double configuredOpacity) ? configuredOpacity : .72;
			items.Add(new OverlayPreviewItem(string.Empty, "OBS RECORDING CUE", .5, .5, 1, 1, 1, 1, 1,
				opacity, OverlayPreviewAnchor.RecordingRenderEdge, OverlayPreviewStyle.System));
		}
		MaskBeanEditor.SetOverlayPreviews(items);
		OverlayPlacementOverride crosshair = _overlayPlacements.TryGetValue("crosshair", out OverlayPlacementOverride custom)
			? custom : new OverlayPlacementOverride(_globalCrosshairX, _globalCrosshairY, _globalCrosshair.VrScale);
		OverlaySettingsDefinition crosshairDefinition = OverlaySettingsCatalog.All["crosshair"];
		crosshair = crosshair with
		{
			X = OverlayDouble("crosshair", crosshairDefinition.XKey, crosshair.X),
			Y = OverlayDouble("crosshair", crosshairDefinition.YKey, crosshair.Y),
			Scale = OverlayDouble("crosshair", crosshairDefinition.ScaleKey, crosshair.Scale)
		};
		CrosshairSettings settings = _globalCrosshair.Clone();
		settings.VrScale = crosshair.Scale;
		settings.Size = OverlayDouble("crosshair", "crosshair_size", settings.Size);
		settings.Gap = OverlayDouble("crosshair", "crosshair_gap", settings.Gap);
		settings.Thickness = OverlayDouble("crosshair", "crosshair_thickness", settings.Thickness);
		settings.OutlineThickness = OverlayDouble("crosshair", "crosshair_outline_thickness", settings.OutlineThickness);
		settings.Alpha = OverlayDouble("crosshair", "crosshair_alpha", settings.Alpha);
		settings.Dot = OverlayBool("crosshair", "crosshair_dot", settings.Dot);
		settings.Outline = OverlayBool("crosshair", "crosshair_outline", settings.Outline);
		settings.TStyle = OverlayBool("crosshair", "crosshair_tstyle", settings.TStyle);
		if (uint.TryParse(OverlayValue("crosshair", "crosshair_color", settings.ColorRgb.ToString(CultureInfo.InvariantCulture)), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint color))
		{ settings.R = (byte)(color >> 16); settings.G = (byte)(color >> 8); settings.B = (byte)color; }
		MaskBeanEditor.SetCrosshair(settings, OverlayBool("crosshair", "crosshair_enabled", _globalCrosshairVisible), crosshair.X, crosshair.Y);
	}

	private string OverlayValue(string feature, string key, string fallback = "0") =>
		_overlayOverrides.Get(feature, key) ?? _globalOverlaySettings.Get(feature, key) ?? fallback;

	private bool OverlayBool(string feature, string key, bool fallback = false)
	{
		string value = OverlayValue(feature, key, fallback ? "1" : "0");
		return value is "1" or "true" or "yes" or "on";
	}

	private double OverlayDouble(string feature, string key, double fallback) =>
		double.TryParse(OverlayValue(feature, key, fallback.ToString(CultureInfo.InvariantCulture)), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : fallback;

	private HudWidgetOption CloneHudWidget(HudWidgetOption source) => new()
	{
		MetricId = source.MetricId, Id = source.Id, Label = source.Label, Provider = source.Provider, Unit = source.Unit,
		ThresholdUnit = source.ThresholdUnit, DefaultWarning = source.DefaultWarning, DefaultCritical = source.DefaultCritical,
		LowerIsWorse = source.LowerIsWorse, Availability = source.Availability, ToolTip = source.ToolTip,
		Enabled = OverlayBool("hud", $"hud_widget_{source.Id}_enabled", source.Enabled),
		UseSymbol = OverlayBool("hud", $"hud_widget_{source.Id}_symbol", source.UseSymbol),
		ShowUnit = OverlayBool("hud", $"hud_widget_{source.Id}_unit", source.ShowUnit),
		Warning = OverlayDouble("hud", $"hud_widget_{source.Id}_warning", source.Warning),
		Critical = OverlayDouble("hud", $"hud_widget_{source.Id}_critical", source.Critical)
	};

	private StickyNoteOption CloneStickyNote(StickyNoteOption source)
	{
		int index = Math.Max(0, source.Number - 1); string prefix = $"sticky_note_{index}_";
		return new StickyNoteOption { Number = source.Number, Enabled = OverlayBool("sticky", prefix + "enabled", source.Enabled),
			Text = OverlayValue("sticky", prefix + "text", source.Text), X = OverlayDouble("sticky", prefix + "x", source.X),
			Y = OverlayDouble("sticky", prefix + "y", source.Y), Scale = OverlayDouble("sticky", prefix + "scale", source.Scale),
			Opacity = OverlayDouble("sticky", prefix + "opacity", source.Opacity), Theme = (int)OverlayDouble("sticky", prefix + "theme", source.Theme) };
	}

	private void LoadOverlayControls()
	{
		foreach (FrameworkElement element in FindLogicalChildren<FrameworkElement>(this).Where(e => e.Tag is string tag && tag.Contains(':')))
		{
			string[] parts = ((string)element.Tag).Split(':', 2);
			if (parts[0] == "inherit") continue; // inheritance toggles are handled separately
			string value = OverlayValue(parts[0], parts[1]);
			switch (element)
			{
				case CheckBox check: check.IsChecked = value is "1" or "true" or "yes" or "on"; break;
				case ComboBox combo when int.TryParse(value, out int index): combo.SelectedIndex = parts[1].EndsWith("_toggle_vk", StringComparison.Ordinal) ? OverlaySettingsCatalog.ComboIndexFromVirtualKey(index) : parts[1] == "performance_trace_marker_vk" ? Math.Clamp(index - 117, 0, 6) : Math.Clamp(index, 0, Math.Max(0, combo.Items.Count - 1)); break;
				case Slider slider when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number): slider.Value = Math.Clamp(number, slider.Minimum, slider.Maximum); break;
				case TextBox text: text.Text = value; break;
			}
		}
	}

	// ---- Per-overlay "Use Global Values" inheritance (item 24) ----------------------------------
	// A per-app overlay inherits the global configuration exactly when it has no override keys.
	// Ticking the box clears that overlay's override keys (it then follows future global changes);
	// unticking seeds override keys from the current effective values. Other overlays are untouched.
	private static readonly string[] InheritFeatures = { "clock", "hud", "trace", "notifications", "crosshair", "sticky", "obs", "iracing" };

	private CheckBox? InheritCheckboxFor(string feature) =>
		FindLogicalChildren<CheckBox>(this).FirstOrDefault(c => c.Tag is string t && t == "inherit:" + feature);

	private void SetInheritCheckbox(string feature, bool inherit)
	{
		CheckBox? box = InheritCheckboxFor(feature);
		if (box == null || box.IsChecked == inherit) return;
		bool previous = _syncingControls; _syncingControls = true;
		box.IsChecked = inherit;
		_syncingControls = previous;
		SetFeatureControlsEnabled(feature, !inherit);
	}

	private void SetFeatureControlsEnabled(string feature, bool enabled)
	{
		foreach (FrameworkElement element in FindLogicalChildren<FrameworkElement>(this)
			.Where(e => e.Tag is string tag && tag.StartsWith(feature + ":", StringComparison.Ordinal)))
			element.IsEnabled = enabled;
		if (feature == "hud" && ProfileHudWidgetList != null) ProfileHudWidgetList.IsEnabled = enabled;
		if (feature == "sticky" && ProfileStickyNotesList != null) ProfileStickyNotesList.IsEnabled = enabled;
	}

	private void InitInheritCheckboxes()
	{
		foreach (string feature in InheritFeatures)
		{
			CheckBox? box = InheritCheckboxFor(feature);
			if (box == null) continue;
			bool inherit = !_overlayOverrides.HasFeature(feature);
			bool previous = _syncingControls; _syncingControls = true;
			box.IsChecked = inherit;
			_syncingControls = previous;
			SetFeatureControlsEnabled(feature, !inherit);
		}
	}

	private void OverlayInherit_Changed(object sender, RoutedEventArgs e)
	{
		if (!_initialized || _syncingControls || sender is not CheckBox box || box.Tag is not string tag || !tag.StartsWith("inherit:", StringComparison.Ordinal)) return;
		string feature = tag.Substring("inherit:".Length);
		bool inherit = box.IsChecked == true;
		if (inherit) _overlayOverrides.ClearFeature(feature);
		else EnsureFeatureCustom(feature);
		// Reflect the now-effective values in the controls without re-recording them as overrides.
		bool previous = _syncingControls; _syncingControls = true;
		if (feature == "hud") RebuildHudWidgetsFromEffective();
		if (feature == "sticky") RebuildStickyNotesFromEffective();
		LoadOverlayControls();
		_syncingControls = previous;
		SetFeatureControlsEnabled(feature, !inherit);
		ApplyOverlayPreviewState();
	}

	private void RebuildHudWidgetsFromEffective()
	{
		var snapshot = _profileHudWidgets.Select(w => new HudWidgetOption
		{
			MetricId = w.MetricId, Id = w.Id, Label = w.Label, Provider = w.Provider, Unit = w.Unit,
			ThresholdUnit = w.ThresholdUnit, DefaultWarning = w.DefaultWarning, DefaultCritical = w.DefaultCritical,
			LowerIsWorse = w.LowerIsWorse, Availability = w.Availability, ToolTip = w.ToolTip
		}).ToList();
		var ordered = snapshot.Select((w, i) => (w, i)).OrderBy(p => OverlayDouble("hud", $"hud_widget_{p.w.Id}_order", p.i)).Select(p => p.w).ToList();
		_profileHudWidgets.Clear();
		foreach (var w in ordered) _profileHudWidgets.Add(CloneHudWidget(w));
	}

	private void RebuildStickyNotesFromEffective()
	{
		var numbers = _profileStickyNotes.Select(n => n.Number).ToList();
		_profileStickyNotes.Clear();
		int desired = Math.Clamp((int)OverlayDouble("sticky", "sticky_note_count", numbers.Count), 0, StickyNoteLiveStateService.MaxNotes);
		for (int i = 0; i < desired; i++) _profileStickyNotes.Add(CloneStickyNote(new StickyNoteOption { Number = i + 1 }));
	}

	private void ConfigureProfileHotkeys()
	{
		foreach (ComboBox combo in new[] { ProfileClockHotkey, ProfileHudHotkey, ProfileTraceHotkey, ProfileStickyHotkey, ProfileCrosshairHotkey, ProfileNotifyHotkey })
		{
			combo.Items.Add("None"); for (int key = 6; key <= 12; key++) combo.Items.Add("F" + key.ToString(CultureInfo.InvariantCulture)); combo.SelectedIndex = 0;
		}
	}

	// Overlay controls live inside collapsed Expanders and are hydrated during construction, before the
	// window is laid out — so the VISUAL tree does not contain them yet. The LOGICAL tree (built by
	// InitializeComponent) always does, regardless of expansion or realization, so tag-based hydration,
	// enable-state and inheritance-checkbox discovery must walk it. Using the visual tree here was the
	// root cause of inherited overlays loading blank until a section was toggled/expanded.
	private static IEnumerable<T> FindLogicalChildren<T>(System.Windows.DependencyObject root) where T : System.Windows.DependencyObject
	{
		foreach (object child in System.Windows.LogicalTreeHelper.GetChildren(root))
		{
			if (child is not System.Windows.DependencyObject dependencyChild) continue;
			if (dependencyChild is T match) yield return match;
			foreach (T descendant in FindLogicalChildren<T>(dependencyChild)) yield return descendant;
		}
	}

	private void RecordOverlaySetting(FrameworkElement element, string value)
	{
		if (!_initialized || _syncingControls || element.Tag is not string tag || !tag.Contains(':')) return;
		string[] parts = tag.Split(':', 2);
		if (parts[0] == "inherit") return; // inheritance toggles are not per-key overrides
		EnsureFeatureCustom(parts[0]);
		_overlayOverrides.Set(parts[0], parts[1], value);
		// Editing any control implies this overlay is now customised, so its inheritance box unticks.
		SetInheritCheckbox(parts[0], false);
		ApplyOverlayPreviewState();
	}

	private void EnsureFeatureCustom(string feature)
	{
		if (_overlayOverrides.HasFeature(feature)) return;
		foreach ((string key, string globalValue) in _globalOverlaySettings.Values.Where(p => p.Key.StartsWith(feature + ":", StringComparison.OrdinalIgnoreCase)))
			_overlayOverrides.Values[key] = globalValue;
	}

	private void OverlaySetting_Changed(object sender, RoutedEventArgs e)
	{
		if (sender is CheckBox check)
		{
			RecordOverlaySetting(check, check.IsChecked == true ? "1" : "0");
			if (check == ProfileTraceEnabled)
			{
				_overlayOverrides.Set("trace", "hud_trace_visibility_mode", check.IsChecked == true ? Math.Max(1, ProfileTraceVisibility.SelectedIndex).ToString(CultureInfo.InvariantCulture) : "0");
				ProfileTraceVisibility.SelectedIndex = check.IsChecked == true ? Math.Max(1, ProfileTraceVisibility.SelectedIndex) : 0;
			}
		}
		else if (sender is ComboBox combo)
		{
			bool hotkey = combo.Tag is string tag && tag.EndsWith("_toggle_vk", StringComparison.Ordinal);
			bool marker = combo.Tag is string markerTag && markerTag.EndsWith(":performance_trace_marker_vk", StringComparison.Ordinal);
			int value = hotkey ? OverlaySettingsCatalog.VirtualKeyFromComboIndex(combo.SelectedIndex) : marker ? 117 + Math.Max(0, combo.SelectedIndex) : Math.Max(0, combo.SelectedIndex);
			RecordOverlaySetting(combo, value.ToString(CultureInfo.InvariantCulture));
		}
	}

	private void OverlaySlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (sender is Slider slider) RecordOverlaySetting(slider, slider.Value.ToString("0.###", CultureInfo.InvariantCulture));
	}

	private void OverlayText_Changed(object sender, RoutedEventArgs e)
	{
		if (sender is TextBox text) RecordOverlaySetting(text, text.Text ?? string.Empty);
	}

	private void RecordHudWidgets()
	{
		if (!_initialized || _syncingControls) return; EnsureFeatureCustom("hud"); SetInheritCheckbox("hud", false);
		for (int index = 0; index < _profileHudWidgets.Count; index++)
		{
			HudWidgetOption widget = _profileHudWidgets[index]; string prefix = $"hud_widget_{widget.Id}_";
			_overlayOverrides.Set("hud", prefix + "enabled", widget.Enabled ? "1" : "0");
			_overlayOverrides.Set("hud", prefix + "symbol", widget.UseSymbol ? "1" : "0");
			_overlayOverrides.Set("hud", prefix + "unit", widget.ShowUnit ? "1" : "0");
			_overlayOverrides.Set("hud", prefix + "warning", widget.Warning.ToString("0.###", CultureInfo.InvariantCulture));
			_overlayOverrides.Set("hud", prefix + "critical", widget.Critical.ToString("0.###", CultureInfo.InvariantCulture));
			_overlayOverrides.Set("hud", prefix + "order", index.ToString(CultureInfo.InvariantCulture));
		}
		ApplyOverlayPreviewState();
	}

	private void ProfileHudWidget_Changed(object sender, RoutedEventArgs e) => RecordHudWidgets();
	private void ProfileHudThreshold_Changed(object sender, RoutedEventArgs e) => RecordHudWidgets();
	private void MoveHudWidget(string id, int delta)
	{
		int index = _profileHudWidgets.ToList().FindIndex(widget => widget.Id == id); int target = Math.Clamp(index + delta, 0, _profileHudWidgets.Count - 1);
		if (index < 0 || index == target) return; _profileHudWidgets.Move(index, target); RecordHudWidgets();
	}
	private void ProfileHudWidgetUp_Click(object sender, RoutedEventArgs e) { if (sender is Button b && b.CommandParameter is string id) MoveHudWidget(id, -1); }
	private void ProfileHudWidgetDown_Click(object sender, RoutedEventArgs e) { if (sender is Button b && b.CommandParameter is string id) MoveHudWidget(id, 1); }

	private void RecordStickyNotes()
	{
		if (!_initialized || _syncingControls) return; EnsureFeatureCustom("sticky"); SetInheritCheckbox("sticky", false);
		_overlayOverrides.Set("sticky", "sticky_note_count", _profileStickyNotes.Count.ToString(CultureInfo.InvariantCulture));
		for (int index = 0; index < _profileStickyNotes.Count; index++)
		{
			StickyNoteOption note = _profileStickyNotes[index]; note.Number = index + 1; string prefix = $"sticky_note_{index}_";
			_overlayOverrides.Set("sticky", prefix + "enabled", note.Enabled ? "1" : "0"); _overlayOverrides.Set("sticky", prefix + "text", note.Text);
			_overlayOverrides.Set("sticky", prefix + "x", note.X.ToString("0.###", CultureInfo.InvariantCulture)); _overlayOverrides.Set("sticky", prefix + "y", note.Y.ToString("0.###", CultureInfo.InvariantCulture));
			_overlayOverrides.Set("sticky", prefix + "scale", note.Scale.ToString("0.###", CultureInfo.InvariantCulture)); _overlayOverrides.Set("sticky", prefix + "opacity", note.Opacity.ToString("0.###", CultureInfo.InvariantCulture));
			_overlayOverrides.Set("sticky", prefix + "theme", note.Theme.ToString(CultureInfo.InvariantCulture));
		}
		ApplyOverlayPreviewState();
	}
	private void ProfileSticky_Changed(object sender, RoutedEventArgs e) => RecordStickyNotes();
	private void ProfileStickySlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => RecordStickyNotes();
	private void ProfileStickyAdd_Click(object sender, RoutedEventArgs e) { if (_profileStickyNotes.Count >= StickyNoteLiveStateService.MaxNotes) return; _profileStickyNotes.Add(new StickyNoteOption { Number = _profileStickyNotes.Count + 1 }); RecordStickyNotes(); }
	private void ProfileStickyRemove_Click(object sender, RoutedEventArgs e) { if (sender is Button b && b.CommandParameter is StickyNoteOption note) { _profileStickyNotes.Remove(note); RecordStickyNotes(); } }

	private void UpdateHideShowButton()
	{
		if (HideShowButton != null)
		{
			HideShowButton.Content = HiddenValue ? "Show Selected" : "Hide Selected";
		}
	}

	private void UpdateHints()
	{
		if (!_initialized) return;
		double value;
		bool flag = TryRead(TopBox.Text, out value);
		double value2;
		bool flag2 = TryRead(BottomBox.Text, out value2);
		double value3;
		bool flag3 = TryRead(HorizontalBox.Text, out value3);
		TopHint.Text = (flag ? (FormatPercent(value) + " top") : "Enter top value");
		BottomHint.Text = (flag2 ? (FormatPercent(value2) + " bottom") : "Enter bottom value");
		HorizontalHint.Text = (flag3 ? (FormatPercent(value3) + " horizontal") : "Enter horizontal width");
		RenderHint.Text = TryReadPercent(RenderBox.Text, out var rs) ? ($"{rs * 100.0:0.####}% render res") : "100 = no change";
		bool maskOk = TryRead(MaskVerticalBox.Text, out var maskV) && TryRead(MaskHorizontalBox.Text, out var maskH) && maskV >= 0.01 && maskH >= 0.01;
		CombinedHint.Text = ((flag && flag2 && maskOk) ? ("Combined: " + FormatPercent((value + value2) * 0.5) + " total render height") : "Combined: enter valid values");
	}

	private void Values_Changed(object sender, TextChangedEventArgs e)
	{
		if (!_initialized) return;
		if (MaskBeanEditor != null)
		{
			SyncMaskEditorFromSliders();
		}
		UpdateHints();
	}

	private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		DragMove();
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		if (!TryRead(TopBox.Text, out var value) || !TryRead(BottomBox.Text, out var value2) || !TryRead(HorizontalBox.Text, out var value3) || (value + value2) * 0.5 < 0.01 || value3 < 0.01)
		{
			MessageBox.Show(this, "Enter values from 0.00 to 1.00. Combined vertical and horizontal values must be at least 0.01.", "ViewLab", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		TryRead(MaskVerticalBox.Text, out var maskV);
		TryRead(MaskHorizontalBox.Text, out var maskH);
		maskV = Math.Clamp(maskV, 0.01, 1.0);
		maskH = Math.Clamp(maskH, 0.01, 1.0);
		TopValue = value * 0.5;
		BottomValue = value2 * 0.5;
		HorizontalValue = value3;
		DisplayName = (NameBox.Text ?? "").Trim();
		TryReadPercent(RenderBox.Text, out double rs);
		RenderScaleValue = rs;
		MaskEnabledValue = VisorEnabledCheck.IsChecked == true;
		MaskVerticalValue = maskV;
		MaskHorizontalValue = maskH;
		MaskRoundedValue = VisorCurveSlider.Value > 0.001;
		MaskCornerValue = 1.0 - VisorCurveSlider.Value;
		MaskTopBiasValue = 0.0;
		MaskBottomBiasValue = 0.0;
		MaskLeftBiasValue = 0.0;
		MaskRightBiasValue = 0.0;
		MaskTopCurveValue = 0.0;
		MaskBottomCurveValue = 0.0;
		VisorSizeValue = VisorSizeSlider.Value;
		VisorWidthValue = VisorWidthSlider.Value;
		VisorHeightValue = VisorHeightSlider.Value;
		VisorCurveValue = VisorCurveSlider.Value;
		VisorOuterApexYValue = VisorApexYSlider.Value;
		VisorInnerLowerYValue = VisorInnerLowerSlider.Value;
		VisorInnerBridgeWidthValue = 0.5;
		VisorInnerBridgeRiseValue = 0.0;
		VisorInnerBridgePeakXValue = 0.5;
		VisorInnerBridgeSteepnessValue = 0.5;
		VisorNoseSpreadXValue = VisorNoseSpreadXSlider.Value;
		UseGlobalVisor = UseGlobalVisorCheck.IsChecked == true;
		base.DialogResult = true;
	}

	private void HideShow_Click(object sender, RoutedEventArgs e)
	{
		HiddenValue = !HiddenValue;
		HiddenChanged = true;
		UpdateHideShowButton();
		base.DialogResult = true;
	}

	private void LoadGlobalVisorValues()
	{
		MaskVerticalBox.Text = FormatScale(_globalMaskVertical);
		MaskHorizontalBox.Text = FormatScale(_globalMaskHorizontal);
		VisorEnabledCheck.IsChecked = _globalMaskEnabled;
		VisorSizeSlider.Value = _globalVisorSize;
		VisorWidthSlider.Value = _globalVisorWidth;
		VisorHeightSlider.Value = _globalVisorHeight;
		VisorCurveSlider.Value = Math.Clamp(1.0 - _globalMaskCorner, 0.0, 1.0);
		VisorApexYSlider.Value = _globalVisorOuterApexY;
		VisorInnerLowerSlider.Value = _globalVisorInnerLowerY;
		VisorNoseSpreadXSlider.Value = _globalVisorNoseSpreadX;
	}

	private void LoadCustomVisorValues()
	{
		MaskVerticalBox.Text = FormatScale(_customMaskVertical);
		MaskHorizontalBox.Text = FormatScale(_customMaskHorizontal);
		VisorEnabledCheck.IsChecked = _customMaskEnabled;
		VisorSizeSlider.Value = _customVisorSize;
		VisorWidthSlider.Value = _customVisorWidth;
		VisorHeightSlider.Value = _customVisorHeight;
		VisorCurveSlider.Value = Math.Clamp(1.0 - _customMaskCorner, 0.0, 1.0);
		VisorApexYSlider.Value = _customVisorOuterApexY;
		VisorInnerLowerSlider.Value = _customVisorInnerLowerY;
		VisorNoseSpreadXSlider.Value = _customVisorNoseSpreadX;
	}

	private void UseGlobal_Click(object sender, RoutedEventArgs e)
	{
		UseGlobalValues = true;
		UseGlobalVisor = true;
		_overlayPlacements.Clear();
		_overlayOverrides.Clear();
		LoadOverlayControls();
		LoadGlobalVisorValues();
		UseGlobalVisorCheck.IsChecked = true;
		SetVisorSlidersEnabled(false);
		SyncMaskEditorFromSliders();
		ApplyOverlayPreviewState();
		UpdateHints();
		base.DialogResult = true;
	}

	private void MaskRoundnessSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_initialized || _syncingControls || MaskBeanEditor == null) return;
		SyncMaskEditorFromSliders();
	}

	private void MaskShapeSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_initialized || _syncingControls || MaskBeanEditor == null) return;
		SyncMaskEditorFromSliders();
	}

	private void VisorApexYSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_initialized || _syncingControls || MaskBeanEditor == null) return;
		SyncMaskEditorFromSliders();
	}

	private void VisorInnerLowerSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_initialized || _syncingControls || MaskBeanEditor == null) return;
		SyncMaskEditorFromSliders();
	}

	private void VisorEnabled_Changed(object sender, RoutedEventArgs e)
	{
		if (!_initialized || _syncingControls || MaskBeanEditor == null) return;
		SyncMaskEditorFromSliders();
	}

	private void VisorSliderReset_RightClick(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
		if (sender == VisorSizeSlider) VisorSizeSlider.Value = 1.0;
		else if (sender == VisorWidthSlider) VisorWidthSlider.Value = 1.0;
		else if (sender == VisorHeightSlider) VisorHeightSlider.Value = 1.0;
		else if (sender == VisorCurveSlider) VisorCurveSlider.Value = 0.5;
		else if (sender == VisorApexYSlider) VisorApexYSlider.Value = 0.0;
		else if (sender == VisorInnerLowerSlider) VisorInnerLowerSlider.Value = 0.0;
		else if (sender == VisorNoseSpreadXSlider) VisorNoseSpreadXSlider.Value = 0.0;
	}

	private void UseGlobalVisor_Changed(object sender, RoutedEventArgs e)
	{
		if (!_initialized) return;
		bool useGlobal = UseGlobalVisorCheck.IsChecked == true;
		UseGlobalVisor = useGlobal;
		if (useGlobal)
		{
			LoadGlobalVisorValues();
		}
		else
		{
			LoadCustomVisorValues();
		}
		SetVisorSlidersEnabled(!useGlobal);
		SyncMaskEditorFromSliders();
		UpdateHints();
	}

	private void MaskBeanEditor_ShapeChanged(object sender, EventArgs e)
	{
		if (!_initialized || _syncingControls || UseGlobalVisorCheck.IsChecked == true)
		{
			return;
		}
		_syncingControls = true;
		VisorApexYSlider.Value = MaskBeanEditor.OuterApexY;
		VisorSizeSlider.Value = MaskBeanEditor.Size;
		VisorWidthSlider.Value = MaskBeanEditor.WidthScale;
		VisorHeightSlider.Value = MaskBeanEditor.HeightScale;
		VisorCurveSlider.Value = MaskBeanEditor.Curve;
		VisorInnerLowerSlider.Value = MaskBeanEditor.InnerLowerY;
		VisorNoseSpreadXSlider.Value = MaskBeanEditor.NoseSpreadX;
		_syncingControls = false;
	}

	private void MaskBeanEditor_OverlayPreviewChanged(object? sender, OverlayPreviewChangedEventArgs e)
	{
		int index = _globalOverlayPreviews.FindIndex(item => item.Id == e.Id);
		if (index < 0) return;
		OverlayPreviewItem global = _globalOverlayPreviews[index];
		OverlayPlacementOverride prior = _overlayPlacements.TryGetValue(e.Id, out OverlayPlacementOverride existing)
			? existing : new OverlayPlacementOverride(global.X, global.Y, global.Scale);
		_overlayPlacements[e.Id] = e.Kind == OverlayPreviewEditKind.Position
			? prior with { X = e.X, Y = e.Y }
			: prior with { Scale = e.Scale };
		if (OverlaySettingsCatalog.All.TryGetValue(e.Id, out OverlaySettingsDefinition? definition))
		{
			EnsureFeatureCustom(e.Id);
			if (e.Kind == OverlayPreviewEditKind.Position)
			{
				_overlayOverrides.Set(e.Id, definition.XKey, e.X.ToString("0.###", CultureInfo.InvariantCulture));
				_overlayOverrides.Set(e.Id, definition.YKey, e.Y.ToString("0.###", CultureInfo.InvariantCulture));
			}
			else _overlayOverrides.Set(e.Id, definition.ScaleKey, e.Scale.ToString("0.###", CultureInfo.InvariantCulture));
		}
		else if (e.Id.StartsWith("sticky:", StringComparison.Ordinal) && int.TryParse(e.Id.AsSpan("sticky:".Length), out int stickyIndex) && stickyIndex >= 0 && stickyIndex < _profileStickyNotes.Count)
		{
			EnsureFeatureCustom("sticky"); StickyNoteOption note = _profileStickyNotes[stickyIndex]; string prefix = $"sticky_note_{stickyIndex}_";
			if (e.Kind == OverlayPreviewEditKind.Position) { note.X = e.X; note.Y = e.Y; _overlayOverrides.Set("sticky", prefix + "x", e.X.ToString("0.###", CultureInfo.InvariantCulture)); _overlayOverrides.Set("sticky", prefix + "y", e.Y.ToString("0.###", CultureInfo.InvariantCulture)); }
			else { note.Scale = e.Scale; _overlayOverrides.Set("sticky", prefix + "scale", e.Scale.ToString("0.###", CultureInfo.InvariantCulture)); }
		}
		ApplyOverlayPreviewState();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
	}
}
