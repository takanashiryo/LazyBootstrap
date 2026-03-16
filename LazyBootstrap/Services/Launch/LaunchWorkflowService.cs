using System;
using SystemEnvironment = System.Environment;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Microsoft.Extensions.Logging;

namespace LazyBootstrap.Services.Launch
{
    public interface ILaunchWorkflowService
    {
        Task InitializeStartupAsync(LaunchPageViewModel viewModel, SettingsPageViewModel settingsViewModel, DisplayConfigurationPageViewModel displayViewModel);

        Task ToggleLaunchLogAsync(LaunchPageViewModel viewModel);

        Task NavigateToSettingsAsync();

        Task OpenLogAsync();

        Task KillProcessesAsync();

        Task StartAsync(LaunchPageViewModel launchViewModel, SettingsPageViewModel settingsViewModel, DisplayConfigurationPageViewModel displayViewModel, bool asphyxiaDevOnly);

        Task HandleClosingAsync(DisplayConfigurationPageViewModel displayViewModel);
    }

    internal sealed class LaunchWorkflowService : ILaunchWorkflowService
    {
        private const int MaxLogLines = 1200;
        private static readonly TimeSpan RestartDetectionGracePeriod = TimeSpan.FromSeconds(5);

        private readonly ILauncherPaths _paths;
        private readonly IGameProcessTracker _gameProcessTracker;
        private readonly IDisplayWorkflowService _displayWorkflowService;
        private readonly IWindowsDefenderExclusionService _windowsDefenderExclusionService;
        private readonly IUiInteractionService _uiInteractionService;
        private readonly IShellStateService _shellStateService;
        private readonly ILogger<LaunchWorkflowService> _logger;

        private readonly Queue<string> _logLines = new Queue<string>(MaxLogLines + 64);
        private Process _gameProcess;
        private LaunchPageViewModel _launchViewModel;
        private DisplayConfigurationPageViewModel _displayViewModel;
        private IReadOnlyDictionary<string, DisplayState> _displayRestoreStates = new Dictionary<string, DisplayState>(StringComparer.OrdinalIgnoreCase);

