using System;

namespace irSidekickProfiles;

public static class gfxConfig
{
	public static gfxGPUList GPUList = new gfxGPUList();

	public static gfxResolution WhichResolution(int MonitorCount, int MonitorWidth, int MonitorHeight)
	{
		int num = MonitorWidth * MonitorHeight * MonitorCount;
		gfxResolution[] array = (gfxResolution[])Enum.GetValues(typeof(gfxResolution));
		for (int num2 = array.Length - 1; num2 > 0; num2--)
		{
			if (array[num2].PixelCount() <= num)
			{
				return array[num2];
			}
		}
		return gfxResolution.Single1920x1080;
	}
}
