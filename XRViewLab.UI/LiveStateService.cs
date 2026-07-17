using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace XRViewLab.UI;

// Fixed binary contract with dllmain.cpp.  The generation is written last, so the layer never
// consumes a half-written UI snapshot and never needs to poll the ini during rendering.
internal sealed class LiveStateService : IDisposable
{
    private const string Name = "Local\\XRViewLabLiveState";
    private const int Size = 260;
    private const uint Magic = 0x534C4C56; // VLLS
    private MemoryMappedFile? _map;
    private MemoryMappedViewAccessor? _view;
    private uint _generation;

    public LiveStateService()
    {
        _map = MemoryMappedFile.CreateOrOpen(Name, Size, MemoryMappedFileAccess.ReadWrite);
        _view = _map.CreateViewAccessor(0, Size, MemoryMappedFileAccess.ReadWrite);
        _view.Write(0, Magic); _view.Write(4, 8u); _view.Write(8, (uint)Size);
    }

    public void Publish(uint calibrationMask,
        bool maskEnabled, bool topmostOverlays, double size, double corner, double apex, double innerLow,
        double bridgeWidth, double bridgeRise, double bridgePeakX, double bridgeSteepness,
        bool hudEnabled, int traceVisibilityMode, double hudX, double hudY, double hudScale, double hudSafeMargin, bool hudClamp,
        bool hudAlarmOnly, double hudTraceSensitivityMs, double traceX, double traceY, double traceScale,
        double traceWidth, double traceHistory, double alarmHoldMs,
        uint hudWidgetMask, uint hudWidgetOrder, uint hudGraphChannels, uint hudGraphMode,
        bool boundaryDrag,
        bool crosshairEnabled, bool crosshairDot, bool crosshairOutline, bool crosshairTStyle,
        double chSize, double chGap, double chThickness, double chOutlineThickness, double chAlpha, double chScale, uint chColor,
        double chOffsetX, double chOffsetY,
        bool notifyEnabled, bool notifyShowIcon, bool notifyShowImage, double notifyX, double notifyY,
        double notifyScale, double notifyOpacity, double notifyDurationMs, uint notifyMaxVisible, uint notifyPrivacy,
        bool iracingEnabled, bool iracingLapPopup, bool iracingSpotterGlow, bool iracingFlagBorder,
        bool clockEnabled, bool sessionTimerEnabled, bool clock24Hour, double clockX, double clockY,
        double clockScale, double clockOpacity, uint clockTheme,
        IReadOnlyList<int> overlayToggleKeys)
    {
        if (_view == null) return;
        _view.Write(16, calibrationMask);
        _view.Write(20, 1u | (maskEnabled ? 4u : 0u)); // visor tuning is always live
        _view.Write(24, (float)size); _view.Write(28, (float)corner); _view.Write(32, (float)apex);
        _view.Write(36, (float)innerLow); _view.Write(40, (float)bridgeWidth); _view.Write(44, (float)bridgeRise);
        _view.Write(48, (float)bridgePeakX); _view.Write(52, (float)bridgeSteepness);
        _view.Write(56, (float)hudX); _view.Write(60, (float)hudY); _view.Write(64, (float)hudScale); _view.Write(68, (float)hudSafeMargin);
        _view.Write(72, (hudEnabled ? 1u : 0u) | (hudClamp ? 2u : 0u) | (hudAlarmOnly ? 4u : 0u));
        _view.Write(76, (float)hudTraceSensitivityMs);
        _view.Write(80, (float)traceX); _view.Write(84, (float)traceY); _view.Write(88, (float)traceScale);
        _view.Write(92, (float)traceWidth); _view.Write(96, (float)traceHistory); _view.Write(100, (float)alarmHoldMs);
        // v4 additions
        _view.Write(104, boundaryDrag ? 1u : 0u);
        _view.Write(108, (crosshairEnabled ? 1u : 0u) | (crosshairDot ? 2u : 0u) | (crosshairOutline ? 4u : 0u) | (crosshairTStyle ? 8u : 0u));
        _view.Write(112, (float)chSize); _view.Write(116, (float)chGap); _view.Write(120, (float)chThickness);
        _view.Write(124, (float)chOutlineThickness); _view.Write(128, (float)chAlpha); _view.Write(132, (float)chScale);
        _view.Write(136, chColor);
        _view.Write(140, (notifyEnabled ? 1u : 0u) | (notifyShowIcon ? 2u : 0u) | (notifyShowImage ? 4u : 0u));
        _view.Write(144, (float)notifyX); _view.Write(148, (float)notifyY); _view.Write(152, (float)notifyScale);
        _view.Write(156, (float)notifyOpacity); _view.Write(160, (float)notifyDurationMs);
        _view.Write(164, notifyMaxVisible); _view.Write(168, notifyPrivacy);
        _view.Write(172, (iracingEnabled ? 1u : 0u) | (iracingLapPopup ? 2u : 0u) | (iracingSpotterGlow ? 4u : 0u) | (iracingFlagBorder ? 8u : 0u));
        _view.Write(176, traceVisibilityMode == 0 ? 0u : 1u | (traceVisibilityMode == 2 ? 2u : 0u));
        _view.Write(180, (float)chOffsetX); _view.Write(184, (float)chOffsetY);
        _view.Write(188, topmostOverlays ? 1u : 0u);
        _view.Write(192, hudWidgetMask); _view.Write(196, hudWidgetOrder);
        _view.Write(200, hudGraphChannels); _view.Write(204, hudGraphMode);
        _view.Write(208, (clockEnabled ? 1u : 0u) | (sessionTimerEnabled ? 2u : 0u) | (clock24Hour ? 4u : 0u));
        _view.Write(212, (float)clockX); _view.Write(216, (float)clockY); _view.Write(220, (float)clockScale); _view.Write(224, (float)clockOpacity);
        _view.Write(228, clockTheme); _view.Write(232, 0u);
        for (int i = 0; i < 6; ++i) _view.Write(236 + i * 4, (uint)(i < overlayToggleKeys.Count ? Math.Clamp(overlayToggleKeys[i], 0, 255) : 0));
        Thread.MemoryBarrier();
        _view.Write(12, unchecked(++_generation));
    }

    public void Dispose() { _view?.Dispose(); _map?.Dispose(); }
}
