using System;
using SystemEnvironment = System.Environment;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Layout;
using Microsoft.Extensions.Logging;

namespace LazyBootstrap.Services.Tools
{
    public sealed class ToolsWorkflowService
    {
        private readonly LauncherPaths _paths;
        private readonly SavedataTransferPlanner _savedataTransferPlanner;
        private readonly UiInteractionService _uiInteractionService;
        private readonly ShellStateService _shellStateService;
        private readonly ILogger<ToolsWorkflowService> _logger;

        public ToolsWorkflowService(
            LauncherPaths paths,
            SavedataTransferPlanner savedataTransferPlanner,
            UiInteractionService uiInteractionService,
            ShellStateService shellStateService,
            ILogger<ToolsWorkflowService> logger)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _savedataTransferPlanner = savedataTransferPlanner ?? throw new ArgumentNullException(nameof(savedataTransferPlanner));
            _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
            _shellStateService = shellStateService ?? throw new ArgumentNullException(nameof(shellStateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task ClearCacheAsync()
        {
            string cachePath = Path.Combine(_paths.GetContentsDirectoryPath(), "data_mods", "_cache");
            _logger.LogInformation("Cache cleanup requested. CachePath={CachePath}", cachePath);
            try
            {
                if (Directory.Exists(cachePath))
                {
                    Directory.Delete(cachePath, true);
                    _logger.LogInformation("Cache cleanup completed.");
                    _uiInteractionService.ShowInfoToast("清理缓存", "缓存已成功清理。");
                }
                else
                {
                    _logger.LogWarning("Cache cleanup skipped because the cache directory does not exist.");
                    _uiInteractionService.ShowWarningToast("清理缓存", "缓存文件夹不存在。");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache cleanup failed.");
                _uiInteractionService.ShowErrorToast("清理缓存失败", ex.Message);
            }

            return Task.CompletedTask;
        }

        public async Task AddFirewallRuleAsync()
        {
            const string ruleName = "SpiceTools";
            string spicePath = _paths.GetSpicePath();
            _logger.LogInformation("Firewall rule creation requested.");

            bool confirmed = await _uiInteractionService.ShowDialogAsync(
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
                _uiInteractionService.ShowErrorToast("添加防火墙规则失败", $"未找到目标程序：{spicePath}");
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
                    _uiInteractionService.ShowErrorToast("添加防火墙规则失败", "未能启动 netsh。");
                    return;
                }

                await process.WaitForExitAsync();
                _logger.LogInformation("Firewall rule netsh process exited. ExitCode={ExitCode}", process.ExitCode);
                if (process.ExitCode != 0)
                {
                    _uiInteractionService.ShowErrorToast("添加防火墙规则失败", $"netsh 退出代码：{process.ExitCode}");
                    return;
                }

                _uiInteractionService.ShowInfoToast("防火墙规则", "防火墙规则添加完成。");
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                _logger.LogWarning("Firewall rule creation cancelled at UAC prompt.");
                _uiInteractionService.ShowWarningToast("防火墙规则", "用户取消了 UAC 提示，未添加规则。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Firewall rule creation failed.");
                _uiInteractionService.ShowErrorToast("添加防火墙规则失败", ex.Message);
            }
        }

        public Task OpenAudioPanelAsync()
        {
            try
            {
                _logger.LogInformation("Opening audio control panel.");
                ProcessExecutionHelper.OpenControlPanel("mmsys.cpl");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open audio control panel.");
                _uiInteractionService.ShowErrorToast("打开音频面板失败", ex.Message);
            }

            return Task.CompletedTask;
        }

        public async Task InstallRuntimeAsync(Action<bool> setVisible, Action<string, double> setProgress)
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
                _uiInteractionService.ShowErrorToast("安装运行库失败", "未找到运行库安装程序。请确保文件存在。");
                return;
            }

            // Track the most recent progress value so cancellation messages keep the current bar position.
            double lastProgress = 5d;
            void Report(string text, double value)
            {
                lastProgress = value;
                setProgress(text, value);
            }

            setVisible(true);
            Report("正在准备安装运行库...", 5d);
            _shellStateService.IsInteractionEnabled = false;

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
                        _uiInteractionService.ShowWarningToast("安装运行库", "用户取消了 DirectX 安装授权。");
                        return;
                    }

                    if (dxResult != 0)
                    {
                        _uiInteractionService.ShowWarningToast("DirectX 安装", $"DirectX 安装返回代码: {dxResult}");
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
                        _uiInteractionService.ShowWarningToast("安装运行库", "用户取消了 Visual C++ Redistributable 安装授权。");
                        return;
                    }

