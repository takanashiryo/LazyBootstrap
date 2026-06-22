using System;
using SystemEnvironment = System.Environment;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using SukiUI.Controls;
using SukiUI.MessageBox;

namespace LazyBootstrap.Features.Launch.Services
{

    public sealed class LaunchOrchestrator
    {
        private const int MaxLogLines = 1200;
        private static readonly TimeSpan StartupRestartProbeDelay = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan LauncherAutoMinimizeDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan AsphyxiaToSpiceLaunchDelay = TimeSpan.FromMilliseconds(300);

        private readonly LauncherPaths _paths;
        private readonly GameProcessTracker _gameProcessTracker;
        private readonly DisplayOrchestrator _displayOrchestrator;
        private readonly WindowsDefenderExclusionService _windowsDefenderExclusionService;
        private readonly UiInteractionService _uiInteractionService;
        private readonly AppShellState _shellStateService;
        private readonly ILogger<LaunchOrchestrator> _logger;

        private readonly Queue<string> _logLines = new Queue<string>(MaxLogLines + 64);
        private Process _gameProcess;
        private CancellationTokenSource _gameProcessMonitorCts;
        private bool _suppressGameProcessExitHandling;
        private LaunchState _launchState;
        private DisplayConfigurationSnapshot _display;
        private ILaunchWorkflowObserver _observer;
        private AppShellState.ShellBusyLease _launchNavigationLock;
        private IReadOnlyDictionary<string, DisplayState> _displayRestoreStates = new Dictionary<string, DisplayState>(StringComparer.OrdinalIgnoreCase);

