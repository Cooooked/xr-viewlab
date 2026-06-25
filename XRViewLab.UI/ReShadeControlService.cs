using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace XRViewLab.UI;

public sealed class ReShadeControlService : IDisposable
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct XRControlBlock
    {
        public uint version;
        public uint size;
        public uint xr_mode;
        public uint revision;
        public uint win_headless;
        public uint win_always_on_top;
        public uint win_snap_cursor;
        public uint menu_visible;
        public uint quad_edit_mode;
        public uint heartbeat;
        public float quad_pos_x;
        public float quad_pos_y;
        public float quad_pos_z;
        public float quad_quat_x;
        public float quad_quat_y;
        public float quad_quat_z;
        public float quad_quat_w;
        public float quad_width;
        public float quad_height;
        public float quad_alpha;
    }

    private const string MappingName = "Local\\ReShadeXRControl";
    private static readonly uint ExpectedSize = (uint)Marshal.SizeOf<XRControlBlock>();

    private IntPtr _hMap;
    private IntPtr _view;
    private bool _connected;

    public bool Connected => _connected;

	public event Action<bool> ConnectionChanged;

    public bool TryConnect()
    {
        Disconnect();

        _hMap = OpenFileMappingW(0x000F001F, false, MappingName);
        if (_hMap == IntPtr.Zero)
            return false;

        _view = MapViewOfFile(_hMap, 0x000F001F, 0, 0, (UIntPtr)ExpectedSize);
        if (_view == IntPtr.Zero)
        {
            CloseHandle(_hMap);
            _hMap = IntPtr.Zero;
            return false;
        }

        var block = ReadBlock();
        if (block.version != 1 || block.size != ExpectedSize)
        {
            Disconnect();
            return false;
        }

        _connected = true;
        ConnectionChanged?.Invoke(true);
        return true;
    }

    public void Disconnect()
    {
        _connected = false;
        if (_view != IntPtr.Zero) { UnmapViewOfFile(_view); _view = IntPtr.Zero; }
        if (_hMap != IntPtr.Zero) { CloseHandle(_hMap); _hMap = IntPtr.Zero; }
        ConnectionChanged?.Invoke(false);
    }

    public XRControlBlock ReadBlock()
    {
        if (_view == IntPtr.Zero)
            return default;
        return Marshal.PtrToStructure<XRControlBlock>(_view);
    }

    public void WriteBlock(ref XRControlBlock block)
    {
        if (_view == IntPtr.Zero) return;
        block.revision++;
        Marshal.StructureToPtr(block, _view, false);
    }

    public void Dispose()
    {
        Disconnect();
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenFileMappingW(uint dwDesiredAccess, bool bInheritHandle, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
}
