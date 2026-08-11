using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using LazyBootstrap.Models;
using LazyBootstrap.Services;
using LazyBootstrap.Serialization;

namespace LazyBootstrap.UI
{
    public partial class MainWindow : SukiWindow
    {
        private readonly LauncherPaths _paths = null!;
        private readonly LaunchWorkflowSnapshot _launchSnapshot = new LaunchWorkflowSnapshot();
        private readonly LaunchOrchestrator _launchOrchestrator = null!;
        private readonly SettingsData _settingsData = new SettingsData();
        private readonly SettingsOrchestrator _settingsWorkflowService = null!;
        private readonly DisplayConfigurationData _displayData = new DisplayConfigurationData();
        private readonly DisplayOrchestrator _displayOrchestrator = null!;
        private readonly EnvironmentScanResult _environmentScanResult = new EnvironmentScanResult();
        private SettingsData _settingsState => _settingsData;
        private DisplayConfigurationData _displayState => _displayData;
        private readonly DiagnosticOrchestrator _diagnosticOrchestrator = null!;
        private readonly ToolsOrchestrator _toolsWorkflowService = null!;
        private readonly UpdateOrchestrator _updateWorkflowService = null!;

        private readonly ISukiDialogManager _dialogManager = null!;
        private readonly ISukiToastManager _toastManager = null!;
        private readonly UiInteractionService _uiInteractionService = null!;
        private readonly ConfigHandler _configHandler = null!;
        private readonly ILogger<MainWindow> _logger = null!;

        private bool _startupSequenceStarted;
        private bool _isWindowCloseAnimationRunning;
        private bool _allowImmediateWindowClose;
        private bool _pendingEnvironmentScanErrorDialog;
        private bool _isRestoringSideMenuSelection;
        private bool _isNavigationLocked;
        private ShellPage _selectedPage = ShellPage.Launch;
        private SukiSideMenuItem _lastUnlockedSideMenuItem;
        private readonly object _busySync = new object();
        private readonly List<BusyEntry> _busyEntries = new List<BusyEntry>();
        private int _nextBusyId;
        private const int WindowFadeDurationMs = 480;
        private const int WindowFadeFrameDelayMs = 8;
        private const int ExStyleIndex = -20;
        private const int LayeredWindowStyle = 0x00080000;
        private const uint LayeredWindowAlphaFlag = 0x2;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        public MainWindow()
        {
            InitializeComponent();

            if (!Design.IsDesignMode)
            {
                throw new InvalidOperationException("MainWindow must be created from the application composition root.");
            }
        }

