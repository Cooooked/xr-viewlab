using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace XRViewLab.UI;

internal sealed class HudWidgetOption : INotifyPropertyChanged
{
    public required int MetricId { get; init; }
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Provider { get; init; }
    public required string Unit { get; init; }
    public string Availability { get; set; } = "Detected when VR starts";
    public required string ToolTip { get; init; }
    private bool _enabled = true;
    public bool Enabled { get => _enabled; set { if(_enabled==value)return;_enabled=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Enabled))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
