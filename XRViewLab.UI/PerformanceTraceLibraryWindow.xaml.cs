using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;

namespace XRViewLab.UI;

public partial class PerformanceTraceLibraryWindow : Window
{
    private sealed record Row(string Path, string FileName, string Started, string Duration, int Samples, string AverageFps, string P99Ms, string MaximumMs, int CadenceMisses, string Gpu, double AverageFpsValue, double P99Value);
    private readonly string _directory;
    private Row? Selected => TraceGrid.SelectedItem as Row;

    public PerformanceTraceLibraryWindow()
    {
        InitializeComponent();
        _directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"XR ViewLab","PerformanceTraces");
        Reload();
    }

    private void Reload()
    {
        Directory.CreateDirectory(_directory); var rows=new List<Row>();
        IEnumerable<string> files=Directory.EnumerateFiles(_directory,"session-*.csv").OrderByDescending(File.GetLastWriteTimeUtc);
        if(!files.Any()&&File.Exists(Path.Combine(_directory,"latest.csv"))) files=new[]{Path.Combine(_directory,"latest.csv")};
        foreach(string file in files) try
        {
            PerformanceTrace trace=PerformanceTrace.Load(file); if(trace.Samples.Count==0)continue; PerformanceTraceSummary s=trace.Analyze();
            double duration=trace.Samples[^1].ElapsedMs/1000d, fps=s.AverageMs>0?1000d/s.AverageMs:0;
            rows.Add(new(file,Path.GetFileName(file),(trace.StartUtc??File.GetCreationTimeUtc(file)).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),TimeSpan.FromSeconds(duration).ToString(@"hh\:mm\:ss"),trace.Samples.Count,$"{fps:0.0} FPS",$"{s.P99Ms:0.00} ms",$"{s.MaximumMs:0.00} ms",s.CadenceMisses,trace.HasGpuTelemetry?"Available":"Unavailable",fps,s.P99Ms));
        } catch { }
        TraceGrid.ItemsSource=rows; if(rows.Count>0)TraceGrid.SelectedIndex=0;
        ComparisonText.Text=rows.Count==0?"No durable VR session traces exist yet.":$"{rows.Count} retained VR session(s).";
    }

    private void Open_Click(object sender,RoutedEventArgs e){if(Selected is{} row)new PerformanceTraceWindow(row.Path){Owner=this}.Show();}
    private void Compare_Click(object sender,RoutedEventArgs e)
    {
        List<Row> rows=TraceGrid.SelectedItems.Cast<Row>().OrderBy(x=>x.Started).ToList(); if(rows.Count<2){ComparisonText.Text="Select two or more sessions to compare.";return;}
        Row first=rows[0];ComparisonText.Text="Reference: "+first.Started+". "+string.Join("  |  ",rows.Skip(1).Select(row=>$"{row.Started}: FPS {Delta(row.AverageFpsValue,first.AverageFpsValue)}; P99 {Delta(row.P99Value,first.P99Value)}"));
    }
    private static string Delta(double value,double baseline)=>baseline==0?"unavailable":((value-baseline)/baseline*100).ToString("+0.0;-0.0;0.0",CultureInfo.CurrentCulture)+"%";
    private void Delete_Click(object sender,RoutedEventArgs e)
    {
        List<Row> rows=TraceGrid.SelectedItems.Cast<Row>().ToList();if(rows.Count==0)return;
        if(MessageBox.Show(this,$"Permanently delete {rows.Count} selected VR session trace(s)?","Delete session history",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
        foreach(Row row in rows)try{File.Delete(row.Path);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Could not delete trace",MessageBoxButton.OK,MessageBoxImage.Warning);}Reload();
    }
}
