using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LazyBootstrap.UI.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public T GetControl<T>(string name) where T : Control
    {
        return this.FindControl<T>(name);
    }
}
