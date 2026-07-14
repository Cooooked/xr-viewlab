using System;
using System.Globalization;
using System.IO;
using XRViewLab.UI;

string path=Path.Combine(Path.GetTempPath(),"ViewLab-performance-trace-fixture.csv");
File.WriteAllText(path,"ViewLabPerformanceTrace,1\nfrequency,10000000\nstart_qpc,10000000\n"+
	"type,sequence,qpc,elapsed_ms,actual_ms,target_ms,deviation_ms,app_ms,wait_ms,submit_ms,display_ms,marker\n"+
	"S,1,11000000,100.000000,11.200000,11.111000,0.089000,5.000000,4.000000,0.400000,11.111000,0\n"+
	"S,2,11100000,110.000000,16.700000,11.111000,5.589000,9.000000,4.000000,0.500000,11.111000,1\n"+
	"M,1,11050000,105.000000,,,,,,,,1\n");
try {
	var trace=PerformanceTrace.Load(path);
	if(trace.Samples.Count!=2||trace.Markers.Count!=1)throw new Exception("record counts changed");
	if(trace.Markers[0].Number!=1||Math.Abs(trace.Markers[0].ElapsedMs-105)>0.0001)throw new Exception("exact marker timestamp was lost");
	if(trace.Samples[1].Marker!=1||Math.Abs(trace.Samples[1].ActualMs-16.7)>0.0001)throw new Exception("marker/sample association was lost");
	Console.WriteLine("PASS: real trace samples and exact numbered marker round-trip");
} finally { File.Delete(path); }
