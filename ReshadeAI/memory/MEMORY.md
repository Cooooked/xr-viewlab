# ReShadeAI Project Memory Index

Last updated: 2026-06-25 (session 2, end)

This folder lives at `F:\ReshadeAI\memory\` and is the authoritative handoff doc for any AI session continuing this work. Read this first, then the files you need.

## Files

- [project_goal.md](project_goal.md) — What we're building, current status, next steps
- [architecture_overview.md](architecture_overview.md) — System design, data flow, key structs
- [build_deploy.md](build_deploy.md) — How to build, deploy, and test
- [known_issues.md](known_issues.md) — Every bug fixed and how, plus active issues
- [user_profile.md](user_profile.md) — Who the user is and how they want to work
- [code_map.md](code_map.md) — Every changed file, what it does, key line ranges

## Current Status (2026-06-25 session 2 end)

**Working:**
- HMD overlay renders ImGui menu at correct size/position (OKB Kneeboard 8)
- Home key toggles HMD overlay on/off
- Desktop "ReShade VR Overlay" preview window shows menu content (always, no mode gate)
- Correct colors: sRGB swapchain (format 29), R/B swap in desktop preview
- Gameplay mode (default, no ViewLab): desktop runtime dormant — skips effects + non-overlay hotkeys
- Tuning mode (ViewLab sets xr_mode=1): desktop runtime fully active + Effect Runtime Sync
- Shared memory: `Local\ReShadeXRControl` with version, size, xr_mode, revision fields
- No per-frame logging, no per-frame heap allocs

**Not yet done:**
- Mouse/cursor interaction in preview window (state captured, not fed to ImGui)
- ViewLab companion app (writes shared memory to toggle Gameplay/Tuning)
- iRacing validation (Pistol Whip is current test target)
- Portable payload packaging
