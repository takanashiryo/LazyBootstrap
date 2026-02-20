using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using LazyBootstrap.UI.Dialogs;
using SukiUI.Dialogs;

namespace LazyBootstrap
{
    public partial class MainWindow
    {
        private static readonly string[] SavedataRelativePaths =
        {
            @"asphyxia\savedata",
            @"asphyxia\config.ini",
            @"contents\card0.txt",
            @"contents\card1.txt"
        };

        private void OnGoToGameSettingsClick(object sender, RoutedEventArgs e)
        {
            GoToGameSettingsCore();
        }

        private async void OnEditConfigClick(object sender, RoutedEventArgs e)
        {
            await EditConfigCoreAsync();
        }

        private void OnKillProcessesClick(object sender, RoutedEventArgs e)
        {
            int killedSpice = KillProcessesByName("spice64");
            int killedAsphyxia = KillProcessesByName("asphyxia-core-x64");
            ShowInfoToast("操作完成", $"结束完成：spice64 {killedSpice} 个，asphyxia-core-x64 {killedAsphyxia} 个");
        }

        private int KillProcessesByName(string processName)
        {
            int count = 0;
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch (Exception ex)
            {
                ShowErrorToast("结束进程失败", $"获取进程列表 {processName} 时出错：{ex.Message}");
                return 0;
            }

            foreach (Process p in processes)
            {
                try
                {
                    int pid = p.Id;

                    p.Kill();

                    if (!p.WaitForExit(3000))
                    {
                        ShowWarningToast("进程未响应", $"{processName}.exe (PID: {pid}) 未响应，正在尝试强制终止。");

                        try
                        {
                            ProcessStartInfo taskKillInfo = new ProcessStartInfo
                            {
                                FileName = "taskkill",
                                Arguments = $"/F /PID {pid}",
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            };

                            using (Process taskKillProcess = Process.Start(taskKillInfo))
                            {
                                taskKillProcess.WaitForExit(2000);
                            }
                        }
                        catch (Exception ex)
                        {
                            ShowErrorToast("强制终止失败", ex.Message);
                        }
                    }

                    count++;
                }
                catch (InvalidOperationException)
                {
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    ShowErrorToast("结束进程权限不足", ex.Message);

                    try
                    {
                        ProcessStartInfo taskKillInfo = new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = $"/F /IM {processName}.exe",
                            CreateNoWindow = true,
                            UseShellExecute = true,
                            Verb = "runas"
                        };

                        Process.Start(taskKillInfo);
                    }
                    catch (Exception ex2)
                    {
                        ShowErrorToast("管理员终止失败", ex2.Message);
                    }
                }
                catch (Exception ex)
                {
                    ShowErrorToast("结束进程失败", ex.Message);
                }
                finally
                {
                    try
                    {
                        p.Dispose();
                    }
                    catch { }
                }
            }

            return count;
        }

