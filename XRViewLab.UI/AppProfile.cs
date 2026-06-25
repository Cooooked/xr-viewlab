using System.ComponentModel;

namespace XRViewLab.UI;

public sealed class AppProfile : INotifyPropertyChanged
{
	private bool _appEnabled = true;

	private bool _profileEnabled;

	private double _top = 0.2;

	private double _bottom = 0.2;

	private double _horizontal = 1.0;

	public string Key { get; init; } = "";

	public string DisplayName { get; init; } = "";

	public string Display { get; init; } = "";

	public string XrType { get; init; } = "";

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

	public string Summary
	{
		get
		{
			if (!ProfileEnabled)
			{
				return "Global";
			}
			return $"{Top:0.00};{Bottom:0.00};{Horizontal:0.00}";
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
