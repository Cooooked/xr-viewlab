using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace XRViewLab.UI;

public partial class PerformanceTraceWindow : Window
{
	private const double LeftMargin = 62, RightMargin = 18, TopMargin = 18, BottomMargin = 38;
	private readonly PerformanceTrace _trace;
	private readonly double _fullStart, _fullEnd;
	private double _viewStart, _viewEnd;
	private int _markerIndex = -1;
	private bool _ready, _panning;
	private Point _panOrigin;
	private double _panStart, _panEnd;
	private double? _hoverElapsed;

	private sealed record SeriesSpec(string Name, Func<PerformanceTraceSample, double> Value, CheckBox Toggle, Color Colour, bool Dashed = false);

	public PerformanceTraceWindow(string path)
	{
		InitializeComponent();
		_trace = PerformanceTrace.Load(path);
		_fullStart = _trace.Samples.Count == 0 ? 0 : _trace.Samples[0].ElapsedMs;
		_fullEnd = _trace.Samples.Count == 0 ? 1 : Math.Max(_fullStart + 1, _trace.Samples[^1].ElapsedMs);
		_viewStart = _fullStart; _viewEnd = _fullEnd;
		PerformanceTraceSummary stats = _trace.Analyze();
		SummaryText.Text = $"{_trace.Samples.Count:N0} real OpenXR samples · {_trace.Markers.Count} user markers · schema {_trace.SchemaVersion} · {System.IO.Path.GetFileName(path)}";
		string saturation = _trace.HasGpuTelemetry
			? $"GPU ≥90%: {stats.GpuWarningPeriods:N0} periods / {stats.GpuWarningSeconds:0.0}s · ≥98%: {stats.GpuCriticalPeriods:N0} / {stats.GpuCriticalSeconds:0.0}s"
			: "GPU saturation: unavailable in this legacy trace";
		StatsText.Text = $"Frame interval  avg {stats.AverageMs:0.000} ms · median {stats.MedianMs:0.000} · P95 {stats.P95Ms:0.000} · P99 {stats.P99Ms:0.000} · max {stats.MaximumMs:0.000} · cadence misses (estimated) {stats.CadenceMisses:N0} / {stats.EstimatedMissedDisplayIntervals:N0} display intervals · {saturation}";
		_ready = true;
		Loaded += (_, _) => DrawGraph();
	}

	private IEnumerable<SeriesSpec> Series() => new[]
	{
		new SeriesSpec("Actual interval", s => s.ActualMs, ActualSeries, Color.FromRgb(50,210,255)),
		new SeriesSpec("Detected cadence", s => s.TargetMs, TargetSeries, Color.FromRgb(244,201,93), true),
		new SeriesSpec("Display period", s => s.DisplayMs, DisplaySeries, Color.FromRgb(155,140,255), true),
		new SeriesSpec("App work", s => s.AppMs, AppSeries, Color.FromRgb(124,227,139)),
		new SeriesSpec("Runtime wait", s => s.WaitMs, WaitSeries, Color.FromRgb(255,158,100)),
		new SeriesSpec("Submit", s => s.SubmitMs, SubmitSeries, Color.FromRgb(232,121,249))
	};

	private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawGraph();
	private void SeriesChanged(object sender, RoutedEventArgs e) { if (_ready) DrawGraph(); }

