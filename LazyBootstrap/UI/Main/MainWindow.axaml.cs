// written by Arkito aka Takanashi Ryo, only release in SDVX Lazy Pack.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using Avalonia;

namespace LazyBootstrap
{
    public partial class MainWindow : SukiWindow
    {
        private Process _gameProcess;
        private readonly ConfigHandler _configFile;
        private bool _portableMode = false; // 是否使用便携模式
        private bool _isLoadingSettings = false; // 标记是否正在加载设置

        // 统一路径前缀
        private readonly string _baseDir;
        private readonly string _contentsDir;
        private string _contentsDirOverride = string.Empty;
        private string _asphyxiaDirOverride = string.Empty;

        private string _compatTypeTooltipCache;

        private static Bitmap _warningDialogIconCache;
        private static Bitmap _errorDialogIconCache;
        private readonly IWindowsDefenderExclusionService _windowsDefenderExclusionService = new WindowsDefenderExclusionService();

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
        private readonly List<DisplayConfigure.DisplayInfo> _displayInfos = new List<DisplayConfigure.DisplayInfo>();
        private readonly Dictionary<string, DisplayConfigure.DisplayState> _displayRestoreStates = new Dictionary<string, DisplayConfigure.DisplayState>(StringComparer.OrdinalIgnoreCase);
        private DisplaySelectionTarget _selectedDisplayTarget = DisplaySelectionTarget.None;
        private DispatcherTimer _displayPulseTimer;
        private double _displayPulsePhase = 0d;
        private static readonly ISukiDialogManager _dialogManager = new SukiDialogManager();
        private readonly ISukiToastManager _toastManager = new SukiToastManager();

        private const string NonePresetName = "无";
        private const string AsphyxiaPresetName = "Asphyxia";
        private const string AsphyxiaDefaultUrl = "http://localhost:8083";
        private const string SettingSectionName = AppConfigBootstrapper.SettingSectionName;
        private const string DisplaySectionName = AppConfigBootstrapper.DisplaySectionName;
        private string _activeServerPreset = NonePresetName;
        private readonly List<ServerPresetItem> _serverPresets = new List<ServerPresetItem>();
        private readonly Dictionary<string, string> _lastKnownSpiceValues = new Dictionary<string, string>(StringComparer.Ordinal);
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
        private const int MaxLaunchLogLines = 1200;
        private readonly StringBuilder _launchLogBuffer = new StringBuilder(16 * 1024);
        private readonly Queue<string> _launchLogLineQueue = new Queue<string>(MaxLaunchLogLines + 64);
        private bool _isUpdatingNetworkUi;

        private sealed class AsioDriverChoice
        {
            public AsioDriverChoice(string displayName, string value)
            {
                DisplayName = displayName ?? string.Empty;
                Value = value ?? string.Empty;
            }

            public string DisplayName { get; }

