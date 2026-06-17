using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using Avalonia;

namespace LazyBootstrap.Shell
{
    public partial class MainWindow : SukiWindow
    {
        private readonly AppShellState _shellStateService = null!;
        private readonly DisplayWorkflowService _displayWorkflowService = null!;
        private readonly EnvironmentScanService _environmentScanService = null!;
        private readonly SettingsState _settingsState = null!;
        private readonly DisplayConfigurationSnapshot _displayState = null!;
        private readonly EnvironmentScanPresentation _infoState = new();
        private bool _isLoadingSettings;

        private readonly LauncherPaths _paths = null!;
        private readonly LaunchView _launchView = null!;
        private readonly SettingsView _settingsView = null!;
        private readonly ToolsView _toolsView = null!;
        private readonly UpdateView _updateView = null!;

        private DispatcherTimer _displayPulseTimer;
        private double _displayPulsePhase = 0d;
        private readonly ISukiDialogManager _dialogManager = null!;
        private readonly ISukiToastManager _toastManager = null!;
        private readonly UiInteractionService _uiInteractionService = null!;
        private readonly ILogger<MainWindow> _logger = null!;

        private bool _isUpdatingDisplayLayoutUi;
        private bool _startupSequenceStarted;
        private bool _isDisplayLayoutInitialized;
        private bool _isWindowCloseAnimationRunning;
        private bool _allowImmediateWindowClose;
        private bool _pendingEnvironmentScanErrorDialog;
        private bool _isRestoringSideMenuSelection;
        private SukiSideMenuItem _lastUnlockedSideMenuItem;
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
            AppShellState shellStateService,
            LauncherPaths paths,
            DisplayConfigurationSnapshot displayState,
            DisplayWorkflowService displayWorkflowService,
            EnvironmentScanService environmentScanService,
            LaunchView launchView,
            SettingsView settingsView,
            SettingsState settingsState,
            ToolsView toolsView,
            UpdateView updateView,
            ISukiDialogManager dialogManager,
            ISukiToastManager toastManager,
            UiInteractionService uiInteractionService,
            ILogger<MainWindow> logger)
        {
            InitializeComponent();

            _shellStateService = shellStateService ?? throw new ArgumentNullException(nameof(shellStateService));
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _displayState = displayState ?? throw new ArgumentNullException(nameof(displayState));
            _displayWorkflowService = displayWorkflowService ?? throw new ArgumentNullException(nameof(displayWorkflowService));
            _environmentScanService = environmentScanService ?? throw new ArgumentNullException(nameof(environmentScanService));
            _launchView = launchView ?? throw new ArgumentNullException(nameof(launchView));
            _settingsView = settingsView ?? throw new ArgumentNullException(nameof(settingsView));
            _settingsState = settingsState ?? throw new ArgumentNullException(nameof(settingsState));
            _toolsView = toolsView ?? throw new ArgumentNullException(nameof(toolsView));
            _updateView = updateView ?? throw new ArgumentNullException(nameof(updateView));
            _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
            _toastManager = toastManager ?? throw new ArgumentNullException(nameof(toastManager));
            _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (DialogHost != null)
            {
                DialogHost.Manager = _dialogManager;
            }
            if (ToastHost != null)
            {
                ToastHost.Manager = _toastManager;
            }

            if (LaunchPageHost != null)
            {
                LaunchPageHost.Content = _launchView;
            }

            if (SettingsPageHost != null)
            {
                SettingsPageHost.Content = _settingsView;
            }

            if (UpdatePageHost != null)
            {
                UpdatePageHost.Content = _updateView;
            }

            if (ToolsPageHost != null)
            {
                ToolsPageHost.Content = _toolsView;
            }

            _uiInteractionService.AttachWindow(this);
            Opened += OnWindowOpened;
            Closed += OnWindowClosed;
            _shellStateService.PropertyChanged += OnShellStatePropertyChanged;
            if (MainSideMenu != null)
            {
                MainSideMenu.SelectionChanged += OnMainSideMenuSelectionChanged;
            }

            _isLoadingSettings = true;
            InitializeCustomComponents();
            _isLoadingSettings = false;
            _logger.LogInformation("Main window initialized for base directory {BaseDirectory}.", _paths.BaseDir);
        }

        private void UpdateStatusText(string statusText)
        {
            _shellStateService.StatusText = statusText ?? string.Empty;
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            Opened -= OnWindowOpened;
            _shellStateService.PropertyChanged -= OnShellStatePropertyChanged;
            if (MainSideMenu != null)
            {
                MainSideMenu.SelectionChanged -= OnMainSideMenuSelectionChanged;
            }
            _uiInteractionService.DetachWindow(this);
        }

