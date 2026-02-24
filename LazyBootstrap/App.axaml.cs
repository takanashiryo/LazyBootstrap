using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using SukiUI;
using SukiUI.Models;

namespace LazyBootstrap
{
    public partial class App : Application
    {
        private static readonly SukiColorTheme LazyGreenTheme = new SukiColorTheme(
            "Lazy Green",
            Color.Parse("#8CCF43"),
            Color.Parse("#D8FF8C"));

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            var sukiTheme = SukiTheme.GetInstance(this);
            sukiTheme.AddColorTheme(LazyGreenTheme);
            sukiTheme.ChangeColorTheme(LazyGreenTheme);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
