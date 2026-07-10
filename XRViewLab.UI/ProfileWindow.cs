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

	public bool UseGlobal { get; private set; }

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
	private readonly double _globalVisorSize;
	private readonly double _globalVisorOuterApexY;
	private readonly double _globalVisorInnerLowerY;
	private readonly double _globalVisorInnerBridgeWidth;
	private readonly double _globalVisorInnerBridgeRise;
	private readonly double _globalVisorInnerBridgePeakX;
	private readonly double _globalVisorInnerBridgeSteepness;

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

	public ProfileWindow(string appName, string exeName, bool hidden, double top, double bottom, double horizontal, double renderScale, bool maskEnabled, double maskVertical, double maskHorizontal, bool maskRounded, double maskCorner, double maskTopBias, double maskBottomBias, double maskLeftBias, double maskRightBias, double maskTopCurve, double maskBottomCurve, bool globalMaskEnabled, double globalMaskVertical, double globalMaskHorizontal, double globalMaskCorner, double globalOffsetX, double globalOffsetY, double visorSize, double visorOuterApexY, double visorInnerLowerY, double visorInnerBridgeWidth, double visorInnerBridgeRise, double visorInnerBridgePeakX, double visorInnerBridgeSteepness, double globalVisorSize, double globalVisorOuterApexY, double globalVisorInnerLowerY, double globalVisorInnerBridgeWidth, double globalVisorInnerBridgeRise, double globalVisorInnerBridgePeakX, double globalVisorInnerBridgeSteepness)
	{
		InitializeComponent();
		_initialized = true;
		_globalMaskEnabled = globalMaskEnabled;
		_globalMaskVertical = globalMaskVertical;
		_globalMaskHorizontal = globalMaskHorizontal;
		_globalMaskCorner = globalMaskCorner;
		_globalOffsetX = globalOffsetX;
		_globalOffsetY = globalOffsetY;
		_globalVisorSize = globalVisorSize;
		_globalVisorOuterApexY = globalVisorOuterApexY;
		_globalVisorInnerLowerY = globalVisorInnerLowerY;
		_globalVisorInnerBridgeWidth = globalVisorInnerBridgeWidth;
		_globalVisorInnerBridgeRise = globalVisorInnerBridgeRise;
		_globalVisorInnerBridgePeakX = globalVisorInnerBridgePeakX;
		_globalVisorInnerBridgeSteepness = globalVisorInnerBridgeSteepness;
		DisplayName = appName;
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
		double legacyOpening = OpeningFromMask(maskVertical, maskHorizontal, top + bottom, horizontal);
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
		VisorEnabledCheck.IsChecked = maskEnabled;
		VisorSizeSlider.Value = useGlobal ? globalVisorSize : Math.Clamp(visorSize, 0.05, 1.0);
		VisorCurveSlider.Value = Math.Clamp(1.0 - (useGlobal ? globalMaskCorner : maskCorner), 0.0, 1.0);
		VisorApexYSlider.Value = useGlobal ? globalVisorOuterApexY : Math.Clamp(visorOuterApexY, -0.5, 0.5);
		VisorInnerLowerSlider.Value = useGlobal ? globalVisorInnerLowerY : Math.Clamp(visorInnerLowerY, 0.0, 0.333);
		VisorInnerBridgeSlider.Value = useGlobal ? globalVisorInnerBridgeWidth : Math.Clamp(visorInnerBridgeWidth, 0.0, 1.0);
		VisorInnerBridgeRiseSlider.Value = useGlobal ? globalVisorInnerBridgeRise : Math.Clamp(visorInnerBridgeRise, 0.0, 0.5);
		VisorInnerBridgePeakXSlider.Value = useGlobal ? globalVisorInnerBridgePeakX : Math.Clamp(visorInnerBridgePeakX, 0.0, 1.0);
		VisorInnerBridgeSteepnessSlider.Value = useGlobal ? globalVisorInnerBridgeSteepness : Math.Clamp(visorInnerBridgeSteepness, 0.0, 1.0);
		VisorOffsetXSlider.Value = Math.Clamp((maskLeftBias + maskRightBias) * 0.5, -1.0, 1.0);
		VisorOffsetYSlider.Value = Math.Clamp((maskTopBias + maskBottomBias) * 0.5, -1.0, 1.0);
		SetVisorSlidersEnabled(!useGlobal);
		SyncMaskEditorFromSliders();
		UpdateHideShowButton();
		UpdateHints();
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

	private static double OpeningFromMask(double maskVertical, double maskHorizontal, double cropVertical, double cropHorizontal)
	{
		double verticalOpening = cropVertical > 0.0 ? maskVertical / cropVertical : maskVertical;
		double horizontalOpening = cropHorizontal > 0.0 ? maskHorizontal / cropHorizontal : maskHorizontal;
		return Math.Clamp((verticalOpening + horizontalOpening) * 0.5, 0.05, 1.0);
	}

	private static double WidthScaleFromMask(double maskHorizontal, double cropHorizontal, double size)
	{
		return Math.Clamp(cropHorizontal > 0.0 && size > 0.0 ? maskHorizontal / cropHorizontal / size : 1.0, 0.5, 1.5);
	}

	private static double HeightScaleFromMask(double maskVertical, double cropVertical, double size)
	{
		return Math.Clamp(cropVertical > 0.0 && size > 0.0 ? maskVertical / cropVertical / size : 1.0, 0.5, 1.5);
	}

	private void ApplyMaskOpening(double opening)
	{
		if (!_initialized) return;
		double clamped = Math.Clamp(opening, 0.05, 1.0);
		MaskBeanEditor.Opening = clamped;
		MaskVerticalBox.Text = FormatScale(CurrentVerticalCrop() * Math.Clamp(clamped, 0.01, 1.0));
		MaskHorizontalBox.Text = FormatScale(CurrentHorizontalCrop() * Math.Clamp(clamped, 0.01, 1.0));
		SyncMaskEditorFromSliders();
	}

	private void SyncMaskEditorFromSliders()
	{
		if (MaskBeanEditor == null)
		{
			return;
		}
		MaskBeanEditor.Opening = VisorSizeSlider?.Value ?? 1.0;
		MaskBeanEditor.Curve = VisorCurveSlider?.Value ?? 0.5;
		MaskBeanEditor.OuterApexY = VisorApexYSlider?.Value ?? 0.0;
		MaskBeanEditor.InnerLowerY = VisorInnerLowerSlider?.Value ?? 0.0;
		MaskBeanEditor.InnerBridgeWidth = VisorInnerBridgeSlider?.Value ?? 0.5;
		MaskBeanEditor.InnerBridgeRise = VisorInnerBridgeRiseSlider?.Value ?? 0.0;
		MaskBeanEditor.InnerBridgePeakX = VisorInnerBridgePeakXSlider?.Value ?? 0.5;
		MaskBeanEditor.InnerBridgeSteepness = VisorInnerBridgeSteepnessSlider?.Value ?? 0.5;
		MaskBeanEditor.OffsetX = VisorOffsetXSlider?.Value ?? 0.0;
		MaskBeanEditor.OffsetY = VisorOffsetYSlider?.Value ?? 0.0;
		MaskBeanEditor.OpenInnerPreview = StencilOuterEdgesCheck?.IsChecked == true;
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
		if (MaskBeanEditor != null && TryRead(MaskVerticalBox.Text, out var maskV) && TryRead(MaskHorizontalBox.Text, out var maskH))
		{
			MaskBeanEditor.Opening = OpeningFromMask(maskV, maskH, CurrentVerticalCrop(), CurrentHorizontalCrop());
		}
		if (sender != RenderBox)
		{
			ApplyMaskOpening(VisorSizeSlider?.Value ?? 0.82);
		}
		UpdateHints();
	}

	private void MaskOpeningSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_initialized || _syncingControls || VisorSizeSlider == null)
		{
			return;
		}
		double opening = Math.Clamp(VisorSizeSlider.Value, 0.05, 1.0);
		ApplyMaskOpening(opening);
		UpdateHints();
	}

	private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		DragMove();
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		if (!TryRead(TopBox.Text, out var value) || !TryRead(BottomBox.Text, out var value2) || !TryRead(HorizontalBox.Text, out var value3) || !TryRead(MaskVerticalBox.Text, out var maskV) || !TryRead(MaskHorizontalBox.Text, out var maskH) || value + value2 < 0.01 || value3 < 0.01 || maskV < 0.01 || maskH < 0.01)
		{
			MessageBox.Show(this, "Enter values from 0.00 to 1.00. Combined vertical, horizontal, and mask values must be at least 0.01.", "ViewLab", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
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
		VisorOffsetXValue = VisorOffsetXSlider.Value;
		VisorOffsetYValue = VisorOffsetYSlider.Value;
		UseGlobal = UseGlobalVisorCheck.IsChecked == true;
		base.DialogResult = true;
	}

	private void HideShow_Click(object sender, RoutedEventArgs e)
	{
		HiddenValue = !HiddenValue;
		HiddenChanged = true;
		UpdateHideShowButton();
		base.DialogResult = true;
	}

	private void UseGlobal_Click(object sender, RoutedEventArgs e)
	{
		UseGlobal = true;
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
		VisorOffsetXSlider.Value = _globalOffsetX;
		VisorOffsetYSlider.Value = _globalOffsetY;
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
		ApplyMaskOpening(VisorSizeSlider?.Value ?? 0.82);
	}

	private void StencilOuterEdgesCheck_Changed(object sender, RoutedEventArgs e)
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

	private void UseGlobalVisor_Changed(object sender, RoutedEventArgs e)
	{
		if (!_initialized) return;
		bool useGlobal = UseGlobalVisorCheck.IsChecked == true;
		UseGlobal = useGlobal;
		if (useGlobal)
		{
			VisorEnabledCheck.IsChecked = _globalMaskEnabled;
			VisorSizeSlider.Value = _globalVisorSize;
			VisorCurveSlider.Value = Math.Clamp(1.0 - _globalMaskCorner, 0.0, 1.0);
			VisorApexYSlider.Value = _globalVisorOuterApexY;
			VisorInnerLowerSlider.Value = _globalVisorInnerLowerY;
			VisorInnerBridgeSlider.Value = _globalVisorInnerBridgeWidth;
			VisorInnerBridgeRiseSlider.Value = _globalVisorInnerBridgeRise;
			VisorInnerBridgePeakXSlider.Value = _globalVisorInnerBridgePeakX;
			VisorInnerBridgeSteepnessSlider.Value = _globalVisorInnerBridgeSteepness;
			VisorOffsetXSlider.Value = _globalOffsetX;
			VisorOffsetYSlider.Value = _globalOffsetY;
		}
		SetVisorSlidersEnabled(!useGlobal);
	}

	private void MaskBeanEditor_ShapeChanged(object sender, EventArgs e)
	{
		if (!_initialized || _syncingControls || UseGlobalVisorCheck.IsChecked == true)
		{
			return;
		}
		_syncingControls = true;
		VisorApexYSlider.Value = MaskBeanEditor.OuterApexY;
		VisorInnerLowerSlider.Value = MaskBeanEditor.InnerLowerY;
		VisorInnerBridgeSlider.Value = MaskBeanEditor.InnerBridgeWidth;
		VisorInnerBridgeRiseSlider.Value = MaskBeanEditor.InnerBridgeRise;
		VisorInnerBridgePeakXSlider.Value = MaskBeanEditor.InnerBridgePeakX;
		VisorInnerBridgeSteepnessSlider.Value = MaskBeanEditor.InnerBridgeSteepness;
		VisorSizeSlider.Value = MaskBeanEditor.Opening;
		ApplyMaskOpening(MaskBeanEditor.Opening);
		_syncingControls = false;
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
	}
}
