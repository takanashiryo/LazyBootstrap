using System;
using Avalonia;
using Serilog;
using LazyBootstrap.Application;
using LazyBootstrap.Platform;
using LazyBootstrap.Serialization;

namespace LazyBootstrap
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            ApplicationComposition composition = null;

            try
            {
                AppServices.InitializeSerilog(args);
                Log.Information("LazyBootstrap process started.");
                MediaUpdaterPendingUpdateService.ApplyPendingUpdate(AppServices.Paths.ApplicationDirectoryPath);

                composition = new ApplicationComposition(AppServices.Paths);
                App.Composition = composition;

                // Configuration bootstrap/migration must run before the UI starts. AppConfigStore
                // has no SukiUI dependency, so using it here is safe.
                AppConfigBootstrapper.InitializeAndMigrate(
                    AppServices.Paths.ConfigFilePath,
                    composition.AppConfig);
                Log.Information("Configuration initialized.");

                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                Log.Information("Avalonia lifetime ended.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "LazyBootstrap startup failed.");
                Environment.ExitCode = -1;
            }
            finally
            {
                App.Composition = null;
                composition?.Dispose();
                AppServices.Dispose();
            }
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .With(new Win32PlatformOptions
                {
                    CompositionMode =
                    [
                        Win32CompositionMode.WinUIComposition,
                        Win32CompositionMode.DirectComposition,
                        Win32CompositionMode.RedirectionSurface
                    ]
                })
                .UsePlatformDetect()
                .LogToTrace();
    }
}
