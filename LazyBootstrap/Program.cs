using LazyBootstrap.Services;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Avalonia;
using Serilog;

namespace LazyBootstrap
{
    public class Program
    {
        private const string RelaunchedElevatedMessage = "已将启动流程转交给管理员权限的新进程。";
        private const string MissingExecutablePathMessage = "无法获取当前程序路径，不能重新以管理员权限启动。";
        private const string RelaunchFailedTraceMessage = "管理员权限提升失败：系统未能启动新的提权进程。";
        private const string RelaunchFailedMessage = "无法以管理员权限重新启动程序：系统未能启动新的提权进程。";
        private const string ElevationCancelledTraceMessage = "管理员权限获取已取消。";
        private const string ElevationCancelledMessage = "程序启动已取消：未授予管理员权限。";

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
                    LogStartupMessage(
                        elevationResult.Message,
                        elevationResult.Outcome == ElevationOutcome.Cancelled ? TraceEventType.Warning : TraceEventType.Error);
                }
                else if (elevationResult.Outcome == ElevationOutcome.RelaunchedElevated)
                {
                    Trace.TraceInformation(RelaunchedElevatedMessage);
                }

                return;
            }

            try
            {
                AppServices.Initialize(args);
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "LazyBootstrap startup failed.");
                throw;
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

        private static (ElevationOutcome Outcome, string Message) EnsureElevated(string[] args)
        {
            if (!OperatingSystem.IsWindows())
            {
                return (ElevationOutcome.ContinueCurrentProcess, string.Empty);
            }

            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    return (ElevationOutcome.ContinueCurrentProcess, string.Empty);
                }

                var exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    return (ElevationOutcome.Failed, MissingExecutablePathMessage);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                foreach (var arg in args)
                {
                    startInfo.ArgumentList.Add(arg);
                }

                var elevatedProcess = Process.Start(startInfo);
                if (elevatedProcess is null)
                {
                    Trace.TraceError(RelaunchFailedTraceMessage);
                    return (ElevationOutcome.Failed, RelaunchFailedMessage);
                }

                return (ElevationOutcome.RelaunchedElevated, string.Empty);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                Trace.TraceWarning(ElevationCancelledTraceMessage);
                return (ElevationOutcome.Cancelled, ElevationCancelledMessage);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"管理员权限提升失败: {ex}");
                return (ElevationOutcome.Failed, $"无法以管理员权限重新启动程序：{ex.Message}");
            }
        }

        private static void LogStartupMessage(string message, TraceEventType traceEventType)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            switch (traceEventType)
            {
                case TraceEventType.Warning:
                    Trace.TraceWarning(message);
                    break;
                case TraceEventType.Error:
                case TraceEventType.Critical:
                    Trace.TraceError(message);
                    break;
                default:
                    Trace.TraceInformation(message);
                    break;
            }

            Console.Error.WriteLine(message);
        }
    }
}
