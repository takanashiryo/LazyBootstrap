using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Microsoft.Extensions.Logging;
using LazyBootstrap.Platform;
using LazyBootstrap.Services;

namespace LazyBootstrap.UI
{
    public partial class MainWindow
    {
        private SavedataTransferService _savedataTransferService = null!;

        private void InitializeToolsServices(SavedataTransferService savedataTransferService)
        {
            _savedataTransferService = savedataTransferService ?? throw new ArgumentNullException(nameof(savedataTransferService));
        }

        private Task ClearCacheAsync()
        {
            string cachePath = Path.Combine(_paths.GetContentsDirectoryPath(), "data_mods", "_cache");
            _logger.LogInformation("Cache cleanup requested. CachePath={CachePath}", cachePath);
            try
            {
                if (Directory.Exists(cachePath))
                {
                    Directory.Delete(cachePath, true);
                    _logger.LogInformation("Cache cleanup completed.");
                    ShowInfoToast("清理缓存", "缓存已成功清理。");
                }
                else
                {
                    _logger.LogWarning("Cache cleanup skipped because the cache directory does not exist.");
                    ShowWarningToast("清理缓存", "缓存文件夹不存在。");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache cleanup failed.");
                ShowErrorToast("清理缓存失败", ex.Message);
            }

            return Task.CompletedTask;
        }

        private async Task AddFirewallRuleAsync()
        {
            const string ruleName = "SpiceTools";
            string spicePath = _paths.GetSpicePath();
            _logger.LogInformation("Firewall rule creation requested.");

            bool confirmed = await ShowDialogAsync(
                "添加防火墙规则",
                "确认要执行吗？\n如果之前已经添加过规则，将会重复添加。",
                "确认",
                "取消",
                NotificationType.Warning);
            if (!confirmed)
            {
                _logger.LogInformation("Firewall rule creation cancelled by user.");
                return;
            }

            if (!File.Exists(spicePath))
            {
                _logger.LogWarning("Firewall rule creation failed because spice64.exe was not found: {SpicePath}", spicePath);
                ShowErrorToast("添加防火墙规则失败", $"未找到目标程序：{spicePath}");
                return;
            }

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow program=\"{spicePath}\" enable=yes profile=public,private",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                });

                if (process == null)
                {
                    _logger.LogWarning("Firewall rule creation failed because netsh process creation returned null.");
                    ShowErrorToast("添加防火墙规则失败", "未能启动 netsh。");
                    return;
                }

                await process.WaitForExitAsync();
                _logger.LogInformation("Firewall rule netsh process exited. ExitCode={ExitCode}", process.ExitCode);
                if (process.ExitCode != 0)
                {
                    ShowErrorToast("添加防火墙规则失败", $"netsh 退出代码：{process.ExitCode}");
                    return;
                }

