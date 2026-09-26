namespace irSidekickProfiles;

public static class gfxResolutionExtensions
{
	public static int PixelCount(this gfxResolution resolution)
	{
		return resolution switch
		{
			gfxResolution.Triple3840x2160 => PixelCalc(3, 3840, 2160), 
			gfxResolution.TripleSamsungG9 => PixelCalc(3, 5120, 1440), 
			gfxResolution.Triple3440x1440 => PixelCalc(3, 3440, 1440), 
			gfxResolution.Triple2560x1440 => PixelCalc(3, 2560, 1440), 
			gfxResolution.Single3840x2160 => PixelCalc(1, 3840, 2160), 
			gfxResolution.SingleSamsungG9 => PixelCalc(1, 5120, 1440), 
			gfxResolution.Triple1920x1080 => PixelCalc(3, 1920, 1080), 
			gfxResolution.Single3440x1440 => PixelCalc(1, 3440, 1440), 
			gfxResolution.Single2560x1440 => PixelCalc(1, 2560, 1440), 
			gfxResolution.Single1920x1080 => PixelCalc(1, 1920, 1080), 
			_ => PixelCalc(1, 1920, 1080), 
		};
	}

	public static int PixelCalc(int count, int width, int height)
	{
		return count * width * height;
	}
}
