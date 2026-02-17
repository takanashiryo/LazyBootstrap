using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LazyBootstrap.UI.Pages;

public partial class ToolsPage : UserControl
{
    public ToolsPage()
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
