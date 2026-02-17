// written by Arkito aka Takanashi Ryo, only release in SDVX Lazy Pack.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
        private readonly ServerPresetStore _serverPresetStore;
        private bool _portableMode = false; // 是否使用便携模式
        private bool _isLoadingSettings = false; // 标记是否正在加载设置

        // 统一路径前缀
        private readonly string _baseDir;
        private readonly string _contentsDir;

        private string _compatTypeTooltipCache;

        private static Bitmap _warningDialogIconCache;
        private static Bitmap _errorDialogIconCache;

        private bool _advDisableSubDisplay = false;
        private int _advWindowModeIndex = 0; // 0: 默认, 1: 无边框, 2: 可变窗口
        private bool _advSubBorderless = false;
        private bool _advShowCursorTouchSim = false;
        private bool _advPCoreOptimization = false;
        private bool _advWindowTopMost = false;
        private string _advWindowSize = string.Empty;
        private bool _advSingleAdapter = false;
        private bool _advSubWindowTopMost = false;
        private bool _advSubForceRender = false;
        private bool _advNativeTouch = false;
        private string _advAsioDriver = string.Empty;
        private bool _advCardIo = false;
        private bool _advHidSmartCard = false;

        private bool _dbgNetDump = false;
        private bool _dbgAsphyxiaDebug = false;
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
            _serverPresetStore = new ServerPresetStore(configFilePath);

            _isLoadingSettings = true;
            InitializeCustomComponents();
            HideLaunchLogArea(true);
            LoadSettings();

            // 窗口显示后执行初始化流程
            this.Opened += async (s, e) =>
            {
                await RunEnvironmentScanAsync();
                LoadSpiceConfig();
            };
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
            if (EditConfigButton != null)
            {
                EditConfigButton.Click += OnEditConfigClick;
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
                ServerAddressTextBox.Watermark = "http://SERVERURL:PORT";
            }
            if (PcbIdTextBox != null)
            {
                PcbIdTextBox.Watermark = string.Empty;
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
            if (NoRestoreRotationToggleSwitch != null)
            {
                NoRestoreRotationToggleSwitch.IsCheckedChanged += (s, e) => { SaveSettings(); };
            }

            // 高级选项（主窗口内控件）
            if (AdvNetDumpToggleSwitch != null)
            {
                AdvNetDumpToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(AdvNetDumpToggleSwitch, () => _dbgNetDump = false))
                    {
                        return;
                    }

                    _dbgNetDump = AdvNetDumpToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("netdump", _dbgNetDump ? "/ENABLED" : string.Empty));
                };
            }
            if (AdvAsphyxiaDebugToggleSwitch != null)
            {
                AdvAsphyxiaDebugToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    _dbgAsphyxiaDebug = AdvAsphyxiaDebugToggleSwitch.IsChecked == true;
                };
            }
            if (AdvDisableSubDisplayToggleSwitch != null)
            {
                AdvDisableSubDisplayToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(AdvDisableSubDisplayToggleSwitch, () => _advDisableSubDisplay = false))
                    {
                        return;
                    }

                    _advDisableSubDisplay = AdvDisableSubDisplayToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-sdvxnosub", _advDisableSubDisplay ? "/ENABLED" : string.Empty));
                };
            }
            if (AdvWindowModeComboBox != null)
            {
                AdvWindowModeComboBox.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    _advWindowModeIndex = AdvWindowModeComboBox.SelectedIndex < 0 ? 0 : AdvWindowModeComboBox.SelectedIndex;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-windowborder", ResolveWindowBorderValue()));
                };
            }
            if (AdvPCoreOptimizationToggleSwitch != null)
            {
                AdvPCoreOptimizationToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(AdvPCoreOptimizationToggleSwitch, () => _advPCoreOptimization = false))
                    {
                        return;
                    }

                    _advPCoreOptimization = AdvPCoreOptimizationToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-processefficiency", _advPCoreOptimization ? "pcores" : string.Empty));
                };
            }
            if (AdvSubBorderlessToggleSwitch != null)
            {
                AdvSubBorderlessToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(AdvSubBorderlessToggleSwitch, () => _advSubBorderless = false))
                    {
                        return;
                    }

                    _advSubBorderless = AdvSubBorderlessToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sdvxwsubborderless", _advSubBorderless ? "/ENABLED" : string.Empty));
                };
            }
            if (AdvShowCursorTouchSimToggleSwitch != null)
            {
                AdvShowCursorTouchSimToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(AdvShowCursorTouchSimToggleSwitch, () => _advShowCursorTouchSim = false))
                    {
                        return;
                    }

                    _advShowCursorTouchSim = AdvShowCursorTouchSimToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("s", _advShowCursorTouchSim ? "/ENABLED" : string.Empty));
                };
            }

            if (AdvWindowTopMostToggleSwitch != null)
            {
                AdvWindowTopMostToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(AdvWindowTopMostToggleSwitch, () => _advWindowTopMost = false))
                    {
                        return;
                    }

                    _advWindowTopMost = AdvWindowTopMostToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-windowalwaysontop", _advWindowTopMost ? "/ENABLED" : string.Empty));
                };
            }

            if (AdvSingleAdapterToggleSwitch != null)
            {
                AdvSingleAdapterToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(AdvSingleAdapterToggleSwitch, () => _advSingleAdapter = false))
                    {
                        return;
                    }

                    _advSingleAdapter = AdvSingleAdapterToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("graphics-force-single-adapter", _advSingleAdapter ? "/ENABLED" : string.Empty));
                };
            }

            if (AdvSubWindowTopMostToggleSwitch != null)
            {
                AdvSubWindowTopMostToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(AdvSubWindowTopMostToggleSwitch, () => _advSubWindowTopMost = false))
                    {
                        return;
                    }

                    _advSubWindowTopMost = AdvSubWindowTopMostToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sdvxwsubtop", _advSubWindowTopMost ? "/ENABLED" : string.Empty));
                };
            }

            if (AdvSubForceRenderToggleSwitch != null)
            {
                AdvSubForceRenderToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(AdvSubForceRenderToggleSwitch, () => _advSubForceRender = false))
                    {
                        return;
                    }

                    _advSubForceRender = AdvSubForceRenderToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-sdvxsubredraw", _advSubForceRender ? "/ENABLED" : string.Empty));
                };
            }

            if (AdvNativeTouchToggleSwitch != null)
            {
                AdvNativeTouchToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(AdvNativeTouchToggleSwitch, () => _advNativeTouch = false))
                    {
                        return;
                    }

                    _advNativeTouch = AdvNativeTouchToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sdvxnativetouch", _advNativeTouch ? "/ENABLED" : string.Empty));
                };
            }

            if (AdvCardIoToggleSwitch != null)
            {
                AdvCardIoToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(AdvCardIoToggleSwitch, () => _advCardIo = false))
                    {
                        return;
                    }

                    _advCardIo = AdvCardIoToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("cardio", _advCardIo ? "/ENABLED" : string.Empty));
                };
            }

            if (AdvHidSmartCardToggleSwitch != null)
            {
                AdvHidSmartCardToggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(AdvHidSmartCardToggleSwitch, () => _advHidSmartCard = false))
                    {
                        return;
                    }

                    _advHidSmartCard = AdvHidSmartCardToggleSwitch.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("scard", _advHidSmartCard ? "/ENABLED" : string.Empty));
                };
            }

            if (AdvWindowSizeTextBox != null)
            {
                AdvWindowSizeTextBox.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForTextOrRevert(AdvWindowSizeTextBox, "sp2x-windowsize"))
                    {
                        return;
                    }

                    _advWindowSize = AdvWindowSizeTextBox.Text ?? string.Empty;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-windowsize", _advWindowSize));
                };
            }

            if (AdvAsioDriverTextBox != null)
            {
                AdvAsioDriverTextBox.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForTextOrRevert(AdvAsioDriverTextBox, "sp2x-sdvxasio"))
                    {
                        return;
                    }

                    _advAsioDriver = AdvAsioDriverTextBox.Text ?? string.Empty;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-sdvxasio", _advAsioDriver));
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
            return Path.Combine(_baseDir, "asphyxia", "asphyxia-core-x64.exe");
        }

        private string GetSpicePath()
        {
            return Path.Combine(_contentsDir, "spice64.exe");
        }

        private string GetSpiceXmlPath()
        {
            if (_portableMode)
            {
                return Path.Combine(_contentsDir, "lazy", "spicetools.xml");
            }

            string appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataDir, "spicetools.xml");
        }

        private string GetConfigTomlPath()
        {
            return Path.Combine(_baseDir, "config.toml");
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
                var assetUri = new Uri($"avares://LazyBootstrap/Assets/{assetFileName}");
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
