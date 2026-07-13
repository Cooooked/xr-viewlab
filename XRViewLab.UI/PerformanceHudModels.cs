namespace XRViewLab.UI;

internal sealed class HudWidgetOption
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string ToolTip { get; init; }
    public bool Enabled { get; set; } = true;
}
