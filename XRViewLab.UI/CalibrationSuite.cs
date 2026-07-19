using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XRViewLab.UI;

internal readonly record struct CalibrationCaptureResult(bool Success, string? OutputPath, string? Error);

internal interface IOpenXrLeftEyeCaptureBackend
{
    bool IsAvailable { get; }
    ValueTask<bool> IsSessionActiveAsync(CancellationToken cancellationToken);
    ValueTask<bool> WaitForSettledFramesAsync(int frameCount, CancellationToken cancellationToken);
    ValueTask<CalibrationCaptureResult> CaptureLeftEyeAsync(string fileStem, CancellationToken cancellationToken);
}

internal sealed class UnavailableOpenXrLeftEyeCaptureBackend : IOpenXrLeftEyeCaptureBackend
{
    public bool IsAvailable => false;
    public ValueTask<bool> IsSessionActiveAsync(CancellationToken cancellationToken) => ValueTask.FromResult(false);
    public ValueTask<bool> WaitForSettledFramesAsync(int frameCount, CancellationToken cancellationToken) => ValueTask.FromResult(false);
    public ValueTask<CalibrationCaptureResult> CaptureLeftEyeAsync(string fileStem, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new CalibrationCaptureResult(false, null, "Native OpenXR left-eye capture is not implemented."));
}

internal enum CalibrationSuiteOutcome { Completed, Cancelled, CaptureUnavailable, NoActiveSession, CaptureFailed }
internal readonly record struct CalibrationSuiteResult(CalibrationSuiteOutcome Outcome, string Message);

internal static class CalibrationSuite
{
    internal static readonly IReadOnlyList<string> FileStems = new[]
    {
        "01-texture-grid", "02-pixel-ruler", "03-resolution-gratings", "04-colour-bars",
        "05-frame-beacon", "06-edge-probes", "07-checkerboards", "08-zone-plate",
        "09-clipping-steps", "10-motion-strip"
    };

    internal static async Task<CalibrationSuiteResult> RunAsync(uint originalMask,
        Func<uint, Task> applyMaskAsync, IOpenXrLeftEyeCaptureBackend backend,
        IProgress<string>? progress, CancellationToken cancellationToken)
    {
        try
        {
            if (!backend.IsAvailable)
                return new(CalibrationSuiteOutcome.CaptureUnavailable,
                    "Native OpenXR left-eye capture is not available in this build. No images were generated.");
            if (!await backend.IsSessionActiveAsync(cancellationToken).ConfigureAwait(true))
                return new(CalibrationSuiteOutcome.NoActiveSession, "No active OpenXR session was confirmed.");
            for (int index = 0; index < FileStems.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Calibration {index + 1}/{FileStems.Count}: {FileStems[index]}");
                await applyMaskAsync(1u << index).ConfigureAwait(true);
                if (!await backend.WaitForSettledFramesAsync(6, cancellationToken).ConfigureAwait(true))
                    return new(CalibrationSuiteOutcome.CaptureFailed, "The OpenXR session stopped before the calibration pattern settled.");
                CalibrationCaptureResult capture = await backend.CaptureLeftEyeAsync(FileStems[index], cancellationToken).ConfigureAwait(true);
                if (!capture.Success) return new(CalibrationSuiteOutcome.CaptureFailed, capture.Error ?? "Left-eye capture failed.");
            }
            return new(CalibrationSuiteOutcome.Completed, "Calibration suite completed.");
        }
        catch (OperationCanceledException)
        {
            return new(CalibrationSuiteOutcome.Cancelled, "Calibration suite cancelled.");
        }
        finally
        {
            await applyMaskAsync(originalMask).ConfigureAwait(true);
        }
    }
}
