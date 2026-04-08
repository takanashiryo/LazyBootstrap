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
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
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
        private bool _portableMode = false; // 是否使用便携模式
        private bool _isLoadingSettings = false; // 标记是否正在加载设置

        // 统一路径前缀
        private readonly ILauncherPaths _paths = null!;
        private readonly ISpiceConfigFileService _spiceConfigFileService = null!;
        private readonly IDisplayConfigurationService _displayConfigurationService = null!;
        private readonly IDisplaySettingsTransactionCoordinator _displaySettingsTransactionCoordinator = null!;
        private readonly ISettingsWorkflowService _settingsWorkflowService = null!;

        private string _compatTypeTooltipCache;
        private static Bitmap _warningDialogIconCache;
        private static Bitmap _errorDialogIconCache;

        private bool _disableSubDisplay = false;
        private int _windowModeIndex = 0; // 0: 默认, 1: 无边框, 2: 可变窗口
        private bool _subBorderless = false;
        private bool _showCursorTouchSim = false;
        private bool _pCoreOptimization = false;
        private bool _windowTopMost = false;
        private string _windowSize = string.Empty;
        private bool _singleAdapter = false;
        private bool _subWindowTopMost = false;
        private bool _subForceRender = false;
        private bool _nativeTouch = false;
        private string _asioDriver = string.Empty;
        private bool _lowLatencySharedAudio = false;
        private bool _cardIo = false;
        private bool _hidSmartCard = false;

        private bool _dbgNetDump = false;
        private bool _displayConfigEnabled = false;
        private bool _isDualDisplay = true;
        private readonly List<DisplayInfo> _displayInfos = new List<DisplayInfo>();
        private readonly Dictionary<string, DisplayState> _displayRestoreStates = new Dictionary<string, DisplayState>(StringComparer.OrdinalIgnoreCase);
        private DisplaySelectionTarget _selectedDisplayTarget = DisplaySelectionTarget.None;
        private DispatcherTimer _displayPulseTimer;
        private double _displayPulsePhase = 0d;
        private readonly ISukiDialogManager _dialogManager = null!;
        private readonly ISukiToastManager _toastManager = null!;
        private readonly IUiInteractionService _uiInteractionService = null!;
        private readonly ILogger<MainWindow> _logger = null!;

        private const string NonePresetName = "无";
        private const string AsphyxiaPresetName = "Asphyxia";
        private const string AsphyxiaDefaultUrl = "http://localhost:8083";
        private const string SettingSectionName = AppConfigBootstrapper.SettingSectionName;
        private const string DisplaySectionName = AppConfigBootstrapper.DisplaySectionName;
        private string _activeServerPreset = NonePresetName;
        private readonly List<ServerPresetItem> _serverPresets = new List<ServerPresetItem>();
        private readonly Dictionary<string, string> _lastKnownSpiceValues = new Dictionary<string, string>(StringComparer.Ordinal);
        private string _lastKnownCompatRenderMode = "dx9on12";
        private bool _isSettingsBusy;
        private bool _isSyncingModel;
        private bool _isUpdatingCompatUi;
        private bool _isUpdatingPortableModeUi;
        private bool _isUpdatingSpiceToggleUi;
        private bool _isLaunchLogVisible;
        private bool _isLaunchLogAppendAnimating;
        private bool _isLaunchLogAppendAnimationPending;
        private bool _isApplyingAspectRatio;
        private bool _isUpdatingAsioDriverUi;
        private double _lastNormalWidth;
        private double _lastNormalHeight;
        private const double MainWindowAspectRatio = 16d / 9d;
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

        private enum DisplaySelectionTarget
        {
            None,
            Main,
            Sub
        }

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
            ISpiceConfigFileService spiceConfigFileService,
            IDisplayConfigurationService displayConfigurationService,
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
            _spiceConfigFileService = spiceConfigFileService ?? throw new ArgumentNullException(nameof(spiceConfigFileService));
            _displayConfigurationService = displayConfigurationService ?? throw new ArgumentNullException(nameof(displayConfigurationService));
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
            _isLoadingSettings = false;
            _lastNormalWidth = Width;
            _lastNormalHeight = Height;
            SizeChanged += OnMainWindowSizeChanged;
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
            _uiInteractionService.DetachWindow(this);
        }

        private async void OnWindowOpened(object sender, EventArgs e)
        {
            Opened -= OnWindowOpened;

            if (!_pendingEnvironmentScanErrorDialog)
            {
                return;
            }

            _pendingEnvironmentScanErrorDialog = false;
            await ShowEnvironmentScanErrorDialogAsync();
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
                RefreshEnvironmentScanResultCard();
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

            try
            {
                SetWindowAlpha(hwnd, fromAlpha);
                await AnimateNativeWindowAlphaAsync(hwnd, fromAlpha, toAlpha);
                SetWindowAlpha(hwnd, toAlpha);
                return true;
            }
            finally
            {
            }
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

        private static IntPtr GetWindowExStyle(IntPtr hwnd)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hwnd, ExStyleIndex)
                : new IntPtr(GetWindowLong32(hwnd, ExStyleIndex));
        }

        private static void SetWindowExStyle(IntPtr hwnd, IntPtr exStyle)
        {
            if (IntPtr.Size == 8)
            {
                SetWindowLongPtr64(hwnd, ExStyleIndex, exStyle);
                return;
            }

            SetWindowLong32(hwnd, ExStyleIndex, exStyle.ToInt32());
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

        private void OnMainWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isApplyingAspectRatio || WindowState != WindowState.Normal)
            {
                return;
            }

            var decision = AspectRatioResizeCalculator.Calculate(
                Width,
                Height,
                _lastNormalWidth,
                _lastNormalHeight,
                MinWidth,
                MinHeight,
                MainWindowAspectRatio);

            if (decision.Action == AspectRatioResizeAction.None)
            {
                return;
            }

            if (decision.Action == AspectRatioResizeAction.InitializeTracking)
            {
                _lastNormalWidth = decision.Width;
                _lastNormalHeight = decision.Height;
                return;
            }

            _isApplyingAspectRatio = true;
            try
            {
                Width = decision.Width;
                Height = decision.Height;
                _lastNormalWidth = decision.Width;
                _lastNormalHeight = decision.Height;
            }
            finally
            {
                _isApplyingAspectRatio = false;
            }
        }

        private void RefreshAsioDriverChoices(string selectedValue)
        {
            if (AsioDriverComboBox == null)
            {
                return;
            }

            var choices = new List<AsioDriverOption>
            {
                new("无", string.Empty)
            };

            foreach (var driverName in AsioDriverRegistry.GetInstalledDriverNames())
            {
                choices.Add(new AsioDriverOption(driverName, driverName));
            }

            if (!string.IsNullOrWhiteSpace(selectedValue)
                && !choices.Any(choice => string.Equals(choice.Value, selectedValue, StringComparison.OrdinalIgnoreCase)))
            {
                choices.Add(new AsioDriverOption($"{selectedValue}（当前配置）", selectedValue));
            }

            var targetChoice = choices.FirstOrDefault(choice =>
                string.Equals(choice.Value, selectedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                ?? choices[0];
            _viewModel.Settings.AsioDriver = targetChoice.Value;

            _isUpdatingAsioDriverUi = true;
            try
            {
                AsioDriverComboBox.Items.Clear();
                foreach (var choice in choices)
                {
                    AsioDriverComboBox.Items.Add(choice);
                }

                AsioDriverComboBox.SelectedItem = targetChoice;
            }
            finally
            {
                _isUpdatingAsioDriverUi = false;
            }

            UpdateAsioControlPanelButtonState();
        }

        private string GetSelectedAsioDriverValue()
        {
            return AsioDriverComboBox?.SelectedItem is AsioDriverOption choice
                ? choice.Value
                : string.Empty;
        }

        private void UpdateAsioControlPanelButtonState()
        {
            if (OpenAsioControlPanelButton == null)
            {
                return;
            }

            OpenAsioControlPanelButton.IsEnabled = OperatingSystem.IsWindows()
                && !string.IsNullOrWhiteSpace(GetSelectedAsioDriverValue());
        }

        private bool EnsureSpiceXmlExistsForAsioOrRevert()
        {
            var xmlPath = GetSpiceXmlPath();
            if (File.Exists(xmlPath))
            {
                return true;
            }

            ShowErrorToast("保存设定失败", "未找到 spicetools.xml。");
            RefreshAsioDriverChoices(GetLastKnownSpiceValue("sp2x-sdvxasio"));
            return false;
        }

        private void RefreshNetworkAdapterChoices(string selectedIpAddress, string selectedSubnetMask)
        {
            var normalizedIpAddress = NormalizeNetworkValue(selectedIpAddress);
            var normalizedSubnetMask = NormalizeNetworkValue(selectedSubnetMask);
            var choices = new List<NetworkAdapterOption>
            {
                new("无", string.Empty, string.Empty)
            };

            foreach (var adapter in NetworkAdapterDiscovery.GetAvailableAdapters())
            {
                choices.Add(new NetworkAdapterOption(adapter.DisplayName, adapter.IpAddress, adapter.SubnetMask));
            }

            if ((!string.IsNullOrWhiteSpace(normalizedIpAddress) || !string.IsNullOrWhiteSpace(normalizedSubnetMask))
                && !choices.Any(choice =>
                    string.Equals(choice.IpAddress, normalizedIpAddress, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(choice.SubnetMask, normalizedSubnetMask, StringComparison.OrdinalIgnoreCase)))
            {
                choices.Add(new NetworkAdapterOption(
                    $"{normalizedIpAddress} / {normalizedSubnetMask}（当前配置）".Trim(),
                    normalizedIpAddress,
                    normalizedSubnetMask));
            }

            var targetChoice = choices.FirstOrDefault(choice =>
                                   string.Equals(choice.IpAddress, normalizedIpAddress, StringComparison.OrdinalIgnoreCase)
                                   && string.Equals(choice.SubnetMask, normalizedSubnetMask, StringComparison.OrdinalIgnoreCase))
                               ?? choices[0];

            if (OpenNetworkAdapterPickerButton == null)
            {
                return;
            }

            var hasSelectableChoice = choices.Count > 1;
            OpenNetworkAdapterPickerButton.Content = hasSelectableChoice ? "选择" : "无可用网卡";
            ToolTip.SetTip(
                OpenNetworkAdapterPickerButton,
                hasSelectableChoice
                    ? $"当前配置：{targetChoice.DisplayName}"
                    : "未检测到可用网卡");
        }

        private static string NormalizeNetworkValue(string value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static string BuildCurrentNetworkAdapterDisplayName(string ipAddress, string subnetMask)
        {
            var normalizedIpAddress = NormalizeNetworkValue(ipAddress);
            var normalizedSubnetMask = NormalizeNetworkValue(subnetMask);
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

        private string GetNetworkAdapterIpAddress()
        {
            return NormalizeNetworkValue(NetworkAdapterIpTextBox?.Text);
        }

        private string GetNetworkAdapterSubnetMask()
        {
            return NormalizeNetworkValue(NetworkAdapterSubnetTextBox?.Text);
        }

        private void ApplyNetworkSettings(string ipAddress, string subnetMask, bool persistChanges)
        {
            var normalizedIpAddress = NormalizeNetworkValue(ipAddress);
            var normalizedSubnetMask = NormalizeNetworkValue(subnetMask);

            _isUpdatingNetworkUi = true;
            try
            {
                if (NetworkAdapterIpTextBox != null && !string.Equals(NetworkAdapterIpTextBox.Text, normalizedIpAddress, StringComparison.Ordinal))
                {
                    NetworkAdapterIpTextBox.Text = normalizedIpAddress;
                }

                if (NetworkAdapterSubnetTextBox != null && !string.Equals(NetworkAdapterSubnetTextBox.Text, normalizedSubnetMask, StringComparison.Ordinal))
                {
                    NetworkAdapterSubnetTextBox.Text = normalizedSubnetMask;
                }
            }
            finally
            {
                _isUpdatingNetworkUi = false;
            }

            RefreshNetworkAdapterChoices(normalizedIpAddress, normalizedSubnetMask);

            if (!persistChanges)
            {
                return;
            }

            UpdateSpiceConfig(
                new SpiceOptionUpdate("network", normalizedIpAddress, false),
                new SpiceOptionUpdate("subnet", normalizedSubnetMask, false));
        }

        private bool EnsureSpiceXmlExistsForNetworkOrRevert()
        {
            var xmlPath = GetSpiceXmlPath();
            if (File.Exists(xmlPath))
            {
                return true;
            }

            ShowErrorToast("保存设定失败", "未找到 spicetools.xml。");
            RestoreNetworkUiFromLastKnownValues();
            return false;
        }

        private void RestoreNetworkUiFromLastKnownValues()
        {
            var networkValue = NormalizeNetworkValue(GetLastKnownSpiceValue("network"));
            var subnetValue = NormalizeNetworkValue(GetLastKnownSpiceValue("subnet"));

            _isUpdatingNetworkUi = true;
            try
            {
                if (NetworkAdapterIpTextBox != null) NetworkAdapterIpTextBox.Text = networkValue;
                if (NetworkAdapterSubnetTextBox != null) NetworkAdapterSubnetTextBox.Text = subnetValue;
                RefreshNetworkAdapterChoices(networkValue, subnetValue);
            }
            finally
            {
                _isUpdatingNetworkUi = false;
            }
        }

        private void InitializeCustomComponents()
        {
            InitializeCompatibilityControls();
            InitializeNetworkAndOverrideBindings();
            InitializeStartupSettingsBindings();
            InitializeSpiceSettingsBindings();

            InitializeServerPresetBindings();
            FinalizeInitialViewState();
        }

        private void InitializeCompatibilityControls()
        {
            if (CompatTypeComboBox == null)
            {
                return;
            }

            EnsureCompatRenderModesInitialized();
            ApplyCompatRenderModeSelection(_lastKnownCompatRenderMode);

            if (CompatLayerToggleSwitch != null)
            {
                CompatLayerToggleSwitch.IsCheckedChanged -= OnCompatLayerToggleChanged;
                CompatLayerToggleSwitch.IsCheckedChanged += OnCompatLayerToggleChanged;
            }

            var tipObj = ToolTip.GetTip(CompatTypeComboBox);
            if (tipObj != null)
            {
                _compatTypeTooltipCache = tipObj.ToString();
            }

            CompatTypeComboBox.SelectionChanged += OnCompatTypeSelectionChanged;
        }

        private void InitializeNetworkAndOverrideBindings()
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
                NetworkAdapterIpTextBox.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingNetworkUi || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForNetworkOrRevert())
                    {
                        return;
                    }

                    ApplyNetworkSettings(NetworkAdapterIpTextBox.Text, GetNetworkAdapterSubnetMask(), true);
                };
            }
            if (NetworkAdapterSubnetTextBox != null)
            {
                NetworkAdapterSubnetTextBox.Watermark = string.Empty;
                NetworkAdapterSubnetTextBox.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingNetworkUi || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForNetworkOrRevert())
                    {
                        return;
                    }

                    ApplyNetworkSettings(GetNetworkAdapterIpAddress(), NetworkAdapterSubnetTextBox.Text, true);
                };
            }

            if (OpenNetworkAdapterPickerButton != null)
            {
                OpenNetworkAdapterPickerButton.Content = "加载中...";
                OpenNetworkAdapterPickerButton.IsEnabled = false;
                ToolTip.SetTip(OpenNetworkAdapterPickerButton, "正在读取网卡配置...");
            }

            if (GameDirectoryOverrideTextBox != null)
            {
                GameDirectoryOverrideTextBox.Watermark = "contents";
                GameDirectoryOverrideTextBox.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isSyncingModel) return;
                    _paths.SetContentsDirectoryOverride(GameDirectoryOverrideTextBox.Text);
                    _viewModel.Settings.GameDirectoryOverride = _paths.ContentsDirectoryOverride;
                    SaveSettings();
                    RefreshPathOverrideDependentUi();
                };
            }
            if (AsphyxiaDirectoryOverrideTextBox != null)
            {
                AsphyxiaDirectoryOverrideTextBox.Watermark = "asphyxia";
                AsphyxiaDirectoryOverrideTextBox.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isSyncingModel) return;
                    _paths.SetAsphyxiaDirectoryOverride(AsphyxiaDirectoryOverrideTextBox.Text);
                    _viewModel.Settings.AsphyxiaDirectoryOverride = _paths.AsphyxiaDirectoryOverride;
                    SaveSettings();
                    RefreshPathOverrideDependentUi();
                };
            }
        }

        private void InitializeStartupSettingsBindings()
        {
            if (PortableModeToggleSwitch != null)
            {
                PortableModeToggleSwitch.IsCheckedChanged -= OnPortableModeToggleChanged;
                PortableModeToggleSwitch.IsCheckedChanged += OnPortableModeToggleChanged;
            }

            if (WindowedToggleSwitch != null)
            {
                WindowedToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(WindowedToggleSwitch, null))
                    {
                        return;
                    }

                    UpdateSpiceConfig(new SpiceOptionUpdate("w", WindowedToggleSwitch.IsChecked == true ? "/ENABLED" : string.Empty));
                };
            }
            if (NoAsphyxiaToggleSwitch != null)
            {
                NoAsphyxiaToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    _viewModel.Settings.NoAsphyxia = NoAsphyxiaToggleSwitch.IsChecked == true;
                    SaveSettings();
                };
            }
            if (ExitRestoreToggleSwitch != null)
            {
                ExitRestoreToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    _viewModel.Settings.ExitRestore = ExitRestoreToggleSwitch.IsChecked == true;
                    SaveSettings();
                };
            }
        }

        private void InitializeSpiceSettingsBindings()
        {
            if (NetDumpToggleSwitch != null)
            {
                NetDumpToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(NetDumpToggleSwitch, () => _dbgNetDump = false))
                    {
                        return;
                    }

                    _dbgNetDump = NetDumpToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new SpiceOptionUpdate("netdump", _dbgNetDump ? "/ENABLED" : string.Empty));
                };
            }
            if (DisableSubDisplayToggleSwitch != null)
            {
                DisableSubDisplayToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(DisableSubDisplayToggleSwitch, () => _disableSubDisplay = false))
                    {
                        return;
                    }

                    _disableSubDisplay = DisableSubDisplayToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new SpiceOptionUpdate("sp2x-sdvxnosub", _disableSubDisplay ? "/ENABLED" : string.Empty));
                };
            }
            if (WindowModeComboBox != null)
            {
                WindowModeComboBox.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    _windowModeIndex = WindowModeComboBox.SelectedIndex < 0 ? 0 : WindowModeComboBox.SelectedIndex;
                    UpdateSpiceConfig(new SpiceOptionUpdate("sp2x-windowborder", ResolveWindowBorderValue()));
                };
            }
            if (PCoreOptimizationToggleSwitch != null)
            {
                PCoreOptimizationToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(PCoreOptimizationToggleSwitch, () => _pCoreOptimization = false))
                    {
                        return;
                    }

                    _pCoreOptimization = PCoreOptimizationToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new SpiceOptionUpdate("sp2x-processefficiency", _pCoreOptimization ? "pcores" : string.Empty));
                };
            }
            if (SubBorderlessToggleSwitch != null)
            {
                SubBorderlessToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(SubBorderlessToggleSwitch, () => _subBorderless = false))
                    {
                        return;
                    }

                    _subBorderless = SubBorderlessToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new SpiceOptionUpdate("sdvxwsubborderless", _subBorderless ? "/ENABLED" : string.Empty));
                };
            }
            if (ShowCursorTouchSimToggleSwitch != null)
            {
                ShowCursorTouchSimToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(ShowCursorTouchSimToggleSwitch, () => _showCursorTouchSim = false))
                    {
                        return;
                    }

                    _showCursorTouchSim = ShowCursorTouchSimToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new SpiceOptionUpdate("s", _showCursorTouchSim ? "/ENABLED" : string.Empty));
                };
            }
            if (WindowTopMostToggleSwitch != null)
            {
                WindowTopMostToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(WindowTopMostToggleSwitch, () => _windowTopMost = false))
                    {
                        return;
                    }

                    _windowTopMost = WindowTopMostToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new SpiceOptionUpdate("sp2x-windowalwaysontop", _windowTopMost ? "/ENABLED" : string.Empty));
                };
            }
            if (SingleAdapterToggleSwitch != null)
            {
                SingleAdapterToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(SingleAdapterToggleSwitch, () => _singleAdapter = false))
                    {
                        return;
                    }

                    _singleAdapter = SingleAdapterToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new SpiceOptionUpdate("graphics-force-single-adapter", _singleAdapter ? "/ENABLED" : string.Empty));
                };
            }
            if (SubWindowTopMostToggleSwitch != null)
            {
                SubWindowTopMostToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(SubWindowTopMostToggleSwitch, () => _subWindowTopMost = false))
                    {
                        return;
                    }

                    _subWindowTopMost = SubWindowTopMostToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new SpiceOptionUpdate("sdvxwsubtop", _subWindowTopMost ? "/ENABLED" : string.Empty));
                };
            }
            if (SubForceRenderToggleSwitch != null)
            {
                SubForceRenderToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(SubForceRenderToggleSwitch, () => _subForceRender = false))
                    {
                        return;
                    }

                    _subForceRender = SubForceRenderToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new SpiceOptionUpdate("sp2x-sdvxsubredraw", _subForceRender ? "/ENABLED" : string.Empty));
                };
            }
            if (NativeTouchToggleSwitch != null)
            {
                NativeTouchToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(NativeTouchToggleSwitch, () => _nativeTouch = false))
                    {
                        return;
                    }

                    _nativeTouch = NativeTouchToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new SpiceOptionUpdate("sdvxnativetouch", _nativeTouch ? "/ENABLED" : string.Empty));
                };
            }
            if (CardIoToggleSwitch != null)
            {
                CardIoToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(CardIoToggleSwitch, () => _cardIo = false))
                    {
                        return;
                    }

                    _cardIo = CardIoToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new SpiceOptionUpdate("cardio", _cardIo ? "/ENABLED" : string.Empty));
                };
            }
            if (HidSmartCardToggleSwitch != null)
            {
                HidSmartCardToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(HidSmartCardToggleSwitch, () => _hidSmartCard = false))
                    {
                        return;
                    }

                    _hidSmartCard = HidSmartCardToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new SpiceOptionUpdate("scard", _hidSmartCard ? "/ENABLED" : string.Empty));
                };
            }
            if (WindowSizeTextBox != null)
            {
                WindowSizeTextBox.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForTextOrRevert(WindowSizeTextBox, "sp2x-windowsize"))
                    {
                        return;
                    }

                    _windowSize = WindowSizeTextBox.Text ?? string.Empty;
                    UpdateSpiceConfig(new SpiceOptionUpdate("sp2x-windowsize", _windowSize));
                };
            }
            if (AsioDriverComboBox != null)
            {
                RefreshAsioDriverChoices(_asioDriver);

                AsioDriverComboBox.DropDownOpened += (s, e) =>
                {
                    RefreshAsioDriverChoices(GetSelectedAsioDriverValue());
                };

                AsioDriverComboBox.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi || _isUpdatingAsioDriverUi) return;
                    if (!EnsureSpiceXmlExistsForAsioOrRevert())
                    {
                        return;
                    }

                    _asioDriver = GetSelectedAsioDriverValue();
                    UpdateAsioControlPanelButtonState();
                    UpdateSpiceConfig(new SpiceOptionUpdate("sp2x-sdvxasio", _asioDriver));
                };
            }
            if (LowLatencySharedAudioToggleSwitch != null)
            {
                LowLatencySharedAudioToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(LowLatencySharedAudioToggleSwitch, () => _lowLatencySharedAudio = false))
                    {
                        return;
                    }

                    _lowLatencySharedAudio = LowLatencySharedAudioToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new SpiceOptionUpdate("sp2x-lowlatencysharedaudio", _lowLatencySharedAudio ? "/ENABLED" : string.Empty));
                };
            }
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
                    ApplyServerPresetViewModelStateToUi();
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
                    ApplyServerPresetViewModelStateToUi();
                };
            }
        }

        private void FinalizeInitialViewState()
        {
            UpdateStatusText("就绪");
            UpdateStatusProgress(false);

            Closing += OnWindowClosing;
            UpdateCompatLayerStatus();
        }

        private string GetAsphyxiaPath()
        {
            return _paths.GetAsphyxiaPath();
        }

        private string GetSpicePath()
        {
            return _paths.GetSpicePath();
        }

        private string GetSpiceXmlPath()
        {
            return _paths.GetSpiceXmlPath();
        }

        private string GetConfigTomlPath()
        {
            return _paths.ConfigFilePath;
        }

        private string GetApplicationDirectoryPath()
        {
            return _paths.ApplicationDirectoryPath;
        }

        private string GetBundledLibsDirectoryPath()
        {
            return _paths.GetBundledLibsDirectoryPath();
        }

        private string GetBundledSevenZipExecutablePath()
        {
            return _paths.GetBundledSevenZipExecutablePath();
        }

        private string GetContentsDirectoryPath()
        {
            return _paths.GetContentsDirectoryPath();
        }

        private string GetAsphyxiaDirectoryPath()
        {
            return _paths.GetAsphyxiaDirectoryPath();
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

        private void ShowInfoToast(string title, string content)
        {
            _toastManager.CreateToast()
                .WithTitle(title)
                .WithContent(content)
                .OfType(NotificationType.Information)
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Dismiss().ByClicking()
                .Queue();
        }

        private void ShowErrorToast(string title, string content)
        {
            _toastManager.CreateToast()
                .WithTitle(title)
                .WithContent(content)
                .OfType(NotificationType.Error)
                .Dismiss().After(TimeSpan.FromSeconds(4))
                .Dismiss().ByClicking()
                .Queue();
        }

        private void ShowWarningToast(string title, string content)
        {
            _toastManager.CreateToast()
                .WithTitle(title)
                .WithContent(content)
                .OfType(NotificationType.Warning)
                .Dismiss().After(TimeSpan.FromSeconds(4))
                .Dismiss().ByClicking()
                .Queue();
        }

        private static void ApplyDialogNotificationIcon(SukiDialogBuilder builder, NotificationType type)
        {
            if (builder?.Dialog == null)
            {
                return;
            }

            var iconBitmap = type switch
            {
                NotificationType.Warning => _warningDialogIconCache ??= TryLoadDialogNotificationBitmap("warning.png"),
                NotificationType.Error => _errorDialogIconCache ??= TryLoadDialogNotificationBitmap("error.png"),
                _ => null
            };

            if (iconBitmap == null)
            {
                return;
            }

            builder.Dialog.Icon = iconBitmap;
            builder.Dialog.IconColor = null;
        }

        private static Bitmap TryLoadDialogNotificationBitmap(string assetFileName)
        {
            try
            {
                var assetUri = new Uri($"avares://LazyBootstrap/Assets/Images/{assetFileName}");
                var stream = AssetLoader.Open(assetUri);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }
    }
}
