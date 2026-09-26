using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynLOD;
internal static class SelfTest
{
    static int checks;
    static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); checks++; }
    public static void Run(string[] args)
    {
        string fixture = args.SkipWhile(a => a != "--fixture").Skip(1).First();
        string output = Path.Combine(AppContext.BaseDirectory,"test-output");
        Directory.CreateDirectory(output);
        string path = Path.Combine(output,"rendererDX11OpenXR.ini");
        var original = File.ReadAllBytes(fixture);
        File.WriteAllBytes(path,original);
        var doc = new IniFile(path); var main = doc.Read(IniFile.Main); var replay = doc.Read(IniFile.Replay);
        Check(main.Count == 9 && replay.Count == 9,"read actual sections");
        var window = new MainWindow(path,false);
        window.Show(); window.UpdateLayout();
        Check(File.ReadAllBytes(path).SequenceEqual(original),"launch does not write INI");
        Check(window.Fps.Text == main[IniFile.Fps],"FPS loads literally");
        window.WorldPresets.SelectedItem = "Medium";
        Check(window.Rows[0].MinBox.Text == "50" && window.Rows[0].MaxBox.Text == "300" && window.Rows[2].MinBox.Text == "50" && window.Rows[2].MaxBox.Text == "300","World Medium writes exact main and mirror values");
        window.CarsPresets.SelectedItem = "Maximum";
        Check(window.Rows[1].MinBox.Text == "25" && window.Rows[1].MaxBox.Text == "400" && window.Rows[3].MinBox.Text == "25" && window.Rows[3].MaxBox.Text == "400","Cars Maximum writes exact main and mirror values");
        var presetValues = window.Values();
        Check(presetValues["LODPctDynoMin"] == "50" && presetValues["LODPctDynoMax"] == "300" && presetValues["LODPctMin"] == "25","World maps to Dyno keys and Cars maps to plain keys");
        window.Reload();
        window.Rows[0].MaxBox.Text = "500";
        window.Rows[0].MinBox.Text = "73";
        Check(window.Rows[0].Track.Minimum == 73,"typed non-snapped value synchronizes");
        var minThumb = (System.Windows.Controls.Primitives.Thumb)window.Rows[0].Track.Children[0];
        minThumb.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0,0) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragStartedEvent });
        window.Rows[0].Track.DragTo(true,System.Windows.Input.Mouse.GetPosition(window.Rows[0].Track).X + 27d / 475 * (window.Rows[0].Track.ActualWidth-24));
        Check(window.Rows[0].MinBox.Text == "100", "real thumb drag snaps and updates input");
        window.Rows[0].MinBox.Text = "73";
        window.Rows[0].Track.Move(false,333);
        Check(window.Rows[0].MaxBox.Text == "333","slider synchronizes numeric input");
        window.Fps.Text = "0144";
        Check(window.Values()[IniFile.Fps] == "0144","FPS lexical value retained");
        window.Rows[1].MinBox.Text = "999";
        Check(!window.ApplyMain.IsEnabled && !window.ApplyReplay.IsEnabled,"invalid range blocks apply");
        window.Rows[1].MinBox.Text = main[RowKey(1).Min];
        Check(window.ApplyMain.IsEnabled,"valid range restores apply");
        Check(RangeTrack.Snap(102) == 100 && RangeTrack.Snap(107) == 107 && RangeTrack.Snap(114) == 114 && RangeTrack.Snap(498) == 500,"gentle snapping at multiples of 25");
        var beforeConstraintTest = window.Values();
        var constrained = new Dictionary<string,string>(beforeConstraintTest);
        constrained["LODPctDynoMin"] = constrained["LODPctDynoMax"] = "25";
        constrained["LODPctMin"] = constrained["LODPctMax"] = "500";
        bool constraintRejected = false; try { IniFile.Validate(constrained); } catch (InvalidDataException ex) { constraintRejected = ex.Message.Contains("World Max"); }
        Check(constraintRejected,"reject World Max below Cars Min using product labels");
        window.Rows[0].Load("25","137"); window.Rows[1].Load("137","200");
        window.Rows[0].Track.Move(false,25);
        Check(window.Rows[0].Track.Maximum == 137,"World Max slider stops at Cars Min");
        window.Rows[1].Track.Move(true,500);
        Check(window.Rows[1].Track.Minimum == 137,"Cars Min slider stops at World Max");
        window.Rows[0].Load(beforeConstraintTest["LODPctDynoMin"],beforeConstraintTest["LODPctDynoMax"]);
        window.Rows[1].Load(beforeConstraintTest["LODPctMin"],beforeConstraintTest["LODPctMax"]);
        // External edit while controls remain open must survive both writes.
        File.AppendAllText(path,"\r\n[External edit]\r\nKeep = café ; unrelated\r\n");
        byte[] beforeApply = File.ReadAllBytes(path);
        var values = window.Values();
        string backup = IniFile.Apply(path,IniFile.Main,values);
        Check(File.ReadAllBytes(backup).SequenceEqual(beforeApply),"backup is exact pre-write bytes");
        var written = new IniFile(path);
        Check(written.Read(IniFile.Main).All(p => p.Value == values[p.Key]),"main writes nine exact values");
        Check(written.Read(IniFile.Replay).All(p => p.Value == replay[p.Key]),"main does not touch replay");
        Check(File.ReadAllText(path).Contains("Keep = café ; unrelated"),"preserve intervening external edit");
        window.Apply(IniFile.Replay);
        written = new IniFile(path);
        Check(written.Read(IniFile.Replay).All(p => p.Value == values[p.Key]),"replay button writes same values");
        Check(written.Read(IniFile.Main).All(p => p.Value == values[p.Key]),"replay leaves main untouched");
        Check(window.Fps.Text == "0144","apply retains reapply buffer");
        window.Rows[0].MinBox.Text = "80";
        byte[] beforeRefresh = File.ReadAllBytes(path);
        window.Reload();
        Check(window.Rows[0].MinBox.Text == "73" && File.ReadAllBytes(path).SequenceEqual(beforeRefresh),"refresh reads without writes");
        window.Source.SelectedIndex = 1;
        Check(window.ReadLabel.Text == "[Replay Graphics]","replay inspection selects actual section");
        // Round trips preserve comments, unusual spacing, newline style, BOM and unrelated bytes.
        foreach (var encoding in new[] { new UTF8Encoding(true), Encoding.Unicode, Encoding.BigEndianUnicode, Encoding.Latin1 })
        foreach (string newline in new[] { "\r\n", "\n" })
        {
            string text = "[Graphics Options]" + newline + string.Join(newline, IniFile.Keys.Select(k => "  " + k + " = 100  \t; keep café")) + newline + "[Elsewhere]" + newline + "Keep=é";
            File.WriteAllText(path,text,encoding);
            var same = new IniFile(path).Read(IniFile.Main);
            byte[] before = File.ReadAllBytes(path);
            IniFile.Apply(path,IniFile.Main,same);
            Check(File.ReadAllBytes(path).SequenceEqual(before),"identity write preserves encoding and formatting");
            same[IniFile.Fps] = "0144";
            IniFile.Apply(path,IniFile.Main,same);
            Check(File.ReadAllText(path,encoding) == text.Replace("LODMinFPSTarget = 100", "LODMinFPSTarget = 0144"),"only numeric token changes");
        }
        File.WriteAllText(path,"[Graphics Options]\r\nLODPctMin=25\r\n");
        byte[] malformed = File.ReadAllBytes(path);
        bool rejected = false; try { IniFile.Apply(path,IniFile.Main,values); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && File.ReadAllBytes(path).SequenceEqual(malformed),"incomplete file fails without writing");
        File.WriteAllText(path,"[Graphics Options]\nLODPctMin=25\nLODPctMin=50\n");
        rejected = false; try { _ = new IniFile(path); } catch (InvalidDataException) { rejected = true; }
        Check(rejected,"duplicate DynLOD key rejected");
        // Restore the disposable fixture, render the actual WPF window, then close it.
        File.WriteAllBytes(path,original); window.Source.SelectedIndex=0; window.Reload(); window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
        bitmap.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output,"DynLOD.png"))) png.Save(stream);
        window.Close();
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"self-test.txt"),$"PASS: {checks} checks, INI fixtures + WPF controls + encoding/preservation fixtures. Live INI untouched.");
    }
    static (string Min,string Max) RowKey(int row) => row switch
    {
        0 => ("LODPctDynoMin","LODPctDynoMax"), 1 => ("LODPctMin","LODPctMax"),
        2 => ("LODPctDynoMirrorsMin","LODPctDynoMirrorsMax"), _ => ("LODPctMirrorsMin","LODPctMirrorsMax")
    };
}