	private void DrawGraph()
	{
		GraphCanvas.Children.Clear();
		double width = GraphCanvas.ActualWidth, height = GraphCanvas.ActualHeight;
		if (_trace.Samples.Count < 2 || width < 120 || height < 100) return;
		double plotWidth = width - LeftMargin - RightMargin, plotHeight = height - TopMargin - BottomMargin;
		List<SeriesSpec> active = Series().Where(s => s.Toggle.IsChecked == true).ToList();
		int first = LowerBound(_viewStart), last = Math.Min(_trace.Samples.Count - 1, LowerBound(_viewEnd) + 1);
		if (last <= first) return;
		var scaleValues = new List<double>((last - first + 1) * Math.Max(1, active.Count));
		foreach (SeriesSpec series in active)
			for (int i = first; i <= last; ++i) { double value = series.Value(_trace.Samples[i]); if (double.IsFinite(value) && value >= 0) scaleValues.Add(value); }
		scaleValues.Sort();
		double robust = scaleValues.Count == 0 ? 1 : scaleValues[Math.Clamp((int)(scaleValues.Count * .995), 0, scaleValues.Count - 1)];
		double display = MedianVisible(first, last, s => s.DisplayMs);
		double yMax = NiceCeiling(Math.Max(1, Math.Max(robust * 1.18, display > 0 ? display * 1.35 : 0)));
		double X(double elapsed) => LeftMargin + (elapsed - _viewStart) / Math.Max(1, _viewEnd - _viewStart) * plotWidth;
		double Y(double value) => TopMargin + (1 - Math.Clamp(value / yMax, 0, 1)) * plotHeight;

		DrawAxes(width, height, plotWidth, plotHeight, yMax, X, Y);
		if (BudgetGuides.IsChecked == true && display > 0)
			for (int multiple = 1; multiple <= 4 && display * multiple <= yMax; ++multiple)
				DrawHorizontal(Y(display * multiple), $"{multiple}× {display * multiple:0.00} ms", Color.FromArgb(75, 155, 140, 255), width);

		foreach (SeriesSpec series in active) DrawDownsampledSeries(series, first, last, plotWidth, X, Y, yMax);
		DrawEvents(X, height);
		if (_hoverElapsed is double hover && hover >= _viewStart && hover <= _viewEnd) DrawHover(hover, X, Y, height);
	}

	private void DrawAxes(double width, double height, double plotWidth, double plotHeight, double yMax,
		Func<double,double> x, Func<double,double> y)
	{
		var axis = new SolidColorBrush(Color.FromRgb(92,100,112));
		GraphCanvas.Children.Add(new Line { X1=LeftMargin, X2=LeftMargin, Y1=TopMargin, Y2=height-BottomMargin, Stroke=axis });
		GraphCanvas.Children.Add(new Line { X1=LeftMargin, X2=width-RightMargin, Y1=height-BottomMargin, Y2=height-BottomMargin, Stroke=axis });
		for (int i=0;i<=5;++i)
		{
			double value=yMax*i/5, yy=y(value);
			GraphCanvas.Children.Add(new Line { X1=LeftMargin, X2=width-RightMargin, Y1=yy, Y2=yy, Stroke=new SolidColorBrush(Color.FromArgb(45,120,130,145)) });
			AddText($"{value:0.##}", 4, yy-8, 11, Color.FromRgb(180,187,198));
		}
		for (int i=0;i<=6;++i)
		{
			double elapsed=_viewStart+(_viewEnd-_viewStart)*i/6, xx=x(elapsed);
			GraphCanvas.Children.Add(new Line { X1=xx, X2=xx, Y1=TopMargin, Y2=height-BottomMargin, Stroke=new SolidColorBrush(Color.FromArgb(30,120,130,145)) });
			AddText(FormatElapsed(elapsed), xx-26, height-BottomMargin+7, 10, Color.FromRgb(180,187,198));
		}
		AddText("Latency / interval (ms)", 5, 1, 11, Color.FromRgb(215,220,227));
		AddText("Time from session start", LeftMargin + plotWidth/2 - 55, height-17, 11, Color.FromRgb(215,220,227));
	}

	private void DrawDownsampledSeries(SeriesSpec series, int first, int last, double plotWidth,
		Func<double,double> x, Func<double,double> y, double yMax)
	{
		int buckets=Math.Max(1,(int)plotWidth), count=last-first+1;
		var line=new Polyline { Stroke=new SolidColorBrush(series.Colour), StrokeThickness=1.25 };
		if (series.Dashed) line.StrokeDashArray=new DoubleCollection { 4, 3 };
		bool clipped=false;
		for(int bucket=0;bucket<buckets;++bucket)
		{
			int a=first+(int)((long)count*bucket/buckets), b=first+(int)((long)count*(bucket+1)/buckets)-1;
			if(b<a) continue; b=Math.Min(b,last);
			int minIndex=a,maxIndex=a; double min=series.Value(_trace.Samples[a]),max=min;
			for(int i=a+1;i<=b;++i){double value=series.Value(_trace.Samples[i]);if(value<min){min=value;minIndex=i;}if(value>max){max=value;maxIndex=i;}}
			foreach(int index in minIndex<=maxIndex?new[]{minIndex,maxIndex}:new[]{maxIndex,minIndex})
			{double value=series.Value(_trace.Samples[index]);line.Points.Add(new Point(x(_trace.Samples[index].ElapsedMs),y(value)));clipped|=value>yMax;}
		}
		GraphCanvas.Children.Add(line);
		if(clipped) AddText($"▲ {series.Name} spike above {yMax:0.##} ms",LeftMargin+8,TopMargin+4+GraphCanvas.Children.OfType<TextBlock>().Count(t=>t.Text.StartsWith("▲"))*14,10,series.Colour);
	}

