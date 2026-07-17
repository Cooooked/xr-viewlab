using System;
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
    public string ThresholdUnit { get; set; } = string.Empty;
    public double DefaultWarning { get; set; }
    public double DefaultCritical { get; set; }
    public bool LowerIsWorse { get; set; }
    public string Availability { get; set; } = "Detected when VR starts";
    public required string ToolTip { get; init; }
    private bool _enabled = true;
    private bool _useSymbol;
    private double _warning;
    private double _critical;
    public bool Enabled { get => _enabled; set { if(_enabled==value)return;_enabled=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Enabled))); } }
    public bool UseSymbol { get => _useSymbol; set { if(_useSymbol==value)return;_useSymbol=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(UseSymbol))); } }
    public double Warning { get => _warning; set { if(!double.IsFinite(value)||Math.Abs(_warning-value)<0.0001)return;_warning=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Warning))); } }
    public double Critical { get => _critical; set { if(!double.IsFinite(value)||Math.Abs(_critical-value)<0.0001)return;_critical=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Critical))); } }
    public void ResetThresholds() { Warning=DefaultWarning; Critical=DefaultCritical; }
    public event PropertyChangedEventHandler? PropertyChanged;
}
