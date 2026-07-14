using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;

namespace XRViewLab.UI;

public partial class PerformanceTraceWindow : Window
{
	private readonly PerformanceTrace _trace;
	private int _markerIndex = -1;
	public PerformanceTraceWindow(string path)
	{
		InitializeComponent();
		_trace = PerformanceTrace.Load(path);
		SummaryText.Text = $"{_trace.Samples.Count:N0} real OpenXR samples · {_trace.Markers.Count} markers · {System.IO.Path.GetFileName(path)}";
		Loaded += (_, _) => DrawGraph();
	}

	private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawGraph();
	private void DrawGraph()
	{
		GraphCanvas.Children.Clear(); if (_trace.Samples.Count < 2 || GraphCanvas.ActualWidth < 20 || GraphCanvas.ActualHeight < 20) return;
		double w=GraphCanvas.ActualWidth,h=GraphCanvas.ActualHeight,pad=18;
		double first=_trace.Samples[0].ElapsedMs,last=_trace.Samples[^1].ElapsedMs,span=Math.Max(1,last-first);
		double max=Math.Max(1,_trace.Samples.Max(s=>Math.Max(s.ActualMs,s.TargetMs))*1.10);
		double X(double ms)=>pad+(ms-first)/span*(w-pad*2);
		double Y(double ms)=>h-pad-Math.Clamp(ms/max,0,1)*(h-pad*2);
		var actual=new Polyline{Stroke=new SolidColorBrush(Color.FromRgb(50,210,255)),StrokeThickness=1.5};
		var target=new Polyline{Stroke=new SolidColorBrush(Color.FromRgb(120,130,145)),StrokeThickness=1,StrokeDashArray=new DoubleCollection{4,4}};
		foreach(var s in _trace.Samples){actual.Points.Add(new(X(s.ElapsedMs),Y(s.ActualMs)));target.Points.Add(new(X(s.ElapsedMs),Y(s.TargetMs)));}
		GraphCanvas.Children.Add(target);GraphCanvas.Children.Add(actual);
		foreach(var m in _trace.Markers){double x=X(m.ElapsedMs);var line=new Line{X1=x,X2=x,Y1=pad,Y2=h-pad,Stroke=new SolidColorBrush(Color.FromRgb(53,234,215)),StrokeThickness=1.5};GraphCanvas.Children.Add(line);var label=new TextBlock{Text=$"M{m.Number}",Foreground=new SolidColorBrush(Color.FromRgb(53,234,215)),FontSize=10};Canvas.SetLeft(label,x+3);Canvas.SetTop(label,pad);GraphCanvas.Children.Add(label);}
	}

	private void PreviousMarker_Click(object sender,RoutedEventArgs e)=>SelectMarker(-1);
	private void NextMarker_Click(object sender,RoutedEventArgs e)=>SelectMarker(1);
	private void SelectMarker(int direction)
	{
		if(_trace.Markers.Count==0){MarkerText.Text="No markers were captured in this session.";return;}
		_markerIndex=(_markerIndex+direction+_trace.Markers.Count)%_trace.Markers.Count;var m=_trace.Markers[_markerIndex];
		var nearest=_trace.Samples.MinBy(s=>Math.Abs(s.ElapsedMs-m.ElapsedMs));
		MarkerText.Text=$"M{m.Number} at {TimeSpan.FromMilliseconds(m.ElapsedMs):hh\\:mm\\:ss\\.fff} · actual {nearest?.ActualMs:0.00} ms · target {nearest?.TargetMs:0.00} ms";
	}
}
