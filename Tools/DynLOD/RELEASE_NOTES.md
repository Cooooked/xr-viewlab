# iRacing DynLOD 1.1.0

- Added World and Cars preset selectors for Main and Replay, matching iRacing's Maximum, Medium, Minimum, Decrease, Increase and Off values.
- Retained all four numeric Min/Max ranges for Custom tuning.
- Corrected the labels to iRacing's observed mapping: World uses `LODPctDyno*`; Cars uses plain `LODPct*`.
- Added preset, mirror-value and key-family mapping checks.

# iRacing DynLOD 1.0.0

First public release of a compact, portable Dynamic LOD editor for iRacing.

- Direct World/Car Min/Max controls for Main and Mirrors.
- Gentle snapping near multiples of 25, with unsnapped numeric entry.
- Literal FPS target, independent Main/Replay apply actions and read-only Refresh.
- Fresh-file merging and timestamped backups beside the INI.
- Compact 260 × 285 interface with a dedicated Windows icon and softer controls.
- Self-contained Windows x64 executable; no installer needed.

Download the Windows ZIP, extract it and launch **iRacing-DynLOD.exe**. The adjacent `.sha256` file verifies the ZIP download.

This is a focused companion to iRSidekick Profiles, not a replacement. Close the sim when editing for predictable persistence.

Validation: 37 automated INI and WPF checks passed. The user confirmed that the app edits the INI correctly.

