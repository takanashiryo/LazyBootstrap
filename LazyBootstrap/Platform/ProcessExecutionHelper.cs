using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading.Tasks;

namespace LazyBootstrap.Platform
{
    internal static class ProcessExecutionHelper
    {
        public const int ElevationCancelledExitCode = -1223;

        public static bool IsCurrentProcessElevated()
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static Process StartShellProcess(
            string fileName,
            string workingDirectory,
            bool runAsAdministrator,
            Action<ProcessStartInfo> configure = null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                UseShellExecute = true
            };

            if (runAsAdministrator && OperatingSystem.IsWindows())
            {
                startInfo.Verb = "runas";
            }

            configure?.Invoke(startInfo);
            return Process.Start(startInfo);
        }

        public static async Task<int> RunShellProcessAsync(
            string fileName,
            string workingDirectory,
            bool runAsAdministrator,
            Action<ProcessStartInfo> configure = null)
        {
            using var process = StartShellProcess(fileName, workingDirectory, runAsAdministrator, configure);
            if (process == null)
            {
                return -1;
            }

            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode;
        }

        public static async Task<int> RunElevatedInstallerAsync(string filePath, string arguments, string workingDirectory)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = filePath,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process == null)
                {
                    return -1;
                }

                await process.WaitForExitAsync();
                return process.ExitCode;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return ElevationCancelledExitCode;
            }
        }

        public static async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessCaptureAsync(string fileName, string arguments, string workingDirectory)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                return (-1, string.Empty, "进程创建失败");
            }

            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            string stdOut = await stdOutTask;
            string stdErr = await stdErrTask;
            return (process.ExitCode, stdOut, stdErr);
        }

        public static string GetProcessErrorDetail((int ExitCode, string StdOut, string StdErr) processResult)
        {
            string detail = !string.IsNullOrWhiteSpace(processResult.StdErr)
                ? processResult.StdErr
                : processResult.StdOut;

            if (string.IsNullOrWhiteSpace(detail))
            {
                return $"命令执行失败，退出码：{processResult.ExitCode}";
            }

            string compactDetail = detail.Trim();
            if (compactDetail.Length > 180)
            {
                compactDetail = compactDetail.Substring(0, 180) + "...";
            }

            return $"退出码：{processResult.ExitCode}，{compactDetail}";
        }

        public static void OpenLogFolderAndSelectFile(string logPath)
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{logPath}\"",
                UseShellExecute = true
            });
        }

        public static void OpenControlPanel(string arguments)
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "control.exe",
                Arguments = arguments,
                UseShellExecute = true
            });
        }
    }
}
