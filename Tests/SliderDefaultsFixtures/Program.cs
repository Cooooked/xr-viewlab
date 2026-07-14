using System;
using XRViewLab.UI;

static void Require(bool condition, string message)
{
	if (!condition) throw new InvalidOperationException(message);
}

Require(SliderDefaults.TryParse("0.98", out double a) && a == 0.98, "plain numeric tag parses");
Require(SliderDefaults.TryParse("3000", out double b) && b == 3000, "integer-looking tag parses as a double");
Require(SliderDefaults.TryParse("-0.5", out double c) && c == -0.5, "negative tag parses");

Require(!SliderDefaults.TryParse(null, out _), "a null tag (slider never given a default) is a no-op, not a crash");
Require(!SliderDefaults.TryParse("", out _), "an empty tag is a no-op");
Require(!SliderDefaults.TryParse("not-a-number", out _), "a garbage tag is a no-op, not a thrown exception");
Require(!SliderDefaults.TryParse(3000, out _), "a non-string tag (e.g. left as a boxed int by some other code path) is a no-op");

Console.WriteLine("Slider default tag parsing fixtures passed.");
