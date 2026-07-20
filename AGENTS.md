# ViewLab — AI Operating Manual

Canonical entry point for every coding agent (Claude, Codex, or other). Read this file,
then `STATE.md`, then start working. Everything else is read on demand via the index below.

**One folder, one truth:** the repo is `F:\AI-Projects\ViewLab`. Never depend on files outside it.
Repository memory beats conversation memory: if this file or `STATE.md` disagrees with what you
remember or assume, the repo wins. If the repo is stale, fix the repo immediately.

## What ViewLab is

An OpenXR **implicit API layer** (`XR_APILAYER_cooooked_xrviewlab`, native C++ in `dllmain.cpp`)
plus a **WPF settings app** (`XRViewLab.UI/`). The layer crops the vertical/horizontal FOV and
recommended render resolution reported to VR games (GPU savings), and draws a black "visor" mask
directly into the game's eye textures via D3D11 (Technique C Direct). Config flows UI → ini +
per-app registry → DLL. Ships as an MSI. Primary test game: Pistol Whip via Virtual Desktop (VDXR).

## Compatibility design goal

ViewLab targets universal compatibility by design. One central capability plan selects how every
ViewLab feature is presented from the current graphics binding, swapchains and submitted frame/layer
structure. Application names, executable names and title allowlists are forbidden as compatibility policy.
Native OpenXR remains the direct product path. Legacy API translation moves behind the compiled
`ViewLabBridge` boundary until games need only ViewLab plus their chosen OpenXR runtime. Missing capabilities
degrade individual features with explicit diagnostics; they never classify an entire game as unsupported.
Finite game/hardware matrices are regression evidence, not proof of universal coverage.

## Git workflow

`dev` is the single active branch for ordinary AI-assisted work; `main` is the integration branch and is
updated regularly from `dev`. Start and finish routine changes on `dev` (or a task-specific worktree branch),
then integrate them into `main` once the required contracts, build and user-directed validation are complete.
Fast-forward when `main` has not moved; otherwise merge. After integrating into `main`, fast-forward `dev` back
up to `main` so it never drifts stale — but ONLY when `dev`'s worktree is clean; if another agent has uncommitted
work there, let them commit first and never overwrite it. Create experiment or feature branches only when the
user explicitly requests one, and delete a merged task branch once its work is on `main`. Never push to the
GitHub remote without the user confirming an in-headset test passed. Never force-push, rewrite history, or
delete unmerged branches without explicit user approval.

## Operating rules (non-negotiable)