        internal MainWindow(
            LauncherPaths paths,
            LaunchOrchestrator launchOrchestrator,
            SettingsOrchestrator settingsOrchestrator,
            DisplayOrchestrator displayOrchestrator,
            DiagnosticOrchestrator diagnosticOrchestrator,
            ToolsOrchestrator toolsOrchestrator,
            UpdateOrchestrator updateOrchestrator,
            ISukiDialogManager dialogManager,
            ISukiToastManager toastManager,
            UiInteractionService uiInteractionService,
            ConfigHandler configHandler,
            ILogger<MainWindow> logger)
        {
            InitializeComponent();

            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _launchOrchestrator = launchOrchestrator ?? throw new ArgumentNullException(nameof(launchOrchestrator));
            _settingsWorkflowService = settingsOrchestrator ?? throw new ArgumentNullException(nameof(settingsOrchestrator));
            _displayOrchestrator = displayOrchestrator ?? throw new ArgumentNullException(nameof(displayOrchestrator));
            _diagnosticOrchestrator = diagnosticOrchestrator ?? throw new ArgumentNullException(nameof(diagnosticOrchestrator));
            _toolsWorkflowService = toolsOrchestrator ?? throw new ArgumentNullException(nameof(toolsOrchestrator));
            _updateWorkflowService = updateOrchestrator ?? throw new ArgumentNullException(nameof(updateOrchestrator));
            _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
            _toastManager = toastManager ?? throw new ArgumentNullException(nameof(toastManager));
            _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
            _configHandler = configHandler ?? throw new ArgumentNullException(nameof(configHandler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (DialogHost != null)
            {
                DialogHost.Manager = _dialogManager;
            }
            if (ToastHost != null)
            {
                ToastHost.Manager = _toastManager;
            }

            _uiInteractionService.AttachWindow(this);
            _launchOrchestrator.WorkflowChanged += OnLaunchWorkflowChanged;
            _launchOrchestrator.OpenLogRequested += OnOpenLogRequested;
            Opened += OnWindowOpened;
            Closed += OnWindowClosed;
            if (MainSideMenu != null)
            {
                MainSideMenu.SelectionChanged += OnMainSideMenuSelectionChanged;
            }

            InitializeCustomComponents();
            _logger.LogInformation("Main window initialized for base directory {BaseDirectory}.", _paths.BaseDir);
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            Opened -= OnWindowOpened;
            _launchOrchestrator.WorkflowChanged -= OnLaunchWorkflowChanged;
            _launchOrchestrator.OpenLogRequested -= OnOpenLogRequested;
            if (MainSideMenu != null)
            {
                MainSideMenu.SelectionChanged -= OnMainSideMenuSelectionChanged;
            }
            _uiInteractionService.DetachWindow(this);
        }

        private void ApplyGlobalBusyStateToUi(bool isBusy, string text)
        {
            if (GlobalBusyArea == null)
            {
                return;
            }

            GlobalBusyArea.IsBusy = isBusy;
            GlobalBusyArea.BusyText = text ?? string.Empty;
        }

        private void ApplyRuntimeProgressStateToUi(bool isBusy, string text, double progressValue)
        {
            if (RuntimeInstallOverlay != null)
            {
                RuntimeInstallOverlay.IsVisible = isBusy;
                RuntimeInstallOverlay.Opacity = isBusy ? 1 : 0;
            }

            SetRuntimeInstallProgress(text, progressValue);
        }

        private void OnMainSideMenuSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRestoringSideMenuSelection || MainSideMenu == null)
            {
                return;
            }

            if (MainSideMenu.SelectedItem is not SukiSideMenuItem selectedItem)
            {
                return;
            }

            if (_isNavigationLocked)
            {
                RestoreLockedSideMenuSelection();
                return;
            }

            _lastUnlockedSideMenuItem = selectedItem;
            _selectedPage = ResolveShellPage(selectedItem);
            OnSelectedPageChanged();
        }

        private void ApplySideMenuNavigationLock()
        {
            if (MainSideMenu == null)
            {
                return;
            }

            var items = MainSideMenu.Items?
                .OfType<SukiSideMenuItem>()
                .ToList() ?? new List<SukiSideMenuItem>();

            if (!_isNavigationLocked)
            {
                foreach (var item in items)
                {
                    item.IsEnabled = true;
                }

                if (MainSideMenu.SelectedItem is SukiSideMenuItem selectedItem)
                {
                    _lastUnlockedSideMenuItem = selectedItem;
                    _selectedPage = ResolveShellPage(selectedItem);
                }

                return;
            }

            if (_lastUnlockedSideMenuItem == null)
            {
                _lastUnlockedSideMenuItem = MainSideMenu.SelectedItem as SukiSideMenuItem
                    ?? items.FirstOrDefault();
            }

            foreach (var item in items)
            {
                item.IsEnabled = ReferenceEquals(item, _lastUnlockedSideMenuItem);
            }

            RestoreLockedSideMenuSelection();
        }

        private void RestoreLockedSideMenuSelection()
        {
            if (MainSideMenu == null || _lastUnlockedSideMenuItem == null)
            {
                return;
            }

            if (ReferenceEquals(MainSideMenu.SelectedItem, _lastUnlockedSideMenuItem))
            {
                return;
            }

            try
            {
                _isRestoringSideMenuSelection = true;
                MainSideMenu.SelectedItem = _lastUnlockedSideMenuItem;
            }
            finally
            {
                _isRestoringSideMenuSelection = false;
            }
        }

