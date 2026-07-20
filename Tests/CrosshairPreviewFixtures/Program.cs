using System;
using System.IO;
using XRViewLab.UI;

static void Require(bool ok, string message)
{
    if (!ok) throw new InvalidOperationException($"FAIL: {message}");
    Console.WriteLine($"PASS: {message}");
}

string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string read(string relative) => File.ReadAllText(Path.Combine(repoRoot, relative));

// 1. Small real crosshair settings remain clearly visible in the preview.
{
    var tiny = new CrosshairSettings { Size = 0.5, Thickness = 0.1, Gap = -1, OutlineThickness = 0.5, Outline = true, VrScale = 0.5 };
    var dims = CrosshairPreview.Measure(tiny.Size, tiny.Thickness, tiny.Gap, tiny.OutlineThickness, tiny.Outline,
        CrosshairPreview.BaseUnit(tiny.VrScale));
    Require(dims.arm >= 2, $"Tiny crosshair arm is preview-visible ({dims.arm}px)");
    Require(dims.thick >= 1, $"Tiny crosshair thickness is preview-visible ({dims.thick}px)");
    Require(dims.outline >= 1, "Tiny crosshair outline is preview-visible");
}

// 2. Default settings are enlarged for the preview while still fitting the 280x120 box.
{
    var s = new CrosshairSettings { Size = 5, Thickness = 1, Gap = -2, OutlineThickness = 1, Outline = true, VrScale = 1 };
    var dims = CrosshairPreview.Measure(s.Size, s.Thickness, s.Gap, s.OutlineThickness, s.Outline,
        CrosshairPreview.BaseUnit(s.VrScale));
    Require(dims.arm >= 20, $"Default crosshair arm is enlarged ({dims.arm}px)");
    Require(dims.thick >= 4, $"Default crosshair thickness is enlarged ({dims.thick}px)");
    int span = (int)(dims.arm + dims.gap + dims.thick / 2 + dims.outline);
    Require(span < 60, $"Default preview crosshair fits inside the 120px preview height ({span}px radius)");
}

// 3. Preview enlargement is a separate multiplier; persisted settings do not contain it.
{
    var source = read("XRViewLab.UI/CrosshairConfig.cs");
    Require(!source.Contains("PreviewDisplayScale"), "CrosshairSettings has no preview-only scale field");
}

// 4. Both previews use the same shared preview scale and the same Measure helper.
{
    string preview = read("XRViewLab.UI/CrosshairPreview.cs");
    string editor = read("XRViewLab.UI/BeanMaskEditor.cs");
    Require(preview.Contains("public const double PreviewDisplayScale = 6.0"), "CrosshairPreview exposes the shared preview scale");
    Require(preview.Contains("PreviewDisplayScale"), "CrosshairPreview uses the preview scale");
    Require(editor.Contains("CrosshairPreview.Measure"), "BeanMaskEditor.DrawCrosshair uses CrosshairPreview.Measure (applying the shared preview scale)");
}

// 5. Positioning still uses the shared centred offset (unchanged).
{
    string editor = read("XRViewLab.UI/BeanMaskEditor.cs");
    Require(editor.Contains("Quest3PreviewGeometry.ResolveCentredOffset(sizeReference,_crosshairOffsetX,_crosshairOffsetY)"),
        "BeanMaskEditor.DrawCrosshair keeps the shared centred offset");
}

// 6. Optical-centred transform and permanent widget preview shim are untouched.
{
    string editor = read("XRViewLab.UI/BeanMaskEditor.cs");
    Require(editor.Contains("WidgetPreviewShimY = 0.077"), "Permanent +0.077 widget preview shim preserved");
    Require(editor.Contains("OpticalContentShiftY") && editor.Contains("PreviewFullArea"), "Optical-centred content transform preserved");
}

// 7. Native geometry remains driven by the real (unscaled) settings.
{
    string native = read("dllmain.cpp");
    Require(native.Contains("crosshairSize") && native.Contains("crosshairThickness"), "Native crosshair uses real size/thickness");
    Require(!native.Contains("PreviewDisplayScale"), "Native crosshair has no preview-only scale");
}

Console.WriteLine("Crosshair preview fixtures passed.");
