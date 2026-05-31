using DSX.Core.Enums;

namespace DSX.WPF.Navigation;

public class NavigationItem
{
    public string Icon { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public NavigationPage Page { get; set; }
    public bool IsVisible { get; set; } = true;

    public NavigationItem(string icon, string label, NavigationPage page, bool isVisible = true)
    {
        Icon = icon;
        Label = label;
        Page = page;
        IsVisible = isVisible;
    }
}