        public LaunchOrchestrator(
            LauncherPaths paths,
            GameProcessTracker gameProcessTracker,
            DisplayOrchestrator displayOrchestrator,
            WindowsDefenderExclusionService windowsDefenderExclusionService,
            UiInteractionService uiInteractionService,
            AppShellState shellStateService,
            ILogger<LaunchOrchestrator> logger)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _gameProcessTracker = gameProcessTracker ?? throw new ArgumentNullException(nameof(gameProcessTracker));
            _displayOrchestrator = displayOrchestrator ?? throw new ArgumentNullException(nameof(displayOrchestrator));
            _windowsDefenderExclusionService = windowsDefenderExclusionService ?? throw new ArgumentNullException(nameof(windowsDefenderExclusionService));
            _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
            _shellStateService = shellStateService ?? throw new ArgumentNullException(nameof(shellStateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task InitializeStartupAsync(
            LaunchState launchState,
            DisplayConfigurationSnapshot display,
            ILaunchWorkflowObserver observer)
        {
            _launchState = launchState;
            _display = display;
            _observer = observer;
            launchState.ToggleLaunchLogText = launchState.IsLaunchLogVisible ? "隐藏启动日志" : "显示启动日志";
            launchState.StateText = _shellStateService.StatusText;
            NotifyLaunchStateChanged(launchState);
            NotifyLaunchLogVisibilityChanged(launchState);
            NotifyLaunchMessageChanged(launchState);
            return Task.CompletedTask;
        }

        public Task ToggleLaunchLogAsync(LaunchState launchState, ILaunchWorkflowObserver observer)
        {
            _launchState = launchState;
            _observer = observer;
            launchState.IsLaunchLogVisible = !launchState.IsLaunchLogVisible;
            launchState.ToggleLaunchLogText = launchState.IsLaunchLogVisible ? "隐藏启动日志" : "显示启动日志";
            NotifyLaunchLogVisibilityChanged(launchState);
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
                _logger.LogInformation("Opening spice2x log viewer.");
                if (!File.Exists(logPath))
                {
                    _logger.LogWarning("spice2x log file was not found: {LogPath}", logPath);
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
                    _logger.LogInformation("Opening spice2x log folder from log viewer.");
                    ProcessExecutionHelper.OpenLogFolderAndSelectFile(logPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open spice2x log viewer.");
                _uiInteractionService.ShowErrorToast("查看日志失败", ex.Message);
            }
        }

        public Task KillProcessesAsync()
        {
            _logger.LogInformation("Manual process termination requested.");
            int killedSpice = KillProcessesByName("spice64");
            int killedAsphyxia = KillProcessesByName("asphyxia-core-x64");
            _logger.LogInformation("Manual process termination completed. SpiceKilled={SpiceKilled}, AsphyxiaKilled={AsphyxiaKilled}", killedSpice, killedAsphyxia);
            _uiInteractionService.ShowInfoToast("操作完成", $"结束完成：spice64 {killedSpice} 个，asphyxia-core-x64 {killedAsphyxia} 个");
            return Task.CompletedTask;
        }

        private void BeginLaunchNavigationLock()
        {
            EndLaunchNavigationLock();
            _launchNavigationLock = _shellStateService.BeginBusy(ShellBusyPresentation.NavigationLock);
        }

        private void EndLaunchNavigationLock()
        {
            _launchNavigationLock?.Dispose();
            _launchNavigationLock = null;
        }

        public async Task StartAsync(LaunchState launchState, LaunchRequest request, ILaunchWorkflowObserver observer)
        {
            ArgumentNullException.ThrowIfNull(request);

            string launchSessionId = Guid.NewGuid().ToString("N");
            using var launchScope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["LaunchSessionId"] = launchSessionId
            });

            var settings = request.Settings;
            var display = request.Display;
            bool asphyxiaDevOnly = request.AsphyxiaDevOnly;

            _launchState = launchState;
            _display = display;
            _observer = observer;
            _displayRestoreStates = new Dictionary<string, DisplayState>(StringComparer.OrdinalIgnoreCase);
            CancelGameProcessMonitoring(suppressExitHandling: true);
            _suppressGameProcessExitHandling = false;

            ClearLaunchMessage(launchState);
            _gameProcessTracker.ResetManagedAsphyxiaTracking();
            BeginLaunchNavigationLock();
            _shellStateService.StatusText = "启动中...";
            launchState.StateText = _shellStateService.StatusText;
            launchState.IsLaunching = true;
            launchState.IsLaunchLogVisible = true;
            launchState.ToggleLaunchLogText = "隐藏启动日志";
            NotifyLaunchStateChanged(launchState);
            NotifyLaunchLogVisibilityChanged(launchState);
            ClearLaunchLog(launchState);
            AppendLaunchOutput(launchState, "开始启动...");
            _logger.LogInformation("Game launch workflow started. LaunchSessionId={LaunchSessionId}, AsphyxiaDevOnly={AsphyxiaDevOnly}", launchSessionId, asphyxiaDevOnly);

            bool handoffToGameSession = false;
            bool startedAsphyxiaForGame = false;

            try
            {
                string spicePath = _paths.GetSpicePath();
                string asphyxiaPath = _paths.GetAsphyxiaPath();
                string serverAddress = (settings?.ServerAddress ?? string.Empty).Trim();
                bool useSystemSpice = settings?.UseSystemSpiceConfig ?? false;
                bool displayConfigurationEnabled = display?.IsDisplayConfigurationEnabled == true;
                _logger.LogInformation(
                    "Launch prerequisites resolved. SpiceExists={SpiceExists}, AsphyxiaExists={AsphyxiaExists}, DisplayConfigurationEnabled={DisplayConfigurationEnabled}, UseSystemSpiceConfig={UseSystemSpiceConfig}",
                    File.Exists(spicePath),
                    File.Exists(asphyxiaPath),
                    displayConfigurationEnabled,
                    useSystemSpice);

                if (!asphyxiaDevOnly && string.IsNullOrWhiteSpace(serverAddress))
                {
                    _logger.LogWarning("Launch aborted because no server address is configured.");
                    WarnLaunchAndAbort(
                        launchState,
                        "服务器地址异常",
                        "未检测到任何 e-amusement 服务器地址存在，请前往设置页设置");
                    return;
                }

                if (!asphyxiaDevOnly && !File.Exists(spicePath))
                {
                    _logger.LogWarning("Launch aborted because spice64.exe was not found: {SpicePath}", spicePath);
                    FailLaunch(launchState, $"未找到 spice64.exe: {spicePath}", "未找到spice64.exe");
                    return;
                }

                bool startAsphyxia = asphyxiaDevOnly || !settings.NoAsphyxia;
                int existingAsphyxiaCount = 0;
                bool isAsphyxiaCoreAlreadyRunning = startAsphyxia && IsAsphyxiaCoreRunning(out existingAsphyxiaCount);
                _logger.LogInformation("Asphyxia launch decision resolved. StartAsphyxia={StartAsphyxia}, ExistingProcessCount={ExistingProcessCount}", startAsphyxia, existingAsphyxiaCount);

                if (startAsphyxia && !isAsphyxiaCoreAlreadyRunning && !File.Exists(asphyxiaPath))
                {
                    _logger.LogWarning("Launch aborted because asphyxia-core-x64.exe was not found: {AsphyxiaPath}", asphyxiaPath);
                    FailLaunch(launchState, $"未找到 asphyxia-core-x64.exe: {asphyxiaPath}", "未找到asphyxia-core-x64.exe");
                    return;
                }

                if (!asphyxiaDevOnly)
                {
                    AppendLaunchOutput(launchState, "正在检查 Windows Defender 排除项...");
                    var defenderResult = await _windowsDefenderExclusionService.EnsureDirectoryExcludedAsync(_paths.GetContentsDirectoryPath());
                    _logger.LogInformation("Windows Defender exclusion check completed. Status={Status}", defenderResult.Status);
                    AppendLaunchOutput(launchState, defenderResult.Message, defenderResult.Status == WindowsDefenderExclusionStatus.Failed ? NotificationType.Warning : NotificationType.Information);
                }

                if (!asphyxiaDevOnly && display.IsDisplayConfigurationEnabled)
                {
                    if (display.Displays.Count == 0)
                    {
                        AppendLaunchOutput(launchState, "正在准备显示器配置...");
                        await _displayOrchestrator.WarmDeferredAsync(display);
                    }

                    AppendLaunchOutput(launchState, "正在应用显示器配置...");
                    bool applySucceeded = _displayOrchestrator.TryApplyForLaunch(display, out var restoreStates, out var displayMessages);
                    _logger.LogInformation(
                        "Display configuration apply for launch completed. Succeeded={Succeeded}, RestoreStateCount={RestoreStateCount}, MessageCount={MessageCount}",
                        applySucceeded,
                        restoreStates.Count,
                        displayMessages.Count);
                    _displayRestoreStates = restoreStates;
                    foreach (var displayMessage in displayMessages)
                    {
                        AppendLaunchOutput(launchState, displayMessage, NotificationType.Warning);
                    }

                    if (!applySucceeded && restoreStates.Count > 0)
                    {
                        FailLaunch(launchState, "显示器配置未能完整回滚，请检查当前显示器状态后重试。");
                        return;
                    }

                    if (applySucceeded)
                    {
                        AppendLaunchOutput(launchState, "显示器配置应用完成。");
                        await Task.Delay(5000);
                    }
                }

                if (startAsphyxia)
                {
                    if (isAsphyxiaCoreAlreadyRunning)
                    {
                        AppendLaunchOutput(
                            launchState,
                            existingAsphyxiaCount > 1
                                ? $"检测到已有 {existingAsphyxiaCount} 个 Asphyxia Core 进程，已跳过重复启动。"
                                : "检测到已有 Asphyxia Core 进程，已跳过重复启动。",
                            NotificationType.Warning);
                    }
                    else
                    {
                        AppendLaunchOutput(launchState, "正在启动 Asphyxia Core...");
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
                            _logger.LogWarning("Asphyxia Core process creation returned null.");
                            FailLaunch(launchState, "Asphyxia 启动失败，进程未成功创建。");
                            return;
                        }

                        if (!asphyxiaDevOnly)
                        {
                            _gameProcessTracker.TrackManagedAsphyxiaProcess(asphyxiaProcess);
                            startedAsphyxiaForGame = true;
                        }

                        _logger.LogInformation("Asphyxia Core process started. ProcessId={ProcessId}, DevOnly={DevOnly}", asphyxiaProcess.Id, asphyxiaDevOnly);
                        AppendLaunchOutput(launchState, "Asphyxia Core 启动成功");
                    }
                }
                else
                {
                    AppendLaunchOutput(launchState, "已跳过启动 Asphyxia Core。", NotificationType.Warning);
                }

                if (asphyxiaDevOnly)
                {
                    launchState.IsLaunching = false;
                    launchState.IsGameRunning = false;
                    ClearLaunchMessage(launchState);
                    EndLaunchNavigationLock();
                    _shellStateService.StatusText = "调试模式就绪";
                    launchState.StateText = _shellStateService.StatusText;
                    NotifyLaunchStateChanged(launchState);
                    AppendLaunchOutput(launchState, "已按调试模式启动 Asphyxia Core（--dev），未启动 spice64。");
                    return;
                }

                if (startedAsphyxiaForGame)
                {
                    // wait 300ms to start spice2x
                    await Task.Delay(AsphyxiaToSpiceLaunchDelay);
                }

                var argumentsBuilder = new StringBuilder();
                string spiceArgLine = Spice64CommandLine.BuildGameLaunchArguments(useSystemSpice);
                if (!string.IsNullOrWhiteSpace(spiceArgLine))
                {
                    argumentsBuilder.Append(spiceArgLine);
                }

                AppendLaunchOutput(launchState, "正在启动游戏...");
                AppendLaunchOutput(launchState, $"启动参数: {argumentsBuilder}");

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
                    _logger.LogWarning("spice64 process start returned false.");
                    FailLaunch(launchState, "spice64 启动失败，进程未成功创建。");
                    _gameProcess.Dispose();
                    _gameProcess = null;
                    return;
                }

                _gameProcessTracker.RegisterTrackedSpiceProcess(_gameProcess);
                handoffToGameSession = true;
                _logger.LogInformation("spice64 process started. ProcessId={ProcessId}", _gameProcess.Id);

                launchState.IsLaunching = false;
                launchState.IsGameRunning = true;
                ClearLaunchMessage(launchState);
                _shellStateService.StatusText = "游戏运行中";
                launchState.StateText = _shellStateService.StatusText;
                NotifyLaunchStateChanged(launchState);
                AppendLaunchOutput(launchState, "游戏已启动并进入运行状态。");
                _ = MinimizeLauncherWindowDelayedAsync();
                StartGameProcessMonitoring();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Game launch workflow failed.");
                FailLaunch(launchState, ex.Message);
            }
            finally
            {
                if (!handoffToGameSession && _gameProcessTracker.HasManagedAsphyxiaProcess())
                {
                    _logger.LogInformation("Launch did not hand off to game session. Stopping managed Asphyxia Core process.");
                    if (_gameProcessTracker.TryStopManagedAsphyxiaProcess(out var stopErrorMessage))
                    {
                        AppendLaunchOutput(launchState, "已回收本次启动的 Asphyxia Core。", NotificationType.Warning);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to stop managed Asphyxia Core process after launch abort.");
                        _uiInteractionService.ShowWarningToast("Asphyxia 关闭提示", stopErrorMessage);
                    }
                }

                if (!handoffToGameSession
                    && _display?.IsDisplayConfigurationEnabled == true
                    && _display.ExitRestore
                    && _displayRestoreStates.Count > 0)
                {
                    AppendLaunchOutput(launchState, "启动未完成，正在恢复显示器设置...");
                    var restoreMessages = new List<string>();
                    int restored = _displayOrchestrator.RestoreDisplayStates(_displayRestoreStates, restoreMessages);
                    _logger.LogInformation("Display settings restored after incomplete launch. RestoredCount={RestoredCount}, MessageCount={MessageCount}", restored, restoreMessages.Count);
                    AppendLaunchOutput(launchState, restored > 0 ? $"已恢复 {restored} 个显示器设置。" : "未恢复任何显示器设置。", restored > 0 ? NotificationType.Information : NotificationType.Warning);
                    foreach (var restoreMessage in restoreMessages)
                    {
                        AppendLaunchOutput(launchState, restoreMessage, NotificationType.Warning);
                    }
                }

                if (!handoffToGameSession)
                {
                    _logger.LogInformation("Game launch workflow ended without handoff.");
                    _displayRestoreStates = new Dictionary<string, DisplayState>(StringComparer.OrdinalIgnoreCase);
                    if (_gameProcess != null)
                    {
                        _gameProcess.Dispose();
                        _gameProcess = null;
                    }
                }
            }
        }

