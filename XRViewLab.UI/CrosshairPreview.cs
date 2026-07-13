using System;
using System.Windows;
using System.Windows.Media;

namespace XRViewLab.UI;

// Shared WPF reference renderer for both overlay previews. Geometry mirrors the native rectangle
// construction; the only difference is the preview pixel scale chosen to remain legible.
public sealed class CrosshairPreview : FrameworkElement
{
    public double CrosshairSize { get; set; } = 5;
    public double Gap { get; set; } = -2;
    public double Thickness { get; set; } = 1;
    public double OutlineThickness { get; set; } = 1;
    public double Alpha { get; set; } = 1;
    public double VrScale { get; set; } = 1;
    public bool Dot { get; set; }
    public bool Outline { get; set; }
    public bool TStyle { get; set; }
    public Color Color { get; set; } = Color.FromRgb(0, 255, 0);

    protected override Size MeasureOverride(Size availableSize) => new(280, 120);

    internal void Apply(CrosshairSettings s)
    {
        CrosshairSize=s.Size; Gap=s.Gap; Thickness=s.Thickness; OutlineThickness=s.OutlineThickness;
        Alpha=s.Alpha; VrScale=s.VrScale; Dot=s.Dot; Outline=s.Outline; TStyle=s.TStyle;
        Color=Color.FromRgb(s.R,s.G,s.B); InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds=new Rect(0,0,ActualWidth,ActualHeight);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(28,31,36)),new Pen(new SolidColorBrush(Color.FromRgb(75,80,88)),1),bounds);
        double unit=Math.Max(0.75,1.125*VrScale), arm=Math.Max(1,Math.Round(CrosshairSize*unit));
        double thick=Math.Max(1,Math.Round(Thickness*unit)), gap=Math.Round(Gap*unit), outline=Outline?Math.Max(1,Math.Round(OutlineThickness*unit)):0;
        double cx=Math.Floor(ActualWidth/2)+.5, cy=Math.Floor(ActualHeight/2)+.5, half=thick/2, inner=gap+half;
        Rect[] arms = TStyle
            ? [new(cx-inner-arm,cy-half,arm,thick),new(cx+inner,cy-half,arm,thick),new(cx-half,cy+inner,thick,arm)]
            : [new(cx-inner-arm,cy-half,arm,thick),new(cx+inner,cy-half,arm,thick),new(cx-half,cy-inner-arm,thick,arm),new(cx-half,cy+inner,thick,arm)];
        var brush=new SolidColorBrush(Color.FromArgb((byte)Math.Round(Math.Clamp(Alpha,0,1)*255),Color.R,Color.G,Color.B));
        var black=new SolidColorBrush(Color.FromArgb((byte)Math.Round(Math.Clamp(Alpha,0,1)*255),0,0,0));
        if (outline>0) foreach(var r in arms) dc.DrawRectangle(black,null,new Rect(r.X-outline,r.Y-outline,r.Width+2*outline,r.Height+2*outline));
        if (Dot && outline>0) dc.DrawRectangle(black,null,new Rect(cx-half-outline,cy-half-outline,thick+2*outline,thick+2*outline));
        foreach(var r in arms) dc.DrawRectangle(brush,null,r);
        if (Dot) dc.DrawRectangle(brush,null,new Rect(cx-half,cy-half,thick,thick));
    }
}
