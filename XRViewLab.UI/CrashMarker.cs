using System;
using System.IO;
using System.Text.Json;

namespace XRViewLab.UI;

// Records an unhandled exception from the settings-app process so the next launch can explain
// what happened, instead of the window just vanishing with no explanation. Read once and deleted
// — a marker only ever describes the most recent crash, not a growing log of every one.
public static class CrashMarker
{
	public sealed record Record(string ExceptionType, string Message, string StackTrace, DateTimeOffset TimeUtc);

	private static string PathFor(string configDirectory) => Path.Combine(configDirectory, "last-crash.json");

	public static void Write(string configDirectory, Exception ex)
	{
		try
		{
			Directory.CreateDirectory(configDirectory);
			var record = new Record(ex.GetType().Name, ex.Message, ex.StackTrace ?? "", DateTimeOffset.UtcNow);
			File.WriteAllText(PathFor(configDirectory), JsonSerializer.Serialize(record));
		}
		catch { /* best-effort: a failure to record the crash must never mask the crash itself */ }
	}

	// Reads and clears the marker, if any. Returns null (not an error) when there's nothing to report.
	public static Record? TryReadAndClear(string configDirectory)
	{
		string path = PathFor(configDirectory);
		try
		{
			if (!File.Exists(path)) return null;
			Record? record = JsonSerializer.Deserialize<Record>(File.ReadAllText(path));
			File.Delete(path);
			return record;
		}
		catch { try { File.Delete(path); } catch { } return null; }
	}
}