1. **Build + delivery:** after any implementation change run `.\build.ps1` (auto-bumps version,
   builds WPF + x64/Win32 layer + MSI into `dist\`). The final reply must contain the exact MSI
   path in a plain text block, e.g. `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.105.msi`. Never just
   the folder.
2. **Test before release:** the user tests each build in-headset. NEVER push to GitHub or publish
   a release without the user confirming the build works.
3. **Contracts:** `Tests\Verify-ViewLabContracts.ps1` must pass before committing. It pins the
   UI↔DLL config-key wiring, geometry parity, and past regression fixes.
4. **Documentation synchronization contract:** a behavioral change is NOT complete until the
   canonical memory is updated **in the same commit**: `STATE.md` always (+ `CHANGELOG.md` if
   user-visible); config keys → `docs/CONFIG.md`; permanent decisions → `docs/DECISIONS.md`;
   regressions → `docs/REGRESSIONS.md`; subsystem shape → `docs/ARCHITECTURE.md` +
   `repo-index.json`. If a commit updates no canonical memory, the commit message must say why
   (docs-only / comment-only / build-artifact-only). Never create new status/handoff/session/
   summary documents — update the canonical ones.
5. **Git safety:** NEVER run `git restore` / `git checkout <file>` / `git reset` / `git revert` /
   `git stash` or delete uncommitted changes without explicit user approval. Uncommitted work or
   a recent MSI in `dist\` may be the real working state, not git HEAD. When the user reports a
   regression: stop, ask which build worked, check `dist\` and `backups\` before touching git.
6. **Scope discipline:** one bug, one fix, one commit. Prove root cause before patching. No
   speculative edits, no drive-by refactors, no renames unless asked. If you find an unrelated
   bug, record it in `STATE.md` → Known issues instead of fixing it now.
7. **Agent workspaces:** never create a project-local `.claude/`, `.codex/`, `.agents/` or similar
   memory directory. Tool memory lives outside the repo; repo memory lives in these docs.
8. **VR testing:** check headset/runtime state before launching games.
   `XR_ERROR_FORM_FACTOR_UNAVAILABLE` = headset not streaming — stop and tell the user.

## Reading policy (context economy)

Read metadata before source. **`repo-index.json` is the machine index**: subsystem → files →
symbols → tests → docs → keywords. Answer "where does X live" from it, then grep the symbol.
The two big files are expensive; open them **only for the function you need**, located by
symbol search, never end-to-end "to get familiar":

| File | Lines | Cost | Open when |
|---|---|---|---|
| `dllmain.cpp` | ~3000 | HIGH | editing native behavior — jump to the symbol via grep |
| `XRViewLab.UI/MainWindow.cs` | ~2200 | HIGH | editing UI logic/persistence — jump to symbol |
| everything else | <700 | ok | as needed |

`docs/ARCHITECTURE.md` maps every subsystem to its owning symbols so you can grep instead of read.

## Navigation index

| Question | Answer lives in |
|---|---|
| Where does subsystem/symbol/test X live? (machine index) | `repo-index.json` |
| What's the current version / active work / known issues? | `STATE.md` |
| How does the system work? Where does subsystem X live? | `docs/ARCHITECTURE.md` |
| What does config key X do? Who reads/writes it? | `docs/CONFIG.md` |
| Why is it built this way? | `docs/DECISIONS.md` |
| What broke before and must never break again? | `docs/REGRESSIONS.md` |
| Why do crop edges smear? (closed: VD fixed foveation) | `docs/FIXED_FOVEATION.md` (summary), `docs/EDGE_SMEAR_INVESTIGATION.md` (full record) |
| How do I verify behavior in the headset? | `docs/VERIFICATION.md` |
| What shipped when? | `CHANGELOG.md` |
| Older research/plans/journals (rarely needed) | `docs/history/` |
| ReShade Remote payload internals | `ReShadePayload/Docs/` |
| Backup inventory / recovery sources | `docs/history/SOURCE_BACKUP.md` |

## Source map

| Path | Owns |
|---|---|
| `dllmain.cpp` | the entire native layer: OpenXR hooks, FOV/resolution crop, D3D11 visor renderer, visibility-mask stencil filter, config/registry reads |
| `XR_APILAYER_cooooked_xrviewlab.json` | implicit layer manifest (installer registers x64+Win32 under HKLM Khronos ApiLayers) |
| `XRViewLab.UI/MainWindow.cs` + `MainWindow.xaml` | settings UI, ini/registry persistence, app list, update check |
| `XRViewLab.UI/BeanMaskEditor.cs` | visor preview canvas + draggable pins — **reference spec for native geometry** (must stay formula-identical to `dllmain.cpp`) |
| `XRViewLab.UI/ProfileWindow.cs` + `ProfileWindow.xaml` | per-app profile editor (registry DWORDs) |
| `XRViewLab.UI/AppProfile.cs` | app row model |
| `XRViewLab.UI/ReShadeRemoteWindow.cs`, `ReShadeControlService.cs` | Advanced: ReShade Remote popout (shared-memory control of bundled ReShade payload) |
| `xr-viewlab.ini` | bundled default config (MSI may overwrite the live ini — see REGRESSIONS) |
| `build.ps1` | single build entry point (version bump + WPF + native x64/Win32 + MSI + dist copy) |
| `Installer/Product.wxs` | WiX MSI definition, layer registration |
| `Tests/Verify-ViewLabContracts.ps1` | contract test |
| `ReShadePayload/` | bundled ReShade OpenXR payload + its docs (installed by MSI) |
| `backups/`, `reference/` | snapshots & reading material — never referenced by code |

Live config at runtime: `%LOCALAPPDATA%\XR ViewLab\xr-viewlab.ini` (UI writes, DLL reads).
Per-app profiles: `HKCU\Software\cooooked\xr-viewlab\Apps\<exe>`. Logs: `%LOCALAPPDATA%\XR ViewLab\ViewLab.log`.
