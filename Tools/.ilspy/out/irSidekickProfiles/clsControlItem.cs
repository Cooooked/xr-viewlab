#define TRACE
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using irSidekick;

namespace irSidekickProfiles;

public class clsControlItem
{
	public int SearchScore;

	public string Label { get; }

	public string SettingName
	{
		get
		{
			string[] array = Tag.Split('.');
			if (array.Length == 3)
			{
				return array[2];
			}
			TraceLog.Warn("tag " + Tag + " is incomplete");
			return Tag;
		}
	}

	public string Setting => Tag.Replace("0.", "App.").Replace("1.", "Core.").Replace("2.", "DX11.");

	public string Tag => Control.Tag as string;

	public Control Control { get; }

	public clsControlItem Clone()
	{
		return new clsControlItem(Label, Control);
	}

	private int Find(string[] words, string content)
	{
		int num = 0;
		foreach (string value in words)
		{
			if (content.Contains(value))
			{
				num++;
			}
		}
		return num;
	}

	public int Search(string[] words)
	{
		SearchScore = 0;
		if (!Label.IsNullOrEmpty())
		{
			SearchScore += Find(words, Label.ToLower()) * 2;
		}
		try
		{
			SearchScore += Find(words, SettingName.ToLower());
		}
		catch
		{
		}
		return SearchScore;
	}

	public clsControlItem(string label, Control control)
	{
		Control = control;
		Label = ((label == null) ? ("(" + SettingName + ")") : label);
	}

	public clsControlItem(GroupBox box, Control control)
	{
		Control = control;
		Label = box.Header as string;
	}

	public clsControlItem(Label label, Control control)
	{
		Control = control;
		Label = ((label == null) ? ("(" + SettingName + ")") : (label.Content as string));
	}

	public clsControlItem(RadioButton control)
	{
		Control = control;
		Label = control.Content as string;
	}

	public clsControlItem(CheckBox control)
	{
		Control = control;
		Label = control.Content as string;
	}

	public clsControlItem(XSlider control)
	{
		Control = control;
		Label = control.Label;
	}

	public clsControlItem(YSlider control)
	{
		Control = control;
		Label = control.Label;
	}

	public bool Highlight()
	{
		if (TabSwitch())
		{
			Wink();
			return true;
		}
		return false;
	}

	private bool TabSwitch()
	{
		Grid grid;
		if (Control.Parent is Grid)
		{
			grid = Control.Parent as Grid;
		}
		else
		{
			if (!(Control.Parent is StackPanel) || !((Control.Parent as StackPanel).Parent is Grid))
			{
				return false;
			}
			grid = (Control.Parent as StackPanel).Parent as Grid;
		}
		if (grid.Parent is GroupBox && (grid.Parent as GroupBox).Parent is Grid && ((grid.Parent as GroupBox).Parent as Grid).Parent is MetroTabItem)
		{
			(((grid.Parent as GroupBox).Parent as Grid).Parent as MetroTabItem).IsSelected = true;
			return true;
		}
		if (grid.Parent is MetroTabItem)
		{
			MetroTabItem metroTabItem = grid.Parent as MetroTabItem;
			if (metroTabItem.Parent is MetroTabControl && ((FrameworkElement)(object)(metroTabItem.Parent as MetroTabControl)).Parent is MetroTabItem)
			{
				(((FrameworkElement)(object)(metroTabItem.Parent as MetroTabControl)).Parent as MetroTabItem).IsSelected = true;
			}
			metroTabItem.IsSelected = true;
			return true;
		}
		return false;
	}

	private async void Wink()
	{
		switch (Control.GetType().ToString())
		{
		case "MahApps.Metro.Controls.NumericUpDown":
			await WinkThread(Control as NumericUpDown);
			break;
		case "System.Windows.Controls.RadioButton":
			await WinkThread(Control as RadioButton);
			break;
		case "System.Windows.Controls.ComboBox":
			await WinkThread(Control as ComboBox);
			break;
		case "System.Windows.Controls.CheckBox":
			await WinkThread(Control as CheckBox);
			break;
		case "irSidekickProfiles.XSlider":
			await WinkThread(Control as XSlider);
			break;
		case "irSidekickProfiles.YSlider":
			await WinkThread(Control as YSlider);
			break;
		case "System.Windows.Controls.Slider":
			await WinkThread(Control as Slider);
			break;
		case "System.Windows.Controls.TextBox":
			await WinkThread(Control as TextBox);
			break;
		case "MahApps.Metro.Controls.ColorPicker":
			await WinkThread(Control as ColorPicker);
			break;
		default:
			Console.WriteLine(Control.GetType().ToString());
			break;
		case "System.Windows.Controls.Label":
			break;
		}
	}

