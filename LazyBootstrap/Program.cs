using LazyBootstrap.Services;
using System;
using Avalonia;
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
                AppServices.Initialize(args);
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                Log.Information("Avalonia lifetime ended.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "LazyBootstrap startup failed.");
            }
            finally
            {
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
