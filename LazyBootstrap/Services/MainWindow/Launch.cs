using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace LazyBootstrap
{
    public partial class MainWindow
    {
        private enum LaunchMode
        {
            Normal,
            AsphyxiaDevOnly
        }

        private async void OnStartButtonClick(object sender, RoutedEventArgs e)
        {
            await StartGameCoreAsync(LaunchMode.Normal);
        }

        private async void OnStartAsphyxiaDevMenuItemClick(object sender, RoutedEventArgs e)
        {
            await StartGameCoreAsync(LaunchMode.AsphyxiaDevOnly);
        }

        private async Task StartGameCoreAsync(LaunchMode launchMode)
        {
            SetControlsEnabled(false);
            if (StatusLabel != null) StatusLabel.Text = "启动中...";
            await ShowLaunchLogAreaWithAnimationAsync();
            ClearLaunchOutput();
            AppendLaunchOutput("开始启动...");

            try
            {
                string spicePath = GetSpicePath();
                string asphyxiaPath = GetAsphyxiaPath();
                bool asphyxiaDevOnly = launchMode == LaunchMode.AsphyxiaDevOnly;

                if (!asphyxiaDevOnly && !File.Exists(spicePath))
                {
                    ShowErrorToast("启动失败", $"未找到 spice64.exe: {spicePath}");
                    AppendLaunchOutput($"未找到游戏程序：{spicePath}", NotificationType.Error);
                    SetControlsEnabled(true);
                    if (StatusLabel != null) StatusLabel.Text = "启动失败";
                    return;
                }

                bool startAsphyxia = asphyxiaDevOnly || NoAsphyxiaToggleSwitch?.IsChecked != true;
                if (startAsphyxia && !File.Exists(asphyxiaPath))
                {
                    ShowErrorToast("启动失败", $"未找到 asphyxia-core-x64.exe: {asphyxiaPath}");
                    AppendLaunchOutput($"未找到 Asphyxia Core：{asphyxiaPath}", NotificationType.Error);
                    SetControlsEnabled(true);
                    if (StatusLabel != null) StatusLabel.Text = "启动失败";
                    return;
                }

                if (!asphyxiaDevOnly && _displayConfigEnabled)
                {
                    AppendLaunchOutput("正在应用显示器配置...");
                    bool displayApplySuccess = ApplyDisplaySettingsForLaunch();
                    AppendLaunchOutput(displayApplySuccess ? "显示器配置应用完成。" : "显示器配置部分失败，游戏仍将继续启动。", displayApplySuccess ? NotificationType.Information : NotificationType.Warning);
                    await Task.Delay(5000);
                }

                if (startAsphyxia)
                {
                    AppendLaunchOutput("正在启动 Asphyxia Core...");
                    var asphyxiaStartInfo = asphyxiaDevOnly
                        ? new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/k \"\"{asphyxiaPath}\" --dev\"",
                            UseShellExecute = true,
                            WorkingDirectory = Path.GetDirectoryName(asphyxiaPath)
                        }
                        : new ProcessStartInfo
                        {
                            FileName = asphyxiaPath,
                            Arguments = string.Empty,
                            UseShellExecute = true,
                            WorkingDirectory = Path.GetDirectoryName(asphyxiaPath)
                        };

                    var asphyxiaProcess = Process.Start(asphyxiaStartInfo);
                    if (asphyxiaProcess == null)
                    {
                        ShowErrorToast("启动失败", "Asphyxia 启动失败，进程未成功创建。");
                        AppendLaunchOutput("Asphyxia 启动失败，进程未成功创建。", NotificationType.Error);
                        SetControlsEnabled(true);
                        if (StatusLabel != null) StatusLabel.Text = "启动失败";
                        return;
                    }

                    AppendLaunchOutput("Asphyxia Core 启动成功");
                }
                else
                {
                    AppendLaunchOutput("已跳过启动 Asphyxia Core。", NotificationType.Warning);
                }

                if (asphyxiaDevOnly)
                {
                    AppendLaunchOutput("已按调试模式启动 Asphyxia Core（--dev），未启动 spice64。", NotificationType.Information);
                    if (StatusLabel != null) StatusLabel.Text = "调试模式就绪";
                    SetControlsEnabled(true);
                    return;
                }

                UpdateSpiceConfig();

                var argsBuilder = new StringBuilder();
                if (_portableMode)
                {
                    argsBuilder.Append("-cfgpath lazy/spicetools.xml ");
                    argsBuilder.Append("-patchcfgpath lazy/spicetools_patch_manager.json ");
                    argsBuilder.Append("-modules modules ");
                }

                AppendLaunchOutput("正在启动游戏...");
                AppendLaunchOutput($"启动参数: {argsBuilder.ToString()}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = spicePath,
                    Arguments = argsBuilder.ToString(),
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(spicePath)
                };

                _gameProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

                if (!_gameProcess.Start())
                {
                    ShowErrorToast("启动失败", "spice64 启动失败，进程未成功创建。");
                    AppendLaunchOutput("spice64 启动失败，进程未成功创建。", NotificationType.Error);
                    SetControlsEnabled(true);
                    if (StatusLabel != null) StatusLabel.Text = "启动失败";
                    _gameProcess.Dispose();
                    _gameProcess = null;
                    return;
                }

                _gameProcess.Exited += GameProcess_Exited;

                if (StatusLabel != null) StatusLabel.Text = "游戏运行中";
                AppendLaunchOutput("游戏已启动并进入运行状态。");
            }
            catch (Exception ex)
            {
                ShowErrorToast("启动失败", ex.Message);
                AppendLaunchOutput($"启动过程中发生未处理错误：{ex.Message}", NotificationType.Error);
                SetControlsEnabled(true);
                if (StatusLabel != null) StatusLabel.Text = "启动失败";
            }
        }

        private bool ApplyDisplaySettingsForLaunch()
        {
            _displayRestoreStates.Clear();

            if (!_displayConfigEnabled)
            {
                return true;
            }

            bool allOk = true;
            allOk &= ApplyDisplayTarget(MainScreenComboBox, RotationComboBox, MainResolutionComboBox, MainRefreshRateComboBox, "主屏");

            if (_isDualDisplay)
            {
                allOk &= ApplyDisplayTarget(SubScreenComboBox, SubRotationComboBox, SubResolutionComboBox, SubRefreshRateComboBox, "副屏");
            }

            return allOk;
        }

        private bool ApplyDisplayTarget(ComboBox screenCombo, ComboBox rotationCombo, ComboBox resolutionCombo, ComboBox refreshCombo, string label)
        {
            try
            {
                var info = GetSelectedDisplayInfo(screenCombo);
                if (info == null)
                {
                    return false;
                }

                if (DisplayConfigure.TryGetCurrentState(info.DeviceName, out var currentState))
                {
                    _displayRestoreStates[info.DeviceName] = currentState;
                }

                int rotation = ParseRotationValue(rotationCombo);
                string resolution = resolutionCombo?.SelectedItem?.ToString() ?? string.Empty;
                string refreshText = refreshCombo?.SelectedItem?.ToString() ?? string.Empty;

                if (!TryParseResolution(resolution, out int width, out int height))
                {
                    return false;
                }

                if (!int.TryParse(refreshText, out int refreshRate))
                {
                    return false;
                }

                bool ok = DisplayConfigure.ApplyDisplaySettings(info.DeviceName, rotation, width, height, refreshRate);
                return ok;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryParseResolution(string resolution, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (string.IsNullOrWhiteSpace(resolution))
            {
                return false;
            }

            var parts = resolution.Split('x', 'X');
            if (parts.Length != 2)
            {
                return false;
            }

            return int.TryParse(parts[0], out width) && int.TryParse(parts[1], out height);
        }

        private async void GameProcess_Exited(object sender, EventArgs e)
        {
            int exitCode = 0;
            bool abnormalExit = false;
            try
            {
                if (sender is Process exitedProcess)
                {
                    exitCode = exitedProcess.ExitCode;
                    abnormalExit = exitCode != 0;
                }
            }
            catch
            {
            }

            // Wait 1 second and check if spice64 has restarted
            await Task.Delay(1000);

            try
            {
                var runningSpice = Process.GetProcessesByName("spice64");
                if (runningSpice.Length > 0)
                {
                    // spice64 has restarted, re-attach exit handler to new process
                    var newProcess = runningSpice[0];
                    newProcess.EnableRaisingEvents = true;
                    newProcess.Exited += GameProcess_Exited;
                    _gameProcess = newProcess;

                    Dispatcher.UIThread.Post(() =>
                    {
                        AppendLaunchOutput("检测到 spice64 重新启动，继续监控中...");
                    });
                    return;
                }
            }
            catch
            {
            }

            Dispatcher.UIThread.Post(() =>
            {
                AppendLaunchOutput(abnormalExit
                    ? $"游戏进程异常退出（ExitCode: {exitCode}）。"
                    : "游戏进程已正常退出。", abnormalExit ? NotificationType.Warning : NotificationType.Information);

                if (abnormalExit)
                {
                    ShowErrorToast("游戏异常退出", $"spice64.exe 异常退出（ExitCode: {exitCode}），已自动打开日志窗口。");
                    _ = ShowLogDialogAsync();
                }

                AppendLaunchOutput("正在关闭 Asphyxia Core...");
                try
                {
                    KillProcessesByName("asphyxia-core-x64");
                    AppendLaunchOutput("Asphyxia Core 已关闭");
                }
                catch (Exception ex)
                {
                    ShowWarningToast("Asphyxia 关闭提示", $"未找到正在运行的 Asphyxia Core 进程。{ex.Message}");
                }

                if (_displayConfigEnabled && ExitRestoreToggleSwitch?.IsChecked == true)
                {
                    AppendLaunchOutput("正在恢复显示器设置...");
                    int restoredCount = 0;
                    foreach (var kv in _displayRestoreStates)
                    {
                        if (DisplayConfigure.RestoreDisplaySettings(kv.Value))
                        {
                            restoredCount++;
                        }
                    }
                    AppendLaunchOutput(restoredCount > 0
                        ? $"已恢复 {restoredCount} 个显示器设置。"
                        : "未恢复任何显示器设置。", restoredCount > 0 ? NotificationType.Information : NotificationType.Warning);
                }

                if (StatusLabel != null) StatusLabel.Text = "就绪";
                SetControlsEnabled(true);
                if (_gameProcess != null)
                {
                    _gameProcess.Dispose();
                    _gameProcess = null;
                }
            });
        }
    }
}
