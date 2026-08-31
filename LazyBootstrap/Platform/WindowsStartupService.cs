using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Xml.Linq;

namespace LazyBootstrap.Platform
{
    internal sealed class WindowsStartupService
    {
        private const string StartupTaskName = "LazyBootstrap Startup";
        private const string SchtasksExecutableName = "schtasks.exe";

        public bool IsEnabled(string executablePath)
        {
            if (!OperatingSystem.IsWindows()
                || !TryNormalizeExecutablePath(executablePath, out var normalizedPath))
            {
                return false;
            }

            var result = RunSchtasks(
                "/Query",
                "/TN",
                StartupTaskName,
                "/XML",
                "ONE");

            return result.ExitCode == 0 && ContainsCommand(result.StandardOutput, normalizedPath);
        }

        public bool TrySetEnabled(string executablePath, bool enabled, out string error)
        {
            error = string.Empty;

            if (!OperatingSystem.IsWindows())
            {
                error = "当前平台不支持 Windows 开机自启动。";
                return false;
            }

            if (!enabled)
            {
                return TryDisable(out error);
            }

            if (!TryNormalizeExecutablePath(executablePath, out var normalizedPath))
            {
                error = "启动器程序路径无效。";
                return false;
            }

            if (!File.Exists(normalizedPath))
            {
                error = $"未找到启动器程序：{normalizedPath}";
                return false;
            }

            string currentUser;
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                currentUser = identity.Name ?? string.Empty;
            }
            catch (Exception ex)
            {
                error = $"无法读取当前 Windows 用户：{ex.Message}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(currentUser))
            {
                error = "无法读取当前 Windows 用户。";
                return false;
            }

            var result = RunSchtasks(
                "/Create",
                "/TN",
                StartupTaskName,
                "/TR",
                $"\"{normalizedPath}\"",
                "/SC",
                "ONLOGON",
                "/RU",
                currentUser,
                "/RL",
                "HIGHEST",
                "/IT",
                "/F");

            if (result.ExitCode == 0)
            {
                return true;
            }

            error = BuildCommandError(result);
            return false;
        }

        private static bool TryDisable(out string error)
        {
            error = string.Empty;

            var queryResult = RunSchtasks(
                "/Query",
                "/TN",
                StartupTaskName);
            if (queryResult.ExitCode != 0)
            {
                return true;
            }

            var result = RunSchtasks(
                "/Delete",
                "/TN",
                StartupTaskName,
                "/F");
            if (result.ExitCode == 0)
            {
                return true;
            }

            error = BuildCommandError(result);
            return false;
        }

        private static bool ContainsCommand(string xml, string expectedPath)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return false;
            }

            try
            {
                var document = XDocument.Parse(xml);
                foreach (var command in document.Descendants())
                {
                    if (!string.Equals(command.Name.LocalName, "Command", StringComparison.OrdinalIgnoreCase)
                        || !TryNormalizeExecutablePath(command.Value, out var actualPath))
                    {
                        continue;
                    }

                    if (string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool TryNormalizeExecutablePath(string executablePath, out string normalizedPath)
        {
            normalizedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return false;
            }

            try
            {
                normalizedPath = Path.GetFullPath(executablePath.Trim().Trim('"'));
                return !string.IsNullOrWhiteSpace(normalizedPath);
            }
            catch
            {
                return false;
            }
        }

        private static CommandResult RunSchtasks(params string[] arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = SchtasksExecutableName,
                    WorkingDirectory = AppContext.BaseDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                foreach (var argument in arguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return new CommandResult(-1, string.Empty, "无法启动 schtasks.exe。");
                }

                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new CommandResult(process.ExitCode, standardOutput, standardError);
            }
            catch (Exception ex)
            {
                return new CommandResult(-1, string.Empty, ex.Message);
            }
        }

        private static string BuildCommandError(CommandResult result)
        {
            string detail = !string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardError
                : result.StandardOutput;
            detail = detail?.Trim() ?? string.Empty;
            if (detail.Length > 240)
            {
                detail = detail.Substring(0, 240) + "...";
            }

            return string.IsNullOrWhiteSpace(detail)
                ? $"schtasks.exe 执行失败，退出码：{result.ExitCode}。"
                : $"schtasks.exe 执行失败，退出码：{result.ExitCode}。{detail}";
        }

        private readonly record struct CommandResult(int ExitCode, string StandardOutput, string StandardError);
    }
}