            public string Value { get; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private sealed class NetworkAdapterChoice
        {
            public NetworkAdapterChoice(string displayName, string ipAddress, string subnetMask)
            {
                DisplayName = displayName ?? string.Empty;
                IpAddress = ipAddress ?? string.Empty;
                SubnetMask = subnetMask ?? string.Empty;
            }

            public string DisplayName { get; }

            public string IpAddress { get; }

            public string SubnetMask { get; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private enum DisplaySelectionTarget
        {
            None,
            Main,
            Sub
        }

        public MainWindow()
        {
            InitializeComponent();

            if (Design.IsDesignMode)
            {
                return;
            }

            if (DialogHost != null)
            {
                DialogHost.Manager = _dialogManager;
            }
            if (ToastHost != null)
            {
                ToastHost.Manager = _toastManager;
            }

            _baseDir = AppPathResolver.ResolveBaseDir();
            _contentsDir = Path.Combine(_baseDir, "contents");

            string configFilePath = Path.Combine(_baseDir, "config.toml");
            _configFile = new ConfigHandler(configFilePath);
            AppConfigBootstrapper.InitializeAndMigrate(configFilePath, _configFile);

            _isLoadingSettings = true;
            InitializeCustomComponents();
            HideLaunchLogArea(true);
            LoadSettings();
            _lastNormalWidth = Width;
            _lastNormalHeight = Height;
            SizeChanged += OnMainWindowSizeChanged;

            // 窗口显示后执行初始化流程
            this.Opened += async (s, e) =>
            {
                await RunEnvironmentScanAsync();
                LoadSpiceConfig();
            };
        }

        private void OnMainWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isApplyingAspectRatio || WindowState != WindowState.Normal)
            {
                return;
            }

            var width = Width;
            var height = Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (_lastNormalWidth <= 0 || _lastNormalHeight <= 0)
            {
                _lastNormalWidth = width;
                _lastNormalHeight = height;
                return;
            }

            var deltaWidth = Math.Abs(width - _lastNormalWidth);
            var deltaHeight = Math.Abs(height - _lastNormalHeight);
            if (deltaWidth < 0.5 && deltaHeight < 0.5)
            {
                return;
            }

            double targetWidth;
            double targetHeight;
            if (deltaWidth >= deltaHeight)
            {
                targetWidth = width;
                targetHeight = targetWidth / MainWindowAspectRatio;
            }
            else
            {
                targetHeight = height;
                targetWidth = targetHeight * MainWindowAspectRatio;
            }

            if (targetWidth < MinWidth)
            {
                targetWidth = MinWidth;
                targetHeight = targetWidth / MainWindowAspectRatio;
            }

            if (targetHeight < MinHeight)
            {
                targetHeight = MinHeight;
                targetWidth = targetHeight * MainWindowAspectRatio;
            }

            _isApplyingAspectRatio = true;
            try
            {
                Width = targetWidth;
                Height = targetHeight;
                _lastNormalWidth = targetWidth;
                _lastNormalHeight = targetHeight;
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

            var choices = new List<AsioDriverChoice>
            {
                new("无", string.Empty)
            };

            foreach (var driverName in AsioDriverRegistry.GetInstalledDriverNames())
            {
                choices.Add(new AsioDriverChoice(driverName, driverName));
            }

            if (!string.IsNullOrWhiteSpace(selectedValue)
                && !choices.Any(choice => string.Equals(choice.Value, selectedValue, StringComparison.OrdinalIgnoreCase)))
            {
                choices.Add(new AsioDriverChoice($"{selectedValue}（当前配置）", selectedValue));
            }

            var targetChoice = choices.FirstOrDefault(choice =>
                string.Equals(choice.Value, selectedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                ?? choices[0];

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
        }

        private string GetSelectedAsioDriverValue()
        {
            return AsioDriverComboBox?.SelectedItem is AsioDriverChoice choice
                ? choice.Value
                : string.Empty;
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
            var choices = new List<NetworkAdapterChoice>
            {
                new("无", string.Empty, string.Empty)
            };

            foreach (var adapter in NetworkAdapterDiscovery.GetAvailableAdapters())
            {
                choices.Add(new NetworkAdapterChoice(adapter.DisplayName, adapter.IpAddress, adapter.SubnetMask));
            }

            if ((!string.IsNullOrWhiteSpace(normalizedIpAddress) || !string.IsNullOrWhiteSpace(normalizedSubnetMask))
                && !choices.Any(choice =>
                    string.Equals(choice.IpAddress, normalizedIpAddress, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(choice.SubnetMask, normalizedSubnetMask, StringComparison.OrdinalIgnoreCase)))
            {
                choices.Add(new NetworkAdapterChoice(
                    $"{normalizedIpAddress} / {normalizedSubnetMask}（当前配置）".Trim(),
                    normalizedIpAddress,
                    normalizedSubnetMask));
            }

            var targetChoice = choices.FirstOrDefault(choice =>
                                   string.Equals(choice.IpAddress, normalizedIpAddress, StringComparison.OrdinalIgnoreCase)
                                   && string.Equals(choice.SubnetMask, normalizedSubnetMask, StringComparison.OrdinalIgnoreCase))
                               ?? choices[0];

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

        private static List<NetworkAdapterChoice> BuildNetworkAdapterChoices(string selectedIpAddress, string selectedSubnetMask)
        {
            var normalizedIpAddress = NormalizeNetworkValue(selectedIpAddress);
            var normalizedSubnetMask = NormalizeNetworkValue(selectedSubnetMask);
            var choices = new List<NetworkAdapterChoice>
            {
                new("无", string.Empty, string.Empty)
            };

            foreach (var adapter in NetworkAdapterDiscovery.GetAvailableAdapters())
            {
                choices.Add(new NetworkAdapterChoice(adapter.DisplayName, adapter.IpAddress, adapter.SubnetMask));
            }

            if ((!string.IsNullOrWhiteSpace(normalizedIpAddress) || !string.IsNullOrWhiteSpace(normalizedSubnetMask))
                && !choices.Any(choice =>
                    string.Equals(choice.IpAddress, normalizedIpAddress, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(choice.SubnetMask, normalizedSubnetMask, StringComparison.OrdinalIgnoreCase)))
            {
                choices.Add(new NetworkAdapterChoice(
                    BuildCurrentNetworkAdapterDisplayName(normalizedIpAddress, normalizedSubnetMask),
                    normalizedIpAddress,
                    normalizedSubnetMask));
            }

            return choices;
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
                new OptionUpdate("network", normalizedIpAddress, false),
                new OptionUpdate("subnet", normalizedSubnetMask, false));
        }

        private async Task OpenNetworkAdapterPickerAsync()
        {
            if (!EnsureSpiceXmlExistsForNetworkOrRevert())
            {
                return;
            }

            var currentIpAddress = GetNetworkAdapterIpAddress();
            var currentSubnetMask = GetNetworkAdapterSubnetMask();
            var choices = BuildNetworkAdapterChoices(currentIpAddress, currentSubnetMask);
            var selectedChoice = choices.FirstOrDefault(choice =>
                                     string.Equals(choice.IpAddress, currentIpAddress, StringComparison.OrdinalIgnoreCase)
                                     && string.Equals(choice.SubnetMask, currentSubnetMask, StringComparison.OrdinalIgnoreCase))
                                 ?? choices[0];

            var adapterListBox = new ListBox
            {
                ItemsSource = choices,
                SelectedItem = selectedChoice,
                MinHeight = 240,
                MaxHeight = 360
            };

            var content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "请选择要读取参数的网卡。" },
                    adapterListBox
                }
            };

            var confirmed = await _dialogManager
                .CreateDialog()
                .WithTitle("选择网卡")
                .WithContent(content)
                .WithYesNoResult("确定", "取消", "Flat")
                .TryShowAsync();
            if (!confirmed)
            {
                return;
            }

            if (adapterListBox.SelectedItem is not NetworkAdapterChoice choice)
            {
                ShowWarningToast("选择网卡", "请选择一个网卡。");
                return;
            }

            ApplyNetworkSettings(choice.IpAddress, choice.SubnetMask, true);
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
            if (ToggleLaunchLogButton != null)
            {
                ToggleLaunchLogButton.Click += OnToggleLaunchLogClick;
            }
            if (GotoGameSettingsButton != null)
            {
                GotoGameSettingsButton.Click += OnGoToGameSettingsClick;
            }
            if (OpenLogButton != null)
            {
                OpenLogButton.Click += OnOpenLogClick;
            }
            if (KillProcessesButton != null)
            {
                KillProcessesButton.Click += OnKillProcessesClick;
            }
            if (StartButton != null)
            {
                StartButton.Click += OnStartButtonClick;
            }
            if (StartAsphyxiaDevMenuItem != null)
            {
                StartAsphyxiaDevMenuItem.Click += OnStartAsphyxiaDevMenuItemClick;
            }
            if (EditConfigButton != null)
            {
                EditConfigButton.Click += OnEditConfigClick;
            }
            if (ImportRecommendedSpiceConfigButton != null)
            {
                ImportRecommendedSpiceConfigButton.Click += OnImportRecommendedSpiceConfigClick;
            }
            if (CompatLayerToggleSwitch != null)
            {
                CompatLayerToggleSwitch.IsCheckedChanged += OnCompatLayerToggleChanged;
            }
            if (CompatDx9on12RadioButton != null)
            {
                CompatDx9on12RadioButton.IsCheckedChanged += OnCompatModeChecked;
            }
            if (CompatDx9on12ExternalRadioButton != null)
            {
                CompatDx9on12ExternalRadioButton.IsCheckedChanged += OnCompatModeChecked;
            }
            if (CompatDxvkRadioButton != null)
            {
                CompatDxvkRadioButton.IsCheckedChanged += OnCompatModeChecked;
            }
            if (AddServerPresetButton != null)
            {
                AddServerPresetButton.Click += OnAddServerPresetClick;
            }
            if (DeleteServerPresetButton != null)
            {
                DeleteServerPresetButton.Click += OnDeleteServerPresetClick;
            }
            if (PortableModeToggleSwitch != null)
            {
                PortableModeToggleSwitch.IsCheckedChanged += OnPortableModeToggleChanged;
            }
            if (LoadCompatButton != null)
            {
                LoadCompatButton.Click += OnLoadCompatLayerClick;
            }
            if (UnloadCompatButton != null)
            {
                UnloadCompatButton.Click += OnUnloadCompatLayerClick;
            }
            if (SelectMainScreenAreaButton != null)
            {
                SelectMainScreenAreaButton.Click += OnSelectMainScreenAreaClick;
            }
            if (SelectSubScreenAreaButton != null)
            {
                SelectSubScreenAreaButton.Click += OnSelectSubScreenAreaClick;
            }
            if (TouchPanelButton != null)
            {
                TouchPanelButton.Click += OnTouchPanelClick;
            }
            if (PreviewDisplaySettingsButton != null)
            {
                PreviewDisplaySettingsButton.Click += OnPreviewDisplaySettingsClick;
            }
            if (ClearCacheButton != null)
            {
                ClearCacheButton.Click += OnClearCacheClick;
            }
            if (AddFirewallRuleButton != null)
            {
                AddFirewallRuleButton.Click += OnAddFirewallRuleClick;
            }
            if (AudioPanelButton != null)
            {
                AudioPanelButton.Click += OnAudioPanelClick;
            }
            if (InstallRuntimeButton != null)
            {
                InstallRuntimeButton.Click += OnInstallRuntimeClick;
            }
            if (SavedataBackupImportButton != null)
            {
                SavedataBackupImportButton.Click += OnSavedataBackupImportClick;
            }
            if (SelectGameDirectoryOverrideButton != null)
            {
                SelectGameDirectoryOverrideButton.Click += OnSelectGameDirectoryOverrideClick;
            }
            if (SelectAsphyxiaDirectoryOverrideButton != null)
            {
                SelectAsphyxiaDirectoryOverrideButton.Click += OnSelectAsphyxiaDirectoryOverrideClick;
            }
            if (OpenNetworkAdapterPickerButton != null)
            {
                OpenNetworkAdapterPickerButton.Click += async (s, e) => await OpenNetworkAdapterPickerAsync();
            }

            if (ServerPresetComboBox != null)
            {
                ServerPresetComboBox.SelectionChanged += OnServerPresetSelectionChanged;
            }

            // 初始化默认值与下拉列表
            if (RotationComboBox != null)
            {
                RotationComboBox.Items.Add("0");
                RotationComboBox.Items.Add("90");
                RotationComboBox.Items.Add("180");
                RotationComboBox.Items.Add("270");
                RotationComboBox.SelectedIndex = 0;
            }

            // 兼容模式默认值
            if (CompatTypeComboBox != null)
            {
                if (CompatTypeComboBox.Items.Count == 0)
                {
                    CompatTypeComboBox.Items.Add("dx9on12");
                    CompatTypeComboBox.Items.Add("dx9on12_external");
                    CompatTypeComboBox.Items.Add("dxvk");
                }
                CompatTypeComboBox.SelectedIndex = 0;

                var tipObj = ToolTip.GetTip(CompatTypeComboBox);
                if (tipObj != null)
                {
                    _compatTypeTooltipCache = tipObj.ToString();
                }

                // 实时更新：兼容模式变更时写回 XML
                CompatTypeComboBox.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-dx9on12", ResolveDxModeValue(), false));
                    SaveSettings();
                };
            }

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
            RefreshNetworkAdapterChoices(GetNetworkAdapterIpAddress(), GetNetworkAdapterSubnetMask());
            if (GameDirectoryOverrideTextBox != null)
            {
                GameDirectoryOverrideTextBox.Watermark = "contents";
                GameDirectoryOverrideTextBox.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    _contentsDirOverride = NormalizeDirectoryOverride(GameDirectoryOverrideTextBox.Text);
                    SaveSettings();
                    RefreshPathOverrideDependentUi();
                };
            }
            if (AsphyxiaDirectoryOverrideTextBox != null)
            {
                AsphyxiaDirectoryOverrideTextBox.Watermark = "asphyxia";
                AsphyxiaDirectoryOverrideTextBox.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    _asphyxiaDirOverride = NormalizeDirectoryOverride(AsphyxiaDirectoryOverrideTextBox.Text);
                    SaveSettings();
                    RefreshPathOverrideDependentUi();
                };
            }

            // 选项实时更新（窗口页）
            if (WindowedToggleSwitch != null)
            {
                WindowedToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(WindowedToggleSwitch, null))
                    {
                        return;
                    }

                    UpdateSpiceConfig(new OptionUpdate("w", WindowedToggleSwitch.IsChecked == true ? "/ENABLED" : string.Empty));
                };
            }
            if (NoAsphyxiaToggleSwitch != null)
            {
                NoAsphyxiaToggleSwitch.IsCheckedChanged += (s, e) => { SaveSettings(); };
            }
            if (ExitRestoreToggleSwitch != null)
            {
                ExitRestoreToggleSwitch.IsCheckedChanged += (s, e) => { SaveSettings(); };
            }

