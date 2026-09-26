using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using irSidekick;
using irSidekick.Monitor;

namespace irSidekickProfiles;

public class clsMonitorList : List<clsMonitorItem>
{
	public int SelectedCount()
	{
		int num = 0;
		for (int i = 0; i < base.Count; i++)
		{
			if (base[i].Selected)
			{
				num++;
			}
		}
		return num;
	}

	public List<clsMonitorItem> SelectedMonitors()
	{
		List<clsMonitorItem> list = new List<clsMonitorItem>();
		for (int i = 0; i < base.Count; i++)
		{
			if (base[i].Selected)
			{
				list.Add(base[i]);
			}
		}
		list.Sort();
		return list;
	}

	public clsMonitorItem ReferenceMonitor()
	{
		List<clsMonitorItem> list = SelectedMonitors();
		clsMonitorItem clsMonitorItem2 = null;
		for (int i = 0; i < list.Count; i++)
		{
			if (clsMonitorItem2 == null && list[i].IsPrimary)
			{
				clsMonitorItem2 = list[i];
				break;
			}
		}
		if (clsMonitorItem2 == null && list.Count == 3)
		{
			clsMonitorItem2 = list[1];
		}
		if (clsMonitorItem2 == null && list.Count > 0)
		{
			clsMonitorItem2 = list[0];
		}
		return clsMonitorItem2;
	}

	public void SetVisible(Visibility visibility)
	{
		using Enumerator enumerator = GetEnumerator();
		while (enumerator.MoveNext())
		{
			enumerator.Current.Visibility = visibility;
		}
	}

	public void SetSelected(Rect irBounds)
	{
		for (int i = 0; i < base.Count; i++)
		{
			base[i].Visibility = Visibility.Visible;
			base[i].SetSelected(irBounds);
		}
	}

	public bool IsSequentialRow()
	{
		List<clsMonitorItem> list = SelectedMonitors();
		switch (list.Count)
		{
		case 0:
			return false;
		case 1:
			return true;
		default:
		{
			for (int i = 1; i < list.Count; i++)
			{
				if (list[0].NotSameRow(list[i]))
				{
					return false;
				}
			}
			return true;
		}
		}
	}

	public bool SelectedAlignmentIsOk()
	{
		List<clsMonitorItem> list = SelectedMonitors();
		if (list.Count <= 1)
		{
			return true;
		}
		clsMonitorItem clsMonitorItem2 = list[0];
		for (int i = 1; i < list.Count; i++)
		{
			clsMonitorItem clsMonitorItem3 = list[i];
			if (!IsEqual(clsMonitorItem3.PixelBounds.Left, clsMonitorItem2.PixelBounds.Right))
			{
				return false;
			}
			if (!IsEqual(clsMonitorItem3.PixelBounds.Top, clsMonitorItem2.PixelBounds.Top) && !IsEqual(clsMonitorItem3.PixelBounds.Bottom, clsMonitorItem2.PixelBounds.Bottom))
			{
				return false;
			}
			clsMonitorItem2 = clsMonitorItem3;
		}
		return true;
		static bool IsEqual(double x, double y)
		{
			return (int)x == (int)y;
		}
	}