        private void OnClearCacheClick(object sender, RoutedEventArgs e)
        {
            string cachePath = Path.Combine(_contentsDir, "data_mods", "_cache");
            try
            {
                if (Directory.Exists(cachePath))
                {
                    Directory.Delete(cachePath, true);
                    ShowInfoToast("清理缓存", "缓存已成功清理。");
                }
                else
                {
                    ShowWarningToast("清理缓存", "缓存文件夹不存在。");
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast("清理缓存失败", ex.Message);
            }
        }

        private async Task EditConfigCoreAsync()
        {
            string cfgToolPath = Path.Combine(_contentsDir, "spicecfg.exe");
            string arguments = "";
            if (_portableMode)
            {
                arguments = "-cmdoverride -cfgpath lazy/spicetools.xml -patchcfgpath lazy/spicetools_patch_manager.json -modules modules";
            }

            string xmlPath = GetSpiceXmlPath();
            string configPath = GetConfigTomlPath();

            try
            {
                if (!File.Exists(cfgToolPath))
                {
                    ShowErrorToast("无法启动 spicecfg", $"未找到程序: {cfgToolPath}");
                    return;
                }
                if (!File.Exists(xmlPath))
                {
                    ShowErrorToast("无法启动 spicecfg", $"未找到配置文件: {xmlPath}");
                    return;
                }
                if (!File.Exists(configPath))
                {
                    ShowErrorToast("无法启动 spicecfg", $"未找到配置文件: {configPath}");
                    return;
                }

                SetSettingsBusy(true);
                var startInfo = new ProcessStartInfo
                {
                    FileName = cfgToolPath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(cfgToolPath),
                };

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    ShowErrorToast("无法启动 spicecfg", "创建进程失败。");
                    return;
                }

                await process.WaitForExitAsync();

                bool prev = _isLoadingSettings;
                _isLoadingSettings = true;
                try
                {
                    LoadSettings();
                    LoadSpiceConfig();
                    SelectPresetByCurrentFields();
                }
                finally
                {
                    _isLoadingSettings = prev;
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast("启动 spicecfg 失败", ex.Message);
            }
            finally
            {
                SetSettingsBusy(false);
            }
        }

        private async void OnInstallRuntimeClick(object sender, RoutedEventArgs e)
        {
            string runtimePath = Path.Combine(_baseDir, "runtime");
            string dxSetupPath = Path.Combine(runtimePath, "directx", "DXSETUP.exe");
            string vcRedistPath = Path.Combine(runtimePath, "vcredist", "VisualCppRedist_AIO_x86_x64.exe");

            if (!File.Exists(dxSetupPath) && !File.Exists(vcRedistPath))
            {
                ShowErrorToast("安装运行库失败", "未找到运行库安装程序。请确保 runtime/directx/DXSETUP.exe 和 runtime/vcredist/VisualCppRedist_AIO_x86_x64.exe 存在。");
                return;
            }

            // Show overlay
            if (RuntimeInstallOverlay != null)
            {
                RuntimeInstallOverlay.IsVisible = true;
                RuntimeInstallOverlay.Opacity = 1;
            }
            if (RuntimeStatusText != null) RuntimeStatusText.Text = "正在准备安装运行库...";
            SetControlsEnabled(false);

            bool hasError = false;
            try
            {
                // Step 1: Install DirectX
                if (File.Exists(dxSetupPath))
                {
                    if (RuntimeStatusText != null) RuntimeStatusText.Text = "正在安装 DirectX...";
                    await Task.Delay(200); // Allow UI to update

                    var dxResult = await RunElevatedInstallerAsync(
                        dxSetupPath,
                        "/silent",
                        Path.Combine(runtimePath, "directx"));

                    if (dxResult == -1223)
                    {
                        ShowWarningToast("安装运行库", "用户取消了 DirectX 安装授权。");
                        hasError = true;
                    }
                    else if (dxResult != 0)
                    {
                        ShowWarningToast("DirectX 安装", $"DirectX 安装返回代码: {dxResult}");
                    }
                }

                // Step 2: Install Visual C++ Redistributable
                if (!hasError && File.Exists(vcRedistPath))
                {
                    if (RuntimeStatusText != null) RuntimeStatusText.Text = "正在安装 Visual C++ Redistributable...";
                    await Task.Delay(200);

                    var vcResult = await RunElevatedInstallerAsync(
                        vcRedistPath,
                        "/y",
                        Path.Combine(runtimePath, "vcredist"));

                    if (vcResult == -1223)
                    {
                        ShowWarningToast("安装运行库", "用户取消了 Visual C++ Redistributable 安装授权。");
                        hasError = true;
                    }
                    else if (vcResult != 0)
                    {
                        ShowWarningToast("VC++ Redist 安装", $"Visual C++ Redistributable 安装返回代码: {vcResult}");
                    }
                }

                if (!hasError)
                {
                    ShowInfoToast("运行库安装完成", "DirectX 和 Visual C++ Redistributable 安装完成。");
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast("安装运行库失败", ex.Message);
            }
            finally
            {
                // Hide overlay
                if (RuntimeInstallOverlay != null)
                {
                    RuntimeInstallOverlay.Opacity = 0;
                    await Task.Delay(300); // Wait for fade-out animation
                    RuntimeInstallOverlay.IsVisible = false;
                }
                SetControlsEnabled(true);
            }
        }

        private static async Task<int> RunElevatedInstallerAsync(string filePath, string arguments, string workingDirectory)
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

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return -1;
                    }

                    await process.WaitForExitAsync();
                    return process.ExitCode;
                }
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // UAC cancelled by user.
                return -1223;
            }
        }

        private async void OnAddFirewallRuleClick(object sender, RoutedEventArgs e)
        {
            const string ruleName = "SpiceTools";
            string spicePath = GetSpicePath();

            var dialogBuilder = _dialogManager
                .CreateDialog()
                .OfType(NotificationType.Warning)
                .WithTitle("添加防火墙规则")
                .WithContent("确认要执行吗？\n如果之前已经添加过规则，可能会重复。")
                .WithYesNoResult("确认", "取消", "Flat")
                .Dismiss().ByClickingBackground();
            ApplyDialogNotificationIcon(dialogBuilder, NotificationType.Warning);
            var confirmed = await dialogBuilder.TryShowAsync();

            if (!confirmed)
            {
                return;
            }

            if (!File.Exists(spicePath))
            {
                ShowErrorToast("添加防火墙规则失败", $"未找到目标程序：{spicePath}");
                return;
            }

            try
            {
                var addProcessInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow program=\"{spicePath}\" enable=yes profile=public,private",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };

                using (var addProcess = Process.Start(addProcessInfo))
                {
                    addProcess.WaitForExit();
                    ShowInfoToast("防火墙规则", "防火墙规则添加完成。");
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                {
                    ShowWarningToast("防火墙规则", "用户取消了 UAC 提示，未添加规则。");
                }
                else
                {
                    ShowErrorToast("防火墙规则失败", ex.Message);
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast("防火墙规则失败", ex.Message);
            }
        }

        private void OnAudioPanelClick(object sender, RoutedEventArgs e)
        {
            OpenControlPanel("mmsys.cpl", "打开音频面板失败");
        }

        private async void OnSavedataBackupImportClick(object sender, RoutedEventArgs e)
        {
            var window = new SavedataBackupImportWindow(BackupSavedataAsync, ImportSavedataAsync);
            await window.ShowDialog(this);
        }

        private async Task BackupSavedataAsync()
        {
            string sevenZipPath = ResolveSevenZipExecutablePath();
            if (!File.Exists(sevenZipPath))
            {
                ShowErrorToast("存档备份失败", $"未找到 7za.exe：{sevenZipPath}");
                return;
            }

            var existingEntries = GetExistingSavedataEntries();
            if (existingEntries.Count == 0)
            {
                ShowWarningToast("存档备份", "未找到可备份的存档内容。");
                return;
            }

            string backupDir = Path.Combine(_baseDir, "savedata_backup");
            Directory.CreateDirectory(backupDir);
            string backupFilePath = Path.Combine(backupDir, $"savedata_{DateTime.Now:yyyyMMdd_HHmmss}.7z");

            var argsBuilder = new StringBuilder("a -t7z ");
            argsBuilder.Append('"').Append(backupFilePath).Append('"');
            foreach (var entry in existingEntries)
            {
                argsBuilder.Append(' ').Append('"').Append(entry).Append('"');
            }
            argsBuilder.Append(" -mx=9");

            var result = await RunProcessCaptureAsync(sevenZipPath, argsBuilder.ToString(), _baseDir);
            if (result.ExitCode == 0)
            {
                ShowInfoToast("存档备份完成", $"已备份到：{backupFilePath}");
                return;
            }

            ShowErrorToast("存档备份失败", GetProcessErrorDetail(result));
        }

        private async Task ImportSavedataAsync()
        {
            string sevenZipPath = ResolveSevenZipExecutablePath();
            if (!File.Exists(sevenZipPath))
            {
                ShowErrorToast("存档导入失败", $"未找到 7za.exe：{sevenZipPath}");
                return;
            }

            var selectedFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择存档备份文件",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("7z 备份文件")
                    {
                        Patterns = new[] { "*.7z" }
                    }
                }
            });

            if (selectedFiles == null || selectedFiles.Count == 0)
            {
                return;
            }

            string archivePath = selectedFiles[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                ShowErrorToast("存档导入失败", "当前选择的文件不可直接访问，请选择本地磁盘文件。");
                return;
            }

            CloseSavedataBackupImportWindow();

            bool hasExistingSavedata = SavedataRelativePaths.Any(IsSavedataPathPresent);
            if (hasExistingSavedata)
            {
                var warningDialogBuilder = _dialogManager
                    .CreateDialog()
                    .OfType(NotificationType.Warning)
                    .WithTitle("存档导入覆盖提示")
                    .WithContent("检测到已有存档文件，是否直接覆盖？")
                    .WithYesNoResult("覆盖", "取消", "Flat")
                    .Dismiss().ByClickingBackground();
                ApplyDialogNotificationIcon(warningDialogBuilder, NotificationType.Warning);

                var confirmed = await warningDialogBuilder.TryShowAsync();
                if (!confirmed)
                {
                    return;
                }
            }

            string arguments = $"x \"{archivePath}\" -o\"{_baseDir}\" -y";
            var result = await RunProcessCaptureAsync(sevenZipPath, arguments, _baseDir);
            if (result.ExitCode == 0)
            {
                ShowInfoToast("存档导入完成", "已按备份原始目录结构恢复。");
                return;
            }

            ShowErrorToast("存档导入失败", GetProcessErrorDetail(result));
        }

        private void CloseSavedataBackupImportWindow()
        {
            if (OwnedWindows == null || OwnedWindows.Count == 0)
            {
                return;
            }

            foreach (var window in OwnedWindows)
            {
                if (window is SavedataBackupImportWindow)
                {
                    window.Close();
                    break;
                }
            }
        }

        private List<string> GetExistingSavedataEntries()
        {
            var existing = new List<string>();
            foreach (var relativePath in SavedataRelativePaths)
            {
                if (IsSavedataPathPresent(relativePath))
                {
                    existing.Add(relativePath);
                }
            }

            return existing;
        }

        private bool IsSavedataPathPresent(string relativePath)
        {
            string normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string absolutePath = Path.Combine(_baseDir, normalizedPath);
            return Directory.Exists(absolutePath) || File.Exists(absolutePath);
        }

        private string ResolveSevenZipExecutablePath()
        {
            string mainProgramDir = AppDomain.CurrentDomain.BaseDirectory;
            string mainProgramDirPath = Path.Combine(mainProgramDir, "7za.exe");
            if (File.Exists(mainProgramDirPath))
            {
                return mainProgramDirPath;
            }

            string baseDirPath = Path.Combine(_baseDir, "7za.exe");
            if (File.Exists(baseDirPath))
            {
                return baseDirPath;
            }

            return Path.Combine(_baseDir, "launcher", "7za.exe");
        }

        private static async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessCaptureAsync(string fileName, string arguments, string workingDirectory)
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

            using (var process = Process.Start(startInfo))
            {
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
        }

        private static string GetProcessErrorDetail((int ExitCode, string StdOut, string StdErr) processResult)
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

        private void OnTouchPanelClick(object sender, RoutedEventArgs e)
        {
            OpenControlPanel("/name Microsoft.TabletPCSettings", "打开触摸面板失败");
        }

        private void OpenControlPanel(string arguments, string errorTitle)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "control.exe",
                    Arguments = arguments,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                ShowErrorToast(errorTitle, ex.Message);
            }
        }

        private void GoToGameSettingsCore()
        {
            try
            {
                if (MainSideMenu != null)
                {
                    var target = MainSideMenu.Items?.OfType<object>().Skip(1).FirstOrDefault();
                    if (target != null)
                    {
                        MainSideMenu.SelectedItem = target;
                    }
                }
            }
            catch { }
        }

        private void SetControlsEnabled(bool enabled)
        {
            if (StartButton != null) StartButton.IsEnabled = enabled;
            if (LoadCompatButton != null) LoadCompatButton.IsEnabled = enabled;
            if (UnloadCompatButton != null) UnloadCompatButton.IsEnabled = enabled;
            if (CompatTypeComboBox != null) CompatTypeComboBox.IsEnabled = enabled;

            if (PortableModeToggleSwitch != null) PortableModeToggleSwitch.IsEnabled = enabled;
            if (WindowedToggleSwitch != null) WindowedToggleSwitch.IsEnabled = enabled;
            if (NoAsphyxiaToggleSwitch != null) NoAsphyxiaToggleSwitch.IsEnabled = enabled;
            if (ExitRestoreToggleSwitch != null) ExitRestoreToggleSwitch.IsEnabled = enabled;
            if (EditConfigButton != null) EditConfigButton.IsEnabled = enabled;
            if (ServerPresetComboBox != null) ServerPresetComboBox.IsEnabled = enabled;
            if (AddServerPresetButton != null) AddServerPresetButton.IsEnabled = enabled;
            if (DeleteServerPresetButton != null) DeleteServerPresetButton.IsEnabled = enabled;
            if (CompatLayerToggleSwitch != null) CompatLayerToggleSwitch.IsEnabled = enabled;
            if (CompatDx9on12RadioButton != null) CompatDx9on12RadioButton.IsEnabled = enabled;
            if (CompatDx9on12ExternalRadioButton != null) CompatDx9on12ExternalRadioButton.IsEnabled = enabled;
            if (CompatDxvkRadioButton != null) CompatDxvkRadioButton.IsEnabled = enabled;
            if (OpenLogButton != null) OpenLogButton.IsEnabled = enabled;
            if (TouchPanelButton != null) TouchPanelButton.IsEnabled = enabled;
            if (GotoGameSettingsButton != null) GotoGameSettingsButton.IsEnabled = enabled;
            if (AdvNetDumpToggleSwitch != null) AdvNetDumpToggleSwitch.IsEnabled = enabled;
            if (AdvDisableSubDisplayToggleSwitch != null) AdvDisableSubDisplayToggleSwitch.IsEnabled = enabled;
            if (AdvWindowModeComboBox != null) AdvWindowModeComboBox.IsEnabled = enabled;
            if (AdvPCoreOptimizationToggleSwitch != null) AdvPCoreOptimizationToggleSwitch.IsEnabled = enabled;
            if (AdvSubBorderlessToggleSwitch != null) AdvSubBorderlessToggleSwitch.IsEnabled = enabled;
            if (AdvShowCursorTouchSimToggleSwitch != null) AdvShowCursorTouchSimToggleSwitch.IsEnabled = enabled;
            if (AdvWindowTopMostToggleSwitch != null) AdvWindowTopMostToggleSwitch.IsEnabled = enabled;
            if (AdvWindowSizeTextBox != null) AdvWindowSizeTextBox.IsEnabled = enabled;
            if (AdvSingleAdapterToggleSwitch != null) AdvSingleAdapterToggleSwitch.IsEnabled = enabled;
            if (AdvSubWindowTopMostToggleSwitch != null) AdvSubWindowTopMostToggleSwitch.IsEnabled = enabled;
            if (AdvSubForceRenderToggleSwitch != null) AdvSubForceRenderToggleSwitch.IsEnabled = enabled;
            if (AdvNativeTouchToggleSwitch != null) AdvNativeTouchToggleSwitch.IsEnabled = enabled;
            if (AdvAsioDriverTextBox != null) AdvAsioDriverTextBox.IsEnabled = enabled;
            if (AdvCardIoToggleSwitch != null) AdvCardIoToggleSwitch.IsEnabled = enabled;
            if (AdvHidSmartCardToggleSwitch != null) AdvHidSmartCardToggleSwitch.IsEnabled = enabled;
            if (ServerAddressTextBox != null) ServerAddressTextBox.IsEnabled = enabled;
            if (PcbIdTextBox != null) PcbIdTextBox.IsEnabled = enabled;
            if (DisplayConfigEnabledToggleSwitch != null) DisplayConfigEnabledToggleSwitch.IsEnabled = enabled;
            if (DisplayModeComboBox != null) DisplayModeComboBox.IsEnabled = enabled;
            if (MainScreenComboBox != null) MainScreenComboBox.IsEnabled = enabled;
            if (MainResolutionComboBox != null) MainResolutionComboBox.IsEnabled = enabled;
            if (MainRefreshRateComboBox != null) MainRefreshRateComboBox.IsEnabled = enabled;
            if (SubScreenComboBox != null) SubScreenComboBox.IsEnabled = enabled;
            if (SubRotationComboBox != null) SubRotationComboBox.IsEnabled = enabled;
            if (SubResolutionComboBox != null) SubResolutionComboBox.IsEnabled = enabled;
            if (SubRefreshRateComboBox != null) SubRefreshRateComboBox.IsEnabled = enabled;
            if (RotationComboBox != null) RotationComboBox.IsEnabled = enabled;
            if (PreviewDisplaySettingsButton != null) PreviewDisplaySettingsButton.IsEnabled = enabled;
            if (SelectMainScreenAreaButton != null) SelectMainScreenAreaButton.IsEnabled = enabled;
            if (SelectSubScreenAreaButton != null) SelectSubScreenAreaButton.IsEnabled = enabled && _isDualDisplay;

            if (ClearCacheButton != null) ClearCacheButton.IsEnabled = enabled;
            if (InstallRuntimeButton != null) InstallRuntimeButton.IsEnabled = enabled;
            if (AddFirewallRuleButton != null) AddFirewallRuleButton.IsEnabled = enabled;
            if (AudioPanelButton != null) AudioPanelButton.IsEnabled = enabled;
            if (SavedataBackupImportButton != null) SavedataBackupImportButton.IsEnabled = enabled;
            if (KillProcessesButton != null) KillProcessesButton.IsEnabled = true;

            if (enabled)
            {
                UpdateCompatLayerStatus();
                UpdateDisplayLayoutControlsEnabled();
            }
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
            try
            {
                if (_gameProcess != null && !_gameProcess.HasExited)
                    _gameProcess.Kill();
            }
            catch (Exception) { }
        }
    }
}
