using System;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace XRViewLab.UI;

// UI side of the native calibration-capture contract. The settings app owns the small
// read/write "XRViewLabCalibrationCapture" control block; the OpenXR layer stamps a
// heartbeat while a session submits frames, and answers request serials with a saved
// left-eye PNG path or a truthful error code. Layout must match CalibrationCaptureBlock
// in dllmain.cpp exactly (pack 4, UTF-16 fixed buffers).
internal sealed class NativeOpenXrLeftEyeCaptureBackend : IOpenXrLeftEyeCaptureBackend, IDisposable
{
    private const int BlockSize = 16 + 8 + 8 + 96 * 2 + 512 * 2;
    private const int OffVersion = 0;
    private const int OffRequestSerial = 4;
    private const int OffCompletedSerial = 8;
    private const int OffResultCode = 12;
    private const int OffHeartbeat = 16;
    private const int OffCancelledSerial = 24;
    private const int OffStem = 32;
    private const int OffPath = 32 + 96 * 2;
    private const int StemChars = 96;
    private const int PathChars = 512;

    private readonly MemoryMappedFile _map;
    private readonly MemoryMappedViewAccessor _view;
    private uint _requestSerial;

    public NativeOpenXrLeftEyeCaptureBackend()
    {
        _map = MemoryMappedFile.CreateOrOpen("XRViewLabCalibrationCapture", BlockSize);
        _view = _map.CreateViewAccessor(0, BlockSize);
        _view.Write(OffVersion, 1u);
        _requestSerial = _view.ReadUInt32(OffRequestSerial);
    }

    public bool IsAvailable => true;

    public async ValueTask<bool> IsSessionActiveAsync(CancellationToken cancellationToken)
    {
        // The layer connects to the block with a one-second retry, so allow a short grace
        // window before declaring that no session is submitting frames.
        for (int attempt = 0; attempt < 10; attempt++)
        {
            long beat = _view.ReadInt64(OffHeartbeat);
            if (beat != 0 && Math.Abs(Environment.TickCount64 - beat) < 2500) return true;
            await Task.Delay(250, cancellationToken).ConfigureAwait(true);
        }
        return false;
    }

    public async ValueTask<bool> WaitForSettledFramesAsync(int frameCount, CancellationToken cancellationToken)
    {
        long previous = _view.ReadInt64(OffHeartbeat);
        int observed = 0;
        int target = Math.Max(1, frameCount);
        long deadline = Environment.TickCount64 + 3000;
        while (observed < target && Environment.TickCount64 < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long current = _view.ReadInt64(OffHeartbeat);
            if (current != 0 && current != previous)
            {
                previous = current;
                observed++;
            }
            if (observed < target) await Task.Delay(5, cancellationToken).ConfigureAwait(true);
        }
        return observed >= target;
    }

    public async ValueTask<CalibrationCaptureResult> CaptureLeftEyeAsync(string fileStem, CancellationToken cancellationToken)
    {
        char[] stem = new char[StemChars];
        fileStem.AsSpan(0, Math.Min(fileStem.Length, StemChars - 1)).CopyTo(stem);
        _view.WriteArray(OffStem, stem, 0, StemChars);
        uint serial = ++_requestSerial;
        _view.Write(OffRequestSerial, serial);
        long deadline = Environment.TickCount64 + 10000;
        try
        {
            while (Environment.TickCount64 < deadline)
            {
                if (_view.ReadUInt32(OffCompletedSerial) == serial)
                {
                    int code = _view.ReadInt32(OffResultCode);
                    if (code == 0)
                    {
                        char[] chars = new char[PathChars];
                        _view.ReadArray(OffPath, chars, 0, PathChars);
                        int length = Array.IndexOf(chars, '\0');
                        if (length < 0) length = PathChars;
                        return new CalibrationCaptureResult(true, new string(chars, 0, length), null);
                    }
                    return new CalibrationCaptureResult(false, null, DescribeError(code));
                }
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(100, cancellationToken).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            _view.Write(OffCancelledSerial, serial);
            throw;
        }
        _view.Write(OffCancelledSerial, serial);
        return new CalibrationCaptureResult(false, null,
            "Timed out waiting for the native left-eye capture to complete.");
    }

    private static string DescribeError(int code) => code switch
    {
        1 => "No submitted OpenXR projection frame was available to capture.",
        2 => "The game's swapchain texture format is not supported by the capture encoder.",
        3 => "The ViewLab renderer is not available in the running game.",
        4 => "Writing the capture PNG failed. See ViewLab.log for details.",
        5 => "No left-eye view could be resolved from the submitted frame.",
        6 => "Copying the left-eye texture from the GPU failed.",
        7 => "The left-eye capture was cancelled.",
        _ => $"Native capture failed with code {code}.",
    };

    public void Dispose()
    {
        _view.Dispose();
        _map.Dispose();
    }
}

internal static class CalibrationCaptureBackendFactory
{
    internal static IOpenXrLeftEyeCaptureBackend Create()
    {
        try
        {
            return new NativeOpenXrLeftEyeCaptureBackend();
        }
        catch (Exception)
        {
            return new UnavailableOpenXrLeftEyeCaptureBackend();
        }
    }
}
