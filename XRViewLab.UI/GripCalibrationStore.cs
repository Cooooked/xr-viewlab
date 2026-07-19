using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace XRViewLab.UI;

// Versioned per-car Grip-O-Bar calibration persistence. A JSON dictionary keyed by a stable car id,
// stored under %LOCALAPPDATA%\XR ViewLab\grip-calibration.json. Reject/migrate is handled by only
// loading records whose schema matches the current one; unknown/older records are dropped safely so a
// bad or stale calibration never silently drives the runtime.
internal sealed class GripCalibrationStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { IncludeFields = true, WriteIndented = true };
    private readonly string _path;
    private readonly Dictionary<string, GripCarCalibration> _byCar = new(StringComparer.OrdinalIgnoreCase);

    public GripCalibrationStore() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XR ViewLab", "grip-calibration.json")) { }

    internal GripCalibrationStore(string path)
    {
        _path = path;
        try
        {
            if (File.Exists(_path))
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, GripCarCalibration>>(File.ReadAllText(_path), JsonOpts);
                if (loaded != null)
                    foreach (var kv in loaded)
                        if (kv.Value != null && kv.Value.Schema == GripCarCalibration.CurrentSchema) // migration: drop other schemas
                            _byCar[kv.Key] = kv.Value;
            }
        }
        catch { /* a corrupt store is treated as empty, never fatal */ }
    }

    public GripCarCalibration ForCar(string carId)
    {
        if (string.IsNullOrWhiteSpace(carId)) carId = "default";
        if (!_byCar.TryGetValue(carId, out var cal))
            _byCar[carId] = cal = new GripCarCalibration { CarId = carId };
        return cal;
    }

    public void ResetCar(string carId)
    {
        if (string.IsNullOrWhiteSpace(carId)) carId = "default";
        _byCar[carId] = new GripCarCalibration { CarId = carId };
        Save();
    }

    public void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_path);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(_byCar, JsonOpts));
        }
        catch { /* persistence failure is non-fatal; calibration simply won't survive restart */ }
    }
}