	private void DrawEvents(Func<double,double> x, double height)
	{
		IEnumerable<PerformanceTraceEvent> events=_trace.Events.Where(e=>e.ElapsedMs>=_viewStart&&e.ElapsedMs<=_viewEnd);
		int drawn=0;
		foreach(var ev in events)
		{
			if(++drawn>400) break;
			Color colour=ev.Kind=="alarm"?(ev.Severity>=2?Color.FromRgb(255,75,75):Color.FromRgb(255,166,66)):ev.Kind=="cadence"?Color.FromRgb(255,105,105):ev.Kind=="marker"?Color.FromRgb(53,234,215):Color.FromRgb(135,145,160);
			double xx=x(ev.ElapsedMs);
			GraphCanvas.Children.Add(new Line { X1=xx,X2=xx,Y1=TopMargin,Y2=height-BottomMargin,Stroke=new SolidColorBrush(Color.FromArgb(145,colour.R,colour.G,colour.B)),StrokeThickness=ev.Kind=="marker"?1.5:1,StrokeDashArray=new DoubleCollection{2,4} });
		}
	}

	private void DrawHover(double elapsed, Func<double,double> x, Func<double,double> y, double height)
	{
		int index=Math.Clamp(LowerBound(elapsed),0,_trace.Samples.Count-1);
		if(index>0&&Math.Abs(_trace.Samples[index-1].ElapsedMs-elapsed)<Math.Abs(_trace.Samples[index].ElapsedMs-elapsed)) index--;
		PerformanceTraceSample s=_trace.Samples[index]; double xx=x(s.ElapsedMs);
		GraphCanvas.Children.Add(new Line { X1=xx,X2=xx,Y1=TopMargin,Y2=height-BottomMargin,Stroke=Brushes.White,StrokeThickness=1 });
		GraphCanvas.Children.Add(new Ellipse { Width=7,Height=7,Fill=new SolidColorBrush(Color.FromRgb(50,210,255)),Margin=new Thickness(xx-3.5,y(s.ActualMs)-3.5,0,0) });
		string clock=_trace.StartUtc is DateTimeOffset start ? start.AddMilliseconds(s.ElapsedMs).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff zzz") : FormatElapsed(s.ElapsedMs);
		string gpu=s.GpuPercent is double value?$" · GPU {value:0.0}%":"";
		string events=string.Join("; ",_trace.Events.Where(e=>Math.Abs(e.ElapsedMs-s.ElapsedMs)<=Math.Max(2,s.ActualMs)).Select(e=>e.Label).Take(3));
		HoverText.Text=$"{clock} · actual {s.ActualMs:0.000} ms · cadence {s.TargetMs:0.000} · display {s.DisplayMs:0.000} · app {s.AppMs:0.000} · wait {s.WaitMs:0.000} · submit {s.SubmitMs:0.000}{gpu}{(events.Length>0?$" · {events}":"")}";
	}

	private void DrawHorizontal(double yy,string label,Color colour,double width)
	{
		GraphCanvas.Children.Add(new Line{X1=LeftMargin,X2=width-RightMargin,Y1=yy,Y2=yy,Stroke=new SolidColorBrush(colour),StrokeDashArray=new DoubleCollection{5,5}});
		AddText(label,width-RightMargin-82,yy-14,9,colour);
	}
	private void AddText(string text,double left,double top,double size,Color colour){var block=new TextBlock{Text=text,FontSize=size,Foreground=new SolidColorBrush(colour)};Canvas.SetLeft(block,left);Canvas.SetTop(block,top);GraphCanvas.Children.Add(block);}
	private int LowerBound(double elapsed){int lo=0,hi=_trace.Samples.Count;while(lo<hi){int mid=(lo+hi)/2;if(_trace.Samples[mid].ElapsedMs<elapsed)lo=mid+1;else hi=mid;}return Math.Min(lo,_trace.Samples.Count-1);}
	private double MedianVisible(int first,int last,Func<PerformanceTraceSample,double> value){double[] v=_trace.Samples.Skip(first).Take(last-first+1).Select(value).Where(x=>x>0&&double.IsFinite(x)).Order().ToArray();return v.Length==0?0:v[v.Length/2];}
	private static double NiceCeiling(double value){double magnitude=Math.Pow(10,Math.Floor(Math.Log10(value)));double normalized=value/magnitude;double nice=normalized<=1?1:normalized<=2?2:normalized<=5?5:10;return nice*magnitude;}
	private static string FormatElapsed(double ms){TimeSpan t=TimeSpan.FromMilliseconds(Math.Max(0,ms));return t.TotalHours>=1?t.ToString(@"hh\:mm\:ss"):t.ToString(@"mm\:ss\.f");}

