using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SSMM_UI.Settings;

public partial class ThemeOption(string key, string name, ThemeVariant variant, string resourceUri) : ObservableObject
{
    public string Key { get; } = key;
    public string Name { get; } = name;
    public ThemeVariant Variant { get; } = variant;
    public string ResourceUri { get; } = resourceUri;

    [ObservableProperty]
    private bool isSelected;
}
