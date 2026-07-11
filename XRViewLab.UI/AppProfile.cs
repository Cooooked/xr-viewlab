using System.ComponentModel;

namespace XRViewLab.UI;

public sealed class AppProfile : INotifyPropertyChanged
{
	private bool _appEnabled = true;

	private bool _profileEnabled;

	private bool _hidden;

	private double _top = 0.2;

	private double _bottom = 0.2;

	private double _horizontal = 1.0;

	private double _renderScale = 1.0;

	private bool _maskEnabled;

	private double _maskVertical = 1.0;

	private double _maskHorizontal = 1.0;

	private bool _maskRounded;

	private double _maskCorner = 1.0;

	private double _maskTopBias;

	private double _maskBottomBias;

	private double _maskLeftBias;

	private double _maskRightBias;

	private double _maskTopCurve;

	private double _maskBottomCurve;

	private string _displayName = "";

	private string _display = "";

	private string _hookStatus = "";

	public string Key { get; init; } = "";

	// The executable file name (e.g. "dirtrally2.exe"), used to recompute Display on rename.
	public string ExeName { get; init; } = "";

	public string DisplayName
	{
		get => _displayName;
		set
		{
			if (_displayName != value)
			{
				_displayName = value;
				OnPropertyChanged("DisplayName");
			}
		}
	}

	public string Display
	{
		get => _display;
		set
		{
			if (_display != value)
			{
				_display = value;
				OnPropertyChanged("Display");
			}
		}
	}

	public string XrType { get; init; } = "";

	public string HookStatus
	{
		get => _hookStatus;
		set
		{
			if (_hookStatus != value)
			{
				_hookStatus = value;
				OnPropertyChanged("HookStatus");
			}
		}
	}

	public bool AppEnabled
	{
		get
		{
			return _appEnabled;
		}
		set
		{
			if (_appEnabled != value)
			{
				_appEnabled = value;
				OnPropertyChanged("AppEnabled");
			}
		}
	}

	public bool ProfileEnabled
	{
		get
		{
			return _profileEnabled;
		}
		set
		{
			if (_profileEnabled != value)
			{
				_profileEnabled = value;
				RefreshSummary();
			}
		}
	}

	public double Top
	{
		get
		{
			return _top;
		}
		set
		{
			_top = value;
			RefreshSummary();
		}
	}

	public double Bottom
	{
		get
		{
			return _bottom;
		}
		set
		{
			_bottom = value;
			RefreshSummary();
		}
	}

	public double Horizontal
	{
		get
		{
			return _horizontal;
		}
		set
		{
			_horizontal = value;
			RefreshSummary();
		}
	}

	public bool Hidden
	{
		get
		{
			return _hidden;
		}
		set
		{
			if (_hidden != value)
			{
				_hidden = value;
				OnPropertyChanged("Hidden");
			}
		}
	}

	public double RenderScale
	{
		get
		{
			return _renderScale;
		}
		set
		{
			_renderScale = value;
			RefreshSummary();
		}
	}

	public bool MaskEnabled
	{
		get
		{
			return _maskEnabled;
		}
		set
		{
			_maskEnabled = value;
			RefreshSummary();
		}
	}

	public double MaskVertical
	{
		get
		{
			return _maskVertical;
		}
		set
		{
			_maskVertical = value;
			RefreshSummary();
		}
	}

	public double MaskHorizontal
	{
		get
		{
			return _maskHorizontal;
		}
		set
		{
			_maskHorizontal = value;
			RefreshSummary();
		}
	}

	public bool MaskRounded
	{
		get
		{
			return _maskRounded;
		}
		set
		{
			_maskRounded = value;
			RefreshSummary();
		}
	}

	public double MaskCorner
	{
		get
		{
			return _maskCorner;
		}
		set
		{
			_maskCorner = value;
			RefreshSummary();
		}
	}

	public double MaskTopBias
	{
		get
		{
			return _maskTopBias;
		}
		set
		{
			_maskTopBias = value;
			RefreshSummary();
		}
	}

	public double MaskBottomBias
	{
		get
		{
			return _maskBottomBias;
		}
		set
		{
			_maskBottomBias = value;
			RefreshSummary();
		}
	}

	public double MaskLeftBias
	{
		get => _maskLeftBias;
		set
		{
			_maskLeftBias = value;
			RefreshSummary();
		}
	}

	public double MaskRightBias
	{
		get => _maskRightBias;
		set
		{
			_maskRightBias = value;
			RefreshSummary();
		}
	}

	public double MaskTopCurve
	{
		get => _maskTopCurve;
		set
		{
			_maskTopCurve = value;
			RefreshSummary();
		}
	}

	public double MaskBottomCurve
	{
		get => _maskBottomCurve;
		set
		{
			_maskBottomCurve = value;
			RefreshSummary();
		}
	}

	// 0 = use global (no per-app override stored)
	public double VisorSize { get; set; }
	public double VisorWidth { get; set; }
	public double VisorHeight { get; set; }
	public double VisorOuterApexY { get; set; }
	public double VisorInnerLowerY { get; set; }
	public double VisorInnerBridgeWidth { get; set; }
	public double VisorInnerBridgeRise { get; set; }
	public double VisorInnerBridgePeakX { get; set; }
	public double VisorInnerBridgeSteepness { get; set; }

	public string Summary
	{
		get
		{
			if (!ProfileEnabled)
			{
				return "Global";
			}
			return $"{Top:0.###};{Bottom:0.###};{Horizontal:0.###};{RenderScale * 100.0:0.####}%";
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public void RefreshSummary()
	{
		OnPropertyChanged("ProfileEnabled");
		OnPropertyChanged("Summary");
	}

	private void OnPropertyChanged(string propertyName)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
