using System;

namespace irSidekickProfiles;

internal class clsFOV
{
	public static double DegToRad(double deg)
	{
		return deg.Deg2Rad();
	}

	public static double RadToDeg(double rad)
	{
		return rad.Rad2Deg();
	}

	public static double calcVerticalFOV(int distanceViewing, int heightImage)
	{
		return Math.Round(((double)heightImage / 2.0 / (double)distanceViewing).invTan() * 2.0, 1);
	}

	public static int calcDistanceViewing(double angleMonitors, double widthMonitor)
	{
		return (int)Math.Round(widthMonitor / (Math.Tan(DegToRad(angleMonitors / 2.0)) * 2.0), 0);
	}

	public static double calcAngleMonitors(double distanceViewing, double widthMonitor)
	{
		return Math.Round((widthMonitor / (distanceViewing * 2.0)).invTan() * 2.0, 1);
	}

	public static double calcHorizontalFOV(double distanceViewing, double widthMonitor, int countMonitors)
	{
		return Math.Round(calcAngleMonitors(distanceViewing, widthMonitor) * (double)countMonitors, 1);
	}
}
