namespace irSidekickProfiles;

public class gfxGPUCard
{
	public gfxGPU GPU { get; }

	public string Name { get; }

	public int gbMem { get; }

	public string Info { get; }

	public gfxPreset[] Presets { get; }

	public gfxGPUCard(gfxGPU gpu)
	{
		GPU = gpu;
		switch (gpu)
		{
		default:
			Name = "RTX 1060";
			gbMem = 6;
			Info = "Ok for single monitor, will struggle on triple monitor";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.RTX1070:
			Name = "RTX 1070";
			gbMem = 8;
			Info = "Ok for single monitor, will struggle on triple monitor";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.RTX1080:
			Name = "RTX 1080";
			gbMem = 8;
			Info = "Good for single monitor, will struggle on triple monitor";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.GTX1650:
			Name = "GTX 1650";
			gbMem = 4;
			Info = "Good for single monitor, adequate on triple 1080 but will struggle at higher resolutions";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.GTX1660:
			Name = "GTX 1660";
			gbMem = 6;
			Info = "Good for single monitor, adequate on triple 1080 but will struggle at higher resolutions";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.RTX2060:
			Name = "RTX 2060";
			gbMem = 6;
			Info = "Ok for single monitor, adequate on triple 1080 but will struggle at higher resolutions";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.RTX2070:
			Name = "RTX 2070";
			gbMem = 8;
			Info = "Good for single monitor, adequate on triple 1080 but will struggle at higher resolutions";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.RTX2080:
			Name = "RTX 2080";
			gbMem = 8;
			Info = "Good for single monitor, adequate on triple 1080 but will struggle at higher resolutions";
			Presets = new gfxPreset[10]
			{
				gfxPreset.High,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Standard,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.RTX3060:
			Name = "RTX 3060";
			gbMem = 8;
			Info = "Good for single monitor, adequate on triple 1080 but will struggle at higher resolutions";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Standard,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.RTX3070:
			Name = "RTX 3070";
			gbMem = 8;
			Info = "Good for single monitor, adequate on triple 1440 but will struggle at higher resolutions";
			Presets = new gfxPreset[10]
			{
				gfxPreset.High,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.RTX3080:
			Name = "RTX 3080";
			gbMem = 10;
			Info = "Great for single monitor, good on triple monitors below 4k";
			Presets = new gfxPreset[10]
			{
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Faster
			};
			break;
		case gfxGPU.RTX3090:
			Name = "RTX 3090";
			gbMem = 24;
			Info = "Great for single monitor, good on triple monitors";
			Presets = new gfxPreset[10]
			{
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard
			};
			break;
		case gfxGPU.RTX4060:
			Name = "RTX 4060";
			gbMem = 8;
			Info = "Good for single monitor, adequate on triple 1080 but will struggle at higher resolutions";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.RTX4070:
			Name = "RTX 4070";
			gbMem = 12;
			Info = "Great for single monitor, adequate on triple 1440 but will struggle at higher resolutions";
			Presets = new gfxPreset[10]
			{
				gfxPreset.High,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.RTX4080:
			Name = "RTX 4080";
			gbMem = 16;
			Info = "Great for single monitor, great on triple monitors below 4k, adequate on triple 4k";
			Presets = new gfxPreset[10]
			{
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster
			};
			break;
		case gfxGPU.RTX4090:
			Name = "RTX 4090";
			gbMem = 24;
			Info = "Great for single monitor, great on triple monitors below 4k, ok on triple 4k";
			Presets = new gfxPreset[10]
			{
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard
			};
			break;
		case gfxGPU.RTX5060:
			Name = "RTX 5060";
			gbMem = 8;
			Info = "Good for single monitor, adequate on triple 1080 but will struggle at higher resolutions";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.RTX5070:
			Name = "RTX 5070";
			gbMem = 12;
			Info = "Good for single monitor, adequate on triple 1080 but will struggle at higher resolutions";
			Presets = new gfxPreset[10]
			{
				gfxPreset.High,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.RTX5070TI:
			Name = "RTX 5070ti";
			gbMem = 16;
			Info = "Great for single monitor, good on triple 1440 but will struggle at higher resolutions";
			Presets = new gfxPreset[10]
			{
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Faster
			};
			break;
		case gfxGPU.RTX5080:
			Name = "RTX 5080";
			gbMem = 16;
			Info = "Great for single monitor, great on triple monitors below 4k, ok on triple 4k";
			Presets = new gfxPreset[10]
			{
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard
			};
			break;
		case gfxGPU.RTX5090:
			Name = "RTX 5090";
			gbMem = 24;
			Info = "Great for single monitor, great on triple monitors below 4k, ok on triple 4k";
			Presets = new gfxPreset[10]
			{
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard
			};
			break;
		case gfxGPU.AMD6600:
			Name = "AMD 6600";
			gbMem = 8;
			Info = "Ok for single monitor, will struggle on triple monitor higher than 1080p";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.AMD6600XT:
			Name = "AMD 6600 XT";
			gbMem = 8;
			Info = "Ok for single monitor, will struggle on triple monitor higher than 1080p";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.AMD6700XT:
			Name = "AMD 6700 XT";
			gbMem = 12;
			Info = "Ok for single monitor, will struggle on triple monitor higher than 1080p";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.AMD6800:
			Name = "AMD 6800";
			gbMem = 16;
			Info = "Good for single monitor, adequate on triple 1080 but will struggle at higher resolutions\nTriple monitor configuration & performance can be problematic";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.AMD6800XT:
			Name = "AMD 6800 XT";
			gbMem = 16;
			Info = "Good for single monitor, adequate on triple 1080 but will struggle at higher resolutions\nTriple monitor configuration & performance can be problematic";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.AMD6900XT:
			Name = "AMD 6900 XT";
			gbMem = 16;
			Info = "Good for single monitor, adequate on triple 1080 but will struggle at higher resolutions\nTriple monitor configuration & performance can be problematic";
			Presets = new gfxPreset[10]
			{
				gfxPreset.High,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.AMD7600XT:
			Name = "AMD 7600 XT";
			gbMem = 16;
			Info = "Good for single monitor, adequate on triple 1080 but will struggle at higher resolutions\nTriple monitor configuration & performance can be problematic";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.AMD7800XT:
			Name = "AMD 7800 XT";
			gbMem = 16;
			Info = "Great for single monitor, adequate on triple 1080 but will struggle at higher resolutions\nTriple monitor configuration & performance can be problematic";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Low,
				gfxPreset.Low
			};
			break;
		case gfxGPU.AMD7900:
			Name = "AMD 7900";
			gbMem = 20;
			Info = "Great for single monitor, adequate on triple 1440 but will struggle at higher resolutions\nTriple monitor configuration & performance can be problematic";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Low
			};
			break;
		case gfxGPU.AMD7900XT:
			Name = "AMD 7900 XT";
			gbMem = 24;
			Info = "Great for single monitor, adequate on triple 1440 but will struggle at higher resolutions\nTriple monitor configuration & performance can be problematic";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Low
			};
			break;
		case gfxGPU.AMD9070:
			Name = "AMD 9070";
			gbMem = 16;
			Info = "Great for single monitor, adequate on triple 1440 but will struggle at higher resolutions\nTriple monitor configuration & performance can be problematic";
			Presets = new gfxPreset[10]
			{
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster
			};
			break;
		case gfxGPU.AMD9070XT:
			Name = "AMD 9070 XT";
			gbMem = 16;
			Info = "Great for single monitor, adequate on triple 1440 but will struggle at higher resolutions\nTriple monitor configuration & performance can be problematic";
			Presets = new gfxPreset[10]
			{
				gfxPreset.High,
				gfxPreset.High,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Pretty,
				gfxPreset.Standard,
				gfxPreset.Standard,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster
			};
			break;
		case gfxGPU.A380:
			Name = "Intel A380";
			gbMem = 16;
			Info = "Not recommended, below average performance on single 1080p\nTriple 1080p performance is poor";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster
			};
			break;
		case gfxGPU.A770:
			Name = "Intel A770";
			gbMem = 16;
			Info = "Not recommended, below average performance on single 1080p\nTriple 1080p performance is poor";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster
			};
			break;
		case gfxGPU.B570:
			Name = "Intel B570";
			gbMem = 16;
			Info = "Not recommended, below average performance on single 1080p\nTriple 1080p performance is poor";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster
			};
			break;
		case gfxGPU.B580:
			Name = "Intel B580";
			gbMem = 16;
			Info = "Not recommended, below average performance on single 1080p\nTriple 1080p performance is poor";
			Presets = new gfxPreset[10]
			{
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster,
				gfxPreset.Faster
			};
			break;
		}
	}
}
