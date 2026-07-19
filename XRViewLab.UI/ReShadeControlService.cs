using System;
using System.Runtime.InteropServices;
using System.Globalization;
using System.IO;
using System.Text;
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
	private static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XR ViewLab", "xr-viewlab.ini");

    public bool Connected => _connected;

	public event Action<bool>? ConnectionChanged;

    public bool TryConnect()
    {
        Disconnect();

        // Create (or open existing) shared memory — ViewLab creates it so its settings
        // survive the DLL creating defaults when a game launches later.
        _hMap = CreateFileMappingW(new IntPtr(-1), IntPtr.Zero,
            0x04 /* PAGE_READWRITE */, 0, ExpectedSize, MappingName);
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
            // Fresh mapping — init with ViewLab defaults
            block = new XRControlBlock
            {
                version = 1,
                size = ExpectedSize,
                xr_mode = 0,
                revision = 0,
                // Start the desktop preview as an ordinary focusable window. Headless/borderless
                // remains an explicit user choice, rather than silently disabling text entry.
                win_headless = 0,
                win_always_on_top = 1,
                win_snap_cursor = 0,
                menu_visible = 1,
                quad_edit_mode = 0,
                heartbeat = 0,
                quad_pos_x = 0, quad_pos_y = 0, quad_pos_z = float.NaN,
                quad_quat_x = 0, quad_quat_y = 0, quad_quat_z = 0, quad_quat_w = 1,
                quad_width = 0.32f, quad_height = 0.30f, quad_alpha = 1
            };
            WriteBlock(ref block);
        }

		ApplyPersistedPreferences(ref block);
		WriteBlock(ref block);

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
		PersistPreferences(block);
    }

	private static void ApplyPersistedPreferences(ref XRControlBlock block)
	{
		block.xr_mode = ReadPreference("xr_mode", FactoryBaseline.ReShadeRemote["xr_mode"]);
		block.menu_visible = ReadPreference("menu_visible", FactoryBaseline.ReShadeRemote["menu_visible"]);
		block.win_headless = ReadPreference("win_headless", FactoryBaseline.ReShadeRemote["win_headless"]);
		block.win_always_on_top = ReadPreference("win_always_on_top", FactoryBaseline.ReShadeRemote["win_always_on_top"]);
	}

	private static uint ReadPreference(string key, uint fallback)
	{
		var value = new StringBuilder(32);
		GetPrivateProfileStringW("Settings", "reshade_remote_" + key, fallback.ToString(CultureInfo.InvariantCulture), value, (uint)value.Capacity, ConfigPath);
		return uint.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed) ? parsed : fallback;
	}

	private static void PersistPreferences(XRControlBlock block)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
		WritePrivateProfileStringW("Settings", "reshade_remote_xr_mode", block.xr_mode.ToString(CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileStringW("Settings", "reshade_remote_menu_visible", block.menu_visible.ToString(CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileStringW("Settings", "reshade_remote_win_headless", block.win_headless.ToString(CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileStringW("Settings", "reshade_remote_win_always_on_top", block.win_always_on_top.ToString(CultureInfo.InvariantCulture), ConfigPath);
	}

	public bool CheckMappingExists()
	{
		IntPtr h = OpenFileMappingW(0x000F001F, false, MappingName);
		if (h != IntPtr.Zero)
		{
			CloseHandle(h);
			return true;
		}
		return false;
	}

	public void Dispose()
	{
		Disconnect();
	}

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFileMappingW(IntPtr hFile, IntPtr lpAttributes, uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenFileMappingW(uint dwDesiredAccess, bool bInheritHandle, string lpName);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern uint GetPrivateProfileStringW(string section, string key, string defaultValue, StringBuilder value, uint size, string filePath);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern bool WritePrivateProfileStringW(string section, string key, string? value, string filePath);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
}
