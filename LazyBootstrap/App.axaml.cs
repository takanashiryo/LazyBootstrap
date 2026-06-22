using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SukiUI;
using SukiUI.Models;
using LazyBootstrap.Shell;

namespace LazyBootstrap
{
    public partial class App : Application
    {
        private static readonly SukiColorTheme LazyGreenTheme = new SukiColorTheme(
            "Lazy Green",
            Color.Parse("#8CCF43"),
            Color.Parse("#D8FF8C"));

        /// <summary>
        /// Application composition root. Set from <c>Program.Main</c> before the Avalonia
        /// lifetime starts and used to resolve the main window after the theme is loaded.
        /// </summary>
        public static IServiceProvider Services { get; set; }

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

                // SukiTheme is loaded by Initialize() above, so resolving MainWindow here is the
                // first time the SukiUI dialog/toast managers are instantiated. This satisfies the
                // SukiUI initialization-order constraint.
                var mainWindow = Services.GetRequiredService<MainWindow>();

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
