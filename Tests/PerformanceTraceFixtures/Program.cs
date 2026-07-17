using System;
using System.Globalization;
using System.IO;
using System.Linq;
using XRViewLab.UI;

string path=Path.Combine(Path.GetTempPath(),"ViewLab-performance-trace-fixture.csv");
File.WriteAllText(path,"ViewLabPerformanceTrace,1\nfrequency,10000000\nstart_qpc,10000000\n"+
	"type,sequence,qpc,elapsed_ms,actual_ms,target_ms,deviation_ms,app_ms,wait_ms,submit_ms,display_ms,marker\n"+
	"S,1,11000000,100.000000,11.200000,11.111000,0.089000,5.000000,4.000000,0.400000,11.111000,0\n"+
	"S,2,11100000,110.000000,16.700000,11.111000,5.589000,9.000000,4.000000,0.500000,11.111000,1\n"+
	"M,1,11050000,105.000000,,,,,,,,1\n"+
	"S,3,11200000,120.000000,11.1,11.1,0,5,4,0.4,broken,0\n");
try {
	var trace=PerformanceTrace.Load(path);
	if(trace.Samples.Count!=2||trace.Markers.Count!=1)throw new Exception("record counts changed");
	if(trace.Markers[0].Number!=1||Math.Abs(trace.Markers[0].ElapsedMs-105)>0.0001)throw new Exception("exact marker timestamp was lost");
	if(trace.Samples[1].Marker!=1||Math.Abs(trace.Samples[1].ActualMs-16.7)>0.0001)throw new Exception("marker/sample association was lost");
	if(trace.Samples.Any(s=>s.Sequence==3))throw new Exception("partial checkpoint record was accepted");
	Console.WriteLine("PASS: schema 1 trace and exact numbered marker round-trip");
} finally { File.Delete(path); }

string v2Path=Path.Combine(Path.GetTempPath(),"ViewLab-performance-trace-v2-fixture.csv");
File.WriteAllText(v2Path,"ViewLabPerformanceTrace,2\nfrequency,10000000\nstart_qpc,10000000\nstart_utc_filetime,133801632000000000\n"+
	"type,sequence,qpc,elapsed_ms,actual_ms,target_ms,deviation_ms,app_ms,wait_ms,submit_ms,display_ms,marker,gpu_pct,warning_mask,critical_mask,visible_alarm_mask\n"+
	"S,1,10000000,0,11.111,11.111,0,3,5,1,11.111,0,75,0,0,0\n"+
	"S,2,10200000,20,24,11.111,12.889,4,6,1,11.111,0,91,2,0,2\n"+
	"S,3,10400000,40,11.111,11.111,0,3,5,1,11.111,0,74,0,0,0\n");
try {
	var trace=PerformanceTrace.Load(v2Path); var summary=trace.Analyze();
	if(trace.SchemaVersion!=2||trace.StartUtc is null||trace.Samples[1].GpuPercent!=91)throw new Exception("schema 2 metadata or GPU telemetry was lost");
	if(summary.CadenceMisses!=1||summary.GpuWarningPeriods!=1||summary.GpuWarningSeconds<=0)throw new Exception("summary statistics changed");
	if(!trace.Events.Any(e=>e.Kind=="alarm")||!trace.Events.Any(e=>e.Kind=="cadence"))throw new Exception("alarm/cadence events were not derived");
	Console.WriteLine("PASS: schema 2 telemetry, events and session summary round-trip");
} finally { File.Delete(v2Path); }
