using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace XRViewLab.UI;

internal sealed class HistoryService
{
    internal const int MaxRecords = 512;
    internal const int MaxBytes = 512 * 1024;
    internal static readonly TimeSpan Retention = TimeSpan.FromDays(14);
    private readonly object _gate = new();
    private readonly string _path;
    private readonly List<Entry> _entries = new();
    internal sealed record Entry(DateTimeOffset TimeUtc, string Category, string Event, string? Source, string? Title, string? Detail);

    public HistoryService(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XR ViewLab", "history.jsonl");
        LoadAndTrim();
    }

    public void Record(string category, string eventName, string? source = null, string? title = null, string? detail = null)
    {
        try { lock (_gate) { _entries.Add(new Entry(DateTimeOffset.UtcNow, Limit(category,48)!, Limit(eventName,64)!, Limit(source,96), Limit(title,160), Limit(detail,256))); Trim(); WriteAtomic(); } }
        catch { /* history failure never affects telemetry, notifications or rendering */ }
    }

    public void Clear()
    {
        try { lock (_gate) { _entries.Clear(); if (File.Exists(_path)) File.Delete(_path); } } catch { }
    }

    private void LoadAndTrim()
    {
        try { if (File.Exists(_path)) foreach (string line in File.ReadLines(_path)) try { Entry? e=JsonSerializer.Deserialize<Entry>(line); if(e!=null)_entries.Add(e); } catch { } Trim(); WriteAtomic(); }
        catch { _entries.Clear(); }
    }
    private void Trim()
    {
        DateTimeOffset cutoff=DateTimeOffset.UtcNow-Retention; _entries.RemoveAll(e=>e.TimeUtc<cutoff);
        if(_entries.Count>MaxRecords)_entries.RemoveRange(0,_entries.Count-MaxRecords);
        while(_entries.Count>1&&EncodedSize()>MaxBytes)_entries.RemoveAt(0);
    }
    private int EncodedSize()=>_entries.Sum(e=>Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(e))+1);
    private void WriteAtomic(){Directory.CreateDirectory(Path.GetDirectoryName(_path)!);string temp=_path+".tmp";File.WriteAllLines(temp,_entries.Select(e=>JsonSerializer.Serialize(e)),new UTF8Encoding(false));File.Move(temp,_path,true);}
    private static string? Limit(string? value,int max){if(string.IsNullOrWhiteSpace(value))return null;string v=value.Trim();return v.Length<=max?v:v[..max];}
}
