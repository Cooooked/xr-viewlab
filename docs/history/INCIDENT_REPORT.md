# ViewLab Topmost incident — 2026-07-13

## Verdict

ViewLab caused an unsafe user-mode graphics failure cascade during the DiRT Rally 2 menu-to-car
transition. Its Topmost backend repeatedly destroyed and recreated a large OpenXR swapchain after
allocation began failing. The loop continued 193 times in about 21 seconds and overlapped the AMD
device removal and DWM restart loop. This was not a kernel bugcheck, but it did take down the user-mode
display stack badly enough to blank applications, disconnect a monitor, and require a reboot.

## Timeline (Australia/Brisbane)

| Time | Evidence |
|---|---|
| 14:56:47 | DiRT Rally session started; ViewLab D3D11 renderer initialized. |
| 14:56:52 | Topmost projection swapchain created at 3630×1763, format 29. |
| 14:56:52–14:57:18 | ViewLab repeatedly recreated Topmost for changing submitted rectangles, including a 1763/1764 one-pixel oscillation. |
| 14:57:18.677 | ReShade first recorded `DXGI_ERROR_DEVICE_REMOVED`; removal reason was `DXGI_ERROR_DRIVER_INTERNAL_ERROR`. ViewLab recorded Topmost RTV creation failure in the same millisecond. |
| 14:57:18.690–14:57:39.808 | ViewLab attempted another Topmost swapchain 193 times; VDXR returned `XR_ERROR_RUNTIME_FAILURE`. |
| 14:57:36–14:57:38 | `dwm.exe`/`dwmcore.dll` failed and restarted eight times (`0xc00001ad`, then `0x8007001f`). |
| 14:57:48 | Brave hung while desktop composition was impaired. |
| 14:58:02–14:58:03 | ViewLab UI failed in WPF composition with `UCEERR_RENDERTHREADFAILURE`, downstream of DWM failure. |
| 15:02:49 | User initiated an orderly reboot. |

## System evidence

- GPU: AMD Radeon RX 7800 XT; driver `32.0.22021.1009`.
- OS registry: Windows 10 22H2, build `19045.7417`.
- No Event 4101, Kernel-Power 41, BugCheck 1001, WHEA report, LiveKernelReport, or minidump was
  recorded for this interval. Thus this was a confirmed D3D device removal/user-mode display-stack
  collapse, not a captured kernel TDR or BSOD.
- DISM health scans and `sfc /verifyonly` could not run from the non-elevated agent session (error
  740). No repair command was attempted.
- Exported event/log evidence is indexed at `diagnostics/topmost-incident-20260713/README.md`;
  large EVTX and log files remain ignored by Git.

## Confidence and alternatives

Confidence is high that ViewLab's allocation churn and unbounded retry loop turned the transition
failure into the desktop-wide incident. AMD's user-mode driver and ReShade were involved in the same
process and may affect the first device-removal trigger, but neither explains ViewLab's independently
confirmed 193 retries. The fix therefore removes both ViewLab hazards regardless of the first mover.
