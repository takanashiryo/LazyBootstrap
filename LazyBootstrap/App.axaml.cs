using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Serilog;
using SukiUI;
using SukiUI.Models;
using LazyBootstrap.Services;
using LazyBootstrap.Views;

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
            Log.Information("Avalonia application initialization started.");
            AvaloniaXamlLoader.Load(this);
            var sukiTheme = SukiTheme.GetInstance(this);
            sukiTheme.AddColorTheme(LazyGreenTheme);
            sukiTheme.ChangeColorTheme(LazyGreenTheme);

            if (Design.IsDesignMode)
            {
                Log.Information("Avalonia application initialized in design mode.");
                return;
            }

            // SukiUI managers must be created AFTER SukiTheme is loaded.
            AppServices.InitSukiManagers();
            Log.Information("Avalonia application initialization completed.");
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (Design.IsDesignMode)
            {
                base.OnFrameworkInitializationCompleted();
                return;
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Log.Information("Framework initialization completed. Preparing main window.");
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                var mainWindow = new MainWindow(
                    AppServices.ShellState,
                    AppServices.Paths,
                    AppServices.LaunchWorkflow,
                    AppServices.DisplayWorkflow,
                    AppServices.EnvironmentScan,
                    AppServices.SettingsWorkflow,
                    AppServices.DialogManager,
                    AppServices.ToastManager,
                    AppServices.UI,
                    AppServices.CreateLogger<MainWindow>());

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
                Log.Information("Main window prepared and shown.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to prepare main window before showing.");
                desktop.TryShutdown(-1);
            }
        }
    }
}
