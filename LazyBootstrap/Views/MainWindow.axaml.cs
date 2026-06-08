// written by Arkito aka Takanashi Ryo, only release in SDVX Lazy Pack.
using System;
using System.Collections.Generic;
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

namespace LazyBootstrap.Views
{
    public partial class MainWindow : SukiWindow
    {
        private readonly MainWindowViewModel _viewModel = null!;
        private readonly IConfigHandler _configFile = null!;
        private bool _isLoadingSettings = false; // 标记是否正在加载设置

        // 统一路径前缀
        private readonly ILauncherPaths _paths = null!;
        private readonly IDisplaySettingsTransactionCoordinator _displaySettingsTransactionCoordinator = null!;
        private readonly ISettingsWorkflowService _settingsWorkflowService = null!;

        private DispatcherTimer _displayPulseTimer;
        private double _displayPulsePhase = 0d;
        private readonly ISukiDialogManager _dialogManager = null!;
        private readonly ISukiToastManager _toastManager = null!;
        private readonly IUiInteractionService _uiInteractionService = null!;
        private readonly ILogger<MainWindow> _logger = null!;

        private bool _isSettingsBusy;
        private bool _isSyncingModel;
        private bool _isUpdatingGpuCompatLayerUi;
        private bool _isUpdatingServerPresetUi;
        private bool _isLaunchLogVisible;
        private bool _isLaunchLogAppendAnimating;
        private bool _isLaunchLogAppendAnimationPending;
        private bool _isUpdatingAsioDriverUi;
        private bool _isUpdatingNetworkUi;
        private bool _startupSequenceStarted;
        private bool _isDisplayLayoutInitialized;
        private bool _isWindowCloseAnimationRunning;
        private bool _allowImmediateWindowClose;
        private bool _pendingEnvironmentScanErrorDialog;
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

            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;

            if (!Design.IsDesignMode)
            {
                throw new InvalidOperationException("请通过依赖注入创建 MainWindow。");
            }
        }

        internal MainWindow(
            MainWindowViewModel viewModel,
            IConfigHandler configFile,
            ILauncherPaths paths,
            IDisplaySettingsTransactionCoordinator displaySettingsTransactionCoordinator,
            ISettingsWorkflowService settingsWorkflowService,
            ISukiDialogManager dialogManager,
            ISukiToastManager toastManager,
            IUiInteractionService uiInteractionService,
            ILogger<MainWindow> logger)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _configFile = configFile ?? throw new ArgumentNullException(nameof(configFile));
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _displaySettingsTransactionCoordinator = displaySettingsTransactionCoordinator ?? throw new ArgumentNullException(nameof(displaySettingsTransactionCoordinator));
            _settingsWorkflowService = settingsWorkflowService ?? throw new ArgumentNullException(nameof(settingsWorkflowService));
            _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
            _toastManager = toastManager ?? throw new ArgumentNullException(nameof(toastManager));
            _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            DataContext = _viewModel;

            if (DialogHost != null)
            {
                DialogHost.Manager = _dialogManager;
            }
            if (ToastHost != null)
            {
                ToastHost.Manager = _toastManager;
            }

            _uiInteractionService.AttachWindow(this);
            Opened += OnWindowOpened;
            Closed += OnWindowClosed;

