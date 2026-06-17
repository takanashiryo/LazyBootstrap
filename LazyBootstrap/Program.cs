using System;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace LazyBootstrap
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                AppServices.InitializeSerilog(args);
                Log.Information("LazyBootstrap process started.");

                // Build the DI container. Lazy singletons keep SukiUI managers uncreated until
                // MainWindow is resolved (after SukiTheme loads); do NOT enable ValidateOnBuild.
                var serviceProvider = new ServiceCollection()
                    .AddLazyBootstrapServices(AppServices.RuntimeContext)
                    .BuildServiceProvider();
                App.Services = serviceProvider;

                // Configuration bootstrap/migration must run before the UI starts. ConfigHandler
                // has no SukiUI dependency, so resolving it here is safe.
                AppConfigBootstrapper.InitializeAndMigrate(
                    AppServices.RuntimeContext.ConfigFilePath,
                    serviceProvider.GetRequiredService<ConfigHandler>());
                Log.Information("Configuration initialized and migrated.");

                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                Log.Information("Avalonia lifetime ended.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "LazyBootstrap startup failed.");
            }
            finally
            {
                (App.Services as IDisposable)?.Dispose();
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
