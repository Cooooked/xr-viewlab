#define TRACE
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MahApps.Metro.Controls;
using irSidekick;

namespace irSidekickProfiles;

public class clsRainbow : FrameworkElement
{
	private winMain winMain;

	private bool isDesignMode;

	private List<Brush> bowList = new List<Brush>();

	private List<string> tagList = new List<string>();

	public clsRainbow()
	{
		isDesignMode = !(Application.Current is App);
	}

	public void InitBasic(winMain main, Control ctrl, GroupBox box)
	{
		InitInternal(main, ctrl);
		clsControlGlobal.Add(box, ctrl);
	}

	public void InitBasic(winMain main, Control ctrl, Label lbl = null)
	{
		InitInternal(main, ctrl);
		clsControlGlobal.Add(lbl, ctrl);
	}

	private void InitInternal(winMain main, Control ctrl)
	{
		winMain = main;
		tagAdd(ctrl.GetTag());
		ctrl.MouseEnter += Control_MouseEnter;
		ctrl.MouseLeave += Control_MouseLeave;
	}

	public void InitControl(winMain main, Control ctrl, GroupBox box)
	{
		InitControl(main, ctrl, box.Header as string);
	}

	public void InitControl(winMain main, Control ctrl, Label lbl)
	{
		string lbl2 = ((lbl == null) ? null : (lbl.Content as string));
		InitControl(main, ctrl, lbl2);
	}

	public void InitControl(winMain main, Control ctrl, string lbl = null)
	{
		InitInternal(main, ctrl);
		switch (ctrl.GetType().ToString())
		{
		case "MahApps.Metro.Controls.NumericUpDown":
		{
			NumericUpDown numericUpDown = ctrl as NumericUpDown;
			numericUpDown.ValueChanged += Number_Changed;
			clsControlGlobal.Add(lbl, numericUpDown);
			break;
		}
		case "System.Windows.Controls.RadioButton":
		{
			RadioButton obj4 = ctrl as RadioButton;
			obj4.Click += RadioButton_Click;
			clsControlGlobal.Add(obj4);
			break;
		}
		case "System.Windows.Controls.ComboBox":
			(ctrl as ComboBox).SelectionChanged += ComboValue_Changed;
			clsControlGlobal.Add(lbl, ctrl);
			break;
		case "System.Windows.Controls.CheckBox":
		{
			CheckBox obj3 = ctrl as CheckBox;
			obj3.Click += CheckBox_Click;
			clsControlGlobal.Add(obj3);
			break;
		}
		case "irSidekickProfiles.XSlider":
		{
			XSlider obj2 = ctrl as XSlider;
			obj2.ValueChanged += Slider_Changed;
			clsControlGlobal.Add(obj2);
			break;
		}
		case "irSidekickProfiles.YSlider":
		{
			YSlider obj = ctrl as YSlider;
			obj.ValueChanged += Slider_Changed;
			clsControlGlobal.Add(obj);
			break;
		}
		case "System.Windows.Controls.Slider":
		{
			Slider slider = ctrl as Slider;
			slider.ValueChanged += Slider_Changed;
			clsControlGlobal.Add(lbl, slider);
			break;
		}
		case "System.Windows.Controls.TextBox":
		{
			TextBox textBox = ctrl as TextBox;
			textBox.LostFocus += TextBox_LostFocus;
			clsControlGlobal.Add(lbl, textBox);
			break;
		}
		case "MahApps.Metro.Controls.ColorPicker":
		{
			ColorPicker colorPicker = ctrl as ColorPicker;
			colorPicker.DropDownClosed += Color_DropDownClosed;
			colorPicker.PreviewKeyDown += Color_PreviewKeyDown;
			clsControlGlobal.Add(lbl, colorPicker);
			tagCompare();
			break;
		}
		default:
			Console.WriteLine(ctrl.GetType().ToString());
			break;
		case "System.Windows.Controls.Label":
			break;
		}
	}

	public void InitNumber(winMain main, NumericUpDown num, Label lbl = null)
	{
		InitInternal(main, num);
		num.ValueChanged += Number_Changed;
		clsControlGlobal.Add(lbl, num);
	}

	public void InitDecimal(winMain main, NumericUpDown num, Label lbl = null)
	{
		InitInternal(main, num);
		num.ValueChanged += Decimal_Changed;
		clsControlGlobal.Add(lbl, num);
	}

