using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XRViewLab.UI;

internal static class Program
{
    private sealed class Backend : IOpenXrLeftEyeCaptureBackend
    {
        internal bool Available = true;
        internal bool Active = true;
        internal bool Settled = true;
        internal int FailCaptureAt = -1;
        internal bool CancelDuringSettle;
        internal readonly List<int> SettleFrames = new();
        internal readonly List<string> Captures = new();
        public bool IsAvailable => Available;
        public ValueTask<bool> IsSessionActiveAsync(CancellationToken token) => ValueTask.FromResult(Active);
        public ValueTask<bool> WaitForSettledFramesAsync(int frameCount, CancellationToken token)
        {
            SettleFrames.Add(frameCount);
            if (CancelDuringSettle) throw new OperationCanceledException(token);
            return ValueTask.FromResult(Settled);
        }
        public ValueTask<CalibrationCaptureResult> CaptureLeftEyeAsync(string stem, CancellationToken token)
        {
            int index = Captures.Count;
            Captures.Add(stem);
            return ValueTask.FromResult(index == FailCaptureAt
                ? new CalibrationCaptureResult(false, null, "fixture failure")
                : new CalibrationCaptureResult(true, stem + ".png", null));
        }
    }

    private static async Task Main()
    {
        await SuccessRestoresAndSequencesPatterns();
        await UnavailableAndInactiveRestore();
        await CaptureAndSettleFailuresRestore();
        await CancellationRestores();
        await ApplyFailureRestores();
        Console.WriteLine("Calibration suite orchestration fixtures passed.");
    }

    private static async Task SuccessRestoresAndSequencesPatterns()
    {
        const uint original = 0x155;
        var masks = new List<uint>();
        var backend = new Backend();
        CalibrationSuiteResult result = await CalibrationSuite.RunAsync(original, mask => { masks.Add(mask); return Task.CompletedTask; }, backend, null, CancellationToken.None);
        Require(result.Outcome == CalibrationSuiteOutcome.Completed, "successful suite did not complete");
        Require(masks.Count == 11 && masks[^1] == original, "successful suite did not restore original mask");
        for (int i = 0; i < 10; i++)
        {
            Require(masks[i] == 1u << i, $"pattern mask {i} was out of sequence");
            Require(backend.SettleFrames[i] == 6, $"pattern {i} did not settle for six submitted frames");
            Require(backend.Captures[i] == CalibrationSuite.FileStems[i], $"capture stem {i} was out of sequence");
        }
    }

    private static async Task UnavailableAndInactiveRestore()
    {
        foreach (Backend backend in new[] { new Backend { Available = false }, new Backend { Active = false } })
        {
            var masks = new List<uint>();
            CalibrationSuiteResult result = await CalibrationSuite.RunAsync(7, mask => { masks.Add(mask); return Task.CompletedTask; }, backend, null, CancellationToken.None);
            Require(result.Outcome is CalibrationSuiteOutcome.CaptureUnavailable or CalibrationSuiteOutcome.NoActiveSession, "availability failure outcome was incorrect");
            Require(masks.Count == 1 && masks[0] == 7, "availability failure did not restore original mask");
        }
    }

    private static async Task CaptureAndSettleFailuresRestore()
    {
        foreach (Backend backend in new[] { new Backend { FailCaptureAt = 0 }, new Backend { Settled = false } })
        {
            var masks = new List<uint>();
            CalibrationSuiteResult result = await CalibrationSuite.RunAsync(9, mask => { masks.Add(mask); return Task.CompletedTask; }, backend, null, CancellationToken.None);
            Require(result.Outcome == CalibrationSuiteOutcome.CaptureFailed, "capture failure outcome was incorrect");
            Require(masks.Count == 2 && masks[^1] == 9, "capture failure did not restore original mask");
        }
    }

    private static async Task CancellationRestores()
    {
        var masks = new List<uint>();
        CalibrationSuiteResult result = await CalibrationSuite.RunAsync(11, mask => { masks.Add(mask); return Task.CompletedTask; }, new Backend { CancelDuringSettle = true }, null, CancellationToken.None);
        Require(result.Outcome == CalibrationSuiteOutcome.Cancelled, "cancellation outcome was incorrect");
        Require(masks.Count == 2 && masks[^1] == 11, "cancellation did not restore original mask");
    }

    private static async Task ApplyFailureRestores()
    {
        var masks = new List<uint>();
        bool threw = false;
        try
        {
            await CalibrationSuite.RunAsync(13, mask =>
            {
                masks.Add(mask);
                if (mask == 1) throw new InvalidOperationException("fixture apply failure");
                return Task.CompletedTask;
            }, new Backend(), null, CancellationToken.None);
        }
        catch (InvalidOperationException) { threw = true; }
        Require(threw, "apply failure was unexpectedly swallowed");
        Require(masks.Count == 2 && masks[^1] == 13, "apply failure did not restore original mask");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
