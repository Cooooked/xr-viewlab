using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace XRViewLab.UI;

internal sealed class StickyNoteOption : INotifyPropertyChanged
{
    private int _number;
    private bool _enabled = true;
    private string _text = string.Empty;
    private double _x = .78, _y = .22, _scale = 1, _opacity = .85;
    private int _theme;

    public int Number { get => _number; set { _number = value; Changed(); Changed(nameof(Title)); } }
    public string Title => $"Note {Number}";
    public bool Enabled { get => _enabled; set { _enabled = value; Changed(); } }
    public string Text { get => _text; set { _text = value ?? string.Empty; Changed(); } }
    public double X { get => _x; set { _x = value; Changed(); } }
    public double Y { get => _y; set { _y = value; Changed(); } }
    public double Scale { get => _scale; set { _scale = value; Changed(); } }
    public double Opacity { get => _opacity; set { _opacity = value; Changed(); } }
    public int Theme { get => _theme; set { _theme = value; Changed(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
