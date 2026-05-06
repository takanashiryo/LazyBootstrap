using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LazyBootstrap.Services.Security
{
    internal interface IWindowsDefenderExclusionService
    {
        Task<WindowsDefenderExclusionResult> EnsureDirectoryExcludedAsync(string directoryPath);
    }

    internal sealed class WindowsDefenderExclusionService : IWindowsDefenderExclusionService
    {
        public async Task<WindowsDefenderExclusionResult> EnsureDirectoryExcludedAsync(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return new WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus.Failed, "未提供需要添加排除项的目录。");
            }

            if (!OperatingSystem.IsWindows())
            {
                return new WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus.Skipped, "当前系统不是 Windows，已跳过 Defender 排除项处理。");
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(directoryPath);
            }
            catch (Exception ex)
            {
                return new WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus.Failed, $"解析目录路径失败：{ex.Message}");
            }

            if (!Directory.Exists(fullPath))
            {
                return new WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus.Skipped, $"目录不存在：{fullPath}");
            }

            var escapedPowerShellPath = EscapePowerShellSingleQuotedString(fullPath);
            var script = @"
$ErrorActionPreference = 'Stop'
$targetPath = '" + escapedPowerShellPath + @"'
$service = Get-Service -Name 'WinDefend' -ErrorAction SilentlyContinue
if ($null -eq $service -or $service.Status -ne 'Running') {
    Write-Output 'SKIPPED_SERVICE_STOPPED'
    exit 0
}

$statusCommand = Get-Command -Name 'Get-MpComputerStatus' -ErrorAction SilentlyContinue
$preferenceCommand = Get-Command -Name 'Get-MpPreference' -ErrorAction SilentlyContinue
$addPreferenceCommand = Get-Command -Name 'Add-MpPreference' -ErrorAction SilentlyContinue
if ($null -eq $statusCommand -or $null -eq $preferenceCommand -or $null -eq $addPreferenceCommand) {
    Write-Output 'SKIPPED_MODULE_UNAVAILABLE'
    exit 0
}

$status = Get-MpComputerStatus -ErrorAction SilentlyContinue
if ($null -eq $status) {
    Write-Output 'SKIPPED_STATUS_UNAVAILABLE'
    exit 0
}

$runningMode = [string]$status.AMRunningMode
if (-not [string]::IsNullOrWhiteSpace($runningMode) -and $runningMode -match 'Passive|Disabled') {
    Write-Output ('SKIPPED_MODE|' + $runningMode)
    exit 0
}

$preferences = Get-MpPreference -ErrorAction Stop
$existingPaths = @($preferences.ExclusionPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
foreach ($existingPath in $existingPaths) {
    try {
        $normalizedExistingPath = [System.IO.Path]::GetFullPath($existingPath)
    }
    catch {
        $normalizedExistingPath = $existingPath
    }

    if ([string]::Equals($normalizedExistingPath.TrimEnd('\'), $targetPath.TrimEnd('\'), [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Output 'EXISTS'
        exit 0
    }
}

Add-MpPreference -ExclusionPath $targetPath -ErrorAction Stop
Write-Output 'ADDED'
";

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-NonInteractive");
                startInfo.ArgumentList.Add("-ExecutionPolicy");
                startInfo.ArgumentList.Add("Bypass");
                startInfo.ArgumentList.Add("-Command");
                startInfo.ArgumentList.Add(script);

                using var process = new Process { StartInfo = startInfo };
                if (!process.Start())
                {
                    return new WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus.Failed, "无法启动 PowerShell 以处理 Defender 排除项。");
                }

                var standardOutputTask = process.StandardOutput.ReadToEndAsync();
                var standardErrorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync().ConfigureAwait(false);

                var standardOutput = (await standardOutputTask.ConfigureAwait(false)).Trim();
                var standardError = (await standardErrorTask.ConfigureAwait(false)).Trim();

                if (process.ExitCode != 0)
                {
                    var failureMessage = !string.IsNullOrWhiteSpace(standardError) ? standardError : standardOutput;
                    return new WindowsDefenderExclusionResult(
                        WindowsDefenderExclusionStatus.Failed,
                        string.IsNullOrWhiteSpace(failureMessage) ? "添加 Windows Defender 排除项失败。" : failureMessage);
                }

                return ParseResult(standardOutput);
            }
            catch (Exception ex)
            {
                return new WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus.Failed, $"处理 Windows Defender 排除项时发生错误：{ex.Message}");
            }
        }

        private static WindowsDefenderExclusionResult ParseResult(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return new WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus.Failed, "Windows Defender 未返回任何状态。");
            }

            var parts = output.Split(['|'], 2, StringSplitOptions.None);
            var code = parts[0].Trim();
            var detail = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            return code switch
            {
                "ADDED" => new WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus.Added, "已将目录添加到 Windows Defender 排除项。"),
                "EXISTS" => new WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus.AlreadyExcluded, "Windows Defender 排除项已存在。"),
                "SKIPPED_SERVICE_STOPPED" => new WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus.Skipped, "Windows Defender 服务未运行，已跳过排除项处理。"),
                "SKIPPED_MODULE_UNAVAILABLE" => new WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus.Skipped, "当前系统不可用 Windows Defender PowerShell 模块，已跳过排除项处理。"),
                "SKIPPED_STATUS_UNAVAILABLE" => new WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus.Skipped, "无法获取 Windows Defender 状态，已跳过排除项处理。"),
                "SKIPPED_MODE" => new WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus.Skipped, $"Windows Defender 当前模式为 {detail}，已跳过排除项处理。"),
                _ => new WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus.Failed, string.IsNullOrWhiteSpace(detail) ? output.Trim() : detail)
            };
        }

        private static string EscapePowerShellSingleQuotedString(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }
    }
}