        private void OnShellStatePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnShellStatePropertyChanged(sender, e));
                return;
            }

            string propertyName = e?.PropertyName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(propertyName)
                || string.Equals(propertyName, nameof(AppShellState.IsGlobalBusy), StringComparison.Ordinal)
                || string.Equals(propertyName, nameof(AppShellState.GlobalBusyText), StringComparison.Ordinal))
            {
                ApplyGlobalBusyStateToUi();
            }

            if (string.IsNullOrWhiteSpace(propertyName)
                || string.Equals(propertyName, nameof(AppShellState.IsRuntimeProgressBusy), StringComparison.Ordinal)
                || string.Equals(propertyName, nameof(AppShellState.RuntimeProgressText), StringComparison.Ordinal)
                || string.Equals(propertyName, nameof(AppShellState.RuntimeProgressValue), StringComparison.Ordinal))
            {
                ApplyRuntimeProgressStateToUi();
            }

            if (string.IsNullOrWhiteSpace(propertyName)
                || string.Equals(propertyName, nameof(AppShellState.IsNavigationLocked), StringComparison.Ordinal))
            {
                ApplySideMenuNavigationLock();
            }
        }

        private void ApplyGlobalBusyStateToUi()
        {
            if (GlobalBusyArea == null)
            {
                return;
            }

            GlobalBusyArea.IsBusy = _shellStateService.IsGlobalBusy;
            GlobalBusyArea.BusyText = _shellStateService.GlobalBusyText;
        }

        private void ApplyRuntimeProgressStateToUi()
        {
            bool visible = _shellStateService.IsRuntimeProgressBusy;
            if (RuntimeInstallOverlay != null)
            {
                RuntimeInstallOverlay.IsVisible = visible;
                RuntimeInstallOverlay.Opacity = visible ? 1 : 0;
            }

            SetRuntimeInstallProgress(
                _shellStateService.RuntimeProgressText,
                _shellStateService.RuntimeProgressValue);
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

            if (_shellStateService.IsNavigationLocked)
            {
                RestoreLockedSideMenuSelection();
                return;
            }

            _lastUnlockedSideMenuItem = selectedItem;
            _shellStateService.SelectedPage = ResolveShellPage(selectedItem);
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

            if (!_shellStateService.IsNavigationLocked)
            {
                foreach (var item in items)
                {
                    item.IsEnabled = true;
                }

                if (MainSideMenu.SelectedItem is SukiSideMenuItem selectedItem)
                {
                    _lastUnlockedSideMenuItem = selectedItem;
                    _shellStateService.SelectedPage = ResolveShellPage(selectedItem);
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
            if (MainSideMenu == null || selectedItem == null)
            {
                return _shellStateService.SelectedPage;
            }

            var items = MainSideMenu.Items?
                .OfType<SukiSideMenuItem>()
                .ToList() ?? new List<SukiSideMenuItem>();
            int index = items.IndexOf(selectedItem);

            return index switch
            {
                0 => ShellPage.Launch,
                1 => ShellPage.Settings,
                2 => ShellPage.Display,
                3 => ShellPage.Tools,
                4 => ShellPage.Update,
                5 => ShellPage.Info,
                6 => ShellPage.About,
                _ => _shellStateService.SelectedPage
            };
        }

        private async void OnWindowOpened(object sender, EventArgs e)
        {
            try
            {
                Opened -= OnWindowOpened;

                if (!_pendingEnvironmentScanErrorDialog)
                {
                    return;
                }

                _pendingEnvironmentScanErrorDialog = false;
                await ShowEnvironmentScanErrorDialogAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Environment scan error dialog failed.");
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
                UpdateStatusText("正在读取启动配置...");
                await _settingsView.InitializeStartupAsync();
                await _launchView.InitializeStartupAsync();

                UpdateStatusText("正在预热页面内容...");
                await _settingsView.WarmDeferredAsync();
                await _displayWorkflowService.WarmDeferredAsync(_displayState);
                await _environmentScanService.InitializeInfoAsync(_infoState);
                await _environmentScanService.RunScanAsync(_infoState);
                ApplyInfoStateToUi();
                InitializeDisplayLayoutControls();
                RefreshEnvironmentOverviewChrome();
                _pendingEnvironmentScanErrorDialog = _infoState.HasEnvironmentScanErrors;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pre-show initialization failed.");
                throw;
            }

            UpdateStatusText("就绪");
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
            InitializeExitRestoreBinding();
            FinalizeInitialViewState();
        }

        private void InitializeExitRestoreBinding()
        {
            if (ExitRestoreToggleSwitch != null)
            {
                ExitRestoreToggleSwitch.IsCheckedChanged += async (_, _) =>
                {
                    if (_isLoadingSettings) return;
                    bool enabled = ExitRestoreToggleSwitch.IsChecked == true;
                    _displayState.ExitRestore = enabled;
                    _settingsState.ExitRestore = enabled;
                    await _displayWorkflowService.PersistGeneralSettingsAsync(_displayState);
                };
            }
        }

        private void FinalizeInitialViewState()
        {
            UpdateStatusText("就绪");
            ApplyGlobalBusyStateToUi();
            ApplyRuntimeProgressStateToUi();
            ApplySideMenuNavigationLock();

            Closing += OnWindowClosing;
        }

        private string GetContentsDirectoryPath()
        {
            return _paths.GetContentsDirectoryPath();
        }
    }
}
