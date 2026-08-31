using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Serilog;
using SukiUI;
using SukiUI.Models;
using LazyBootstrap.Application;
using LazyBootstrap.Services;
using LazyBootstrap.UI;
using AvaloniaApplication = Avalonia.Application;

namespace LazyBootstrap
{
    public partial class App : AvaloniaApplication
    {
        private static readonly SukiColorTheme LazyGreenTheme = new SukiColorTheme(
            "Lazy Green",
            Color.Parse("#8CCF43"),
            Color.Parse("#D8FF8C"));

        internal static ApplicationComposition Composition { get; set; }

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

                // Create the UI object graph only after Initialize() has loaded SukiTheme.
                var composition = Composition
                    ?? throw new InvalidOperationException("Application composition has not been initialized.");
                var mainWindow = composition.CreateMainWindow();

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