            _isLoadingSettings = true;
            InitializeCustomComponents();
            HideLaunchLogArea(true);
            HookLaunchViewModelState();
            HookSettingsViewModelState();
            HookDisplayViewModelState();
            HookToolsViewModelState();
            HookInfoViewModelState();
            _isLoadingSettings = false;
            _logger.LogInformation("Main window initialized for base directory {BaseDirectory}.", _paths.BaseDir);
        }

        private void UpdateStatusText(string statusText)
        {
            _viewModel.StatusText = statusText ?? string.Empty;
            _viewModel.Launch.StateText = _viewModel.StatusText;

            if (StatusLabel != null)
            {
                StatusLabel.Text = _viewModel.StatusText;
            }
        }

        private void UpdateStatusProgress(bool isVisible, double value = 0d)
        {
            _viewModel.IsStatusProgressVisible = isVisible;
            _viewModel.StatusProgressValue = value;

            if (StatusProgress != null)
            {
                StatusProgress.IsVisible = isVisible;
                StatusProgress.Value = value;
            }
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            Opened -= OnWindowOpened;
            UnhookLaunchViewModelState();
            UnhookSettingsViewModelState();
            UnhookDisplayViewModelState();
            UnhookToolsViewModelState();
            UnhookInfoViewModelState();
            _uiInteractionService.DetachWindow(this);
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
                await _viewModel.InitializeStartupAsync();
                ApplyStartupSettingsViewModelStateToUi();

                UpdateStatusText("正在预热页面内容...");
                await _viewModel.WarmSecondaryPagesAsync();
                ApplyDeferredSettingsViewModelStateToUi();
                ApplyInfoViewModelStateToUi();
                InitializeDisplayLayoutControls();
                RefreshEnvironmentOverviewChrome();
                _pendingEnvironmentScanErrorDialog = _viewModel.Info.HasEnvironmentScanErrors;
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

        private void UpdateAsioControlPanelButtonState()
        {
            if (OpenAsioControlPanelButton == null)
            {
                return;
            }

            var selectedDriverValue = _viewModel.Settings.SelectedAsioDriver?.Value
                ?? _viewModel.Settings.AsioDriverValue
                ?? string.Empty;

            OpenAsioControlPanelButton.IsEnabled = OperatingSystem.IsWindows()
                && !string.IsNullOrWhiteSpace(selectedDriverValue);
        }

        private Task PersistSpice() => _settingsWorkflowService.PersistSpiceSettingsAsync(_viewModel.Settings);

        private void BindToggleSwitch(ToggleSwitch toggle, Action<bool> setValue, Func<Task> persist)
        {
            if (toggle is null) return;
            toggle.IsCheckedChanged += async (_, _) =>
            {
                if (_isLoadingSettings) return;
                setValue(toggle.IsChecked == true);
                await persist();
            };
        }

        private static string BuildCurrentNetworkAdapterDisplayName(string ipAddress, string subnetMask)
        {
            var normalizedIpAddress = ConfigHelper.NormalizeNetworkValue(ipAddress);
            var normalizedSubnetMask = ConfigHelper.NormalizeNetworkValue(subnetMask);
            if (string.IsNullOrEmpty(normalizedIpAddress) && string.IsNullOrEmpty(normalizedSubnetMask))
            {
                return "无";
            }

            if (string.IsNullOrEmpty(normalizedIpAddress))
            {
                return $"{normalizedSubnetMask}（当前配置）";
            }

            if (string.IsNullOrEmpty(normalizedSubnetMask))
            {
                return $"{normalizedIpAddress}（当前配置）";
            }

            return $"{normalizedIpAddress} / {normalizedSubnetMask}（当前配置）";
        }

        private void InitializeCustomComponents()
        {
            InitializeGpuCompatLayerControls();
            InitializeNetworkBindings();
            InitializeStartupSettingsBindings();
            InitializeSpiceSettingsBindings();

            InitializeServerPresetBindings();
            FinalizeInitialViewState();
        }

        private void InitializeGpuCompatLayerControls()
        {
            if (GpuCompatLayerToggleSwitch != null)
            {
                GpuCompatLayerToggleSwitch.IsCheckedChanged -= OnGpuCompatLayerToggleChanged;
                GpuCompatLayerToggleSwitch.IsCheckedChanged += OnGpuCompatLayerToggleChanged;
            }
        }

        private void InitializeNetworkBindings()
        {
            if (ServerAddressTextBox != null)
            {
                ServerAddressTextBox.Watermark = "http://SERVER:PORT";
            }
            if (PcbIdTextBox != null)
            {
                PcbIdTextBox.Watermark = string.Empty;
            }
            if (NetworkAdapterIpTextBox != null)
            {
                NetworkAdapterIpTextBox.Watermark = string.Empty;
                NetworkAdapterIpTextBox.TextChanged += async (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingNetworkUi) return;
                    _viewModel.Settings.NetworkAdapterIp = NetworkAdapterIpTextBox.Text ?? string.Empty;
                    _viewModel.Settings.NetworkAdapterSubnet = NetworkAdapterSubnetTextBox?.Text ?? string.Empty;
                    await _settingsWorkflowService.PersistNetworkSettingsAsync(_viewModel.Settings);
                };
            }
            if (NetworkAdapterSubnetTextBox != null)
            {
                NetworkAdapterSubnetTextBox.Watermark = string.Empty;
                NetworkAdapterSubnetTextBox.TextChanged += async (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingNetworkUi) return;
                    _viewModel.Settings.NetworkAdapterIp = NetworkAdapterIpTextBox?.Text ?? string.Empty;
                    _viewModel.Settings.NetworkAdapterSubnet = NetworkAdapterSubnetTextBox.Text ?? string.Empty;
                    await _settingsWorkflowService.PersistNetworkSettingsAsync(_viewModel.Settings);
                };
            }

            if (OpenNetworkAdapterPickerButton != null)
            {
                OpenNetworkAdapterPickerButton.Content = "加载中...";
                OpenNetworkAdapterPickerButton.IsEnabled = false;
                ToolTip.SetTip(OpenNetworkAdapterPickerButton, "正在读取网卡配置...");
            }
        }

        private void InitializeStartupSettingsBindings()
        {
            BindToggleSwitch(WindowedToggleSwitch,
                v => _viewModel.Settings.Windowed = v,
                () => _settingsWorkflowService.PersistSpiceSettingsAsync(_viewModel.Settings));

            if (NoAsphyxiaToggleSwitch != null)
            {
                NoAsphyxiaToggleSwitch.IsCheckedChanged += async (_, _) =>
                {
                    _viewModel.Settings.NoAsphyxia = NoAsphyxiaToggleSwitch.IsChecked == true;
                    await _settingsWorkflowService.PersistLauncherSettingsAsync(_viewModel.Settings);
                };
            }

            if (UseSystemSpiceConfigToggleSwitch != null)
            {
                UseSystemSpiceConfigToggleSwitch.IsCheckedChanged += async (_, _) =>
                {
                    if (_isLoadingSettings) return;
                    _viewModel.Settings.UseSystemSpiceConfig = UseSystemSpiceConfigToggleSwitch.IsChecked == true;
                    await _settingsWorkflowService.PersistUseSystemSpiceConfigAsync(_viewModel.Settings);
                };
            }

            if (ExitRestoreToggleSwitch != null)
            {
                ExitRestoreToggleSwitch.IsCheckedChanged += async (_, _) =>
                {
                    if (_isLoadingSettings) return;
                    bool enabled = ExitRestoreToggleSwitch.IsChecked == true;
                    _viewModel.Display.ExitRestore = enabled;
                    _viewModel.Settings.ExitRestore = enabled;
                    await _viewModel.Display.PersistGeneralSettingsAsync();
                };
            }
        }

        private void InitializeSpiceSettingsBindings()
        {
            if (DllInjectionTextBox != null)
            {
                DllInjectionTextBox.Watermark = "example.dll";
                DllInjectionTextBox.TextChanged += async (_, _) =>
                {
                    if (_isLoadingSettings) return;
                    _viewModel.Settings.DllInjection = DllInjectionTextBox.Text ?? string.Empty;
                    await _settingsWorkflowService.PersistSpiceSettingsAsync(_viewModel.Settings);
                };
            }

            BindToggleSwitch(NetDumpToggleSwitch, v => _viewModel.Settings.NetDump = v, PersistSpice);
            BindToggleSwitch(DisableSubDisplayToggleSwitch, v => _viewModel.Settings.DisableSubDisplay = v, PersistSpice);

            if (WindowModeComboBox != null)
            {
                WindowModeComboBox.SelectionChanged += async (_, _) =>
                {
                    if (_isLoadingSettings) return;
                    _viewModel.Settings.WindowModeIndex = WindowModeComboBox.SelectedIndex < 0 ? 0 : WindowModeComboBox.SelectedIndex;
                    await PersistSpice();
                };
            }

            BindToggleSwitch(PCoreOptimizationToggleSwitch, v => _viewModel.Settings.PCoreOptimization = v, PersistSpice);
            BindToggleSwitch(SubBorderlessToggleSwitch, v => _viewModel.Settings.SubBorderless = v, PersistSpice);
            BindToggleSwitch(ShowCursorTouchSimToggleSwitch, v => _viewModel.Settings.ShowCursorTouchSim = v, PersistSpice);
            BindToggleSwitch(WindowTopMostToggleSwitch, v => _viewModel.Settings.WindowTopMost = v, PersistSpice);
            BindToggleSwitch(SingleAdapterToggleSwitch, v => _viewModel.Settings.SingleAdapter = v, PersistSpice);
            BindToggleSwitch(NvidiaPerformanceProfileToggleSwitch, v => _viewModel.Settings.NvidiaPerformanceProfile = v, PersistSpice);
            BindToggleSwitch(SubWindowTopMostToggleSwitch, v => _viewModel.Settings.SubWindowTopMost = v, PersistSpice);
            BindToggleSwitch(SubForceRenderToggleSwitch, v => _viewModel.Settings.SubForceRender = v, PersistSpice);
            BindToggleSwitch(NativeTouchToggleSwitch, v => _viewModel.Settings.NativeTouch = v, PersistSpice);
            BindToggleSwitch(CardIoToggleSwitch, v => _viewModel.Settings.CardIo = v, PersistSpice);
            BindToggleSwitch(HidSmartCardToggleSwitch, v => _viewModel.Settings.HidSmartCard = v, PersistSpice);

            if (WindowSizeTextBox != null)
            {
                WindowSizeTextBox.TextChanged += async (_, _) =>
                {
                    if (_isLoadingSettings) return;
                    _viewModel.Settings.WindowSize = WindowSizeTextBox.Text ?? string.Empty;
                    await PersistSpice();
                };
            }
            if (AsioDriverComboBox != null)
            {
                ApplyAsioDriverChoicesFromViewModel();

                AsioDriverComboBox.DropDownOpened += (s, e) =>
                {
                    ApplyAsioDriverChoicesFromViewModel();
                };

                AsioDriverComboBox.SelectionChanged += async (_, _) =>
                {
                    if (_isLoadingSettings || _isUpdatingAsioDriverUi) return;
                    if (AsioDriverComboBox.SelectedItem is AsioDriverOption choice)
                    {
                        _viewModel.Settings.SelectedAsioDriver = choice;
                        _viewModel.Settings.AsioDriverValue = choice.Value;
                    }
                    else
                    {
                        _viewModel.Settings.SelectedAsioDriver = null;
                        _viewModel.Settings.AsioDriverValue = string.Empty;
                    }

                    await PersistSpice();
                };
            }
            BindToggleSwitch(LowLatencySharedAudioToggleSwitch, v => _viewModel.Settings.LowLatencySharedAudio = v, PersistSpice);
        }

        private void InitializeServerPresetBindings()
        {
            if (ServerAddressTextBox != null)
            {
                ServerAddressTextBox.TextChanged += async (s, e) =>
                {
                    if (_isLoadingSettings || _isSyncingModel) return;
                    _viewModel.Settings.ServerAddress = ServerAddressTextBox.Text ?? string.Empty;
                    _viewModel.Settings.PcbId = PcbIdTextBox?.Text ?? string.Empty;
                    await _settingsWorkflowService.PersistServerEndpointAsync(_viewModel.Settings);
                };
            }
            if (PcbIdTextBox != null)
            {
                PcbIdTextBox.TextChanged += async (s, e) =>
                {
                    if (_isLoadingSettings || _isSyncingModel) return;
                    _viewModel.Settings.ServerAddress = ServerAddressTextBox?.Text ?? string.Empty;
                    _viewModel.Settings.PcbId = PcbIdTextBox.Text ?? string.Empty;
                    await _settingsWorkflowService.PersistServerEndpointAsync(_viewModel.Settings);
                };
            }
        }

        private void FinalizeInitialViewState()
        {
            UpdateStatusText("就绪");
            UpdateStatusProgress(false);

            Closing += OnWindowClosing;
            UpdateGpuCompatLayerStatus();
        }

        private string GetContentsDirectoryPath()
        {
            return _paths.GetContentsDirectoryPath();
        }

        private void SetSettingsBusy(bool isBusy)
        {
            _isSettingsBusy = isBusy;
            if (SettingsBusyArea != null)
            {
                SettingsBusyArea.IsBusy = isBusy;
            }

            if (EditConfigButton != null)
            {
                EditConfigButton.IsEnabled = !isBusy;
                EditConfigButton.Content = isBusy ? "编辑 spicecfg（运行中...）" : "编辑 spicecfg";
            }
        }
    }
}
