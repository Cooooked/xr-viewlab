# Windows Notification Implementation

## Selected architecture

ViewLab retains its WiX MSI and grants a dedicated notification broker package identity through a
signed **package with external location** (sparse identity package). This is Microsoft's supported
model for an existing Win32/WPF product that needs identity-gated Windows APIs without moving its
files or update mechanism into full MSIX.

The ordinary `xr-viewlab.exe` settings process now runs at medium integrity. Machine-wide OpenXR
registration changes relaunch that same executable with a narrow `--set-layer-enabled` command and
Windows elevation only when the x64 or Win32 registration actually needs changing. ReShade install
and application updates retain their existing explicit elevation boundaries.

## Package identity and capability

`NotificationBroker/AppxManifest.xml.template` defines:

- identity `cooooked.ViewLab.NotificationBroker`;
- external content from the MSI installation directory;
- a full-trust, medium-integrity Win32 application;
- `runFullTrust` and `unvirtualizedResources` for external Win32 compatibility;
- the required `uap3:userNotificationListener` capability.

`NotificationBroker/app.manifest` binds `ViewLab.NotificationBroker.exe` to the package identity.
The build creates the package with Windows SDK `MakeAppx`, signs it with `SignTool`, and packages
the matching public certificate into the MSI. The private PFX is ignored local build state or is
provided by `VIEWLAB_MSIX_PFX`/`VIEWLAB_MSIX_PFX_PASSWORD`; it is never included in source or MSI.

Local development builds use a stable machine-local self-signed code-signing certificate. The MSI
installs only its public certificate into Local Machine Trusted People. A public release should use
a production code-signing certificate through the environment variables; a CA-trusted production
certificate needs no extra trust installation.

## Broker/process boundary

`ViewLab.NotificationBroker.exe` owns:

- `UserNotificationListener` consent and collection;
- source application, title/sender and privacy-filtered preview extraction;
- application allow/block filtering;
- icon loading and one-time WPF card composition;
- duplicate suppression keyed by package family plus Windows notification ID;
- stale/removed notification clearing;
- bounded card backlog, duration, animation and expiry;
- the existing `Local\\XRViewLabNotifications` renderer mapping.

It never enters a game process. The native layer only samples bounded card metadata and pixels and
uploads a texture when the content serial changes. Notification bodies are not written to logs or
the broker status file.

The settings process writes the existing global INI keys and sends small named-pipe commands for
permission and synthetic tests. It reads a privacy-safe status JSON containing state, diagnostic,
process ID and identity state. Closing settings does not stop the broker.

## Startup, shutdown, upgrade and uninstall

- MSI installs an HKLM Run entry. Each signed-in user receives a broker process; it exits from no
  render path and reads that user's `%LOCALAPPDATA%` configuration.
- On first start, the broker registers or upgrades its signed identity package against the exact
  MSI installation directory, then relaunches so package identity is active.
- Windows listener access is requested only from an explicit ViewLab enable/test action. Background
  startup reports `PermissionNotGranted` rather than repeatedly prompting.
- Configuration is checked once per second. Windows history is safety-polled every two seconds in
  addition to `NotificationChanged`; concurrent refreshes are suppressed and five consecutive
  failures latch collection off until explicit user retry.
- The MSI's major-upgrade uninstall stops the old broker, unregisters the old package identity,
  removes its startup entry and certificate, then installs the new version. Full uninstall performs
  the same narrow cleanup by exact package name.

## Availability and permission states

The UI surfaces the broker's exact state:

- `Ready` — package identity active and listener access allowed;
- `PermissionNotGranted` — Windows access is denied, revoked or not yet requested;
- `UnsupportedDeployment` — package, certificate, supported OS or identity registration missing;
- `ListenerInitializationFailure` — listener/refresh failed, including HRESULT;
- `InternalRendererFailure` — the card shared-memory bridge could not be created;
- broker launch/status errors with a repair or reinstall instruction.

Synthetic Test Notification remains explicitly a presentation test. It does not count as Windows
collection validation.

## Security and privacy

- Permission is global and requested once through Windows; it is not a per-game profile permission.
- Collection and full message text remain in the user's medium-integrity broker.
- Preview privacy modes are full, title-only and app-only; application allow/block filtering occurs
  before card composition.
- No message body enters ordinary logs, status JSON, installer logs or the native mapping after the
  card expires.
- Backlog and rendered cards are bounded; removed Windows notifications clear their cards.
- Broker, listener and disk/status failure cannot disable or block OpenXR rendering.

## Real notification validation

On 2026-07-13, installed build 4.1.202 registered package
`cooooked.ViewLab.NotificationBroker_4.1.202.0_neutral__zbpk5xpqh6vgw`. Windows reported
`UserNotificationListenerAccessStatus=Allowed`; the broker remained alive after settings closed.

`Tests/Invoke-RealNotificationFixture.ps1` then built and temporarily registered the independent
package `cooooked.ViewLab.NotificationFixture`, sent a real Windows toast through that package's
normal `ToastNotificationManager`, and observed exactly one production shared-memory card (ID 1) active with
`CardCount=1`. The source reported its own Windows notification-history count as 1. The fixture was
then unregistered. This proves external packaged notification → Windows history/listener → broker
composition → production native card queue. Final perceived placement/stereo still requires the
release headset check.

The same independent test was repeated on 2026-07-13 after candidate 4.1.209 was built. The installed
packaged broker still reported `Ready` with listener access `Allowed`, and the new toast reached the
production mapping as exactly one active card (ID 2). This repetition validates the operating-system
collection path; installing 4.1.209 and checking its card in-headset remains a separate release gate.

Microsoft references:

- <https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps>
- <https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations>
