using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    private sealed record Pattern(string Id, Action<DrawingContext, double, double> Draw);
    private static readonly Pen White = new(Brushes.White, 2);
    private static readonly Pen Cyan = new(Brushes.Cyan, 2);
    private static readonly IReadOnlyDictionary<string,string> Expected = new Dictionary<string,string>
    {
        ["grid"]="F66352AAFF16B0C50685CFED3956D250CB6651A8CBAC1869293C5E4A333120A2",
        ["ruler"]="C41A3683034B1A57A28C86F3C9510E51E5F3938857FAA4E4A2DC241CCB5C3CA9",
        ["gratings"]="244DCD4F98A5CCDC2AE319327BB7A76EFA444371F7511B7F720D6129C9FD3336",
        ["colour-bars"]="32618CCFE62DA0472A69B75AD0A214541B25ABBD2D56EA7DDFFFAFE86EBC6C36",
        ["beacon"]="582D588C44ADE7D695DC2DFA6FA4D53B0590DE65811BE1F8B2098AA05E1DAB55",
        ["edge-probes"]="750EF0388CB90DAD73D2B323337A237E6BD1E7C9B2A6DA90C184D7D305465D79",
        ["checkerboards"]="56A2F23C9DDF07D17561D3B9635573E9E4BAC0BAFD443C18A5339348D3200868",
        ["zone-plate"]="0EA1D633A0E3064F9E610D3EB22AEBB4C77B3B740D9FD8668BDF7307E150116A",
        ["clipping-steps"]="E641C32A4274E308AA0769DA195CBD0381ACDCD562343B0D2ED21E1B7FBC616C",
        ["motion-strip"]="79AE171E3A7A65DDC2E07108C7207F0D85D512B5A6A9120E514C99A96E8BD2F5",
    };

    [STAThread]
    private static int Main(string[] args)
    {
        string root = FindRepository();
        VerifyWiring(root);
        string output = Path.Combine(root, "TestResults", "CalibrationReferences");
        Directory.CreateDirectory(output);
        foreach (Pattern pattern in Patterns())
        {
            string path = Path.Combine(output, pattern.Id + ".png");
            Render(path, pattern.Draw, null);
            string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            Require(hash == Expected[pattern.Id], $"reference image changed for {pattern.Id}: {hash}");
            string verticalPath=Path.Combine(output,pattern.Id+"-vertical-crop.png"),horizontalPath=Path.Combine(output,pattern.Id+"-horizontal-crop.png");
            Render(verticalPath,pattern.Draw,new Rect(0,55,640,250)); Render(horizontalPath,pattern.Draw,new Rect(90,0,460,360));
            string verticalHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(verticalPath))),horizontalHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(horizontalPath)));
            Require(new FileInfo(verticalPath).Length>0&&new FileInfo(horizontalPath).Length>0,$"crop references were not captured for {pattern.Id}");
            Console.WriteLine($"{pattern.Id} {hash}");
        }
        Console.WriteLine($"Calibration reference fixture passed: 10/10 patterns; PNG references: {output}");
        return 0;
    }

    private static string FindRepository()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "dllmain.cpp"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static void VerifyWiring(string root)
    {
        string xaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        string ui = File.ReadAllText(Path.Combine(root, "XRViewLab.UI", "MainWindow.cs"));
        string native = File.ReadAllText(Path.Combine(root, "dllmain.cpp"));
        string[] controls = { "CalGridCheck", "CalRulerCheck", "CalGratingsCheck", "CalBarsCheck", "CalBeaconCheck", "CalEdgeProbesCheck", "CalCheckerboardsCheck", "CalZonePlateCheck", "CalClippingCheck", "CalMotionCheck" };
        string[] keys = { "calibration_grid", "calibration_ruler", "calibration_gratings", "calibration_bars", "calibration_beacon", "calibration_edge_probes", "calibration_checkerboards", "calibration_zone_plate", "calibration_clipping_steps", "calibration_motion_strip" };
        for (int i = 0; i < controls.Length; ++i)
        {
            Require(xaml.Contains($"Name=\"{controls[i]}\"", StringComparison.Ordinal), $"missing UI control {controls[i]}");
            Require(ui.Contains(keys[i], StringComparison.Ordinal), $"missing UI persistence key {keys[i]}");
            Require(native.Contains(keys[i], StringComparison.Ordinal), $"missing native key {keys[i]}");
        }
        Require(ui.Contains("mask |= 1u << 9", StringComparison.Ordinal), "live calibration bit sequence is incomplete");
        Require(native.Contains("DrawCalibrationOverlayToTexture", StringComparison.Ordinal), "shared calibration renderer is missing");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private static IReadOnlyList<Pattern> Patterns() => new Pattern[]
    {
        new("grid", (d,w,h) => { for(int x=0;x<w;x+=32)d.DrawLine(x%64==0?Cyan:White,new(x,0),new(x,h)); for(int y=0;y<h;y+=32)d.DrawLine(y%64==0?Cyan:White,new(0,y),new(w,y)); }),
        new("ruler", (d,w,h) => { d.DrawLine(White,new(20,h/2),new(w-20,h/2)); for(int x=20;x<w-20;x+=10)d.DrawLine(Cyan,new(x,h/2-(x%50==20?18:8)),new(x,h/2+8)); }),
        new("gratings", (d,w,h) => { for(int band=0;band<3;band++)for(int x=0;x<w/3;x+=band+1)if((x/(band+1))%2==0)d.DrawRectangle(Brushes.White,null,new(band*w/3+x,0,band+1,h)); }),
        new("colour-bars", (d,w,h) => { Brush[] b={Brushes.White,Brushes.Yellow,Brushes.Cyan,Brushes.Lime,Brushes.Magenta,Brushes.Red,Brushes.Blue,Brushes.Black};for(int i=0;i<b.Length;i++)d.DrawRectangle(b[i],null,new(i*w/b.Length,0,w/b.Length,h*.65));for(int i=0;i<16;i++){byte v=(byte)(i*17);d.DrawRectangle(new SolidColorBrush(Color.FromRgb(v,v,v)),null,new(i*w/16,h*.7,w/16,h*.3));} }),
        new("beacon", (d,w,h) => { d.DrawEllipse(Brushes.White,Cyan,new(w/2,h/2),75,75);d.DrawEllipse(Brushes.Black,null,new(w/2,h/2),22,22); }),
        new("edge-probes", (d,w,h) => { d.DrawRectangle(null,Cyan,new(2,2,w-4,h-4));for(int i=0;i<24;i++){double y=i*h/24;d.DrawLine(i%2==0?White:Cyan,new(0,y),new(35,y));d.DrawLine(i%2==0?White:Cyan,new(w-35,y),new(w,y));} }),
        new("checkerboards", (d,w,h) => { for(int size=2;size<=16;size*=2)for(int y=0;y<h/4;y+=size)for(int x=0;x<w;x+=size)if(((x/size)+(y/size))%2==0)d.DrawRectangle(Brushes.White,null,new(x,(Math.Log2(size)-1)*h/4+y,size,size)); }),
        new("zone-plate", (d,w,h) => { Point c=new(w/2,h/2);for(int r=170;r>0;r-=7)d.DrawEllipse(r%14==0?Brushes.White:Brushes.Black,null,c,r,r);for(int a=0;a<24;a++){double q=a*Math.PI/12;d.DrawLine(Cyan,c,new(c.X+Math.Cos(q)*180,c.Y+Math.Sin(q)*180));} }),
        new("clipping-steps", (d,w,h) => { for(int i=0;i<32;i++){byte v=(byte)Math.Round(i*255d/31);d.DrawRectangle(new SolidColorBrush(Color.FromRgb(v,v,v)),null,new(i*w/32,0,w/32,h));} }),
        new("motion-strip", (d,w,h) => { for(int x=-80;x<w;x+=80){d.DrawRectangle(Brushes.White,null,new(x,h*.35,34,h*.3));d.DrawLine(Cyan,new(x+17,h*.2),new(x+57,h*.2));} }),
    };

    private static void Render(string path, Action<DrawingContext,double,double> draw, Rect? clip)
    {
        const int width=640,height=360;
        DrawingVisual visual = new(); using (DrawingContext dc=visual.RenderOpen()){dc.DrawRectangle(Brushes.Black,null,new(0,0,width,height));if(clip.HasValue)dc.PushClip(new RectangleGeometry(clip.Value));draw(dc,width,height);if(clip.HasValue)dc.Pop();}
        RenderTargetBitmap bitmap=new(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        PngBitmapEncoder encoder=new();encoder.Frames.Add(BitmapFrame.Create(bitmap));using FileStream stream=File.Create(path);encoder.Save(stream);
    }
}
