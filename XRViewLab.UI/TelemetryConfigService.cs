using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace XRViewLab.UI;

// Versioned extension for the telemetry catalogue. Kept separate from the 208-byte overlay
// contract so future metrics do not become another exercise in binary Tetris.
internal sealed class TelemetryConfigService : IDisposable
{
    private const int Size = 576;
    private readonly MemoryMappedFile _map = MemoryMappedFile.CreateOrOpen("Local\\XRViewLabTelemetryConfigV1", Size, MemoryMappedFileAccess.ReadWrite);
    private readonly MemoryMappedViewAccessor _view;
    private uint _generation;
    public TelemetryConfigService() { _view=_map.CreateViewAccessor(0,Size,MemoryMappedFileAccess.ReadWrite);_view.Write(0,0x31435456u);_view.Write(4,2u);_view.Write(8,(uint)Size); }
    public void Publish(IReadOnlyList<HudWidgetOption> widgets, int maxPerRow, string? liveProfileKey, string? networkProbeTarget)
    {
        ulong mask=0; uint symbolMask=0; uint unitHiddenMask=0; Span<byte> order=stackalloc byte[16]; order.Fill(0xFF);
        for(int i=0;i<widgets.Count&&i<16;++i){order[i]=(byte)widgets[i].MetricId;if(widgets[i].Enabled)mask|=1UL<<widgets[i].MetricId;if(widgets[i].UseSymbol)symbolMask|=1u<<widgets[i].MetricId;if(widgets[i].HasUnit&&!widgets[i].ShowUnit)unitHiddenMask|=1u<<widgets[i].MetricId;}
        _view.Write(16,mask);for(int i=0;i<16;++i)_view.Write(24+i,order[i]);
        // unitHiddenMask at offset 56 (reserved[0]): bit set = hide that metric's unit. Zero (legacy/absent) = all
        // units shown, so an old native layer that ignores this field keeps the prior appearance. No version bump.
        HudWidgetOption? sys=widgets.Count>0?System.Linq.Enumerable.FirstOrDefault(widgets,w=>w.Id=="sys"):null;
        _view.Write(40,(uint)Math.Clamp(maxPerRow,1,16));_view.Write(44,(float)(sys?.Warning??30));_view.Write(48,(float)(sys?.Critical??10));_view.Write(52,symbolMask);_view.Write(56,unitHiddenMask);
        WriteFixedString(64,128,liveProfileKey);
        Span<float> warning=stackalloc float[16];Span<float> critical=stackalloc float[16];
        foreach(HudWidgetOption widget in widgets)if(widget.MetricId is >=0 and <16){warning[widget.MetricId]=(float)widget.Warning;critical[widget.MetricId]=(float)widget.Critical;}
        for(int i=0;i<16;++i){_view.Write(320+i*4,warning[i]);_view.Write(384+i*4,critical[i]);}
        WriteFixedString(448,64,networkProbeTarget);
        Thread.MemoryBarrier();_view.Write(12,unchecked(++_generation));
    }
    private void WriteFixedString(int offset,int characters,string? value){string text=value??string.Empty;int count=Math.Min(text.Length,characters-1);for(int i=0;i<characters;++i)_view.Write(offset+i*2,i<count?text[i]:'\0');}
    public void Dispose(){_view.Dispose();_map.Dispose();}
}