        public LaunchWorkflowService(
            ILauncherPaths paths,
            IGameProcessTracker gameProcessTracker,
            IDisplayWorkflowService displayWorkflowService,
            IWindowsDefenderExclusionService windowsDefenderExclusionService,
            IUiInteractionService uiInteractionService,
            IShellStateService shellStateService,
            ILogger<LaunchWorkflowService> logger)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _gameProcessTracker = gameProcessTracker ?? throw new ArgumentNullException(nameof(gameProcessTracker));
            _displayWorkflowService = displayWorkflowService ?? throw new ArgumentNullException(nameof(displayWorkflowService));
            _windowsDefenderExclusionService = windowsDefenderExclusionService ?? throw new ArgumentNullException(nameof(windowsDefenderExclusionService));
            _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
            _shellStateService = shellStateService ?? throw new ArgumentNullException(nameof(shellStateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task InitializeStartupAsync(LaunchPageViewModel viewModel, SettingsPageViewModel settingsViewModel, DisplayConfigurationPageViewModel displayViewModel)
        {
            _launchViewModel = viewModel;
            _displayViewModel = displayViewModel;
            viewModel.ToggleLaunchLogText = viewModel.IsLaunchLogVisible ? "隐藏启动日志" : "显示启动日志";
            viewModel.StateText = _shellStateService.StatusText;
            return Task.CompletedTask;
        }

        public Task ToggleLaunchLogAsync(LaunchPageViewModel viewModel)
        {
            viewModel.IsLaunchLogVisible = !viewModel.IsLaunchLogVisible;
            viewModel.ToggleLaunchLogText = viewModel.IsLaunchLogVisible ? "隐藏启动日志" : "显示启动日志";
            return Task.CompletedTask;
        }

        public Task NavigateToSettingsAsync()
        {
            _shellStateService.SelectedPage = ShellPage.Settings;
            return Task.CompletedTask;
        }

        public async Task OpenLogAsync()
        {
            try
            {
                string logPath = Path.Combine(_paths.GetContentsDirectoryPath(), "log.txt");
                if (!File.Exists(logPath))
                {
                    _uiInteractionService.ShowErrorToast("查看日志失败", $"未找到日志文件: {logPath}");
                    return;
                }

                var content = await File.ReadAllTextAsync(logPath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(content))
                {
                    content = "(log.txt 为空)";
                }

                var logViewer = new TextBox
                {
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
                    Text = content,
                    MinWidth = 960,
                    MinHeight = 520,
                    MaxWidth = 1200,
                    MaxHeight = 680,
                    [ScrollViewer.HorizontalScrollBarVisibilityProperty] = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    [ScrollViewer.VerticalScrollBarVisibilityProperty] = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
                };

                bool openFolder = await _uiInteractionService.ShowDialogAsync(
                    "log.txt",
                    logViewer,
                    "打开日志文件夹",
                    "关闭");

                if (openFolder)
                {
                    ProcessExecutionHelper.OpenLogFolderAndSelectFile(logPath);
                }
            }
            catch (Exception ex)
            {
                _uiInteractionService.ShowErrorToast("查看日志失败", ex.Message);
            }
        }

        public Task KillProcessesAsync()
        {
            int killedSpice = KillProcessesByName("spice64");
            int killedAsphyxia = KillProcessesByName("asphyxia-core-x64");
            _uiInteractionService.ShowInfoToast("操作完成", $"结束完成：spice64 {killedSpice} 个，asphyxia-core-x64 {killedAsphyxia} 个");
            return Task.CompletedTask;
        }

        public async Task StartAsync(LaunchPageViewModel launchViewModel, SettingsPageViewModel settingsViewModel, DisplayConfigurationPageViewModel displayViewModel, bool asphyxiaDevOnly)
        {
            _launchViewModel = launchViewModel;
            _displayViewModel = displayViewModel;

            _gameProcessTracker.ResetManagedAsphyxiaTracking();
            _shellStateService.IsInteractionEnabled = false;
            _shellStateService.StatusText = "启动中...";
            launchViewModel.StateText = _shellStateService.StatusText;
            launchViewModel.IsLaunching = true;
            launchViewModel.IsLaunchLogVisible = true;
            launchViewModel.ToggleLaunchLogText = "隐藏启动日志";
            ClearLaunchLog(launchViewModel);
            AppendLaunchOutput(launchViewModel, "开始启动...");

            bool handoffToGameSession = false;

            try
            {
                string spicePath = _paths.GetSpicePath();
                string asphyxiaPath = _paths.GetAsphyxiaPath();

                if (!asphyxiaDevOnly && !File.Exists(spicePath))
                {
                    FailLaunch(launchViewModel, $"未找到 spice64.exe: {spicePath}");
                    return;
                }

                bool startAsphyxia = asphyxiaDevOnly || !settingsViewModel.NoAsphyxia;
                if (startAsphyxia && !File.Exists(asphyxiaPath))
                {
                    FailLaunch(launchViewModel, $"未找到 asphyxia-core-x64.exe: {asphyxiaPath}");
                    return;
                }

                if (!asphyxiaDevOnly)
                {
                    AppendLaunchOutput(launchViewModel, "正在检查 Windows Defender 排除项...");
                    var defenderResult = await _windowsDefenderExclusionService.EnsureDirectoryExcludedAsync(_paths.GetContentsDirectoryPath());
                    AppendLaunchOutput(launchViewModel, defenderResult.Message, defenderResult.Status == WindowsDefenderExclusionStatus.Failed ? NotificationType.Warning : NotificationType.Information);
                }

                if (!asphyxiaDevOnly && displayViewModel.IsDisplayConfigurationEnabled)
                {
                    if (displayViewModel.Displays.Count == 0)
                    {
                        AppendLaunchOutput(launchViewModel, "正在准备显示器配置...");
                        await displayViewModel.WarmDeferredAsync();
                    }

                    AppendLaunchOutput(launchViewModel, "正在应用显示器配置...");
                    bool applySucceeded = _displayWorkflowService.TryApplyForLaunch(displayViewModel, out var restoreStates, out var displayMessages);
                    _displayRestoreStates = restoreStates;
                    foreach (var displayMessage in displayMessages)
                    {
                        AppendLaunchOutput(launchViewModel, displayMessage, NotificationType.Warning);
                    }

                    if (!applySucceeded && restoreStates.Count > 0)
                    {
                        FailLaunch(launchViewModel, "显示器配置未能完整回滚，请检查当前显示器状态后重试。");
                        return;
                    }

                    if (applySucceeded)
                    {
                        AppendLaunchOutput(launchViewModel, "显示器配置应用完成。");
                        await Task.Delay(5000);
                    }
                }

                if (startAsphyxia)
                {
                    AppendLaunchOutput(launchViewModel, "正在启动 Asphyxia Core...");
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
                            UseShellExecute = true,
                            WorkingDirectory = Path.GetDirectoryName(asphyxiaPath)
                        };

                    var asphyxiaProcess = Process.Start(asphyxiaStartInfo);
                    if (asphyxiaProcess == null)
                    {
                        FailLaunch(launchViewModel, "Asphyxia 启动失败，进程未成功创建。");
                        return;
                    }

                    if (!asphyxiaDevOnly)
                    {
                        _gameProcessTracker.TrackManagedAsphyxiaProcess(asphyxiaProcess);
                    }

                    AppendLaunchOutput(launchViewModel, "Asphyxia Core 启动成功");
                }
                else
                {
                    AppendLaunchOutput(launchViewModel, "已跳过启动 Asphyxia Core。", NotificationType.Warning);
                }

                if (asphyxiaDevOnly)
                {
                    launchViewModel.IsLaunching = false;
                    launchViewModel.IsGameRunning = false;
                    _shellStateService.IsInteractionEnabled = true;
                    _shellStateService.StatusText = "调试模式就绪";
                    launchViewModel.StateText = _shellStateService.StatusText;
                    AppendLaunchOutput(launchViewModel, "已按调试模式启动 Asphyxia Core（--dev），未启动 spice64。");
                    return;
                }

                var argumentsBuilder = new StringBuilder();
                if (settingsViewModel.PortableMode)
                {
                    argumentsBuilder.Append("-cfgpath lazy/spicetools.xml ");
                    argumentsBuilder.Append("-patchcfgpath lazy/spicetools_patch_manager.json ");
                    argumentsBuilder.Append("-modules modules ");
                }

                AppendLaunchOutput(launchViewModel, "正在启动游戏...");
                AppendLaunchOutput(launchViewModel, $"启动参数: {argumentsBuilder}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = spicePath,
                    Arguments = argumentsBuilder.ToString(),
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(spicePath)
                };

                _gameProcessTracker.PrepareTrackedSpiceSession(spicePath);
                _gameProcess = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

                if (!_gameProcess.Start())
                {
                    FailLaunch(launchViewModel, "spice64 启动失败，进程未成功创建。");
                    _gameProcess.Dispose();
                    _gameProcess = null;
                    return;
                }

                _gameProcessTracker.RegisterTrackedSpiceProcess(_gameProcess);
                _gameProcess.Exited += GameProcessExited;
                handoffToGameSession = true;

                launchViewModel.IsLaunching = false;
                launchViewModel.IsGameRunning = true;
                _shellStateService.StatusText = "游戏运行中";
                launchViewModel.StateText = _shellStateService.StatusText;
                AppendLaunchOutput(launchViewModel, "游戏已启动并进入运行状态。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Game launch workflow failed.");
                FailLaunch(launchViewModel, ex.Message);
            }
            finally
            {
                if (!handoffToGameSession && _gameProcessTracker.HasManagedAsphyxiaProcess())
                {
                    if (_gameProcessTracker.TryStopManagedAsphyxiaProcess(out var stopErrorMessage))
                    {
                        AppendLaunchOutput(launchViewModel, "已回收本次启动的 Asphyxia Core。", NotificationType.Warning);
                    }
                    else
                    {
                        _uiInteractionService.ShowWarningToast("Asphyxia 关闭提示", stopErrorMessage);
                    }
                }
            }
        }

        public Task HandleClosingAsync(DisplayConfigurationPageViewModel displayViewModel)
        {
            try
            {
                if (_gameProcess != null && !_gameProcess.HasExited)
                {
                    _gameProcess.Kill();
                }
            }
            catch
            {
            }

            if (_gameProcessTracker.HasManagedAsphyxiaProcess())
            {
                _gameProcessTracker.TryStopManagedAsphyxiaProcess(out _);
            }

            if (displayViewModel?.ExitRestore == true && _displayRestoreStates.Count > 0)
            {
                _displayWorkflowService.RestoreDisplayStates(_displayRestoreStates, new List<string>());
            }

            return Task.CompletedTask;
        }

        private async void GameProcessExited(object sender, EventArgs e)
        {
            var exitedProcess = sender as Process;
            int exitCode = 0;
            bool abnormalExit = false;
            DateTime exitedAtUtc = DateTime.MinValue;

            try
            {
                if (exitedProcess != null)
                {
                    exitCode = exitedProcess.ExitCode;
                    abnormalExit = exitCode != 0;
                    exitedAtUtc = exitedProcess.ExitTime.ToUniversalTime();
                }
            }
            catch
            {
            }

            await Task.Delay(1000);

            try
            {
                var restartedProcess = _gameProcessTracker.TryFindRestartedSpiceProcess(exitedAtUtc, RestartDetectionGracePeriod);
                if (restartedProcess != null)
                {
                    restartedProcess.EnableRaisingEvents = true;
                    restartedProcess.Exited += GameProcessExited;
                    _gameProcess = restartedProcess;
                    _gameProcessTracker.RegisterTrackedSpiceProcess(restartedProcess);
                    AppendLaunchOutput(_launchViewModel, "检测到 spice64 重新启动，继续监控中...");
                    exitedProcess?.Dispose();
                    return;
                }
            }
            catch
            {
            }

            try
            {
                if (abnormalExit)
                {
                    _uiInteractionService.ShowErrorToast("游戏异常退出", $"spice64.exe 异常退出（ExitCode: {exitCode}）。");
                }

                AppendLaunchOutput(_launchViewModel, abnormalExit ? $"游戏进程异常退出（ExitCode: {exitCode}）。" : "游戏进程已正常退出。", abnormalExit ? NotificationType.Warning : NotificationType.Information);

                if (_gameProcessTracker.HasManagedAsphyxiaProcess())
                {
                    AppendLaunchOutput(_launchViewModel, "正在关闭 Asphyxia Core...");
                    if (_gameProcessTracker.TryStopManagedAsphyxiaProcess(out var stopErrorMessage))
                    {
                        AppendLaunchOutput(_launchViewModel, "Asphyxia Core 已关闭");
                    }
                    else
                    {
                        _uiInteractionService.ShowWarningToast("Asphyxia 关闭提示", stopErrorMessage);
                    }
                }

                if (_displayViewModel?.IsDisplayConfigurationEnabled == true
                    && _displayViewModel.ExitRestore
                    && _displayRestoreStates.Count > 0)
                {
                    var restoreMessages = new List<string>();
                    int restored = _displayWorkflowService.RestoreDisplayStates(_displayRestoreStates, restoreMessages);
                    AppendLaunchOutput(_launchViewModel, restored > 0 ? $"已恢复 {restored} 个显示器设置。" : "未恢复任何显示器设置。");
                    foreach (var restoreMessage in restoreMessages)
                    {
                        AppendLaunchOutput(_launchViewModel, restoreMessage, NotificationType.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Launch cleanup after game exit failed.");
            }
            finally
            {
                _shellStateService.IsInteractionEnabled = true;
                _shellStateService.StatusText = "就绪";

                if (_launchViewModel != null)
                {
                    _launchViewModel.IsLaunching = false;
                    _launchViewModel.IsGameRunning = false;
                    _launchViewModel.StateText = _shellStateService.StatusText;
                }

                if (_gameProcess != null)
                {
                    _gameProcess.Dispose();
                    _gameProcess = null;
                }

                exitedProcess?.Dispose();
            }
        }

        private void FailLaunch(LaunchPageViewModel launchViewModel, string message)
        {
            _uiInteractionService.ShowErrorToast("启动失败", message);
            AppendLaunchOutput(launchViewModel, message, NotificationType.Error);
            launchViewModel.IsLaunching = false;
            launchViewModel.IsGameRunning = false;
            _shellStateService.IsInteractionEnabled = true;
            _shellStateService.StatusText = "启动失败";
            launchViewModel.StateText = _shellStateService.StatusText;
        }

        private void ClearLaunchLog(LaunchPageViewModel viewModel)
        {
            _logLines.Clear();
            viewModel.LaunchLogText = string.Empty;
        }

        private void AppendLaunchOutput(LaunchPageViewModel viewModel, string message, NotificationType type = NotificationType.Information)
        {
            if (viewModel == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            foreach (var line in message.Replace("\r\n", "\n").Split('\n'))
            {
                _logLines.Enqueue(FormatLogEntry(line, type));
                while (_logLines.Count > MaxLogLines)
                {
                    _logLines.Dequeue();
                }
            }

            viewModel.LaunchLogText = string.Join(SystemEnvironment.NewLine, _logLines);
        }

        private static string FormatLogEntry(string line, NotificationType type)
        {
            string prefix = type switch
            {
                NotificationType.Error => "[错误] ",
                NotificationType.Warning => "[警告] ",
                _ => string.Empty
            };

            return $"[{DateTime.Now:HH:mm:ss}] {prefix}{line}";
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
                _uiInteractionService.ShowErrorToast("结束进程失败", $"获取进程列表 {processName} 时出错：{ex.Message}");
                return 0;
            }

            foreach (var process in processes)
            {
                try
                {
                    int pid = process.Id;
                    process.Kill();

                    if (!process.WaitForExit(3000))
                    {
                        _uiInteractionService.ShowWarningToast("进程未响应", $"{processName}.exe (PID: {pid}) 未响应，正在尝试强制终止。");
                        using var taskKillProcess = Process.Start(new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = $"/F /PID {pid}",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        });
                        taskKillProcess?.WaitForExit(2000);
                    }

                    process.Refresh();
                    if (!process.HasExited)
                    {
                        _uiInteractionService.ShowWarningToast("结束进程未完成", $"{processName}.exe (PID: {pid}) 仍在运行。");
                        continue;
                    }

                    count++;
                }
                catch (InvalidOperationException)
                {
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    _uiInteractionService.ShowErrorToast("结束进程权限不足", ex.Message);
                }
                catch (Exception ex)
                {
                    _uiInteractionService.ShowErrorToast("结束进程失败", ex.Message);
                }
                finally
                {
                    process.Dispose();
                }
            }

            return count;
        }
    }
}