	private void ZoomIn_Click(object sender,RoutedEventArgs e)=>Zoom(.5,(_viewStart+_viewEnd)/2);
	private void ZoomOut_Click(object sender,RoutedEventArgs e)=>Zoom(2,(_viewStart+_viewEnd)/2);
	private void ResetView_Click(object sender,RoutedEventArgs e){_viewStart=_fullStart;_viewEnd=_fullEnd;_hoverElapsed=null;HoverText.Text="Wheel to zoom · drag to pan · hover for exact values";DrawGraph();}
	private void Zoom(double factor,double centre){double span=Math.Clamp((_viewEnd-_viewStart)*factor,25,_fullEnd-_fullStart);double ratio=(centre-_viewStart)/Math.Max(1,_viewEnd-_viewStart);_viewStart=centre-span*ratio;_viewEnd=_viewStart+span;ClampView();DrawGraph();}
	private void ClampView(){double span=_viewEnd-_viewStart;if(_viewStart<_fullStart){_viewStart=_fullStart;_viewEnd=_viewStart+span;}if(_viewEnd>_fullEnd){_viewEnd=_fullEnd;_viewStart=_viewEnd-span;}}
	private void GraphCanvas_MouseWheel(object sender,MouseWheelEventArgs e){double x=e.GetPosition(GraphCanvas).X;double ratio=Math.Clamp((x-LeftMargin)/Math.Max(1,GraphCanvas.ActualWidth-LeftMargin-RightMargin),0,1);Zoom(e.Delta>0?.72:1.38,_viewStart+ratio*(_viewEnd-_viewStart));}
	private void GraphCanvas_MouseLeftButtonDown(object sender,MouseButtonEventArgs e){_panning=true;_panOrigin=e.GetPosition(GraphCanvas);_panStart=_viewStart;_panEnd=_viewEnd;GraphCanvas.CaptureMouse();}
	private void GraphCanvas_MouseLeftButtonUp(object sender,MouseButtonEventArgs e){_panning=false;GraphCanvas.ReleaseMouseCapture();}
	private void GraphCanvas_MouseMove(object sender,MouseEventArgs e){Point p=e.GetPosition(GraphCanvas);if(_panning){double dx=p.X-_panOrigin.X;double shift=-dx/Math.Max(1,GraphCanvas.ActualWidth-LeftMargin-RightMargin)*(_panEnd-_panStart);_viewStart=_panStart+shift;_viewEnd=_panEnd+shift;ClampView();DrawGraph();return;}double ratio=Math.Clamp((p.X-LeftMargin)/Math.Max(1,GraphCanvas.ActualWidth-LeftMargin-RightMargin),0,1);_hoverElapsed=_viewStart+ratio*(_viewEnd-_viewStart);DrawGraph();}
	private void GraphCanvas_MouseLeave(object sender,MouseEventArgs e){if(!_panning){_hoverElapsed=null;HoverText.Text="Wheel to zoom · drag to pan · hover for exact values";DrawGraph();}}
	private void PreviousMarker_Click(object sender,RoutedEventArgs e)=>SelectMarker(-1);
	private void NextMarker_Click(object sender,RoutedEventArgs e)=>SelectMarker(1);
	private void SelectMarker(int direction){if(_trace.Markers.Count==0){MarkerText.Text="No user markers captured.";return;}_markerIndex=(_markerIndex+direction+_trace.Markers.Count)%_trace.Markers.Count;var marker=_trace.Markers[_markerIndex];double span=Math.Min(_viewEnd-_viewStart,Math.Max(1000,(_fullEnd-_fullStart)/20));_viewStart=marker.ElapsedMs-span/2;_viewEnd=marker.ElapsedMs+span/2;ClampView();_hoverElapsed=marker.ElapsedMs;MarkerText.Text=$"Marker {marker.Number} · {FormatElapsed(marker.ElapsedMs)}";DrawGraph();}
}