	public void InitComboIndex(winMain main, ComboBox cbo, Label lbl = null)
	{
		InitInternal(main, cbo);
		cbo.SelectionChanged += ComboIndex_Changed;
		clsControlGlobal.Add(lbl, cbo);
	}

	public void InitComboValue(winMain main, ComboBox cbo, Label lbl = null)
	{
		InitInternal(main, cbo);
		cbo.SelectionChanged += ComboValue_Changed;
		clsControlGlobal.Add(lbl, cbo);
	}

	public void tagAdd(string tag)
	{
		if (tag != null && tag.Length != 0 && !tagList.Contains(tag))
		{
			tagList.Add(tag);
		}
	}

	public void tagCompare()
	{
		if (winMain == null || winMain.IsClosing || winMain.ProfileView == null)
		{
			return;
		}
		if (tagList.Count == 1)
		{
			bowList = winMain.ProfileList.Compare(winMain.ProfileView.Name, tagList[0]);
		}
		else
		{
			bowList.Clear();
			foreach (string tag in tagList)
			{
				addBows(winMain.ProfileList.Compare(winMain.ProfileView.Name, tag));
			}
		}
		InvalidateVisual();
	}

	public bool bowEmpty()
	{
		return bowList.Count == 0;
	}

	private bool bowExists(Brush brush)
	{
		for (int i = 0; i < bowList.Count; i++)
		{
			if (bowList[i].ToString() == brush.ToString())
			{
				return true;
			}
		}
		return false;
	}

	private void addBows(List<Brush> list)
	{
		for (int i = 0; i < list.Count; i++)
		{
			if (!bowExists(list[i]))
			{
				bowList.Add(list[i]);
			}
		}
	}

	public void setBows(List<Brush> list, bool add)
	{
		if (add)
		{
			addBows(list);
		}
		else
		{
			bowList = list;
		}
		InvalidateVisual();
	}

	private void DesignRender(DrawingContext dc)
	{
		dc.DrawRectangle(rectangle: new Rect(0.0, 0.0, base.ActualWidth, base.ActualHeight), brush: Brushes.Crimson, pen: null);
	}

	protected override void OnRender(DrawingContext dc)
	{
		if (isDesignMode)
		{
			DesignRender(dc);
		}
		else if (bowList.Count != 0)
		{
			double actualHeight = base.ActualHeight;
			double num = base.ActualWidth / (double)bowList.Count;
			for (int i = 0; i < bowList.Count; i++)
			{
				dc.DrawRectangle(rectangle: new Rect((double)i * num, 0.0, num, actualHeight), brush: bowList[i], pen: null);
			}
			dc.DrawRectangle(rectangle: new Rect(0.0, 0.0, base.ActualWidth, base.ActualHeight), brush: null, pen: new Pen(Brushes.LightSlateGray, 2.0));
		}
	}

	private void Control_MouseEnter(object sender, MouseEventArgs e)
	{
		if (!bowEmpty() && winMain != null)
		{
			winMain.ProfileList.ValueLabelsShow(sender.GetTag());
		}
	}

	private void Control_MouseLeave(object sender, MouseEventArgs e)
	{
		if (winMain != null)
		{
			winMain.ProfileList.ValueLabelsClear();
		}
	}

	public void Decimal_Changed(object sender, RoutedEventArgs e)
	{
		if (winMain == null || winMain.ProfileView == null)
		{
			return;
		}
		if (winMain.DataLoading)
		{
			tagCompare();
			return;
		}
		NumericUpDown numericUpDown = sender as NumericUpDown;
		double num = (numericUpDown.Value.HasValue ? numericUpDown.Value.Value : 0.0);
		if (winMain.ProfileView.tagSetValue(numericUpDown.GetTag(), num.ToString(numericUpDown.StringFormat.Replace("N", "F"), CultureInfo.InvariantCulture)))
		{
			tagCompare();
		}
	}

	public void Number_Changed(object sender, RoutedEventArgs e)
	{
		if (winMain == null || winMain.ProfileView == null)
		{
			return;
		}
		if (winMain.DataLoading)
		{
			tagCompare();
			return;
		}
		NumericUpDown numericUpDown = sender as NumericUpDown;
		double num = (numericUpDown.Value.HasValue ? numericUpDown.Value.Value : 0.0);
		if (winMain.ProfileView.tagSetValue(numericUpDown.GetTag(), num.ToString(numericUpDown.StringFormat.Replace("N", "F"), CultureInfo.InvariantCulture)))
		{
			tagCompare();
		}
	}

