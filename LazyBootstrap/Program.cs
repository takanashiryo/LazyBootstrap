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
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                AppServices.InitializeSerilog(args);
                if (!EnsureElevated(args))
                {
                    return;
                }

                AppServices.Initialize(args);
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
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

        private static bool EnsureElevated(string[] args)
        {
            if (!OperatingSystem.IsWindows())
            {
                return true;
            }

            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    return true;
                }

                var exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    Log.Error("无法获取当前程序路径，不能重新以管理员权限启动。");
                    return false;
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
                    Log.Error("管理员权限提升失败：系统未能启动新的提权进程。");
                    return false;
                }

                Log.Information("已将启动流程转交给管理员权限的新进程。");
                return false;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                Log.Warning("程序启动已取消：未授予管理员权限。");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "管理员权限提升失败。");
                return false;
            }
        }
    }
}
