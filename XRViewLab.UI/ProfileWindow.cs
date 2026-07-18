using System;
using System.Globalization;
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
	public bool UseCircularEyeGuidesValue { get; private set; } = true;
	public bool UsePerEyeFrameGuidesValue { get; private set; }
	public double PreviewIpdMillimetresValue { get; private set; } = Quest3PreviewGeometry.DefaultIpdMillimetres;

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
	private readonly double _customVisorInnerBridgeSteepness;
	private readonly double _customVisorNoseSpreadX;

	// In-memory boxes feed the legacy mask_vertical/horizontal calculation (not shown in UI).
	private readonly TextBox MaskVerticalBox = new() { Text = "1" };
	private readonly TextBox MaskHorizontalBox = new() { Text = "1" };

	// Guards event handlers that fire via WPF property coercion during InitializeComponent.
	private bool _initialized;
	private bool _syncingControls;

	public double VisorSizeValue { get; private set; } = 0.82;
	public double VisorCurveValue { get; private set; } = 0.75;
	public double VisorOffsetXValue { get; private set; }
	public double VisorOffsetYValue { get; private set; }
	public double VisorOuterApexYValue { get; private set; }
	public double VisorInnerLowerYValue { get; private set; }
	public double VisorInnerBridgeWidthValue { get; private set; } = 0.5;
	public double VisorInnerBridgeRiseValue { get; private set; }
	public double VisorInnerBridgePeakXValue { get; private set; } = 0.5;
	public double VisorInnerBridgeSteepnessValue { get; private set; } = 0.5;
	public double VisorNoseSpreadXValue { get; private set; }

	public ProfileWindow(string appName, string exeName, bool hidden, double top, double bottom, double horizontal, double renderScale, bool maskEnabled, double maskVertical, double maskHorizontal, bool maskRounded, double maskCorner, double maskTopBias, double maskBottomBias, double maskLeftBias, double maskRightBias, double maskTopCurve, double maskBottomCurve, bool globalMaskEnabled, double globalMaskVertical, double globalMaskHorizontal, double globalMaskCorner, double globalOffsetX, double globalOffsetY, double visorSize, double visorOuterApexY, double visorInnerLowerY, double visorInnerBridgeWidth, double visorInnerBridgeRise, double visorInnerBridgePeakX, double visorInnerBridgeSteepness, double visorNoseSpreadX, double globalVisorSize, double globalVisorOuterApexY, double globalVisorInnerLowerY, double globalVisorInnerBridgeWidth, double globalVisorInnerBridgeRise, double globalVisorInnerBridgePeakX, double globalVisorInnerBridgeSteepness, double globalVisorNoseSpreadX, bool useCircularEyeGuides, bool usePerEyeFrameGuides, double previewIpdMillimetres, bool globalStencilOuterEdges)
	{
		InitializeComponent();
		_globalMaskEnabled = globalMaskEnabled;
		_globalMaskVertical = globalMaskVertical;
		_globalMaskHorizontal = globalMaskHorizontal;
		_globalMaskCorner = globalMaskCorner;
		_globalOffsetX = globalOffsetX;
		_globalOffsetY = globalOffsetY;
		_globalVisorSize = Math.Clamp(globalVisorSize, 0.05, 1.0);
		_globalVisorOuterApexY = globalVisorOuterApexY;
		_globalVisorInnerLowerY = globalVisorInnerLowerY;
		_globalVisorInnerBridgeWidth = globalVisorInnerBridgeWidth;
		_globalVisorInnerBridgeRise = globalVisorInnerBridgeRise;
		_globalVisorInnerBridgePeakX = globalVisorInnerBridgePeakX;
		_globalVisorInnerBridgeSteepness = globalVisorInnerBridgeSteepness;
		_globalVisorNoseSpreadX = Math.Clamp(globalVisorNoseSpreadX, 0.0, 0.5);
		_customMaskEnabled = maskEnabled;
		_customMaskVertical = maskVertical;
		_customMaskHorizontal = maskHorizontal;
		_customMaskCorner = maskCorner;
		_customOffsetX = Math.Clamp((maskLeftBias + maskRightBias) * 0.5, -1.0, 1.0);
		_customOffsetY = Math.Clamp((maskTopBias + maskBottomBias) * 0.5, -1.0, 1.0);
		_customVisorSize = Math.Clamp(visorSize, 0.1, 1.0);
		_customVisorOuterApexY = Math.Clamp(visorOuterApexY, -0.5, 0.5);
		_customVisorInnerLowerY = Math.Clamp(visorInnerLowerY, 0.0, 0.666);
		_customVisorInnerBridgeWidth = Math.Clamp(visorInnerBridgeWidth, 0.0, 1.0);
		_customVisorInnerBridgeRise = Math.Clamp(visorInnerBridgeRise, -0.5, 1.0);
		_customVisorInnerBridgePeakX = Math.Clamp(visorInnerBridgePeakX, -1.0, 2.0);
		_customVisorInnerBridgeSteepness = Math.Clamp(visorInnerBridgeSteepness, -1.0, 2.0);
		_customVisorNoseSpreadX = Math.Clamp(visorNoseSpreadX, 0.0, 0.5);
		DisplayName = appName;
		UseCircularEyeGuidesValue = useCircularEyeGuides;
		PreviewCircleGuidesCheck.IsChecked = useCircularEyeGuides;
		UsePerEyeFrameGuidesValue = usePerEyeFrameGuides;
		PreviewPerEyeFramesCheck.IsChecked = usePerEyeFrameGuides;
		PreviewIpdMillimetresValue = Math.Round(Math.Clamp(previewIpdMillimetres,
			Quest3PreviewGeometry.MinimumIpdMillimetres, Quest3PreviewGeometry.MaximumIpdMillimetres), 1);
		PreviewIpdBox.Text = PreviewIpdMillimetresValue.ToString("0.0", CultureInfo.InvariantCulture);
		HiddenValue = hidden;
		NameBox.Text = appName;
		ExeNameText.Text = exeName;
		TopBox.Text = FormatScale(top);
		BottomBox.Text = FormatScale(bottom);
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
			return Math.Clamp(top + bottom, 0.01, 1.0);
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
		MaskBeanEditor.Curve = VisorCurveSlider?.Value ?? 0.5;
		MaskBeanEditor.OuterApexY = VisorApexYSlider?.Value ?? 0.0;
		MaskBeanEditor.InnerLowerY = VisorInnerLowerSlider?.Value ?? 0.0;
		MaskBeanEditor.InnerBridgeWidth = VisorInnerBridgeSlider?.Value ?? 0.5;
		MaskBeanEditor.InnerBridgeRise = VisorInnerBridgeRiseSlider?.Value ?? 0.0;
		MaskBeanEditor.InnerBridgePeakX = VisorInnerBridgePeakXSlider?.Value ?? 0.5;
		MaskBeanEditor.InnerBridgeSteepness = VisorInnerBridgeSteepnessSlider?.Value ?? 0.5;
		MaskBeanEditor.NoseSpreadX = VisorNoseSpreadXSlider?.Value ?? 0.0;
		MaskBeanEditor.UseCircularEyeGuides = PreviewCircleGuidesCheck?.IsChecked == true;
		MaskBeanEditor.UsePerEyeFrameGuides = PreviewPerEyeFramesCheck?.IsChecked == true;
		MaskBeanEditor.PreviewIpdMillimetres = CurrentPreviewIpd();
		MaskBeanEditor.OffsetX = VisorOffsetXSlider?.Value ?? 0.0;
		MaskBeanEditor.OffsetY = VisorOffsetYSlider?.Value ?? 0.0;
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
		VisorEnabledCheck.IsEnabled = false;
		VisorSizeSlider.IsEnabled = enabled;
		VisorCurveSlider.IsEnabled = enabled;
		VisorApexYSlider.IsEnabled = enabled;
		VisorInnerLowerSlider.IsEnabled = enabled;
		VisorInnerBridgeSlider.IsEnabled = enabled;
		VisorInnerBridgeRiseSlider.IsEnabled = enabled;
		VisorInnerBridgePeakXSlider.IsEnabled = enabled;
		VisorInnerBridgeSteepnessSlider.IsEnabled = enabled;
		VisorNoseSpreadXSlider.IsEnabled = enabled;
		VisorOffsetXSlider.IsEnabled = enabled;
		VisorOffsetYSlider.IsEnabled = enabled;
		MaskBeanEditor.IsEnabled = enabled;
	}

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
		CombinedHint.Text = ((flag && flag2 && maskOk) ? ("Combined: " + FormatPercent(value + value2) + " total render height") : "Combined: enter valid values");
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
		if (!TryRead(TopBox.Text, out var value) || !TryRead(BottomBox.Text, out var value2) || !TryRead(HorizontalBox.Text, out var value3) || value + value2 < 0.01 || value3 < 0.01)
		{
			MessageBox.Show(this, "Enter values from 0.00 to 1.00. Combined vertical and horizontal values must be at least 0.01.", "ViewLab", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		TryRead(MaskVerticalBox.Text, out var maskV);
		TryRead(MaskHorizontalBox.Text, out var maskH);
		maskV = Math.Clamp(maskV, 0.01, 1.0);
		maskH = Math.Clamp(maskH, 0.01, 1.0);
		TopValue = value;
		BottomValue = value2;
		HorizontalValue = value3;
		DisplayName = (NameBox.Text ?? "").Trim();
		TryReadPercent(RenderBox.Text, out double rs);
		RenderScaleValue = rs;
		MaskEnabledValue = VisorEnabledCheck.IsChecked == true;
		MaskVerticalValue = maskV;
		MaskHorizontalValue = maskH;
		MaskRoundedValue = VisorCurveSlider.Value > 0.001;
		MaskCornerValue = 1.0 - VisorCurveSlider.Value;
		double offsetX = VisorOffsetXSlider.Value;
		double offsetY = VisorOffsetYSlider.Value;
		MaskTopBiasValue = offsetY;
		MaskBottomBiasValue = offsetY;
		MaskLeftBiasValue = offsetX;
		MaskRightBiasValue = offsetX;
		MaskTopCurveValue = 0.0;
		MaskBottomCurveValue = 0.0;
		VisorSizeValue = VisorSizeSlider.Value;
		VisorCurveValue = VisorCurveSlider.Value;
		VisorOuterApexYValue = VisorApexYSlider.Value;
		VisorInnerLowerYValue = VisorInnerLowerSlider.Value;
		VisorInnerBridgeWidthValue = VisorInnerBridgeSlider.Value;
		VisorInnerBridgeRiseValue = VisorInnerBridgeRiseSlider.Value;
		VisorInnerBridgePeakXValue = VisorInnerBridgePeakXSlider.Value;
		VisorInnerBridgeSteepnessValue = VisorInnerBridgeSteepnessSlider.Value;
		VisorNoseSpreadXValue = VisorNoseSpreadXSlider.Value;
		VisorOffsetXValue = VisorOffsetXSlider.Value;
		VisorOffsetYValue = VisorOffsetYSlider.Value;
		UseGlobalVisor = UseGlobalVisorCheck.IsChecked == true;
		UseCircularEyeGuidesValue = PreviewCircleGuidesCheck.IsChecked == true;
		UsePerEyeFrameGuidesValue = PreviewPerEyeFramesCheck.IsChecked == true;
		PreviewIpdMillimetresValue = CurrentPreviewIpd();
		base.DialogResult = true;
	}

	private void PreviewGuideMode_Changed(object sender, RoutedEventArgs e)
	{
		UseCircularEyeGuidesValue = PreviewCircleGuidesCheck?.IsChecked == true;
		if (MaskBeanEditor != null)
			MaskBeanEditor.UseCircularEyeGuides = UseCircularEyeGuidesValue;
	}

	private void PreviewFrameMode_Changed(object sender, RoutedEventArgs e)
	{
		UsePerEyeFrameGuidesValue = PreviewPerEyeFramesCheck?.IsChecked == true;
		if (MaskBeanEditor != null)
			MaskBeanEditor.UsePerEyeFrameGuides = UsePerEyeFrameGuidesValue;
	}

	private double CurrentPreviewIpd()
	{
		if (!double.TryParse(PreviewIpdBox?.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
			return Quest3PreviewGeometry.DefaultIpdMillimetres;
		return Math.Round(Math.Clamp(value, Quest3PreviewGeometry.MinimumIpdMillimetres,
			Quest3PreviewGeometry.MaximumIpdMillimetres), 1);
	}

	private void PreviewIpd_Changed(object sender, TextChangedEventArgs e)
	{
		if (!_initialized || MaskBeanEditor == null || !double.TryParse(PreviewIpdBox.Text,
			NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return;
		PreviewIpdMillimetresValue = CurrentPreviewIpd();
		MaskBeanEditor.PreviewIpdMillimetres = PreviewIpdMillimetresValue;
	}

	private void PreviewIpd_Commit(object sender, KeyboardFocusChangedEventArgs e) =>
		PreviewIpdBox.Text = CurrentPreviewIpd().ToString("0.0", CultureInfo.InvariantCulture);

	private void PreviewIpd_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key != Key.Up && e.Key != Key.Down) return;
		double delta = e.Key == Key.Up ? 0.1 : -0.1;
		PreviewIpdBox.Text = Math.Clamp(CurrentPreviewIpd() + delta,
			Quest3PreviewGeometry.MinimumIpdMillimetres, Quest3PreviewGeometry.MaximumIpdMillimetres)
			.ToString("0.0", CultureInfo.InvariantCulture);
		PreviewIpdBox.SelectAll();
		e.Handled = true;
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
		VisorCurveSlider.Value = Math.Clamp(1.0 - _globalMaskCorner, 0.0, 1.0);
		VisorApexYSlider.Value = _globalVisorOuterApexY;
		VisorInnerLowerSlider.Value = _globalVisorInnerLowerY;
		VisorInnerBridgeSlider.Value = _globalVisorInnerBridgeWidth;
		VisorInnerBridgeRiseSlider.Value = _globalVisorInnerBridgeRise;
		VisorInnerBridgePeakXSlider.Value = _globalVisorInnerBridgePeakX;
		VisorInnerBridgeSteepnessSlider.Value = _globalVisorInnerBridgeSteepness;
		VisorNoseSpreadXSlider.Value = _globalVisorNoseSpreadX;
		VisorOffsetXSlider.Value = _globalOffsetX;
		VisorOffsetYSlider.Value = _globalOffsetY;
	}

	private void LoadCustomVisorValues()
	{
		MaskVerticalBox.Text = FormatScale(_customMaskVertical);
		MaskHorizontalBox.Text = FormatScale(_customMaskHorizontal);
		VisorEnabledCheck.IsChecked = _customMaskEnabled;
		VisorSizeSlider.Value = _customVisorSize;
		VisorCurveSlider.Value = Math.Clamp(1.0 - _customMaskCorner, 0.0, 1.0);
		VisorApexYSlider.Value = _customVisorOuterApexY;
		VisorInnerLowerSlider.Value = _customVisorInnerLowerY;
		VisorInnerBridgeSlider.Value = _customVisorInnerBridgeWidth;
		VisorInnerBridgeRiseSlider.Value = _customVisorInnerBridgeRise;
		VisorInnerBridgePeakXSlider.Value = _customVisorInnerBridgePeakX;
		VisorInnerBridgeSteepnessSlider.Value = _customVisorInnerBridgeSteepness;
		VisorNoseSpreadXSlider.Value = _customVisorNoseSpreadX;
		VisorOffsetXSlider.Value = _customOffsetX;
		VisorOffsetYSlider.Value = _customOffsetY;
	}

	private void UseGlobal_Click(object sender, RoutedEventArgs e)
	{
		UseGlobalValues = true;
		UseGlobalVisor = true;
		LoadGlobalVisorValues();
		UseGlobalVisorCheck.IsChecked = true;
		SetVisorSlidersEnabled(false);
		SyncMaskEditorFromSliders();
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

	private void VisorInnerBridgeSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_initialized || _syncingControls || MaskBeanEditor == null) return;
		SyncMaskEditorFromSliders();
	}

	private void VisorInnerBridgeDetailSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_initialized || _syncingControls || MaskBeanEditor == null) return;
		SyncMaskEditorFromSliders();
	}

	private void VisorNoseSpreadXSlider_Reset(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
		VisorNoseSpreadXSlider.Value = 0.0;
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
		VisorCurveSlider.Value = MaskBeanEditor.Curve;
		VisorInnerLowerSlider.Value = MaskBeanEditor.InnerLowerY;
		VisorInnerBridgeSlider.Value = MaskBeanEditor.InnerBridgeWidth;
		VisorInnerBridgeRiseSlider.Value = MaskBeanEditor.InnerBridgeRise;
		VisorInnerBridgePeakXSlider.Value = MaskBeanEditor.InnerBridgePeakX;
		VisorInnerBridgeSteepnessSlider.Value = MaskBeanEditor.InnerBridgeSteepness;
		VisorNoseSpreadXSlider.Value = MaskBeanEditor.NoseSpreadX;
		_syncingControls = false;
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
	}
}