	public void TextBox_LostFocus(object sender, RoutedEventArgs e)
	{
		if (winMain != null && winMain.ProfileView != null)
		{
			TextBox textBox = sender as TextBox;
			if (winMain.ProfileView.tagSetValue(textBox.GetTag(), textBox.Text))
			{
				tagCompare();
			}
		}
	}

	private void Color_DropDownClosed(object sender, EventArgs e)
	{
		if (winMain != null && winMain.ProfileView != null)
		{
			ColorPicker colorPicker = sender as ColorPicker;
			if (winMain.ProfileView.tagSetValue(colorPicker.GetTag(), colorPicker.SelectedColor.ToString().Replace("#FF", "#")))
			{
				tagCompare();
			}
		}
	}

	private void Color_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			(sender as ColorPicker).IsDropDownOpen = false;
		}
	}

	public void Color_Changed(object sender, RoutedEventArgs e)
	{
		if (winMain == null || winMain.ProfileView == null)
		{
			return;
		}
		if (winMain.DataLoading)
		{
			tagCompare();
			return;
		}
		ColorPicker colorPicker = sender as ColorPicker;
		if (winMain.ProfileView.tagSetValue(colorPicker.GetTag(), colorPicker.SelectedColor.ToString().Replace("#FF", "#")))
		{
			tagCompare();
		}
	}

	public void Slider_Changed(object sender, RoutedEventArgs e)
	{
		if (winMain == null || winMain.ProfileView == null)
		{
			return;
		}
		if (winMain.DataLoading)
		{
			tagCompare();
			return;
		}
		string tag = (sender as Control).GetTag();
		double value = (sender as Slider).Value;
		iniSetting iniSetting2 = iniSetting.Find(tag);
		if (iniSetting2 == null)
		{
			TraceLog.Warn("Slider_Changed can't find ini setting definition for " + tag);
		}
		else if (iniSetting2.ValueIsDouble)
		{
			if (winMain.ProfileView.tagSetValue(tag, value.ToString4Storage()))
			{
				tagCompare();
			}
		}
		else if (winMain.ProfileView.tagSetValue(tag, value.ToIntStr()))
		{
			tagCompare();
		}
	}

	public void CheckBox_Click(object sender, RoutedEventArgs e)
	{
		if (winMain == null || winMain.ProfileView == null)
		{
			return;
		}
		if (winMain.DataLoading)
		{
			tagCompare();
			return;
		}
		CheckBox checkBox = sender as CheckBox;
		if (winMain.ProfileView.tagSetValue(checkBox.GetTag(), checkBox.IsChecked.ToText()))
		{
			tagCompare();
		}
	}

	public void RadioButton_Click(object sender, RoutedEventArgs e)
	{
		if (winMain == null || winMain.ProfileView == null)
		{
			return;
		}
		if (winMain.DataLoading)
		{
			tagCompare();
			return;
		}
		RadioButton radioButton = sender as RadioButton;
		if (winMain.ProfileView.tagSetValue(radioButton.GetTag(), radioButton.IsChecked.ToText()))
		{
			tagCompare();
		}
	}

	public void ComboIndex_Changed(object sender, RoutedEventArgs e)
	{
		if (winMain == null || winMain.ProfileView == null)
		{
			return;
		}
		if (winMain.DataLoading)
		{
			tagCompare();
			return;
		}
		ComboBox obj = sender as ComboBox;
		string tag = obj.GetTag();
		double num = obj.SelectedIndex;
		if (iniApp.blackBox.TagID.Equals(tag) || iniApp.blackBoxPitStop.TagID.Equals(tag) || iniDX11.Graphic_HeadlightLevel.TagID.Equals(tag))
		{
			num -= 1.0;
		}
		if (iniApp.dimensions.TagID.Equals(tag))
		{
			num += 1.0;
		}
		if (winMain.ProfileView.tagSetValue(tag, num.ToIntStr()))
		{
			tagCompare();
		}
	}

	public void ComboValue_Changed(object sender, RoutedEventArgs e)
	{
		if (winMain == null || winMain.ProfileView == null)
		{
			return;
		}
		if (winMain.DataLoading)
		{
			tagCompare();
			return;
		}
		ComboBox comboBox = sender as ComboBox;
		if (comboBox.SelectedValue != null && winMain.ProfileView.tagSetValue(comboBox.GetTag(), comboBox.SelectedValue.ToString()))
		{
			tagCompare();
		}
	}
}
