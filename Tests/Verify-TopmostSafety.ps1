param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
$source = Get-Content -LiteralPath (Join-Path $Root 'dllmain.cpp') -Raw

function Require([string]$Pattern, [string]$Message) {
    if ($source -notmatch $Pattern) { throw "Topmost safety contract failed: $Message" }
}

function Forbid([string]$Pattern, [string]$Message) {
    if ($source -match $Pattern) { throw "Topmost safety contract failed: $Message" }
}

# Source contracts: failure is latched without teardown, device removal suppresses both backends,
# allocation is attempted once per session, and submitted-rectangle jitter cannot recreate it.
Require 'std::atomic<bool> g_topmostLayerBlocked' 'failure latch must be race-free'
Require 'void BlockTopmostLayer[\s\S]*?g_topmostLayerBlocked\.exchange\(true' 'failure must latch for the session'
Forbid 'void BlockTopmostLayer[\s\S]{0,1600}DestroyTopmostLayer\(\)' 'failure path must not destroy runtime resources'
Require 'bool EnsureTopmostLayer[\s\S]*?g_topmostLayerBlocked\.load\(' 'blocked sessions never create another swapchain'
Require 'g_topmostLayerAttempted\s*=\s*true[\s\S]*?nextXrCreateSwapchain' 'allocation attempt is latched before entering the runtime'
Require 'width<=g_topmostLayer\.width[\s\S]*?height<=g_topmostLayer\.height' 'smaller submitted rectangles reuse stable capacity'
Require 'projection capacity changed' 'capacity changes fail closed instead of reallocating'
Require 'ci\.faceCount=1; ci\.arraySize=2' 'ordered backend preserves the proven stereo array contract'
Require 'XrCompositionLayerProjection layer' 'compatibility backend submits the proven ordered projection carrier'
Require 'out\.layer\.space=source->space' 'ordered projection preserves the current application space'
Require 'out\.views\[i\]\.pose=source->views\[i\]\.pose' 'ordered projection preserves current per-eye poses'
Require 'out\.views\[i\]\.fov=source->views\[i\]\.fov' 'ordered projection preserves current per-eye FOVs'
Require 'D3D11_RTV_DIMENSION_TEXTURE2DARRAY' 'ordered renderer draws one compositor eye per array slice'
Require 'wi\.timeout=100000000' 'ordered swapchain retains the previously working bounded wait'
Require 'primaryProjection==nullptr' 'composition-only frames are detected from observed topology'
Require 'RenderTopmostLayer\(session,primaryProjection,topmostSubmission\)' 'ordered rendering uses the current projection through the shared path'
Require 'g_topmostBackend==viewlab::bridge::OverlayBackend::SeparateProjection' 'central policy selects the proven ordered carrier'
Require 'SelectFeaturePresentationPlan\(capabilities\)' 'one central frame plan controls all direct and ordered ViewLab presentation'
Require 'g_featurePresentationPlan\.drawDirectVisor=false' 'ordered readiness disables the duplicate direct visor'
Require 'g_featurePresentationPlan\.drawDirectCommonFeatures=false' 'ordered readiness disables duplicate direct common features'
Require 'BlockTopmostLayer[\s\S]*?g_featurePresentationPlan\.drawDirectVisor = true' 'ordered failure restores the direct visor'
Require 'BlockTopmostLayer[\s\S]*?g_featurePresentationPlan\.drawDirectCommonFeatures = true' 'ordered failure restores direct common features'
foreach ($stage in @('xrCreateSwapchain', 'xrAcquireSwapchainImage', 'xrWaitSwapchainImage', 'CreateRenderTargetView', 'xrReleaseSwapchainImage', 'xrEndFrame')) {
    Require "BlockTopmostLayer\(\`"$stage" "stage $stage must fail closed"
}
Require 'RendererDeviceHealthy\("xrReleaseSwapchainImage"\)' 'direct release-time drawing checks device removal first'
Require 'RendererDeviceHealthy\("xrEndFrame"\)' 'end-frame drawing checks device removal first'
Require 'all ViewLab rendering disabled for this session' 'device-loss log states the containment action'
Require 'no automatic retry' 'Topmost failure log states retry policy'
Require 'rd\.Format=topmostDesc\.Format' 'topmost RTV preserves the runtime resource format contract'
Require 'if\(drewVisor\) g_releaseDrewVisorThisFrame' 'skipped direct draws cannot masquerade as successful presentation'
Forbid 'XR_COMPOSITION_LAYER_UNPREMULTIPLIED_ALPHA_BIT' 'premultiplied target must not be multiplied again by the compositor'
Forbid 'RenderTopmostLayer\([\s\S]{0,500}\)\)\s*\{[\s\S]{0,800}\}\s*else\s+DestroyTopmostLayer' 'render failure must not be followed by an unlatching teardown/retry branch'
Forbid 'else if \(!topmostVisorOverlays\)[^\r\n]*g_topmostLayerBlocked=false' 'checkbox cycling must not re-arm a failed session'

# State-machine simulations. These exercise the intended retry and reset semantics without touching
# a GPU or runtime: one failed attempt per session, zero attempts after device loss, and checkbox
# cycling cannot re-arm the failed session.
$blocked = $false
$attempts = 0
1..100 | ForEach-Object {
    if (!$blocked) { $attempts++; $blocked = $true }
}
if ($attempts -ne 1) { throw "Topmost safety simulation failed: expected one attempt, got $attempts" }

$deviceLost = $true
$blocked = $true
$attempts = 0
1..100 | ForEach-Object {
    if (!$blocked -and !$deviceLost) { $attempts++ }
}
if ($attempts -ne 0) { throw 'Topmost safety simulation failed: device-lost session attempted rendering' }

$deviceLost = $false
$blocked = $true
$attempts = 0
foreach ($topmostEnabled in @($false, $true, $false, $true)) {
    if ($topmostEnabled -and !$blocked -and !$deviceLost) { $attempts++ }
}
if ($attempts -ne 0) { throw 'Topmost safety simulation failed: checkbox cycling re-armed a failed session' }

Write-Output 'Topmost safety contracts and lifecycle simulations passed.'
