using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace irSidekickProfiles;

public class ctlUIOffset : UserControl, IComponentConnector
{
	public static readonly DependencyProperty tagValueProperty = DependencyProperty.Register("tagValue", typeof(string), typeof(ctlUIOffset), new UIPropertyMetadata(string.Empty, tagValueChangedCallback));

	public static readonly DependencyProperty tagStringProperty = DependencyProperty.Register("tagString", typeof(string), typeof(ctlUIOffset), new UIPropertyMetadata(string.Empty, tagStringChangedCallback));

	private static winMain _winMain = null;

	private bool bowInitialized;

	internal clsRainbow bowOffsetValue;

	internal Label lblOffsetValue;

	private bool _contentLoaded;

	private winMain winMain
	{
		get
		{
			if (_winMain != null)
			{
				return _winMain;
			}
			Visual visual = this;
			while (_winMain == null)
			{
				visual = (Visual)VisualTreeHelper.GetParent(visual);
				if (visual == null)
				{
					return null;
				}
				if (visual is winMain)
				{
					_winMain = visual as winMain;
					return _winMain;
				}
			}
			return null;
		}
	}

	public string tagValue
	{
		get
		{
			return (string)GetValue(tagValueProperty);
		}
		set
		{
			SetValue(tagValueProperty, value);
		}
	}

	public string tagString
	{
		get
		{
			return (string)GetValue(tagStringProperty);
		}
		set
		{
			SetValue(tagStringProperty, value);
		}
	}

	public ctlUIOffset()
	{
		InitializeComponent();
	}

	private static void tagValueChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as ctlUIOffset).lblOffsetValue.Content = e.NewValue as string;
	}

	private static void tagStringChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ctlUIOffset ctlUIOffset2 = d as ctlUIOffset;
		ctlUIOffset2.lblOffsetValue.Tag = e.NewValue as string;
		if (!ctlUIOffset2.bowInitialized)
		{
			ctlUIOffset2.bowOffsetValue.InitBasic(ctlUIOffset2.winMain, ctlUIOffset2.lblOffsetValue);
			ctlUIOffset2.bowInitialized = true;
		}
		ctlUIOffset2.bowOffsetValue.tagCompare();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/irSidekickProfiles;component/ctluioffset.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			bowOffsetValue = (clsRainbow)target;
			break;
		case 2:
			lblOffsetValue = (Label)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
