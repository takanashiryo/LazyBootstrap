using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Avalonia;

namespace LazyBootstrap
{
    internal class Program
    {
        private const uint MbOk = 0x00000000;
        private const uint MbIconWarning = 0x00000030;
        private const uint MbIconError = 0x00000010;

        private enum ElevationOutcome
        {
            ContinueCurrentProcess,
            RelaunchedElevated,
            Cancelled,
            Failed
        }

        [STAThread]
        public static void Main(string[] args)
        {
            var elevationResult = EnsureElevated(args);
            if (elevationResult.Outcome != ElevationOutcome.ContinueCurrentProcess)
            {
                if (!string.IsNullOrWhiteSpace(elevationResult.Message))
                {
                    ShowStartupMessage(
                        elevationResult.Message,
                        elevationResult.Outcome == ElevationOutcome.Cancelled ? MbIconWarning : MbIconError);
                }

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

        private static (ElevationOutcome Outcome, string Message) EnsureElevated(string[] args)
        {
            if (!OperatingSystem.IsWindows())
            {
                return (ElevationOutcome.ContinueCurrentProcess, string.Empty);
            }

            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        return (ElevationOutcome.ContinueCurrentProcess, string.Empty);
                    }
                }

                var exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    return (ElevationOutcome.Failed, "无法获取当前程序路径，不能重新以管理员权限启动。");
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
                return (ElevationOutcome.RelaunchedElevated, string.Empty);
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                Trace.TraceWarning("管理员权限获取失败");
                return (ElevationOutcome.Cancelled, "程序启动失败，缺少权限！");
            }
            catch (Exception ex)
            {
                Trace.TraceError($"管理员权限提升失败: {ex}");
                return (ElevationOutcome.Failed, $"无法以管理员权限重新启动程序：{ex.Message}");
            }
        }

        private static void ShowStartupMessage(string message, uint iconFlags)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Trace.TraceError(message);

            if (OperatingSystem.IsWindows())
            {
                MessageBox(IntPtr.Zero, message, "LazyBootstrap", MbOk | iconFlags);
                return;
            }

            Console.Error.WriteLine(message);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

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
