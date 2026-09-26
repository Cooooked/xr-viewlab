using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace irSidekickProfiles;

public class XSlider : UserControl, IComponentConnector
{
	private string _Unit;

	private string _Format = "N1";

	internal Grid controlGrid;

	internal Label lblLabel;

	internal Label lblValue;

	internal Slider slideValue;

	private bool _contentLoaded;

	public string Label
	{
		get
		{
			return lblLabel.Content as string;
		}
		set
		{
			lblLabel.Content = value;
		}
	}

	public Slider Slider => slideValue;

	public double WidthLabel
	{
		get
		{
			return lblLabel.Width;
		}
		set
		{
			lblLabel.Width = value;
		}
	}

	public double WidthValue
	{
		get
		{
			return lblValue.Width;
		}
		set
		{
			lblValue.Width = value;
		}
	}

	public double WidthSlider
	{
		get
		{
			return slideValue.Width;
		}
		set
		{
			slideValue.Width = value;
		}
	}

	public string ValueUnit
	{
		get
		{
			return _Unit;
		}
		set
		{
			_Unit = value;
			slideValue_ValueChanged(null, null);
		}
	}

	public string ValueFormat
	{
		get
		{
			return _Format;
		}
		set
		{
			_Format = value;
			slideValue_ValueChanged(null, null);
		}
	}

	public double Value
	{
		get
		{
			return Math.Round(slideValue.Value, 6);
		}
		set
		{
			slideValue.Value = value;
		}
	}

	public double Minimum
	{
		get
		{
			return slideValue.Minimum;
		}
		set
		{
			slideValue.Minimum = value;
		}
	}

	public double Maximum
	{
		get
		{
			return slideValue.Maximum;
		}
		set
		{
			slideValue.Maximum = value;
		}
	}

	public double SmallChange
	{
		get
		{
			return slideValue.SmallChange;
		}
		set
		{
			slideValue.SmallChange = value;
		}
	}

	public double LargeChange
	{
		get
		{
			return slideValue.LargeChange;
		}
		set
		{
			slideValue.LargeChange = value;
		}
	}

	public double TickFrequency
	{
		get
		{
			return slideValue.TickFrequency;
		}
		set
		{
			slideValue.TickFrequency = value;
		}
	}

	public event RoutedPropertyChangedEventHandler<double> ValueChanged;

	private void UserControl_Loaded(object sender, RoutedEventArgs e)
	{
		slideValue.Tag = base.Tag;
	}

	public XSlider()
	{
		InitializeComponent();
	}

	private void slideValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		lblValue.Content = slideValue.Value.ToString(_Format) + _Unit;
		if (sender != null && this.ValueChanged != null)
		{
			this.ValueChanged(sender, e);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/irSidekickProfiles;component/xslider.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			((XSlider)target).Loaded += UserControl_Loaded;
			break;
		case 2:
			controlGrid = (Grid)target;
			break;
		case 3:
			lblLabel = (Label)target;
			break;
		case 4:
			lblValue = (Label)target;
			break;
		case 5:
			slideValue = (Slider)target;
			slideValue.ValueChanged += slideValue_ValueChanged;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
