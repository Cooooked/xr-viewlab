using System;
using System.Globalization;

namespace XRViewLab.UI;

// Parses a slider's Tag as its right-click-reset default value. Kept dependency-free so the
// parsing rules are fixture-testable without constructing WPF controls. A missing or malformed
// Tag means "no reset value configured" rather than an error — callers should simply do nothing
// in that case.
public static class SliderDefaults
{
	public static bool TryParse(object? tag, out double value)
	{
		value = 0;
		return tag is string text &&
			double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
	}
}
