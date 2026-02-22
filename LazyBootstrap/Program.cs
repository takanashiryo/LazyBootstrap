using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using Avalonia;

namespace LazyBootstrap
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (!EnsureElevated(args))
            {
                return;
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
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

        private static bool EnsureElevated(string[] args)
        {
            if (!OperatingSystem.IsWindows())
            {
                return true;
            }

            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        return true;
                    }
                }

                var exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    return true;
                }

                var argString = string.Join(" ", args.Select(QuoteArg));
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = argString,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(psi);
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static string QuoteArg(string arg)
        {
            if (string.IsNullOrEmpty(arg))
            {
                return "\"\"";
            }

            return arg.IndexOfAny([' ', '\t', '"']) >= 0
                ? "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
                : arg;
        }
    }
}