	public bool SetProfile(appProfile profile, bool MismatchedMonitorKludge, bool GSyncHack)
	{
		bool flag = false;
		int num = SelectedCount();
		if (num != 1 && num != 3)
		{
			return false;
		}
		if (!IsSequentialRow())
		{
			return false;
		}
		double num2 = 18000.0;
		double num3 = -18000.0;
		double num4 = 0.0;
		double num5 = 10000.0;
		if (num == 3 && MismatchedMonitorKludge)
		{
			for (int i = 0; i < base.Count; i++)
			{
				if (!(base[i].Selected & base[i].IsPrimary))
				{
					continue;
				}
				num3 = base[i].PixelBounds.Y;
				num2 = base[i].PixelBounds.Width;
				num5 = base[i].PixelBounds.Height;
				num4 = base[i].PixelBounds.Width;
				SetFullScreen(base[i]);
				flag = true;
				num2 *= -1.0;
				num4 *= 3.0;
				for (int j = 0; j < base.Count; j++)
				{
					if (base[j].Selected && num5 > base[j].PixelBounds.Height)
					{
						num3 = base[j].PixelBounds.Y;
						num5 = base[j].PixelBounds.Height;
					}
				}
				break;
			}
		}
		if (!flag)
		{
			for (int k = 0; k < base.Count; k++)
			{
				if (base[k].Selected)
				{
					num4 += base[k].PixelBounds.Width;
					num2 = Math.Min(num2, base[k].PixelBounds.X);
					num3 = Math.Max(num3, base[k].PixelBounds.Y);
					num5 = Math.Min(num5, base[k].PixelBounds.Height);
					if (num == 1)
					{
						SetFullScreen(base[k]);
					}
				}
				if (base[k].IsPrimary && num > 1)
				{
					SetFullScreen(base[k]);
				}
			}
		}
		profile.tagSetValue(iniDX11.windowedMaximized.TagID, "0");
		if (num == 1 && num4 > 3840.0)
		{
			num = 3;
		}
		profile.tagSetValue(iniDX11.NumMonitors.TagID, num.ToIntStr());
		bool flag2 = profile.tagGetValue(iniDX11.MonitorType.TagID).Equals("1");
		if (num == 3 && GSyncHack)
		{
			num5 -= 1.0;
		}
		profile.tagSetValue(iniDX11.windowedXPos.TagID, num2.ToIntStr());
		profile.tagSetValue(iniDX11.windowedYPos.TagID, num3.ToIntStr());
		profile.tagSetValue(iniDX11.windowedWidth.TagID, num4.ToIntStr());
		profile.tagSetValue(iniDX11.windowedHeight.TagID, num5.ToIntStr());
		if (num == 3 || flag2)
		{
			profile.tagSetValue(iniDX11.fullScreen.TagID, "0");
			profile.tagSetValue(iniDX11.windowedAlignment.TagID, "0");
			profile.tagSetValue(iniDX11.RenderViewPerMonitor.TagID, "1");
			profile.tagSetValue(iniDX11.EnableSMPSurround.TagID, "1");
		}
		else
		{
			profile.tagSetValue(iniDX11.RenderViewPerMonitor.TagID, "0");
			profile.tagSetValue(iniDX11.EnableSMPSurround.TagID, "0");
		}
		try
		{
			profile.tagSetValue(iniDX11.AutoCfgCompleted.TagID, "1");
		}
		catch
		{
		}
		profile.tagSetValue(iniApp.browserWindowedXPos.TagID, "0");
		profile.tagSetValue(iniApp.browserWindowedYPos.TagID, "0");
		profile.tagSetValue(iniApp.browserWindowedWidth.TagID, "1920");
		profile.tagSetValue(iniApp.browserWindowedHeight.TagID, "1080");
		return true;
		void SetFullScreen(clsMonitorItem monitor)
		{
			if (monitor != null)
			{
				profile.tagSetValue(iniDX11.deviceIdx.TagID, monitor.MonitorNum.FormatUSInt());
				_ = monitor.PixelBounds;
				profile.tagSetValue(iniDX11.fullScreenWidth.TagID, monitor.PixelBounds.Width.ToIntStr());
				profile.tagSetValue(iniDX11.fullScreenHeight.TagID, monitor.PixelBounds.Height.ToIntStr());
				if (monitor.RefreshRate >= 60)
				{
					profile.tagSetValue(iniDX11.RefreshRate.TagID, monitor.RefreshRate.ToIntStr());
					profile.tagSetValue(iniDX11.Graphic_DesiredFPSLimit.TagID, (monitor.RefreshRate - 4).ToIntStr());
					profile.tagSetValue(iniDX11.Graphic_LODMinFPSTarget.TagID, ((int)Math.Ceiling((double)monitor.RefreshRate * 0.85)).ToIntStr());
				}
			}
		}
	}

	public void CreateMonitorScreens(winMain win, Grid container, IEnumerable<WpfScreen> Screens)
	{
		hwMonitorList hwMonitorList = new hwMonitorList();
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 0.0;
		double num4 = 0.0;
		foreach (WpfScreen Screen in Screens)
		{
			if (num > Screen.Bounds.Y)
			{
				num = Screen.Bounds.Y;
			}
			if (num2 < Screen.Bounds.Y + Screen.Bounds.Height)
			{
				num2 = Screen.Bounds.Y + Screen.Bounds.Height;
			}
			if (num3 > Screen.Bounds.X)
			{
				num3 = Screen.Bounds.X;
			}
			if (num4 < Screen.Bounds.X + Screen.Bounds.Width)
			{
				num4 = Screen.Bounds.X + Screen.Bounds.Width;
			}
		}
		double num5 = num4 - num3;
		double num6 = num2 - num;
		double num7 = container.ActualWidth / num5;
		double num8 = container.ActualHeight / num6;
		Clear();
		int num9 = -1;
		container.Children.Clear();
		foreach (WpfScreen Screen2 in Screens)
		{
			hwMonitor hwMonitor = hwMonitorList.Find(Screen2.DeviceName);
			if (hwMonitor == null)
			{
				hwMonitor = new hwMonitor();
			}
			num9++;
			clsMonitorItem clsMonitorItem2 = new clsMonitorItem(num9, Screen2, hwMonitor);
			clsMonitorItem2.VerticalAlignment = VerticalAlignment.Bottom;
			clsMonitorItem2.HorizontalAlignment = HorizontalAlignment.Left;
			clsMonitorItem2.ToolTip = "Windows Device: " + clsMonitorItem2.Device;
			clsMonitorItem2.Click += win.Monitor_Click;
			double num10 = clsMonitorItem2.PixelBounds.Height / clsMonitorItem2.PixelBounds.Width;
			double left;
			double bottom;
			if (num7 < num8)
			{
				clsMonitorItem2.Width = clsMonitorItem2.PixelBounds.Width * num7;
				clsMonitorItem2.Height = clsMonitorItem2.Width * num10;
				left = (clsMonitorItem2.PixelBounds.X - num3) * num7;
				bottom = (num6 - (clsMonitorItem2.PixelBounds.Bottom - num)) * num7;
			}
			else
			{
				clsMonitorItem2.Height = clsMonitorItem2.PixelBounds.Height * num8;
				clsMonitorItem2.Width = clsMonitorItem2.Height / num10;
				left = (clsMonitorItem2.PixelBounds.X - num3) * num8;
				bottom = (num6 - (clsMonitorItem2.PixelBounds.Bottom - num)) * num8;
			}
			clsMonitorItem2.Margin = new Thickness(left, 0.0, 0.0, bottom);
			container.Children.Add(clsMonitorItem2);
			Add(clsMonitorItem2);
		}
	}
}
