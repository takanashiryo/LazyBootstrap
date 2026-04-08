using System;
using SystemEnvironment = System.Environment;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using SukiUI.Controls;
using SukiUI.MessageBox;

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
        private static readonly TimeSpan StartupRestartProbeDelay = TimeSpan.FromSeconds(1);
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
        private CancellationTokenSource _gameProcessMonitorCts;
        private bool _suppressGameProcessExitHandling;
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
                    TextWrapping = TextWrapping.NoWrap,
                    Text = content,
                    MinWidth = 960,
                    MinHeight = 520,
                    MaxWidth = 1200,
                    MaxHeight = 680,
                    [ScrollViewer.HorizontalScrollBarVisibilityProperty] = ScrollBarVisibility.Auto,
                    [ScrollViewer.VerticalScrollBarVisibilityProperty] = ScrollBarVisibility.Auto
                };
                logViewer.CaretIndex = logViewer.Text?.Length ?? 0;

                var openFolderButton = SukiMessageBoxButtonsFactory.CreateButton("打开日志文件夹", SukiMessageBoxResult.Yes, "Flat");

                var result = await SukiMessageBox.ShowDialog(new SukiMessageBoxHost
                {
                    UseAlternativeHeaderStyle = true,
                    IconPreset = SukiMessageBoxIcons.Information,
                    Header = "log.txt",
                    Content = logViewer,
                    FooterLeftItemsSource = [new SelectableTextBlock { Text = $"路径: {logPath}" }],
                    ActionButtonsSource = [openFolderButton]
                });

                if (result is SukiMessageBoxResult.Yes)
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
            _displayRestoreStates = new Dictionary<string, DisplayState>(StringComparer.OrdinalIgnoreCase);
            CancelGameProcessMonitoring(suppressExitHandling: true);
            _suppressGameProcessExitHandling = false;

            ClearLaunchMessage(launchViewModel);
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
                string serverAddress = (settingsViewModel?.ServerAddress ?? string.Empty).Trim();

                if (!asphyxiaDevOnly && string.IsNullOrWhiteSpace(serverAddress))
                {
                    WarnLaunchAndAbort(
                        launchViewModel,
                        "服务器地址异常",
                        "未检测到任何 e-amusement 服务器地址存在，请前往设置页设置");
                    return;
                }

                if (!asphyxiaDevOnly && !File.Exists(spicePath))
                {
                    FailLaunch(launchViewModel, $"未找到 spice64.exe: {spicePath}", "未找到spice64.exe");
                    return;
                }

                bool startAsphyxia = asphyxiaDevOnly || !settingsViewModel.NoAsphyxia;
                if (startAsphyxia && !File.Exists(asphyxiaPath))
                {
                    FailLaunch(launchViewModel, $"未找到 asphyxia-core-x64.exe: {asphyxiaPath}", "未找到asphyxia-core-x64.exe");
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
                    ClearLaunchMessage(launchViewModel);
                    _shellStateService.IsInteractionEnabled = true;
                    _shellStateService.StatusText = "调试模式就绪";
                    launchViewModel.StateText = _shellStateService.StatusText;
                    AppendLaunchOutput(launchViewModel, "已按调试模式启动 Asphyxia Core（--dev），未启动 spice64。");
                    return;
                }

                var argumentsBuilder = new StringBuilder();

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
                handoffToGameSession = true;

                launchViewModel.IsLaunching = false;
                launchViewModel.IsGameRunning = true;
                ClearLaunchMessage(launchViewModel);
                _shellStateService.StatusText = "游戏运行中";
                launchViewModel.StateText = _shellStateService.StatusText;
                AppendLaunchOutput(launchViewModel, "游戏已启动并进入运行状态。");
                StartGameProcessMonitoring();
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

                if (!handoffToGameSession
                    && _displayViewModel?.IsDisplayConfigurationEnabled == true
                    && _displayViewModel.ExitRestore
                    && _displayRestoreStates.Count > 0)
                {
                    AppendLaunchOutput(launchViewModel, "启动未完成，正在恢复显示器设置...");
                    var restoreMessages = new List<string>();
                    int restored = _displayWorkflowService.RestoreDisplayStates(_displayRestoreStates, restoreMessages);
                    AppendLaunchOutput(launchViewModel, restored > 0 ? $"已恢复 {restored} 个显示器设置。" : "未恢复任何显示器设置。", restored > 0 ? NotificationType.Information : NotificationType.Warning);
                    foreach (var restoreMessage in restoreMessages)
                    {
                        AppendLaunchOutput(launchViewModel, restoreMessage, NotificationType.Warning);
                    }
                }

                if (!handoffToGameSession)
                {
                    _displayRestoreStates = new Dictionary<string, DisplayState>(StringComparer.OrdinalIgnoreCase);
                    if (_gameProcess != null)
                    {
                        _gameProcess.Dispose();
                        _gameProcess = null;
                    }
                }
            }
        }

        public Task HandleClosingAsync(DisplayConfigurationPageViewModel displayViewModel)
        {
            CancelGameProcessMonitoring(suppressExitHandling: true);
            ClearLaunchMessage(_launchViewModel);

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
            finally
            {
                if (_gameProcess != null)
                {
                    _gameProcess.Dispose();
                    _gameProcess = null;
                }
            }

            if (_gameProcessTracker.HasManagedAsphyxiaProcess())
            {
                _gameProcessTracker.TryStopManagedAsphyxiaProcess(out _);
            }

            if (displayViewModel?.ExitRestore == true && _displayRestoreStates.Count > 0)
            {
                _displayWorkflowService.RestoreDisplayStates(_displayRestoreStates, new List<string>());
            }

            _displayRestoreStates = new Dictionary<string, DisplayState>(StringComparer.OrdinalIgnoreCase);
            return Task.CompletedTask;
        }

        private void StartGameProcessMonitoring()
        {
            CancelGameProcessMonitoring(suppressExitHandling: true);
            _suppressGameProcessExitHandling = false;

            var monitorCts = new CancellationTokenSource();
            _gameProcessMonitorCts = monitorCts;
            _ = MonitorGameProcessLifecycleAsync(monitorCts);
        }

        private void CancelGameProcessMonitoring(bool suppressExitHandling)
        {
            if (suppressExitHandling)
            {
                _suppressGameProcessExitHandling = true;
            }

            if (_gameProcessMonitorCts == null)
            {
                return;
            }

            try
            {
                if (!_gameProcessMonitorCts.IsCancellationRequested)
                {
                    _gameProcessMonitorCts.Cancel();
                }
            }
            catch
            {
            }
            finally
            {
                _gameProcessMonitorCts.Dispose();
                _gameProcessMonitorCts = null;
            }
        }

        private async Task MonitorGameProcessLifecycleAsync(CancellationTokenSource monitorCts)
        {
            var cancellationToken = monitorCts.Token;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var currentProcess = _gameProcess;
                    if (currentProcess == null)
                    {
                        return;
                    }

                    try
                    {
                        await currentProcess.WaitForExitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    if (cancellationToken.IsCancellationRequested || _suppressGameProcessExitHandling)
                    {
                        return;
                    }

                    int exitCode = 0;
                    bool abnormalExit = false;
                    DateTime exitedAtUtc = DateTime.UtcNow;

                    try
                    {
                        exitCode = currentProcess.ExitCode;
                        abnormalExit = exitCode != 0;
                    }
                    catch
                    {
                    }

                    try
                    {
                        exitedAtUtc = currentProcess.ExitTime.ToUniversalTime();
                    }
                    catch
                    {
                    }

                    try
                    {
                        await Task.Delay(StartupRestartProbeDelay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    if (cancellationToken.IsCancellationRequested || _suppressGameProcessExitHandling)
                    {
                        return;
                    }

                    try
                    {
                        var restartedProcess = _gameProcessTracker.TryFindRestartedSpiceProcess(exitedAtUtc, RestartDetectionGracePeriod);
                        if (restartedProcess != null)
                        {
                            restartedProcess.EnableRaisingEvents = true;
                            _gameProcess = restartedProcess;
                            _gameProcessTracker.RegisterTrackedSpiceProcess(restartedProcess);
                            AppendLaunchOutput(_launchViewModel, "检测到 spice64 重新启动，继续监控中...");
                            currentProcess.Dispose();
                            continue;
                        }
                    }
                    catch
                    {
                    }

                    await CompleteGameProcessLifecycleAsync(currentProcess, exitCode, abnormalExit, cancellationToken);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Game process lifecycle monitor failed.");
            }
            finally
            {
                if (ReferenceEquals(_gameProcessMonitorCts, monitorCts))
                {
                    _gameProcessMonitorCts.Dispose();
                    _gameProcessMonitorCts = null;
                }
                else
                {
                    monitorCts.Dispose();
                }
            }
        }

        private async Task CompleteGameProcessLifecycleAsync(Process exitedProcess, int exitCode, bool abnormalExit, CancellationToken cancellationToken)
        {
            try
            {
                AppendLaunchOutput(_launchViewModel, abnormalExit ? $"游戏进程异常退出（ExitCode: {exitCode}）。" : "游戏进程已正常退出。", abnormalExit ? NotificationType.Warning : NotificationType.Information);

                if (abnormalExit && !cancellationToken.IsCancellationRequested && !_suppressGameProcessExitHandling)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_shellStateService.SelectedPage == ShellPage.Launch && _launchViewModel != null)
                        {
                            ShowLaunchMessage(
                                _launchViewModel,
                                NotificationType.Error,
                                "进程异常退出",
                                $"（ExitCode: {exitCode}）",
                                $"检测到游戏进程异常退出{SystemEnvironment.NewLine}请阅读 log.txt");
                        }
                        else
                        {
                            _uiInteractionService.ShowErrorToast("游戏异常退出", $"spice64.exe 异常退出（ExitCode: {exitCode}），已自动打开日志窗口。");
                        }

                        _ = OpenLogAsync();
                    });
                }

                if (cancellationToken.IsCancellationRequested || _suppressGameProcessExitHandling)
                {
                    return;
                }

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
                if (!cancellationToken.IsCancellationRequested && !_suppressGameProcessExitHandling)
                {
                    _shellStateService.IsInteractionEnabled = true;
                    _shellStateService.StatusText = "就绪";

                    if (_launchViewModel != null)
                    {
                        _launchViewModel.IsLaunching = false;
                        _launchViewModel.IsGameRunning = false;
                        _launchViewModel.StateText = _shellStateService.StatusText;

                        if (!abnormalExit || _shellStateService.SelectedPage != ShellPage.Launch)
                        {
                            ClearLaunchMessage(_launchViewModel);
                        }
                    }
                }

                _displayRestoreStates = new Dictionary<string, DisplayState>(StringComparer.OrdinalIgnoreCase);

                if (ReferenceEquals(_gameProcess, exitedProcess))
                {
                    _gameProcess.Dispose();
                    _gameProcess = null;
                }
                else
                {
                    exitedProcess?.Dispose();
                }
            }
        }

        private void FailLaunch(LaunchPageViewModel launchViewModel, string logMessage, string displayMessage = null)
        {
            StopLaunchWithMessage(
                launchViewModel,
                NotificationType.Error,
                "启动失败",
                "启动失败",
                logMessage,
                displayMessage ?? logMessage);
        }

        private void WarnLaunchAndAbort(LaunchPageViewModel launchViewModel, string title, string bodyMessage)
        {
            StopLaunchWithMessage(
                launchViewModel,
                NotificationType.Warning,
                title,
                title,
                bodyMessage,
                bodyMessage);
        }

        private void StopLaunchWithMessage(
            LaunchPageViewModel launchViewModel,
            NotificationType messageType,
            string statusText,
            string title,
            string logMessage,
            string bodyMessage,
            string accentText = "")
        {
            AppendLaunchOutput(launchViewModel, logMessage, messageType);
            launchViewModel.IsLaunching = false;
            launchViewModel.IsGameRunning = false;
            _shellStateService.IsInteractionEnabled = true;
            _shellStateService.StatusText = statusText;
            launchViewModel.StateText = _shellStateService.StatusText;
            ShowLaunchMessage(launchViewModel, messageType, title, accentText, bodyMessage);
        }

        private static void ShowLaunchMessage(LaunchPageViewModel launchViewModel, NotificationType messageType, string title, string accentText, string bodyText)
        {
            if (launchViewModel == null)
            {
                return;
            }

            launchViewModel.MessageType = messageType;
            launchViewModel.MessageTitle = title ?? string.Empty;
            launchViewModel.MessageAccentText = accentText ?? string.Empty;
            launchViewModel.MessageBodyText = bodyText ?? string.Empty;
            launchViewModel.IsMessageVisible = true;
        }

        private static void ClearLaunchMessage(LaunchPageViewModel launchViewModel)
        {
            if (launchViewModel == null)
            {
                return;
            }

            launchViewModel.IsMessageVisible = false;
            launchViewModel.MessageType = NotificationType.Error;
            launchViewModel.MessageTitle = string.Empty;
            launchViewModel.MessageAccentText = string.Empty;
            launchViewModel.MessageBodyText = string.Empty;
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