                    if (vcResult != 0)
                    {
                        _uiInteractionService.ShowWarningToast("VC++ Redist 安装", $"Visual C++ Redistributable 安装返回代码: {vcResult}");
                    }

                    completedInstallers++;
                    Report(
                        "Visual C++ Redistributable 安装完成",
                        CalculateRuntimeInstallProgress(totalInstallers, completedInstallers, false));
                }

                Report("运行库安装完成", 100d);
                await Task.Delay(250);
                _logger.LogInformation("Runtime installation workflow completed.");
                _uiInteractionService.ShowInfoToast("运行库安装完成", "DirectX 和 Visual C++ Redistributable 安装完成。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Runtime installation failed.");
                _uiInteractionService.ShowErrorToast("安装运行库失败", ex.Message);
            }
            finally
            {
                setVisible(false);
                _shellStateService.IsInteractionEnabled = true;
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

        public async Task BackupSavedataAsync()
        {
            _logger.LogInformation("Savedata backup requested.");
            string sevenZipPath = _paths.ResolveSevenZipExecutablePath();
            if (!File.Exists(sevenZipPath))
            {
                _logger.LogWarning("Savedata backup failed because 7za.exe was not found: {SevenZipPath}", sevenZipPath);
                _uiInteractionService.ShowErrorToast("存档备份失败", $"未找到 7za.exe：{sevenZipPath}");
                return;
            }

            var entries = _savedataTransferPlanner.GetCurrentSavedataEntries();
            _logger.LogInformation("Savedata backup entries resolved. EntryCount={EntryCount}", entries.Count);
            if (entries.Count == 0)
            {
                _logger.LogWarning("Savedata backup skipped because no entries were found.");
                _uiInteractionService.ShowWarningToast("存档备份", "未找到可备份的数据");
                return;
            }

            string backupDirectory = _paths.GetSavedataBackupDirectoryPath();
            Directory.CreateDirectory(backupDirectory);
            string backupFilePath = Path.Combine(backupDirectory, $"savedata_{DateTime.Now:yyyyMMdd_HHmmss}.7z");
            string stagingDirectory = SavedataTransferOperations.CreateTemporaryWorkingDirectory("backup");

            try
            {
                SavedataTransferOperations.StageEntries(entries, stagingDirectory);
                var stagedTopLevelEntries = Directory.GetFileSystemEntries(stagingDirectory)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();
                if (stagedTopLevelEntries.Count == 0)
                {
                    _logger.LogWarning("Savedata backup staging generated no compressible entries.");
                    _uiInteractionService.ShowWarningToast("存档备份", "未生成可压缩的备份内容。");
                    return;
                }

                string arguments = $"a -t7z \"{backupFilePath}\" {string.Join(" ", stagedTopLevelEntries.Select(name => $"\"{name}\""))} -mx=9";
                var result = await ProcessExecutionHelper.RunProcessCaptureAsync(sevenZipPath, arguments, stagingDirectory);
                _logger.LogInformation("Savedata backup 7za process exited. ExitCode={ExitCode}", result.ExitCode);
                if (result.ExitCode != 0)
                {
                    _logger.LogWarning("Savedata backup failed during archive creation.");
                    _uiInteractionService.ShowErrorToast("存档备份失败", ProcessExecutionHelper.GetProcessErrorDetail(result));
                    return;
                }

                _logger.LogInformation("Savedata backup completed. BackupPath={BackupPath}", backupFilePath);
                _uiInteractionService.ShowInfoToast("存档备份完成", $"已备份到：{backupFilePath}");
            }
            finally
            {
                _logger.LogDebug("Deleting savedata backup staging directory.");
                SavedataTransferOperations.DeleteDirectoryIfExists(stagingDirectory);
            }
        }

        public async Task ImportSavedataAsync()
        {
            _logger.LogInformation("Savedata import requested.");
            string sevenZipPath = _paths.ResolveSevenZipExecutablePath();
            if (!File.Exists(sevenZipPath))
            {
                _logger.LogWarning("Savedata import failed because 7za.exe was not found: {SevenZipPath}", sevenZipPath);
                _uiInteractionService.ShowErrorToast("存档导入失败", $"未找到 7za.exe：{sevenZipPath}");
                return;
            }

            string archivePath = await _uiInteractionService.PickFileAsync("选择存档备份文件", new[] { "*.7z" });
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                _logger.LogInformation("Savedata import cancelled before archive selection.");
                return;
            }
            _logger.LogInformation("Savedata import archive selected: {ArchivePath}", archivePath);

            var targetEntries = _savedataTransferPlanner.GetCurrentSavedataTargets();
            if (SavedataTransferPlanner.HasExistingTargets(targetEntries))
            {
                bool confirmed = await _uiInteractionService.ShowDialogAsync(
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

            string extractionDirectory = SavedataTransferOperations.CreateTemporaryWorkingDirectory("import");
            try
            {
                string arguments = $"x \"{archivePath}\" -o\"{extractionDirectory}\" -y";
                var extractionResult = await ProcessExecutionHelper.RunProcessCaptureAsync(sevenZipPath, arguments, extractionDirectory);
                _logger.LogInformation("Savedata import extraction exited. ExitCode={ExitCode}", extractionResult.ExitCode);
                if (extractionResult.ExitCode != 0)
                {
                    _logger.LogWarning("Savedata import failed during extraction.");
                    _uiInteractionService.ShowErrorToast("存档导入失败", ProcessExecutionHelper.GetProcessErrorDetail(extractionResult));
                    return;
                }

                var extractedEntries = _savedataTransferPlanner.BuildArchiveEntriesFromDirectory(extractionDirectory);
                _logger.LogInformation("Savedata import extracted entries resolved. EntryCount={EntryCount}", extractedEntries.Count);
                if (extractedEntries.Count == 0)
                {
                    _logger.LogWarning("Savedata import skipped because the archive contained no importable entries.");
                    _uiInteractionService.ShowWarningToast("存档导入", "备份文件中未找到可导入的数据");
                    return;
                }

                await Task.Run(() => SavedataTransferOperations.CopyEntries(extractedEntries));
                _logger.LogInformation("Savedata import completed.");
                _uiInteractionService.ShowInfoToast("存档导入完成", "已导入到当前设置的游戏目录和氧无目录。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Savedata import failed.");
                _uiInteractionService.ShowErrorToast("存档导入失败", ex.Message);
            }
            finally
            {
                _logger.LogDebug("Deleting savedata import extraction directory.");
                SavedataTransferOperations.DeleteDirectoryIfExists(extractionDirectory);
            }
        }

        public async Task MigrateSavedataAsync()
        {
            _logger.LogInformation("Savedata migration requested.");
            var directories = await PromptForMigrationDirectoriesAsync();
            if (directories == null)
            {
                _logger.LogInformation("Savedata migration cancelled before directory selection.");
                return;
            }

            var migrationEntries = _savedataTransferPlanner.BuildMigrationEntries(directories.Value.GameDirectory, directories.Value.AsphyxiaDirectory);
            _logger.LogInformation("Savedata migration entries resolved. EntryCount={EntryCount}", migrationEntries.Count);
            if (migrationEntries.Count == 0)
            {
                _uiInteractionService.ShowWarningToast("存档迁移", "在指定目录中未找到可迁移的数据");
                return;
            }

            var selectedEntries = await PromptForMigrationSelectionAsync(migrationEntries);
            if (selectedEntries == null || selectedEntries.Count == 0)
            {
                _logger.LogInformation("Savedata migration cancelled before copy.");
                return;
            }
            _logger.LogInformation("Savedata migration selection completed. SelectedEntryCount={SelectedEntryCount}", selectedEntries.Count);

            var overwriteEntries = selectedEntries
                .Where(entry => entry.IsDirectory ? Directory.Exists(entry.DestinationPath) : File.Exists(entry.DestinationPath))
                .ToList();
            if (overwriteEntries.Count > 0)
            {
                bool confirmed = await _uiInteractionService.ShowDialogAsync(
                    "存档迁移覆盖提示",
                    "检测到以下目标文件已存在，是否覆盖？" + SystemEnvironment.NewLine + string.Join(SystemEnvironment.NewLine, overwriteEntries.Select(entry => $"• {entry.DisplayName}")),
                    "覆盖",
                    "取消",
                    NotificationType.Warning);
                if (!confirmed)
                {
                    _logger.LogInformation("Savedata migration cancelled at overwrite confirmation.");
                    return;
                }
            }

            try
            {
                await Task.Run(() => SavedataTransferOperations.CopyEntries(selectedEntries));
                _logger.LogInformation("Savedata migration completed.");
                _uiInteractionService.ShowInfoToast("存档迁移完成", "已迁移到当前设置的游戏目录和氧无目录。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Savedata migration failed.");
                _uiInteractionService.ShowErrorToast("存档迁移失败", ex.Message);
            }
        }

        private async Task<(string GameDirectory, string AsphyxiaDirectory)?> PromptForMigrationDirectoriesAsync()
        {
            while (true)
            {
                var gameDirectoryBox = new TextBox { Watermark = "旧游戏目录" };
                var asphyxiaDirectoryBox = new TextBox { Watermark = "旧氧无目录" };

                var selectGameDirectoryButton = new Button { Content = "选择", MinWidth = 72 };
                selectGameDirectoryButton.Classes.Add("Basic");
                selectGameDirectoryButton.Click += async (_, _) =>
                {
                    var path = await _uiInteractionService.PickFolderAsync("选择旧游戏目录");
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        gameDirectoryBox.Text = path;
                    }
                };

                var selectAsphyxiaDirectoryButton = new Button { Content = "选择", MinWidth = 72 };
                selectAsphyxiaDirectoryButton.Classes.Add("Basic");
                selectAsphyxiaDirectoryButton.Click += async (_, _) =>
                {
                    var path = await _uiInteractionService.PickFolderAsync("选择旧氧无目录");
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        asphyxiaDirectoryBox.Text = path;
                    }
                };

                var content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        BuildFolderPickerRow("游戏目录", gameDirectoryBox, selectGameDirectoryButton),
                        BuildFolderPickerRow("氧无目录", asphyxiaDirectoryBox, selectAsphyxiaDirectoryButton),
                        new TextBlock
                        {
                            Text = "点击下一步后将扫描可迁移数据",
                            Opacity = 0.72,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        }
                    }
                };

                bool confirmed = await _uiInteractionService.ShowDialogAsync(
                    "存档迁移",
                    content,
                    "下一步",
                    "取消");
                if (!confirmed)
                {
                    return null;
                }

                string gameDirectory = PathHelper.NormalizePath(gameDirectoryBox.Text);
                string asphyxiaDirectory = PathHelper.NormalizePath(asphyxiaDirectoryBox.Text);
                if (!Directory.Exists(gameDirectory))
                {
                    _uiInteractionService.ShowWarningToast("存档迁移", "请选择有效的旧游戏目录。");
                    continue;
                }

                if (!Directory.Exists(asphyxiaDirectory))
                {
                    _uiInteractionService.ShowWarningToast("存档迁移", "请选择有效的旧氧无目录。");
                    continue;
                }

                return (gameDirectory, asphyxiaDirectory);
            }
        }

        private async Task<List<SavedataTransferEntry>> PromptForMigrationSelectionAsync(IReadOnlyList<SavedataTransferEntry> entries)
        {
            while (true)
            {
                var selectionPanel = new StackPanel { Spacing = 10 };
                var selections = new List<(SavedataTransferEntry Entry, CheckBox CheckBox)>();

                foreach (var entry in entries)
                {
                    var checkBox = new CheckBox
                    {
                        IsChecked = true,
                        Content = entry.DisplayName
                    };

                    selectionPanel.Children.Add(new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            checkBox,
                            new TextBlock
                            {
                                Text = entry.SourcePath,
                                Opacity = 0.68,
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap
                            }
                        }
                    });

                    selections.Add((entry, checkBox));
                }

                var content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "默认已勾选全部项目；取消勾选的项目不会迁移。",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        },
                        new ScrollViewer
                        {
                            MaxHeight = 320,
                            Content = selectionPanel
                        }
                    }
                };

                bool confirmed = await _uiInteractionService.ShowDialogAsync(
                    "选择迁移内容",
                    content,
                    "开始迁移",
                    "取消");
                if (!confirmed)
                {
                    return null;
                }

                var selectedEntries = selections
                    .Where(selection => selection.CheckBox.IsChecked == true)
                    .Select(selection => selection.Entry)
                    .ToList();
                if (selectedEntries.Count > 0)
                {
                    return selectedEntries;
                }

                _uiInteractionService.ShowWarningToast("存档迁移", "请至少选择一个要迁移的项目。");
            }
        }

        private static Grid BuildFolderPickerRow(string label, TextBox textBox, Button button)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    textBox,
                    button
                }
            };

            Grid.SetColumn(textBox, 1);
            Grid.SetColumn(button, 2);
            return grid;
        }

        // Path normalization delegated to PathHelper.NormalizePath
    }
}