        public Task HandleClosingAsync(DisplayConfigurationSnapshot display)
        {
            _logger.LogInformation("Launcher closing cleanup started.");
            CancelGameProcessMonitoring(suppressExitHandling: true);
            EndLaunchNavigationLock();
            ClearLaunchMessage(_launchState);

            try
            {
                if (_gameProcess != null && !_gameProcess.HasExited)
                {
                    _gameProcess.Kill();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to terminate game process during window close.");
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
                _logger.LogInformation("Stopping managed Asphyxia Core process during window close.");
                _gameProcessTracker.TryStopManagedAsphyxiaProcess(out _);
            }

            if (display?.ExitRestore == true && _displayRestoreStates.Count > 0)
            {
                var restoreMessages = new List<string>();
                int restored = _displayOrchestrator.RestoreDisplayStates(_displayRestoreStates, restoreMessages);
                _logger.LogInformation("Display settings restored during window close. RestoredCount={RestoredCount}, MessageCount={MessageCount}", restored, restoreMessages.Count);
            }

            _displayRestoreStates = new Dictionary<string, DisplayState>(StringComparer.OrdinalIgnoreCase);
            _logger.LogInformation("Launcher closing cleanup completed.");
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
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Game process monitor cancellation failed.");
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
                        var restartedProcess = _gameProcessTracker.TryFindRestartedSpiceProcess(exitedAtUtc);
                        if (restartedProcess != null)
                        {
                            restartedProcess.EnableRaisingEvents = true;
                            _gameProcess = restartedProcess;
                            _gameProcessTracker.RegisterTrackedSpiceProcess(restartedProcess);
                            _logger.LogInformation("Detected restarted spice64 process. ProcessId={ProcessId}", restartedProcess.Id);
                            AppendLaunchOutput(_launchState, "检测到 spice64 重新启动，继续监控中...");
                            currentProcess.Dispose();
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to probe restarted spice64 process.");
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
                _logger.LogInformation("spice64 process exited. ExitCode={ExitCode}, AbnormalExit={AbnormalExit}", exitCode, abnormalExit);
                AppendLaunchOutput(_launchState, abnormalExit ? $"游戏进程异常退出（ExitCode: {exitCode}）。" : "游戏进程已正常退出。", abnormalExit ? NotificationType.Warning : NotificationType.Information);

                if (abnormalExit && !cancellationToken.IsCancellationRequested && !_suppressGameProcessExitHandling)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_shellStateService.SelectedPage == ShellPage.Launch && _launchState != null)
                        {
                            ShowLaunchMessage(
                                _launchState,
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
                    AppendLaunchOutput(_launchState, "正在关闭 Asphyxia Core...");
                    if (_gameProcessTracker.TryStopManagedAsphyxiaProcess(out var stopErrorMessage))
                    {
                        _logger.LogInformation("Managed Asphyxia Core process stopped after game exit.");
                        AppendLaunchOutput(_launchState, "Asphyxia Core 已关闭");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to stop managed Asphyxia Core process after game exit.");
                        _uiInteractionService.ShowWarningToast("Asphyxia 关闭提示", stopErrorMessage);
                    }
                }

                if (_display?.IsDisplayConfigurationEnabled == true
                    && _display.ExitRestore
                    && _displayRestoreStates.Count > 0)
                {
                    var restoreMessages = new List<string>();
                    int restored = _displayOrchestrator.RestoreDisplayStates(_displayRestoreStates, restoreMessages);
                    _logger.LogInformation("Display settings restored after game exit. RestoredCount={RestoredCount}, MessageCount={MessageCount}", restored, restoreMessages.Count);
                    AppendLaunchOutput(_launchState, restored > 0 ? $"已恢复 {restored} 个显示器设置。" : "未恢复任何显示器设置。");
                    foreach (var restoreMessage in restoreMessages)
                    {
                        AppendLaunchOutput(_launchState, restoreMessage, NotificationType.Warning);
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
                    _logger.LogInformation("Game process lifecycle cleanup completed.");
                    EndLaunchNavigationLock();
                    _shellStateService.StatusText = "就绪";

                    if (_launchState != null)
                    {
                        _launchState.IsLaunching = false;
                        _launchState.IsGameRunning = false;
                        _launchState.StateText = _shellStateService.StatusText;

                        if (!abnormalExit || _shellStateService.SelectedPage != ShellPage.Launch)
                        {
                            ClearLaunchMessage(_launchState);
                        }

                        NotifyLaunchStateChanged(_launchState);
                    }

                    _uiInteractionService.RestoreAttachedWindow();
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

        private async Task MinimizeLauncherWindowDelayedAsync()
        {
            try
            {
                await Task.Delay(LauncherAutoMinimizeDelay);

                if (_launchState?.IsGameRunning == true)
                {
                    _uiInteractionService.MinimizeAttachedWindow();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to auto minimize launcher window.");
            }
        }

        private void FailLaunch(LaunchState launchState, string logMessage, string displayMessage = null)
        {
            StopLaunchWithMessage(
                launchState,
                NotificationType.Error,
                "启动失败",
                "启动失败",
                logMessage,
                displayMessage ?? logMessage);
        }

        private void WarnLaunchAndAbort(LaunchState launchState, string title, string bodyMessage)
        {
            StopLaunchWithMessage(
                launchState,
                NotificationType.Warning,
                title,
                title,
                bodyMessage,
                bodyMessage);
        }

        private void StopLaunchWithMessage(
            LaunchState launchState,
            NotificationType messageType,
            string statusText,
            string title,
            string logMessage,
            string bodyMessage,
            string accentText = "")
        {
            AppendLaunchOutput(launchState, logMessage, messageType);
            launchState.IsLaunching = false;
            launchState.IsGameRunning = false;
            EndLaunchNavigationLock();
            _shellStateService.StatusText = statusText;
            launchState.StateText = _shellStateService.StatusText;
            NotifyLaunchStateChanged(launchState);
            ShowLaunchMessage(launchState, messageType, title, accentText, bodyMessage);
        }

        private void ShowLaunchMessage(LaunchState launchState, NotificationType messageType, string title, string accentText, string bodyText)
        {
            if (launchState == null)
            {
                return;
            }

            launchState.MessageType = messageType;
            launchState.MessageTitle = title ?? string.Empty;
            launchState.MessageAccentText = accentText ?? string.Empty;
            launchState.MessageBodyText = bodyText ?? string.Empty;
            launchState.IsMessageVisible = true;
            NotifyLaunchMessageChanged(launchState);
        }

        private void ClearLaunchMessage(LaunchState launchState)
        {
            if (launchState == null)
            {
                return;
            }

            launchState.IsMessageVisible = false;
            launchState.MessageType = NotificationType.Error;
            launchState.MessageTitle = string.Empty;
            launchState.MessageAccentText = string.Empty;
            launchState.MessageBodyText = string.Empty;
            NotifyLaunchMessageChanged(launchState);
        }

        private void ClearLaunchLog(LaunchState launchState)
        {
            _logLines.Clear();
            launchState.LaunchLogText = string.Empty;
            NotifyLaunchLogChanged(launchState);
        }

        private void AppendLaunchOutput(LaunchState launchState, string message, NotificationType type = NotificationType.Information)
        {
            if (launchState == null || string.IsNullOrWhiteSpace(message))
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

            launchState.LaunchLogText = string.Join(SystemEnvironment.NewLine, _logLines);
            NotifyLaunchLogChanged(launchState);
        }

        private void NotifyLaunchStateChanged(LaunchState launchState)
        {
            if (launchState != null)
            {
                _observer?.OnLaunchStateChanged(launchState);
            }
        }

        private void NotifyLaunchLogVisibilityChanged(LaunchState launchState)
        {
            if (launchState != null)
            {
                _observer?.OnLaunchLogVisibilityChanged(launchState);
            }
        }

        private void NotifyLaunchLogChanged(LaunchState launchState)
        {
            if (launchState != null)
            {
                _observer?.OnLaunchLogChanged(launchState);
            }
        }

        private void NotifyLaunchMessageChanged(LaunchState launchState)
        {
            _observer?.OnLaunchMessageChanged(launchState?.ToMessage() ?? LaunchMessage.Hidden);
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
                _logger.LogInformation("Found processes for termination. ProcessName={ProcessName}, Count={Count}", processName, processes.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enumerate processes for termination. ProcessName={ProcessName}", processName);
                _uiInteractionService.ShowErrorToast("结束进程失败", $"获取进程列表 {processName} 时出错：{ex.Message}");
                return 0;
            }

            foreach (var process in processes)
            {
                try
                {
                    int pid = process.Id;
                    _logger.LogInformation("Killing process. ProcessName={ProcessName}, ProcessId={ProcessId}", processName, pid);
                    process.Kill();

                    if (!process.WaitForExit(3000))
                    {
                        _logger.LogWarning("Process did not exit after Kill. Falling back to taskkill. ProcessName={ProcessName}, ProcessId={ProcessId}", processName, pid);
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
                        _logger.LogWarning("Process is still running after termination attempt. ProcessName={ProcessName}, ProcessId={ProcessId}", processName, pid);
                        _uiInteractionService.ShowWarningToast("结束进程未完成", $"{processName}.exe (PID: {pid}) 仍在运行。");
                        continue;
                    }

                    _logger.LogInformation("Process terminated. ProcessName={ProcessName}, ProcessId={ProcessId}", processName, pid);
                    count++;
                }
                catch (InvalidOperationException)
                {
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    _logger.LogWarning(ex, "Permission denied while terminating process. ProcessName={ProcessName}", processName);
                    _uiInteractionService.ShowErrorToast("结束进程权限不足", ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to terminate process. ProcessName={ProcessName}", processName);
                    _uiInteractionService.ShowErrorToast("结束进程失败", ex.Message);
                }
                finally
                {
                    process.Dispose();
                }
            }

            return count;
        }

        private bool IsAsphyxiaCoreRunning(out int processCount)
        {
            processCount = 0;

            try
            {
                var processes = Process.GetProcessesByName("asphyxia-core-x64");
                try
                {
                    processCount = processes.Length;
                    _logger.LogDebug("Asphyxia Core process inspection completed. Count={Count}", processCount);
                    return processCount > 0;
                }
                finally
                {
                    foreach (var process in processes)
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to inspect existing Asphyxia Core processes.");
                return false;
            }
        }

    }

    internal static class Spice64CommandLine
    {
        private const string LazyCfgRelative = "lazy/spicetools.xml";
        private const string LazyPatchRelative = "lazy/spicetools_patch_manager.json";

        public static string BuildGameLaunchArguments(bool useSystemConfig)
        {
            if (useSystemConfig)
            {
                return string.Empty;
            }

            return $"-cmdoverride -cfgpath {LazyCfgRelative} -patchcfgpath {LazyPatchRelative}";
        }

        public static string BuildConfigEditorArguments(bool useSystemConfig)
        {
            if (useSystemConfig)
            {
                return "-cfg -forcesoftware";
            }

            return $"-cfg -cmdoverride -forcesoftware -cfgpath {LazyCfgRelative} -patchcfgpath {LazyPatchRelative}";
        }
    }
}
