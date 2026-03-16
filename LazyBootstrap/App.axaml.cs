using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
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
                if (LazyBootstrapHost.Services == null)
                {
                    throw new InvalidOperationException("应用服务尚未初始化。");
                }

                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var mainWindow = LazyBootstrapHost.Services.GetRequiredService<MainWindow>();
                ShowPreparedMainWindowAsync(desktop, mainWindow);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static async void ShowPreparedMainWindowAsync(IClassicDesktopStyleApplicationLifetime desktop, MainWindow mainWindow)
        {
            try
            {
                await mainWindow.PrepareForDisplayAsync();
                desktop.MainWindow = mainWindow;
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to prepare main window before showing.");
                desktop.TryShutdown(-1);
            }
        }
    }
}
