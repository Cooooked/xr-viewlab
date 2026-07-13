using System.Text.Json;
using XRViewLab.UI;

string directory = Path.Combine(Path.GetTempPath(), "ViewLabHistoryFixture_" + Environment.ProcessId);
string path = Path.Combine(directory, "history.jsonl");
Directory.CreateDirectory(directory);

try
{
    var old = new HistoryService.Entry(DateTimeOffset.UtcNow - TimeSpan.FromDays(15), "old", "expired", null, null, null);
    File.WriteAllText(path, JsonSerializer.Serialize(old) + Environment.NewLine + "not-json" + Environment.NewLine);

    var history = new HistoryService(path);
    Assert(File.ReadAllLines(path).Length == 0, "load removes expired and malformed records");

    string unicode = new('測', 600);
    for (int i = 0; i < 700; i++)
        history.Record("notification", i % 2 == 0 ? "shown" : "expired", unicode, unicode, unicode);

    string[] lines = File.ReadAllLines(path);
    Assert(lines.Length <= HistoryService.MaxRecords, "record count is bounded");
    Assert(new FileInfo(path).Length <= HistoryService.MaxBytes, "UTF-8 file size is bounded");
    Assert(lines.Length > 0, "bounded history retains recent entries");

    bool schemaHasNoBody = true, titlesBounded = true, detailsBounded = true;
    foreach (string line in lines)
    {
        using JsonDocument document = JsonDocument.Parse(line);
        JsonElement root = document.RootElement;
        schemaHasNoBody &= !root.TryGetProperty("Body", out _);
        titlesBounded &= root.GetProperty("Title").GetString()!.Length <= 160;
        detailsBounded &= root.GetProperty("Detail").GetString()!.Length <= 256;
    }
    Assert(schemaHasNoBody, "history schema has no notification-body field");
    Assert(titlesBounded, "titles are length-limited");
    Assert(detailsBounded, "details are length-limited");

    history.Clear();
    Assert(!File.Exists(path), "clear removes the history file");
    Console.WriteLine($"History fixtures passed ({lines.Length} retained records before clear). ");
}
finally
{
    if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
}

static void Assert(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException("FAILED: " + description);
    Console.WriteLine("PASS: " + description);
}
