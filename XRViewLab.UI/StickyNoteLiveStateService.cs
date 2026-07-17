using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace XRViewLab.UI;

// Bounded collection contract with dllmain.cpp. Text is fixed-width UTF-16 so publication remains
// allocation-free and generation-last, while each note retains the common overlay placement model.
internal sealed class StickyNoteLiveStateService : IDisposable
{
    internal const int MaxNotes = 8;
    internal const int MaxText = 120;
    private const int HeaderSize = 20;
    private const int RecordSize = 264;
    private const int Size = HeaderSize + MaxNotes * RecordSize;
    private const uint Magic = 0x314E5356; // VSN1
    private readonly MemoryMappedFile _map = MemoryMappedFile.CreateOrOpen("Local\\XRViewLabStickyNotes", Size, MemoryMappedFileAccess.ReadWrite);
    private readonly MemoryMappedViewAccessor _view;
    private uint _generation;

    public StickyNoteLiveStateService()
    {
        _view = _map.CreateViewAccessor(0, Size, MemoryMappedFileAccess.ReadWrite);
        _view.Write(0, Magic); _view.Write(4, 1u); _view.Write(8, (uint)Size);
    }

    public void Publish(bool enabled, IReadOnlyList<StickyNoteOption> notes)
    {
        int count = Math.Min(notes.Count, MaxNotes);
        _view.Write(16, enabled ? 1u : 0u);
        for (int i = 0; i < MaxNotes; ++i)
        {
            int offset = HeaderSize + i * RecordSize;
            StickyNoteOption? note = i < count ? notes[i] : null;
            _view.Write(offset, note?.Enabled == true ? 1u : 0u);
            _view.Write(offset + 4, (float)(note?.X ?? 0)); _view.Write(offset + 8, (float)(note?.Y ?? 0));
            _view.Write(offset + 12, (float)(note?.Scale ?? 1)); _view.Write(offset + 16, (float)(note?.Opacity ?? 0));
            _view.Write(offset + 20, (uint)Math.Clamp(note?.Theme ?? 0, 0, 4));
            string text = Normalize(note?.Text);
            for (int c = 0; c < MaxText; ++c) _view.Write(offset + 24 + c * 2, c < text.Length ? text[c] : '\0');
        }
        Thread.MemoryBarrier();
        _view.Write(12, unchecked(++_generation));
    }

    internal static string Normalize(string? value)
    {
        string text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= MaxText ? text : text[..MaxText];
    }

    public void Dispose() { _view.Dispose(); _map.Dispose(); }
}