	private async Task WinkThread(Slider slider)
	{
		Visibility sliderVisibility = slider.Visibility;
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			SliderHelper.SetThumbFillBrush(slider, Brushes.Yellow);
			SliderHelper.SetTrackFillBrush(slider, Brushes.Yellow);
			SliderHelper.SetTrackValueFillBrush(slider, Brushes.Yellow);
		}, DispatcherPriority.Render);
		await Task.Delay(60);
		for (int count = 0; count < 4; count++)
		{
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				slider.Visibility = Visibility.Hidden;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				slider.Visibility = Visibility.Visible;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
		}
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			slider.Visibility = sliderVisibility;
		}, DispatcherPriority.Render);
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			SliderHelper.SetThumbFillBrush(slider, Brushes.DeepSkyBlue);
			SliderHelper.SetTrackFillBrush(slider, Brushes.DeepSkyBlue);
			SliderHelper.SetTrackValueFillBrush(slider, Brushes.DeepSkyBlue);
		}, DispatcherPriority.Render);
	}

	private async Task WinkThread(XSlider slider)
	{
		Visibility sliderVisibility = slider.Visibility;
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			SliderHelper.SetThumbFillBrush(slider.Slider, Brushes.Yellow);
			SliderHelper.SetTrackFillBrush(slider.Slider, Brushes.Yellow);
			SliderHelper.SetTrackValueFillBrush(slider.Slider, Brushes.Yellow);
		}, DispatcherPriority.Render);
		await Task.Delay(60);
		for (int count = 0; count < 4; count++)
		{
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				slider.Visibility = Visibility.Hidden;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				slider.Visibility = Visibility.Visible;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
		}
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			slider.Visibility = sliderVisibility;
		}, DispatcherPriority.Render);
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			SliderHelper.SetThumbFillBrush(slider.Slider, Brushes.DeepSkyBlue);
			SliderHelper.SetTrackFillBrush(slider.Slider, Brushes.DeepSkyBlue);
			SliderHelper.SetTrackValueFillBrush(slider.Slider, Brushes.DeepSkyBlue);
		}, DispatcherPriority.Render);
	}

	private async Task WinkThread(YSlider slider)
	{
		Visibility sliderVisibility = slider.Visibility;
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			SliderHelper.SetThumbFillBrush(slider.Slider, Brushes.Yellow);
			SliderHelper.SetTrackFillBrush(slider.Slider, Brushes.Yellow);
			SliderHelper.SetTrackValueFillBrush(slider.Slider, Brushes.Yellow);
		}, DispatcherPriority.Render);
		await Task.Delay(60);
		for (int count = 0; count < 4; count++)
		{
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				slider.Visibility = Visibility.Hidden;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				slider.Visibility = Visibility.Visible;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
		}
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			slider.Visibility = sliderVisibility;
		}, DispatcherPriority.Render);
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			SliderHelper.SetThumbFillBrush(slider.Slider, Brushes.DeepSkyBlue);
			SliderHelper.SetTrackFillBrush(slider.Slider, Brushes.DeepSkyBlue);
			SliderHelper.SetTrackValueFillBrush(slider.Slider, Brushes.DeepSkyBlue);
		}, DispatcherPriority.Render);
	}

	private async Task WinkThread(TextBox txt)
	{
		Visibility txtVisibility = txt.Visibility;
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			TextBox textBox = txt;
			Brush foreground = (txt.BorderBrush = Brushes.Yellow);
			textBox.Foreground = foreground;
		}, DispatcherPriority.Render);
		await Task.Delay(60);
		for (int count = 0; count < 4; count++)
		{
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				txt.Visibility = Visibility.Hidden;
			}, DispatcherPriority.Render);
			await Task.Delay(300);
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				txt.Visibility = Visibility.Visible;
			}, DispatcherPriority.Render);
			await Task.Delay(300);
		}
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			txt.Visibility = txtVisibility;
		}, DispatcherPriority.Render);
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			TextBox textBox = txt;
			Brush foreground = (txt.BorderBrush = Brushes.White);
			textBox.Foreground = foreground;
		}, DispatcherPriority.Render);
	}

	private async Task WinkThread(CheckBox cb)
	{
		Visibility cbVisibility = cb.Visibility;
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			CheckBoxHelper.SetForegroundChecked(cb, Brushes.Yellow);
			CheckBoxHelper.SetForegroundUnchecked(cb, Brushes.Yellow);
			CheckBoxHelper.SetCheckGlyphForegroundChecked(cb, Brushes.Yellow);
		}, DispatcherPriority.Render);
		await Task.Delay(60);
		for (int count = 0; count < 4; count++)
		{
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				cb.Visibility = Visibility.Hidden;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				cb.Visibility = Visibility.Visible;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
		}
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			cb.Visibility = cbVisibility;
		}, DispatcherPriority.Render);
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			CheckBoxHelper.SetForegroundChecked(cb, Brushes.White);
			CheckBoxHelper.SetForegroundUnchecked(cb, Brushes.White);
			CheckBoxHelper.SetCheckGlyphForegroundChecked(cb, Brushes.DeepSkyBlue);
		}, DispatcherPriority.Render);
	}

	private async Task WinkThread(ComboBox cbo)
	{
		Visibility cboVisibility = cbo.Visibility;
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			ComboBox comboBox = cbo;
			Brush foreground = (cbo.BorderBrush = Brushes.Yellow);
			comboBox.Foreground = foreground;
		}, DispatcherPriority.Render);
		await Task.Delay(60);
		for (int count = 0; count < 4; count++)
		{
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				cbo.Visibility = Visibility.Hidden;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				cbo.Visibility = Visibility.Visible;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
		}
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			cbo.Visibility = cboVisibility;
		}, DispatcherPriority.Render);
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			ComboBox comboBox = cbo;
			Brush foreground = (cbo.BorderBrush = Brushes.White);
			comboBox.Foreground = foreground;
		}, DispatcherPriority.Render);
	}

	private async Task WinkThread(RadioButton rb)
	{
		Visibility rbVisibility = rb.Visibility;
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			RadioButtonHelper.SetCheckGlyphFill(rb, Brushes.Yellow);
			rb.Foreground = Brushes.Yellow;
		}, DispatcherPriority.Render);
		await Task.Delay(60);
		for (int count = 0; count < 4; count++)
		{
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				rb.Visibility = Visibility.Hidden;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				rb.Visibility = Visibility.Visible;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
		}
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			rb.Visibility = rbVisibility;
		}, DispatcherPriority.Render);
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			RadioButtonHelper.SetCheckGlyphFill(rb, Brushes.DeepSkyBlue);
			rb.Foreground = Brushes.White;
		}, DispatcherPriority.Render);
	}

	private async Task WinkThread(ColorPicker picker)
	{
		Visibility pickerVisibility = picker.Visibility;
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			picker.BorderBrush = Brushes.Yellow;
		}, DispatcherPriority.Render);
		await Task.Delay(60);
		for (int count = 0; count < 4; count++)
		{
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				picker.Visibility = Visibility.Hidden;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				picker.Visibility = Visibility.Visible;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
		}
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			picker.Visibility = pickerVisibility;
		}, DispatcherPriority.Render);
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			picker.BorderBrush = Brushes.White;
		}, DispatcherPriority.Render);
	}

	private async Task WinkThread(NumericUpDown num)
	{
		Visibility numVisibility = num.Visibility;
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			NumericUpDown numericUpDown = num;
			Brush foreground = (num.BorderBrush = Brushes.Yellow);
			numericUpDown.Foreground = foreground;
		}, DispatcherPriority.Render);
		await Task.Delay(60);
		for (int count = 0; count < 4; count++)
		{
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				num.Visibility = Visibility.Hidden;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
			await Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				num.Visibility = Visibility.Visible;
			}, DispatcherPriority.Render);
			await Task.Delay(400);
		}
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			num.Visibility = numVisibility;
		}, DispatcherPriority.Render);
		await Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			NumericUpDown numericUpDown = num;
			Brush foreground = (num.BorderBrush = Brushes.White);
			numericUpDown.Foreground = foreground;
		}, DispatcherPriority.Render);
	}
}