                ShowInfoToast("防火墙规则", "防火墙规则添加完成。");
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                _logger.LogWarning("Firewall rule creation cancelled at UAC prompt.");
                ShowWarningToast("防火墙规则", "用户取消了 UAC 提示，未添加规则。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Firewall rule creation failed.");
                ShowErrorToast("添加防火墙规则失败", ex.Message);
            }
        }

        private Task OpenAudioPanelAsync()
        {
            try
            {
                _logger.LogInformation("Opening audio control panel.");
                ProcessExecutionHelper.OpenControlPanel("mmsys.cpl");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open audio control panel.");
                ShowErrorToast("打开音频面板失败", ex.Message);
            }

            return Task.CompletedTask;
        }

        private async Task InstallRuntimeAsync(Action<string, double> reportProgress)
        {
            string runtimePath = _paths.GetRuntimeDirectoryPath();
            string dxSetupPath = Path.Combine(runtimePath, "directx", "DXSETUP.exe");
            string vcRedistPath = Path.Combine(runtimePath, "vcredist", "VisualCppRedist_AIO_x86_x64.exe");
            bool hasDirectXInstaller = File.Exists(dxSetupPath);
            bool hasVcRedistInstaller = File.Exists(vcRedistPath);
            int totalInstallers = (hasDirectXInstaller ? 1 : 0) + (hasVcRedistInstaller ? 1 : 0);
            int completedInstallers = 0;
            _logger.LogInformation("Runtime installation requested. HasDirectXInstaller={HasDirectXInstaller}, HasVcRedistInstaller={HasVcRedistInstaller}", hasDirectXInstaller, hasVcRedistInstaller);

            if (!hasDirectXInstaller && !hasVcRedistInstaller)
            {
                _logger.LogWarning("Runtime installation failed because no installer was found.");
                ShowErrorToast("安装运行库失败", "未找到运行库安装程序。请确保文件存在。");
                return;
            }

            // Track the most recent progress value so cancellation messages keep the current bar position.
            double lastProgress = 5d;
            void Report(string text, double value)
            {
                lastProgress = value;
                reportProgress?.Invoke(text, value);
            }

            Report("正在准备安装运行库...", 5d);

            try
            {
                if (hasDirectXInstaller)
                {
                    Report(
                        "正在安装 DirectX...",
                        CalculateRuntimeInstallProgress(totalInstallers, completedInstallers, true));
                    var dxResult = await ProcessExecutionHelper.RunElevatedInstallerAsync(dxSetupPath, "/silent", Path.Combine(runtimePath, "directx"));
                    _logger.LogInformation("DirectX installer exited. ExitCode={ExitCode}", dxResult);
                    if (dxResult == -1223)
                    {
                        Report("DirectX 安装已取消", lastProgress);
                        ShowWarningToast("安装运行库", "用户取消了 DirectX 安装授权。");
                        return;
                    }

                    if (dxResult != 0)
                    {
                        ShowWarningToast("DirectX 安装", $"DirectX 安装返回代码: {dxResult}");
                    }

                    completedInstallers++;
                    Report(
                        hasVcRedistInstaller ? "DirectX 安装完成，正在准备下一步..." : "DirectX 安装完成",
                        CalculateRuntimeInstallProgress(totalInstallers, completedInstallers, false));
                }

                if (hasVcRedistInstaller)
                {
                    Report(
                        "正在安装 Visual C++ Redistributable...",
                        CalculateRuntimeInstallProgress(totalInstallers, completedInstallers, true));
                    var vcResult = await ProcessExecutionHelper.RunElevatedInstallerAsync(vcRedistPath, "/y", Path.Combine(runtimePath, "vcredist"));
                    _logger.LogInformation("Visual C++ Redistributable installer exited. ExitCode={ExitCode}", vcResult);
                    if (vcResult == -1223)
                    {
                        Report("Visual C++ Redistributable 安装已取消", lastProgress);
                        ShowWarningToast("安装运行库", "用户取消了 Visual C++ Redistributable 安装授权。");
                        return;
                    }

                    if (vcResult != 0)
                    {
                        ShowWarningToast("VC++ Redist 安装", $"Visual C++ Redistributable 安装返回代码: {vcResult}");
                    }

                    completedInstallers++;
                    Report(
                        "Visual C++ Redistributable 安装完成",
                        CalculateRuntimeInstallProgress(totalInstallers, completedInstallers, false));
                }

                Report("运行库安装完成", 100d);
                await Task.Delay(250);
                _logger.LogInformation("Runtime installation workflow completed.");
                ShowInfoToast("运行库安装完成", "DirectX 和 Visual C++ Redistributable 安装完成。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Runtime installation failed.");
                ShowErrorToast("安装运行库失败", ex.Message);
            }
        }

        private static double CalculateRuntimeInstallProgress(int totalInstallers, int completedInstallers, bool installerRunning)
        {
            if (totalInstallers <= 0)
            {
                return 100d;
            }

            const double startProgress = 5d;
            const double maxProgressBeforeCompletion = 95d;
            double units = completedInstallers + (installerRunning ? 0.5d : 0d);
            return startProgress + ((maxProgressBeforeCompletion - startProgress) * (units / totalInstallers));
        }

        private async Task BackupSavedataAsync()
        {
            _logger.LogInformation("Savedata backup requested.");
            string sevenZipPath = _paths.ResolveSevenZipExecutablePath();
            if (!File.Exists(sevenZipPath))
            {
                _logger.LogWarning("Savedata backup failed because 7za.exe was not found: {SevenZipPath}", sevenZipPath);
                ShowErrorToast("存档备份失败", $"未找到 7za.exe：{sevenZipPath}");
                return;
            }

            var entries = _savedataTransferService.GetCurrentSavedataEntries();
            _logger.LogInformation("Savedata backup entries resolved. EntryCount={EntryCount}", entries.Count);
            if (entries.Count == 0)
            {
                _logger.LogWarning("Savedata backup skipped because no entries were found.");
                ShowWarningToast("存档备份", "未找到可备份的数据");
                return;
            }

            string backupDirectory = _paths.GetSavedataBackupDirectoryPath();
            Directory.CreateDirectory(backupDirectory);
            string backupFilePath = Path.Combine(backupDirectory, $"savedata_{DateTime.Now:yyyyMMdd_HHmmss}.7z");
            string stagingDirectory = _savedataTransferService.CreateTemporaryWorkingDirectory("backup");

            try
            {
                _savedataTransferService.StageEntries(entries, stagingDirectory);
                var stagedTopLevelEntries = Directory.GetFileSystemEntries(stagingDirectory)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();
                if (stagedTopLevelEntries.Count == 0)
                {
                    _logger.LogWarning("Savedata backup staging generated no compressible entries.");
                    ShowWarningToast("存档备份", "未生成可压缩的备份内容。");
                    return;
                }

                string arguments = $"a -t7z \"{backupFilePath}\" {string.Join(" ", stagedTopLevelEntries.Select(name => $"\"{name}\""))} -mx=9";
                var result = await ProcessExecutionHelper.RunProcessCaptureAsync(sevenZipPath, arguments, stagingDirectory);
                _logger.LogInformation("Savedata backup 7za process exited. ExitCode={ExitCode}", result.ExitCode);
                if (result.ExitCode != 0)
                {
                    _logger.LogWarning("Savedata backup failed during archive creation.");
                    ShowErrorToast("存档备份失败", ProcessExecutionHelper.GetProcessErrorDetail(result));
                    return;
                }

                _logger.LogInformation("Savedata backup completed. BackupPath={BackupPath}", backupFilePath);
                ShowInfoToast("存档备份完成", $"已备份到：{backupFilePath}");
            }
            finally
            {
                _logger.LogDebug("Deleting savedata backup staging directory.");
                _savedataTransferService.DeleteDirectoryIfExists(stagingDirectory);
            }
        }

        private async Task ImportSavedataAsync()
        {
            _logger.LogInformation("Savedata import requested.");
            string sevenZipPath = _paths.ResolveSevenZipExecutablePath();
            if (!File.Exists(sevenZipPath))
            {
                _logger.LogWarning("Savedata import failed because 7za.exe was not found: {SevenZipPath}", sevenZipPath);
                ShowErrorToast("存档导入失败", $"未找到 7za.exe：{sevenZipPath}");
                return;
            }

            string archivePath = await PickFileAsync("选择存档备份文件", new[] { "*.7z" });
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                _logger.LogInformation("Savedata import cancelled before archive selection.");
                return;
            }
            _logger.LogInformation("Savedata import archive selected: {ArchivePath}", archivePath);

            var targetEntries = _savedataTransferService.GetCurrentSavedataTargets();
            if (SavedataTransferService.HasExistingTargets(targetEntries))
            {
                bool confirmed = await ShowDialogAsync(
                    "存档导入覆盖提示",
                    "检测到当前游戏目录或氧无目录中已有存档文件，是否覆盖？",
                    "覆盖",
                    "取消",
                    NotificationType.Warning);
                if (!confirmed)
                {
                    _logger.LogInformation("Savedata import cancelled at overwrite confirmation.");
                    return;
                }
            }

            string extractionDirectory = _savedataTransferService.CreateTemporaryWorkingDirectory("import");
            try
            {
                string arguments = $"x \"{archivePath}\" -o\"{extractionDirectory}\" -y";
                var extractionResult = await ProcessExecutionHelper.RunProcessCaptureAsync(sevenZipPath, arguments, extractionDirectory);
                _logger.LogInformation("Savedata import extraction exited. ExitCode={ExitCode}", extractionResult.ExitCode);
                if (extractionResult.ExitCode != 0)
                {
                    _logger.LogWarning("Savedata import failed during extraction.");
                    ShowErrorToast("存档导入失败", ProcessExecutionHelper.GetProcessErrorDetail(extractionResult));
                    return;
                }

                var extractedEntries = _savedataTransferService.BuildArchiveEntriesFromDirectory(extractionDirectory);
                _logger.LogInformation("Savedata import extracted entries resolved. EntryCount={EntryCount}", extractedEntries.Count);
                if (extractedEntries.Count == 0)
                {
                    _logger.LogWarning("Savedata import skipped because the archive contained no importable entries.");
                    ShowWarningToast("存档导入", "备份文件中未找到可导入的数据");
                    return;
                }

                await Task.Run(() => _savedataTransferService.CopyEntries(extractedEntries));
                _logger.LogInformation("Savedata import completed.");
                ShowInfoToast("存档导入完成", "已导入到当前设置的游戏目录和氧无目录。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Savedata import failed.");
                ShowErrorToast("存档导入失败", ex.Message);
            }
            finally
            {
                _logger.LogDebug("Deleting savedata import extraction directory.");
                _savedataTransferService.DeleteDirectoryIfExists(extractionDirectory);
            }
        }

        private IReadOnlyList<SavedataTransferEntry> GetMigrationEntries(string gameDirectory, string asphyxiaDirectory)
        {
            return _savedataTransferService.BuildMigrationEntries(gameDirectory, asphyxiaDirectory);
        }

        private async Task MigrateSavedataAsync(IReadOnlyList<SavedataTransferEntry> selectedEntries)
        {
            ArgumentNullException.ThrowIfNull(selectedEntries);
            try
            {
                await Task.Run(() => _savedataTransferService.CopyEntries(selectedEntries));
                _logger.LogInformation("Savedata migration completed.");
                ShowInfoToast("存档迁移完成", "已迁移到当前设置的游戏目录和氧无目录。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Savedata migration failed.");
                ShowErrorToast("存档迁移失败", ex.Message);
            }
        }

        // Path normalization delegated to PathHelper.NormalizePath
    }
}
