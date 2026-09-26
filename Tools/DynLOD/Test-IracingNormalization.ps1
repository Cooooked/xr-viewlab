param(
    [ValidateSet('Launch','Close','Cycle')]
    [string]$Mode = 'Cycle',
    [string]$IniPath = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'iRacing\rendererDX11OpenXR.ini'),
    [int]$StartupTimeoutSeconds = 90,
    [int]$IniWriteTimeoutSeconds = 30,
    [int]$CloseWindowTimeoutSeconds = 90,
    [int]$HoldSeconds = 3,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$uiProcessName = 'iRacingUI'
$simProcessName = 'iRacingSim64DX11'

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class DynLodNative {
    public delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
    [DllImport("user32.dll", EntryPoint="GetWindowThreadProcessId")] public static extern uint GetWindowProcessId(IntPtr hWnd, out uint processId);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint source, uint target, bool attach);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    public static bool CloseSimulatorWindow(uint processId) {
        bool sent = false;
        EnumWindows((window, parameter) => {
            uint owner;
            GetWindowProcessId(window, out owner);
            var title = new System.Text.StringBuilder(256);
            GetWindowText(window, title, title.Capacity);
            if (owner == processId && IsWindowVisible(window) && title.ToString().Contains("iRacing.com Simulator"))
                sent = PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero) || sent;
            return true;
        }, IntPtr.Zero);
        return sent;
    }
}
'@

function Get-LodSnapshot {
    param([string]$Path)
    $wanted = @('LODPctDynoMin','LODPctDynoMax','LODPctMin','LODPctMax','LODPctDynoMirrorsMin','LODPctDynoMirrorsMax','LODPctMirrorsMin','LODPctMirrorsMax')
    $result = [ordered]@{}
    $section = ''
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -match '^\s*\[([^]]+)\]') { $section = $Matches[1]; continue }
        if ($section -notin @('Graphics Options','Replay Graphics') -or $line -notmatch '^\s*([^=;]+)\s*=\s*([0-9]+)') { continue }
        $key = $Matches[1].Trim()
        if ($key -in $wanted) { $result["$section/$key"] = [int]$Matches[2] }
    }
    if ($result.Count -ne 16) { throw "Expected 16 DynLOD values in $Path; found $($result.Count)." }
    return $result
}

function Show-LodChanges($Before, $After) {
    Write-Output 'DynLOD changes:'
    $changed = 0
    foreach ($key in $Before.Keys) {
        $suffix = if ($Before[$key] -eq $After[$key]) { 'unchanged' } else { $changed++; 'CHANGED' }
        Write-Output ("  {0}: {1} -> {2} ({3})" -f $key,$Before[$key],$After[$key],$suffix)
    }
    Write-Output "Changed values: $changed"
}

function Get-SingleProcess([string]$Name) {
    @(Get-Process -Name $Name -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -Last 1)[0]
}