            // 高级选项（主窗口内控件）
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
                    UpdateSpiceConfig(new OptionUpdate("netdump", _dbgNetDump ? "/ENABLED" : string.Empty));
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
                    UpdateSpiceConfig(new OptionUpdate("sp2x-sdvxnosub", _disableSubDisplay ? "/ENABLED" : string.Empty));
                };
            }
            if (WindowModeComboBox != null)
            {
                WindowModeComboBox.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    _windowModeIndex = WindowModeComboBox.SelectedIndex < 0 ? 0 : WindowModeComboBox.SelectedIndex;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-windowborder", ResolveWindowBorderValue()));
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
                    UpdateSpiceConfig(new OptionUpdate("sp2x-processefficiency", _pCoreOptimization ? "pcores" : string.Empty));
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
                    UpdateSpiceConfig(new OptionUpdate("sdvxwsubborderless", _subBorderless ? "/ENABLED" : string.Empty));
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
                    UpdateSpiceConfig(new OptionUpdate("s", _showCursorTouchSim ? "/ENABLED" : string.Empty));
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
                    UpdateSpiceConfig(new OptionUpdate("sp2x-windowalwaysontop", _windowTopMost ? "/ENABLED" : string.Empty));
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
                    UpdateSpiceConfig(new OptionUpdate("graphics-force-single-adapter", _singleAdapter ? "/ENABLED" : string.Empty));
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
                    UpdateSpiceConfig(new OptionUpdate("sdvxwsubtop", _subWindowTopMost ? "/ENABLED" : string.Empty));
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
                    UpdateSpiceConfig(new OptionUpdate("sp2x-sdvxsubredraw", _subForceRender ? "/ENABLED" : string.Empty));
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
                    UpdateSpiceConfig(new OptionUpdate("sdvxnativetouch", _nativeTouch ? "/ENABLED" : string.Empty));
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
                    UpdateSpiceConfig(new OptionUpdate("cardio", _cardIo ? "/ENABLED" : string.Empty));
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
                    UpdateSpiceConfig(new OptionUpdate("scard", _hidSmartCard ? "/ENABLED" : string.Empty));
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
                    UpdateSpiceConfig(new OptionUpdate("sp2x-windowsize", _windowSize));
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
                    UpdateSpiceConfig(new OptionUpdate("sp2x-sdvxasio", _asioDriver));
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
                    UpdateSpiceConfig(new OptionUpdate("sp2x-lowlatencysharedaudio", _lowLatencySharedAudio ? "/ENABLED" : string.Empty));
                };
            }

            // 使用预配置文件
            // 服务器设定：变更后同步到游戏设定页面
            if (ServerAddressTextBox != null)
            {
                ServerAddressTextBox.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    UpdateSpiceConfig(new OptionUpdate("url", ServerAddressTextBox.Text ?? string.Empty, false));
                    SelectPresetByCurrentFields();
                    SaveServerPresetsToConfig();
                };
            }
            if (PcbIdTextBox != null)
            {
                PcbIdTextBox.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    UpdateSpiceConfig(new OptionUpdate("p", PcbIdTextBox.Text ?? string.Empty, false));
                    SelectPresetByCurrentFields();
                    SaveServerPresetsToConfig();
                };
            }

            InitializeDisplayLayoutControls();

            if (StatusLabel != null)
            {
                StatusLabel.Text = "就绪";
            }
            this.Closing += OnWindowClosing;

            UpdateCompatLayerStatus();
        }

        private string GetAsphyxiaPath()
        {
            return Path.Combine(GetAsphyxiaDirectoryPath(), "asphyxia-core-x64.exe");
        }

        private string GetSpicePath()
        {
            return Path.Combine(GetContentsDirectoryPath(), "spice64.exe");
        }

        private string GetSpiceXmlPath()
        {
            if (_portableMode)
            {
                return Path.Combine(GetContentsDirectoryPath(), "lazy", "spicetools.xml");
            }

            string appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataDir, "spicetools.xml");
        }

        private string GetConfigTomlPath()
        {
            return Path.Combine(_baseDir, "config.toml");
        }

        private string GetApplicationDirectoryPath()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private string GetBundledLibsDirectoryPath()
        {
            return Path.Combine(GetApplicationDirectoryPath(), "libs");
        }

        private string GetBundledSevenZipExecutablePath()
        {
            return Path.Combine(GetApplicationDirectoryPath(), "7za.exe");
        }

        private string GetContentsDirectoryPath()
        {
            return string.IsNullOrWhiteSpace(_contentsDirOverride) ? _contentsDir : _contentsDirOverride;
        }

        private string GetAsphyxiaDirectoryPath()
        {
            return string.IsNullOrWhiteSpace(_asphyxiaDirOverride)
                ? Path.Combine(_baseDir, "asphyxia")
                : _asphyxiaDirOverride;
        }

        private static string NormalizeDirectoryOverride(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path.Trim();
            }
        }

        private async Task PickDirectoryOverrideAsync(TextBox targetTextBox, string title)
        {
            if (targetTextBox == null || StorageProvider == null)
            {
                return;
            }

            var selectedFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

            if (selectedFolders == null || selectedFolders.Count == 0)
            {
                return;
            }

            var selectedPath = selectedFolders[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                ShowErrorToast("选择文件夹失败", "当前选择的文件夹不可直接访问，请选择本地磁盘目录。");
                return;
            }

            targetTextBox.Text = NormalizeDirectoryOverride(selectedPath);
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
