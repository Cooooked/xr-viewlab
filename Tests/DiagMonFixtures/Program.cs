using System.Diagnostics;
using System.IO.Compression;
using XRViewLab.UI;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

string fixture = Path.Combine(Path.GetTempPath(), "ViewLab-DiagMon-Fixtures-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixture);
try
{
    string csv = Path.Combine(fixture, "presentmon.csv");
    File.WriteAllLines(csv, new[]
    {
        "Application,ProcessID,msBetweenPresents,AllowsTearing",
        "fixture.exe,42,10.0,1", "fixture.exe,42,11.0,0", "fixture.exe,42,12.0,1", "fixture.exe,42,120.0,0"
    });
    DiagMonFrameMetrics parsed = DiagMonCaptureService.ParsePresentMon(csv);
    Assert(parsed.PresentedFrames == 4, "PresentMon rows were not parsed.");
    Assert(parsed.MeasuredDroppedFrames == null, "AllowsTearing was incorrectly treated as dropped-frame evidence.");
    Assert(parsed.SevereStutters == 1, "The documented severe-stutter heuristic changed unexpectedly.");

    string? resolvedPresentMon = DiagMonCaptureService.ResolvePresentMonExecutable(null);
    Assert(resolvedPresentMon != null && Path.GetFileName(resolvedPresentMon) == "PresentMon-2.4.1-x64.exe",
        "Bundled PresentMon discovery changed unexpectedly.");
    string pumpedCsv = Path.Combine(fixture, "pumped-presentmon.csv"), pumpedErrors = Path.Combine(fixture, "pumped-presentmon-errors.log");
    using (Process pumpSource = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -Command \"Write-Output 'frame_ms,pid'; Write-Output '11.1,42'; [Console]::Error.WriteLine('fixture warning')\"")
    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!)
    {
        await DiagMonCaptureService.PumpPresentMonOutputAsync(pumpSource, pumpedCsv, pumpedErrors);
        await pumpSource.WaitForExitAsync();
    }
    Assert(File.ReadAllLines(pumpedCsv).Length == 2, "PresentMon stdout pump did not retain streamed rows.");
    Assert(File.ReadAllText(pumpedErrors).Contains("fixture warning"), "PresentMon stderr diagnostics were not retained.");

    var store = new DiagMonStore(Path.Combine(fixture, "store"));
    foreach ((string file, string arguments, string label) in new[]
    {
        (Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/c ping 127.0.0.1 -n 8 > nul", "fixture-command-shell"),
        ("powershell.exe", "-NoProfile -Command Start-Sleep -Seconds 8", "fixture-powershell")
    })
    {
        using Process target = Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = false, CreateNoWindow = true })!;
        using var service = new DiagMonCaptureService(store);
        await service.StartAsync(new DiagMonCaptureOptions { Mode = DiagMonCaptureMode.Standard, TargetMode = DiagMonTargetMode.Manual, TargetPid = target.Id, UserLabel = label });
        await Task.Delay(3200);
        string directory = (await service.StopAsync("Automated two-application integration fixture."))!;
        Assert(Directory.Exists(directory), $"Session directory missing for {label}.");
        Assert(File.Exists(Path.Combine(directory, "session.json")), "session.json missing.");
        Assert(File.Exists(Path.Combine(directory, "summary.md")), "summary.md missing.");
        Assert(File.Exists(Path.Combine(directory, "metrics.json")), "metrics.json missing.");
        Assert(File.Exists(Path.Combine(directory, "annotations.json")), "annotations.json missing.");
        Assert(File.Exists(Path.Combine(directory, "Raw", "process-telemetry.csv")), "Process telemetry missing.");
        var entry = store.LoadIndex().Sessions.Single(x => string.Equals(store.ResolveDirectory(x), directory, StringComparison.OrdinalIgnoreCase));
        store.SaveAnnotations(entry, new DiagMonAnnotations { Validation = DiagMonValidation.Invalid, SessionType = DiagMonSessionType.Experiment, Notes = "Automated generic-workload fixture; deliberately too short for a baseline.", Tags = new() { "integration-test", "generic-workload" } });
        string zip = store.ExportPackage(store.LoadIndex().Sessions.Single(x => x.Id == entry.Id));
        using ZipArchive archive = ZipFile.OpenRead(zip);
        Assert(archive.GetEntry("analysis-context.json") != null, "Analysis context missing from export.");
        Assert(archive.GetEntry("AI-BRIEFING.md") != null, "AI briefing missing from export.");
        Assert(archive.GetEntry("manifest.json") != null, "Export manifest missing.");
        try { if (!target.HasExited) target.Kill(entireProcessTree: true); } catch { }
    }

    using (Process target = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -Command Start-Sleep -Seconds 3") { UseShellExecute = false, CreateNoWindow = true })!)
    using (var service = new DiagMonCaptureService(store))
    {
        await service.StartAsync(new DiagMonCaptureOptions { Mode = DiagMonCaptureMode.Standard, TargetMode = DiagMonTargetMode.Manual, TargetPid = target.Id, UserLabel = "fixture-auto-finalise" });
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (service.IsCapturing && DateTime.UtcNow < deadline) await Task.Delay(200);
        Assert(!service.IsCapturing, "Target exit did not automatically finalise the capture.");
        Assert(service.CurrentDirectory != null && !service.CurrentDirectory.EndsWith(".partial", StringComparison.OrdinalIgnoreCase),
            "Automatically finalised capture retained its partial directory suffix.");
        Assert(service.CurrentSession?.StopReason.Contains("exited", StringComparison.OrdinalIgnoreCase) == true,
            "Automatically finalised capture did not retain the target-exit reason.");
    }
    Assert(await store.RecoverAbandonedAsync() == 0, "Completed sessions were mistaken for abandoned captures.");
    var abandoned = store.CreatePartial(new DiagMonCaptureOptions { Mode = DiagMonCaptureMode.Standard, TargetMode = DiagMonTargetMode.NewProcess, UserLabel = "fixture-abandoned" }, "fixture");
    abandoned.Session.Collectors.Add(new DiagMonCollectorStatus { Name = "fixture", State = DiagMonCollectorState.Running });
    DiagMonStore.WriteJsonAtomic(Path.Combine(abandoned.Directory, "session.json"), abandoned.Session);
    Assert(await store.RecoverAbandonedAsync() == 1, "Abandoned session was not recovered.");
    var recovered = store.LoadIndex().Sessions.Single(x => x.Id == abandoned.Session.Id);
    Assert(recovered.SessionType == DiagMonSessionType.Incomplete, "Recovered session was not classified incomplete.");
    foreach (var generated in store.LoadIndex().Sessions.Where(x => x.UserLabel.StartsWith("fixture-", StringComparison.OrdinalIgnoreCase)).ToList())
        store.Delete(generated);
    Console.WriteLine("DiagMon fixtures passed: parser safety, two generic applications, session finalisation, classification, and export.");
}
finally { try { Directory.Delete(fixture, recursive: true); } catch { } }
