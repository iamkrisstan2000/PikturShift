using System.Windows.Media;
using PikturShift.Common;
using PikturShift.Theming;

namespace PikturShift.ViewModels;

public sealed class AccentSwatchViewModel : ObservableObject
{
    private bool _isSelected;
    private Brush _fill = Brushes.Transparent;
    private string _name = string.Empty;

    public required AccentId Id { get; init; }
    public required bool IsSystem { get; init; }
    public required bool IsMono { get; init; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public Brush Fill
    {
        get => _fill;
        set => SetProperty(ref _fill, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Tooltip => IsSystem ? $"System \u2014 follows your Windows accent ({SystemHex})" : Name;

    public void Refresh(bool dark, Color systemAccent, AccentId selected)
    {
        var color = AccentCatalog.Resolve(Id, dark, systemAccent);
        Fill = Freeze(new SolidColorBrush(color));

        // The System swatch always reads "System" and paints the live Windows accent,
        // so it can never be mistaken for one of the fixed colours next to it.
        Name = IsSystem ? "System" : AccentCatalog.DisplayName(Id, dark);
        IsSelected = selected == Id;
        OnPropertiesChanged(nameof(Tooltip), nameof(IsSelected), nameof(SystemHex));
    }

    /// <summary>Hex of the live Windows accent, shown in the System tooltip.</summary>
    public string SystemHex => IsSystem ? ColorUtil.ToHex(ColorOf(Fill)) : string.Empty;

    private static Color ColorOf(Brush brush) => brush is SolidColorBrush solid ? solid.Color : Colors.Transparent;

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
