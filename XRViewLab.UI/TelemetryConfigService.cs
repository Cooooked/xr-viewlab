using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace XRViewLab.UI;

// Versioned extension for the telemetry catalogue. Kept separate from the 208-byte overlay
// contract so future metrics do not become another exercise in binary Tetris.
internal sealed class TelemetryConfigService : IDisposable
{
    private const int Size = 64;
    private readonly MemoryMappedFile _map = MemoryMappedFile.CreateOrOpen("Local\\XRViewLabTelemetryConfigV1", Size, MemoryMappedFileAccess.ReadWrite);
    private readonly MemoryMappedViewAccessor _view;
    private uint _generation;
    public TelemetryConfigService() { _view=_map.CreateViewAccessor(0,Size,MemoryMappedFileAccess.ReadWrite);_view.Write(0,0x31435456u);_view.Write(4,1u);_view.Write(8,(uint)Size); }
    public void Publish(IReadOnlyList<HudWidgetOption> widgets, int maxPerRow, double sysWarning, double sysCritical)
    {
        ulong mask=0; Span<byte> order=stackalloc byte[16]; order.Fill(0xFF);
        for(int i=0;i<widgets.Count&&i<16;++i){order[i]=(byte)widgets[i].MetricId;if(widgets[i].Enabled)mask|=1UL<<widgets[i].MetricId;}
        _view.Write(16,mask);for(int i=0;i<16;++i)_view.Write(24+i,order[i]);
        _view.Write(40,(uint)Math.Clamp(maxPerRow,1,16));_view.Write(44,(float)sysWarning);_view.Write(48,(float)sysCritical);
        Thread.MemoryBarrier();_view.Write(12,unchecked(++_generation));
    }
    public void Dispose(){_view.Dispose();_map.Dispose();}
}
