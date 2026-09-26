using System;

namespace irSidekickProfiles;

internal static class clsFOVExtensions
{
	public static double MM2CM(this int value)
	{
		return value / 10;
	}

	public static int CM2MM(this double value)
	{
		return (int)Math.Round(value * 10.0, 0);
	}

	public static double MM2Inch(this int value)
	{
		return (double)value * 0.0393701;
	}

	public static int Inch2MM(this double value)
	{
		return (int)Math.Round(value / 0.0393701, 0);
	}

	public static int Total(this int value, int count)
	{
		return value * count;
	}

	public static double Total(this double value, int count)
	{
		return value * (double)count;
	}

	public static double invTan(this double value)
	{
		return Math.Atan(value).Rad2Deg();
	}

	public static double Rad2Deg(this double rad)
	{
		return rad * (180.0 / Math.PI);
	}

	public static double Deg2Rad(this double deg)
	{
		return deg * (Math.PI / 180.0);
	}
}