        private ShellPage ResolveShellPage(SukiSideMenuItem selectedItem)
        {
            if (selectedItem?.Tag is ShellPage page)
            {
                return page;
            }

            return _selectedPage;
        }

        private async void OnWindowOpened(object sender, EventArgs e)
        {
            try
            {
                Opened -= OnWindowOpened;

                if (_configHandler.IsReadOnlySession)
                {
                    await ShowConfigReadOnlyDialogAsync();
                }

                if (_pendingEnvironmentScanErrorDialog)
                {
                    _pendingEnvironmentScanErrorDialog = false;
                    await ShowEnvironmentScanErrorDialogAsync();
                }

                QueueAutoLaunchIfEnabled();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Startup dialog display failed.");
            }
        }

        private void QueueAutoLaunchIfEnabled()
        {
            if (!_settingsData.AutoLaunch)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (_settingsData.AutoLaunch && _launchSnapshot.CanStartLaunch)
                {
                    _ = StartLaunchAsync(false);
                }
            }, DispatcherPriority.Background);
        }

        private async Task ShowConfigReadOnlyDialogAsync()
        {
            string reason = _configHandler.ReadOnlyReason;
            string content =
                "config.toml 被占用或无法读取，当前会话将使用临时内存配置。\n\n" +
                "你仍可继续使用程序，但所有修改将无法保存。";

            if (!string.IsNullOrWhiteSpace(reason))
            {
                content += $"\n\n原因：{reason}";
            }

            await _uiInteractionService.ShowMessageDialogAsync(
                "配置文件无法保存",
                content,
                "我知道了",
                NotificationType.Warning,
                "Flat");
        }

        private async Task ShowEnvironmentScanErrorDialogAsync()
        {
            const string errorContent =
                "(*´ - `*)∩ 啊哇哇。。。Near 检测到你的系统可能缺少必要的运行环境！\n\n" +
                "(∩^-^)∩(∩^-^)∩ Noah 建议的操作步骤：\n" +
                "- 在工具页点击「安装运行库」按钮安装必要运行环境\n" +
                "- 确保已安装最新的显卡驱动程序\n" +
                "- 如为 AMD/Intel 显卡请启用“显卡兼容层”功能\n\n" +
                "如“系统媒体功能包”异常：\n" +
                "- 检查“Windows 设置”中是否已启用“媒体功能包”\n\n" +
                "请注意！由于硬件不同，检查结果可能会误报！\n" +
                "如果所有游戏运行正常没有问题，请忽略以上提示。";

            bool openDiagPage = await _uiInteractionService.ShowDialogAsync(
                "环境检查提示",
                errorContent,
                "查看异常项",
                "关闭",
                NotificationType.Error,
                "Flat");

            if (openDiagPage)
            {
                GoToDiagPageCore();
            }
        }

        private void GoToDiagPageCore()
        {
            try
            {
                if (MainSideMenu == null)
                {
                    return;
                }

                var target = MainSideMenu.Items?
                    .OfType<SukiSideMenuItem>()
                    .FirstOrDefault(item => item.Tag is ShellPage.Diag);

                if (target != null)
                {
                    MainSideMenu.SelectedItem = target;
                }
            }
            catch
            {
            }
        }

        internal async Task PrepareForDisplayAsync()
        {
            if (_startupSequenceStarted)
            {
                return;
            }

            _startupSequenceStarted = true;

            try
            {
                await InitializeSettingsStartupAsync();
                await InitializeLaunchStartupAsync();

                await WarmSettingsDeferredAsync();
                await WarmDisplayDeferredAsync();
                await InitializeDiagnosticStartupAsync();
                ApplyAboutVersion();
                InitializeDisplayLayoutControls();
                _pendingEnvironmentScanErrorDialog = HasEnvironmentScanErrors;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pre-show initialization failed.");
                throw;
            }

        }

        internal async Task PlayWindowFadeOutAndCloseAsync()
        {
            if (_isWindowCloseAnimationRunning)
            {
                return;
            }

            _isWindowCloseAnimationRunning = true;

            try
            {
                if (!await TryPlayNativeWindowFadeAsync(byte.MaxValue, 0))
                {
                    await CreateWindowOpacityAnimation(Math.Clamp(Opacity, 0d, 1d), 0d).RunAsync(this, CancellationToken.None);
                }
            }
            finally
            {
                Hide();
                _allowImmediateWindowClose = true;
                _isWindowCloseAnimationRunning = false;
                Close();
            }
        }

        private async Task<bool> TryPlayNativeWindowFadeAsync(byte fromAlpha, byte toAlpha)
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            if (!TryGetWindowHandle(out var hwnd))
            {
                await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
                if (!TryGetWindowHandle(out hwnd))
                {
                    return false;
                }
            }

            EnsureLayeredWindowStyle(hwnd);

            SetWindowAlpha(hwnd, fromAlpha);
            await AnimateNativeWindowAlphaAsync(hwnd, fromAlpha, toAlpha);
            SetWindowAlpha(hwnd, toAlpha);
            return true;
        }

        private async Task AnimateNativeWindowAlphaAsync(IntPtr hwnd, byte fromAlpha, byte toAlpha)
        {
            var start = Stopwatch.StartNew();

            while (true)
            {
                var progress = Math.Clamp(start.Elapsed.TotalMilliseconds / WindowFadeDurationMs, 0d, 1d);
                var easedProgress = EaseInOutCubic(progress);
                var currentAlpha = (byte)Math.Round(fromAlpha + ((toAlpha - fromAlpha) * easedProgress));
                SetWindowAlpha(hwnd, currentAlpha);

                if (progress >= 1d)
                {
                    break;
                }

                await Task.Delay(WindowFadeFrameDelayMs);
            }
        }

        private static double EaseInOutCubic(double progress)
        {
            return progress < 0.5d
                ? 4d * progress * progress * progress
                : 1d - Math.Pow(-2d * progress + 2d, 3d) / 2d;
        }

        private bool TryGetWindowHandle(out IntPtr hwnd)
        {
            hwnd = IntPtr.Zero;
            var platformHandle = TryGetPlatformHandle();
            if (platformHandle?.Handle is not IntPtr handle || handle == IntPtr.Zero)
            {
                return false;
            }

            hwnd = handle;
            return true;
        }

        private static void SetWindowAlpha(IntPtr hwnd, byte alpha)
        {
            SetLayeredWindowAttributes(hwnd, 0, alpha, LayeredWindowAlphaFlag);
        }

        private static void EnsureLayeredWindowStyle(IntPtr hwnd)
        {
            var exStyle = GetWindowExStyle(hwnd);
            var layeredExStyle = new IntPtr(exStyle.ToInt64() | LayeredWindowStyle);
            if (layeredExStyle != exStyle)
            {
                SetWindowExStyle(hwnd, layeredExStyle);
            }
        }

        private static nint GetWindowExStyle(nint hwnd) =>
            nint.Size == 8
                ? GetWindowLongPtr64(hwnd, ExStyleIndex)
                : GetWindowLong32(hwnd, ExStyleIndex);

        private static void SetWindowExStyle(nint hwnd, nint exStyle)
        {
            if (nint.Size == 8)
                SetWindowLongPtr64(hwnd, ExStyleIndex, exStyle);
            else
                SetWindowLong32(hwnd, ExStyleIndex, (int)exStyle);
        }

        private static Animation CreateWindowOpacityAnimation(double fromOpacity, double toOpacity)
        {
            return new Animation
            {
                Duration = TimeSpan.FromMilliseconds(WindowFadeDurationMs),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0d),
                        Setters =
                        {
                            new Setter
                            {
                                Property = OpacityProperty,
                                Value = fromOpacity
                            }
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters =
                        {
                            new Setter
                            {
                                Property = OpacityProperty,
                                Value = toOpacity
                            }
                        }
                    }
                }
            };
        }

        private void InitializeCustomComponents()
        {
            _isLoadingSettings = true;
            InitializeSettingsComponents();
            _isLoadingSettings = false;

            InitializeExitRestoreBinding();
            HideLaunchLogArea(clearOutput: true);
            InitializeLaunchControls();

            FinalizeInitialViewState();
        }

        private void FinalizeInitialViewState()
        {
            ApplyGlobalBusyStateToUi(false, string.Empty);
            ApplyRuntimeProgressStateToUi(false, string.Empty, 0d);
            ApplySideMenuNavigationLock();

            Closing += OnWindowClosing;
        }

        private string GetContentsDirectoryPath()
        {
            return _paths.GetContentsDirectoryPath();
        }

        private BusyLease BeginBusy(BusyPresentation presentation, string text = "", double progressValue = 0d)
        {
            BusyEntry entry;
            lock (_busySync)
            {
                entry = new BusyEntry(++_nextBusyId, presentation, text, progressValue);
                _busyEntries.Add(entry);
            }

            RefreshBusyState();
            return new BusyLease(this, entry.Id);
        }

        private void SetNavigationLocked(bool locked)
        {
            if (_isNavigationLocked == locked)
            {
                return;
            }

            _isNavigationLocked = locked;
            if (Dispatcher.UIThread.CheckAccess())
            {
                ApplySideMenuNavigationLock();
            }
            else
            {
                Dispatcher.UIThread.Post(ApplySideMenuNavigationLock);
            }
        }

        private void UpdateBusy(int id, string text, double? progressValue)
        {
            lock (_busySync)
            {
                var entry = _busyEntries.FirstOrDefault(candidate => candidate.Id == id);
                if (entry == null)
                {
                    return;
                }

                entry.Text = text ?? string.Empty;
                if (progressValue.HasValue)
                {
                    entry.ProgressValue = Math.Clamp(progressValue.Value, 0d, 100d);
                }
            }

            RefreshBusyState();
        }

        private void EndBusy(int id)
        {
            lock (_busySync)
            {
                _busyEntries.RemoveAll(entry => entry.Id == id);
            }

            RefreshBusyState();
        }

        private void RefreshBusyState()
        {
            BusyEntry global;
            BusyEntry runtime;
            bool navigationLocked;
            lock (_busySync)
            {
                global = _busyEntries.LastOrDefault(entry => entry.Presentation == BusyPresentation.GlobalOverlay);
                runtime = _busyEntries.LastOrDefault(entry => entry.Presentation == BusyPresentation.RuntimeProgress);
                navigationLocked = _busyEntries.Any(entry => entry.Presentation == BusyPresentation.NavigationLock);
            }

            void Apply()
            {
                ApplyGlobalBusyStateToUi(global != null, global?.Text ?? string.Empty);
                ApplyRuntimeProgressStateToUi(runtime != null, runtime?.Text ?? string.Empty, runtime?.ProgressValue ?? 0d);
                SetNavigationLocked(navigationLocked);
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                Apply();
            }
            else
            {
                Dispatcher.UIThread.Post(Apply);
            }
        }

        private enum BusyPresentation
        {
            GlobalOverlay,
            NavigationLock,
            RuntimeProgress
        }

        private sealed class BusyEntry
        {
            public BusyEntry(int id, BusyPresentation presentation, string text, double progressValue)
            {
                Id = id;
                Presentation = presentation;
                Text = text ?? string.Empty;
                ProgressValue = Math.Clamp(progressValue, 0d, 100d);
            }

            public int Id { get; }
            public BusyPresentation Presentation { get; }
            public string Text { get; set; }
            public double ProgressValue { get; set; }
        }

        private sealed class BusyLease : IDisposable
        {
            private MainWindow _owner;
            private readonly int _id;

            public BusyLease(MainWindow owner, int id)
            {
                _owner = owner;
                _id = id;
            }

            public void UpdateText(string text) => _owner?.UpdateBusy(_id, text, null);

            public void UpdateProgress(string text, double progressValue) =>
                _owner?.UpdateBusy(_id, text, progressValue);

            public void Dispose()
            {
                var owner = Interlocked.Exchange(ref _owner, null);
                owner?.EndBusy(_id);
            }
        }
    }
}