function Start-TestDrive {
    if (Get-SingleProcess $simProcessName) { throw 'The iRacing simulator is already running.' }
    $ui = Get-Process -Name $uiProcessName -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } |
        Sort-Object StartTime |
        Select-Object -Last 1
    if (-not $ui) { throw 'Open iRacing UI and leave the Test Drive confirmation window visible.' }
    $rect = New-Object DynLodNative+RECT
    if (-not [DynLodNative]::GetClientRect($ui.MainWindowHandle,[ref]$rect)) { throw 'Could not measure the iRacing UI window.' }
    $width=$rect.Right-$rect.Left; $height=$rect.Bottom-$rect.Top
    if ($width -lt 1200 -or $height -lt 650 -or [math]::Abs(($width/$height)-(2560/1438)) -gt 0.12) {
        throw "Unexpected iRacing client size ${width}x${height}; leave the supplied Test Drive confirmation visible and maximized."
    }
    # Measured from the currently visible desktop. The Electron client can report a larger
    # virtual client than the monitor, so client-height ratios do not identify this button.
    $point = New-Object DynLodNative+POINT
    $point.X = 1960
    $point.Y = 738
    Write-Output "Test Drive click: $($point.X),$($point.Y) in ${width}x${height} client"
    if ($DryRun) { return }
    [DynLodNative]::ShowWindow($ui.MainWindowHandle,9) | Out-Null
    $foreground=[DynLodNative]::GetForegroundWindow()
    $foregroundThread=[DynLodNative]::GetWindowThreadProcessId($foreground,[IntPtr]::Zero)
    $currentThread=[DynLodNative]::GetCurrentThreadId()
    $attached=$foregroundThread -ne 0 -and [DynLodNative]::AttachThreadInput($currentThread,$foregroundThread,$true)
    try {
        [DynLodNative]::BringWindowToTop($ui.MainWindowHandle) | Out-Null
        [DynLodNative]::SetForegroundWindow($ui.MainWindowHandle) | Out-Null
    } finally {
        if ($attached) { [DynLodNative]::AttachThreadInput($currentThread,$foregroundThread,$false) | Out-Null }
    }
    Start-Sleep -Milliseconds 500
    if ([DynLodNative]::GetForegroundWindow() -ne $ui.MainWindowHandle) { throw 'Windows did not allow the script to focus iRacing; no click was sent.' }
    [DynLodNative]::SetCursorPos($point.X,$point.Y) | Out-Null
    [DynLodNative]::mouse_event(0x0002,0,0,0,[UIntPtr]::Zero)
    [DynLodNative]::mouse_event(0x0004,0,0,0,[UIntPtr]::Zero)
    $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)
    do { Start-Sleep -Milliseconds 500; $sim = Get-SingleProcess $simProcessName } until ($sim -or (Get-Date) -ge $deadline)
    if (-not $sim) { throw 'The simulator did not start before the timeout. Check the Test Drive window and measured click position.' }
    Write-Output "Simulator started: PID $($sim.Id)"
}

function Stop-TestDrive {
    $sim = Get-SingleProcess $simProcessName
    if (-not $sim) { Write-Output 'Simulator is already closed.'; return }
    if ($DryRun) { Write-Output "Would close simulator PID $($sim.Id)"; return }
    $closed = $sim.CloseMainWindow()
    if (-not $closed) {
        $deadline = (Get-Date).AddSeconds($CloseWindowTimeoutSeconds)
        do {
            $closed = [DynLodNative]::CloseSimulatorWindow([uint32]$sim.Id)
            if (-not $closed) { Start-Sleep -Milliseconds 500 }
        } until ($closed -or (Get-Date) -ge $deadline)
    }
    if (-not $closed) { throw 'No simulator window accepted a graceful close request; the simulator was left running.' }
    if (-not $sim.WaitForExit(15000)) { throw 'The simulator did not close within 15 seconds; it was left running.' }
    Write-Output 'Simulator closed gracefully.'
}

if ($Mode -in @('Launch','Cycle')) {
    if (-not (Test-Path -LiteralPath $IniPath)) { throw "Renderer INI not found: $IniPath" }
    $beforeWrite = (Get-Item -LiteralPath $IniPath).LastWriteTimeUtc
    $beforeValues = Get-LodSnapshot $IniPath
    Start-TestDrive
    if (-not $DryRun -and $Mode -eq 'Cycle') {
        $deadline = (Get-Date).AddSeconds($IniWriteTimeoutSeconds)
        do { Start-Sleep -Milliseconds 500; $afterWrite=(Get-Item -LiteralPath $IniPath).LastWriteTimeUtc } until ($afterWrite -gt $beforeWrite -or (Get-Date) -ge $deadline)
        if ($afterWrite -le $beforeWrite) { throw 'The renderer INI was not rewritten before the timeout; simulator left running.' }
        Write-Output "Renderer INI rewritten: $afterWrite"
        Start-Sleep -Seconds $HoldSeconds
    }
}
if ($Mode -in @('Close','Cycle') -and -not ($DryRun -and $Mode -eq 'Cycle')) { Stop-TestDrive }
if ($Mode -eq 'Cycle' -and -not $DryRun) { Show-LodChanges $beforeValues (Get-LodSnapshot $IniPath) }
