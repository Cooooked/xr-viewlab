using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;

namespace XRViewLab.UI;

internal sealed record NotificationLiveSnapshot(
    string ProfileKey, bool Authoritative, bool Enabled, bool ShowIcon, bool ShowImage,
    bool AllowlistMode, bool MediaEnabled, double X, double Y, double Scale, double Opacity,
    double DurationMs, double Resolution, int MaxVisible, int Privacy, int Theme, int Palette,
    string Filters);

internal static class NotificationLiveStatePolicy
{
    internal static bool Applies(NotificationLiveSnapshot snapshot, string? activeProfileKey, bool activeProfileOverridesNotifications)
    {
        if (snapshot.Authoritative)
            return !string.IsNullOrWhiteSpace(activeProfileKey) &&
                string.Equals(snapshot.ProfileKey, activeProfileKey, StringComparison.OrdinalIgnoreCase);
        return string.IsNullOrEmpty(snapshot.ProfileKey) && !activeProfileOverridesNotifications;
    }
}

internal sealed class NotificationLiveStateService : IDisposable
{
    internal const string MappingName = "Local\\XRViewLabNotificationSettingsV1";
    internal const int Size = 1344;
    internal const uint Magic = 0x314E4C56; // VLN1
    internal const uint Version = 1;
    private readonly MemoryMappedFile _map = MemoryMappedFile.CreateOrOpen(MappingName, Size, MemoryMappedFileAccess.ReadWrite);
    private readonly MemoryMappedViewAccessor _view;
    private uint _generation;

    internal NotificationLiveStateService()
    {
        _view = _map.CreateViewAccessor(0, Size, MemoryMappedFileAccess.ReadWrite);
        _view.Write(0, Magic); _view.Write(4, Version); _view.Write(8, (uint)Size);
        _view.Write(16, (uint)Environment.ProcessId);
    }

    internal void Publish(NotificationLiveSnapshot value)
    {
        uint flags = (value.Enabled ? 1u : 0u) | (value.ShowIcon ? 2u : 0u) | (value.ShowImage ? 4u : 0u) |
            (value.AllowlistMode ? 8u : 0u) | (value.MediaEnabled ? 16u : 0u) | (value.Authoritative ? 32u : 0u);
        _view.Write(20, flags);
        WriteFixedString(_view, 24, 128, value.ProfileKey);
        _view.Write(280, (float)value.X); _view.Write(284, (float)value.Y);
        _view.Write(288, (float)value.Scale); _view.Write(292, (float)value.Opacity);
        _view.Write(296, (float)value.DurationMs); _view.Write(300, (float)value.Resolution);
        _view.Write(304, (uint)Math.Clamp(value.MaxVisible, 1, 6));
        _view.Write(308, (uint)Math.Clamp(value.Privacy, 0, 2));
        _view.Write(312, (uint)Math.Clamp(value.Theme, 0, 3));
        _view.Write(316, (uint)Math.Clamp(value.Palette, 0, 4));
        WriteFixedString(_view, 320, 512, value.Filters);
        Thread.MemoryBarrier();
        _view.Write(12, unchecked(++_generation));
    }

    internal static void WriteFixedString(MemoryMappedViewAccessor view, int offset, int characters, string? value)
    {
        string text = value ?? string.Empty;
        int count = Math.Min(text.Length, characters - 1);
        for (int i = 0; i < characters; ++i) view.Write(offset + i * 2, i < count ? text[i] : '\0');
    }

    public void Dispose() { _view.Dispose(); _map.Dispose(); }
}

internal sealed class NotificationLiveStateReader : IDisposable
{
    private MemoryMappedFile? _map;
    private MemoryMappedViewAccessor? _view;
    private uint _generation;
    private long _nextConnectTick;
    internal NotificationLiveSnapshot? Current { get; private set; }

    internal bool Poll()
    {
        if (_view == null)
        {
            long now = Environment.TickCount64;
            if (now < _nextConnectTick) return false;
            _nextConnectTick = now + 1000;
            try
            {
                _map = MemoryMappedFile.OpenExisting(NotificationLiveStateService.MappingName, MemoryMappedFileRights.Read);
                _view = _map.CreateViewAccessor(0, NotificationLiveStateService.Size, MemoryMappedFileAccess.Read);
            }
            catch (FileNotFoundException) { CloseMapping(); return ClearCurrent(); }
            catch (UnauthorizedAccessException) { CloseMapping(); return ClearCurrent(); }
        }

        if (_view.ReadUInt32(0) != NotificationLiveStateService.Magic ||
            _view.ReadUInt32(4) != NotificationLiveStateService.Version ||
            _view.ReadUInt32(8) != NotificationLiveStateService.Size)
            return false;

        uint ownerProcessId = _view.ReadUInt32(16);
        if (!ProcessExists(ownerProcessId)) { CloseMapping(); return ClearCurrent(); }
        uint generation = _view.ReadUInt32(12);
        if (generation == 0 || generation == _generation) return false;

        uint flags = _view.ReadUInt32(20);
        string profileKey = ReadFixedString(_view, 24, 128);
        var snapshot = new NotificationLiveSnapshot(profileKey, (flags & 32u) != 0,
            (flags & 1u) != 0, (flags & 2u) != 0, (flags & 4u) != 0,
            (flags & 8u) != 0, (flags & 16u) != 0,
            _view.ReadSingle(280), _view.ReadSingle(284), _view.ReadSingle(288), _view.ReadSingle(292),
            _view.ReadSingle(296), _view.ReadSingle(300), (int)_view.ReadUInt32(304),
            (int)_view.ReadUInt32(308), (int)_view.ReadUInt32(312), (int)_view.ReadUInt32(316),
            ReadFixedString(_view, 320, 512));
        Thread.MemoryBarrier();
        if (generation != _view.ReadUInt32(12)) return false;
        _generation = generation; Current = snapshot; return true;
    }

    private bool ClearCurrent() { if (Current == null) return false; Current = null; _generation = 0; return true; }
    private static bool ProcessExists(uint processId)
    {
        if (processId == 0) return false;
        try { using Process process = Process.GetProcessById((int)processId); return !process.HasExited; }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
    private static string ReadFixedString(MemoryMappedViewAccessor view, int offset, int characters)
    {
        byte[] bytes = new byte[characters * 2]; view.ReadArray(offset, bytes, 0, bytes.Length);
        return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
    }
    private void CloseMapping() { _view?.Dispose(); _map?.Dispose(); _view = null; _map = null; }
    public void Dispose() { CloseMapping(); }
}
