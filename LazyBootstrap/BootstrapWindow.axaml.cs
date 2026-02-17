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
using Avalonia.Threading;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.MessageBox;
using SukiUI.Toasts;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Avalonia;

namespace LazyBootstrap
{
    public partial class BootstrapWindow : SukiWindow
    {
        private Process _gameProcess;
        private readonly ConfigHandler _configFile;
        private bool _usePreconfig = false; // 是否使用预配置文件
        private bool _isLoadingSettings = false; // 标志：是否正在加载设置

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
        private const string SettingSectionName = "Setting";
        private const string LegacySettingsSectionName = "Settings";
        private const string DisplaySectionName = "Display";
        private string _activeServerPreset = NonePresetName;
        private readonly List<ServerPresetItem> _serverPresets = new List<ServerPresetItem>();
        private readonly Dictionary<string, string> _lastKnownSpiceValues = new Dictionary<string, string>(StringComparer.Ordinal);
        private bool _isSettingsBusy;
        private bool _isSyncingModel;
        private bool _isUpdatingCompatUi;
        private bool _isUpdatingPreconfigUi;
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

        private sealed class ServerPresetItem
        {
            public string Name { get; set; } = string.Empty;
            public string ServerUrl { get; set; } = string.Empty;
            public string PcbId { get; set; } = string.Empty;

            public override string ToString() => Name;
        }

        public BootstrapWindow()
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

            // 优先使用启动器参数/环境变量传递的根目录，否则使用当前程序所在目录
            string argBaseDir = null;
            try
            {
                foreach (var arg in Environment.GetCommandLineArgs())
                {
                    if (arg.StartsWith("--basedir=", StringComparison.OrdinalIgnoreCase))
                    {
                        argBaseDir = arg.Substring("--basedir=".Length).Trim('"');
                        break;
                    }
                }
            }
            catch { }

            var envBaseDir = Environment.GetEnvironmentVariable("LAZYBOOTSTRAP_BASEDIR");
            _baseDir = !string.IsNullOrEmpty(argBaseDir)
                ? argBaseDir
                : (!string.IsNullOrEmpty(envBaseDir) ? envBaseDir : AppDomain.CurrentDomain.BaseDirectory);
            _contentsDir = Path.Combine(_baseDir, "contents");

            string configFilePath = Path.Combine(_baseDir, "config.toml");
            bool newConfigCreated = !System.IO.File.Exists(configFilePath);
            if (newConfigCreated)
            {
                WriteInitialConfigToml(configFilePath);
            }

            _configFile = new ConfigHandler(configFilePath);
            EnsureConfigSchema();

            _isLoadingSettings = true;
            InitializeCustomComponents();
            HideLaunchLogArea(true);
            LoadSettings();

            // 窗体显示后进行环境检测
            this.Opened += async (s, e) =>
            {
                await RunEnvironmentScanAsync();
                LoadSpiceConfig();
            };
        }

        private async void btnAdvancedOptions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new AdvancedOptionsWindow
                {
                    NetDumpEnabled = _dbgNetDump,
                    AsphyxiaDebugEnabled = _dbgAsphyxiaDebug,
                    PCoreOptimizationEnabled = _advPCoreOptimization,
                    DisableSubDisplay = _advDisableSubDisplay,
                    WindowModeIndex = _advWindowModeIndex,
                    SubBorderless = _advSubBorderless,
                    ShowCursorAndTouchSim = _advShowCursorTouchSim
                };
                await dlg.ShowDialog(this);
                if (dlg.Confirmed)
                {
                    // 读取对话框选择并缓存
                    _dbgNetDump = dlg.NetDumpEnabled;
                    _dbgAsphyxiaDebug = dlg.AsphyxiaDebugEnabled;

                    // 缓存高级选项选择
                    _advPCoreOptimization = dlg.PCoreOptimizationEnabled;
                    _advDisableSubDisplay = dlg.DisableSubDisplay;
                    _advWindowModeIndex = dlg.WindowModeIndex;
                    _advSubBorderless = dlg.SubBorderless;
                    _advShowCursorTouchSim = dlg.ShowCursorAndTouchSim;

                    bool prev = _isLoadingSettings;
                    _isLoadingSettings = true;
                    try
                    {
                        if (chkAdvNetDump != null) chkAdvNetDump.IsChecked = _dbgNetDump;
                        if (chkAdvAsphyxiaDebug != null) chkAdvAsphyxiaDebug.IsChecked = _dbgAsphyxiaDebug;
                        if (chkAdvDisableSubDisplay != null) chkAdvDisableSubDisplay.IsChecked = _advDisableSubDisplay;
                        if (cmbAdvWindowMode != null) cmbAdvWindowMode.SelectedIndex = _advWindowModeIndex;
                        if (chkAdvPCoreOptimization != null) chkAdvPCoreOptimization.IsChecked = _advPCoreOptimization;
                        if (chkAdvSubBorderless != null) chkAdvSubBorderless.IsChecked = _advSubBorderless;
                        if (chkAdvShowCursorTouchSim != null) chkAdvShowCursorTouchSim.IsChecked = _advShowCursorTouchSim;
                    }
                    finally
                    {
                        _isLoadingSettings = prev;
                    }

                    // 立即更新 XML 配置，仅修改对应的 Option
                    UpdateSpiceConfig(
                        new OptionUpdate("sp2x-processefficiency", _advPCoreOptimization ? "pcores" : string.Empty),
                        new OptionUpdate("sp2x-sdvxnosub", _advDisableSubDisplay ? "/ENABLED" : string.Empty),
                        new OptionUpdate("sp2x-windowborder", ResolveWindowBorderValue()),
                        new OptionUpdate("sdvxwsubborderless", _advSubBorderless ? "/ENABLED" : string.Empty),
                        new OptionUpdate("s", _advShowCursorTouchSim ? "/ENABLED" : string.Empty),
                        new OptionUpdate("netdump", _dbgNetDump ? "/ENABLED" : string.Empty)
                    );
                }
            }
            catch { }
        }

        private async void btnManageServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new ServerManagementWindow();
                await dlg.ShowDialog(this);
                if (dlg.Confirmed)
                {
                    ShowInfoToast("服务器配置", "服务器配置已更新。");
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast("打开服务器管理失败", ex.Message);
            }
        }

        private void InitializeCustomComponents()
        {
            if (cmbServerPreset != null)
            {
                cmbServerPreset.SelectionChanged += cmbServerPreset_SelectionChanged;
            }

            // 设置默认值和下拉列表项
            if (cmbRotation != null)
            {
                cmbRotation.Items.Add("0");
                cmbRotation.Items.Add("90");
                cmbRotation.Items.Add("180");
                cmbRotation.Items.Add("270");
                cmbRotation.SelectedIndex = 0;
            }

            // 兼容层类型默认值
            if (cmbCompatType != null)
            {
                if (cmbCompatType.Items.Count == 0)
                {
                    cmbCompatType.Items.Add("dx9on12");
                    cmbCompatType.Items.Add("dx9on12_external");
                    cmbCompatType.Items.Add("dxvk");
                }
                cmbCompatType.SelectedIndex = 0;

                var tipObj = ToolTip.GetTip(cmbCompatType);
                if (tipObj != null)
                {
                    _compatTypeTooltipCache = tipObj.ToString();
                }

                // 实时更新：兼容层选择改变时写入 XML
                cmbCompatType.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-dx9on12", ResolveDxModeValue(), false));
                    SaveSettings();
                };
            }

            if (txtServerAddress != null)
            {
                txtServerAddress.Watermark = "http://SERVERURL:PORT";
            }
            if (txtPcbId != null)
            {
                txtPcbId.Watermark = string.Empty;
            }

            // 勾选项实时更新：窗口化
            if (chkWindowed != null)
            {
                chkWindowed.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(chkWindowed, null))
                    {
                        return;
                    }

                    UpdateSpiceConfig(new OptionUpdate("w", chkWindowed.IsChecked == true ? "/ENABLED" : string.Empty));
                };
            }
            if (chkNoAsphyxia != null)
            {
                chkNoAsphyxia.IsCheckedChanged += (s, e) => { SaveSettings(); };
            }
            if (chkNoRestoreRotation != null)
            {
                chkNoRestoreRotation.IsCheckedChanged += (s, e) => { SaveSettings(); };
            }

            // 高级选项（页面内控件）
            if (chkAdvNetDump != null)
            {
                chkAdvNetDump.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(chkAdvNetDump, () => _dbgNetDump = false))
                    {
                        return;
                    }

                    _dbgNetDump = chkAdvNetDump.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("netdump", _dbgNetDump ? "/ENABLED" : string.Empty));
                };
            }
            if (chkAdvAsphyxiaDebug != null)
            {
                chkAdvAsphyxiaDebug.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    _dbgAsphyxiaDebug = chkAdvAsphyxiaDebug.IsChecked == true;
                };
            }
            if (chkAdvDisableSubDisplay != null)
            {
                chkAdvDisableSubDisplay.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(chkAdvDisableSubDisplay, () => _advDisableSubDisplay = false))
                    {
                        return;
                    }

                    _advDisableSubDisplay = chkAdvDisableSubDisplay.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-sdvxnosub", _advDisableSubDisplay ? "/ENABLED" : string.Empty));
                };
            }
            if (cmbAdvWindowMode != null)
            {
                cmbAdvWindowMode.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    _advWindowModeIndex = cmbAdvWindowMode.SelectedIndex < 0 ? 0 : cmbAdvWindowMode.SelectedIndex;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-windowborder", ResolveWindowBorderValue()));
                };
            }
            if (chkAdvPCoreOptimization != null)
            {
                chkAdvPCoreOptimization.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(chkAdvPCoreOptimization, () => _advPCoreOptimization = false))
                    {
                        return;
                    }

                    _advPCoreOptimization = chkAdvPCoreOptimization.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-processefficiency", _advPCoreOptimization ? "pcores" : string.Empty));
                };
            }
            if (chkAdvSubBorderless != null)
            {
                chkAdvSubBorderless.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(chkAdvSubBorderless, () => _advSubBorderless = false))
                    {
                        return;
                    }

                    _advSubBorderless = chkAdvSubBorderless.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sdvxwsubborderless", _advSubBorderless ? "/ENABLED" : string.Empty));
                };
            }
            if (chkAdvShowCursorTouchSim != null)
            {
                chkAdvShowCursorTouchSim.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(chkAdvShowCursorTouchSim, () => _advShowCursorTouchSim = false))
                    {
                        return;
                    }

                    _advShowCursorTouchSim = chkAdvShowCursorTouchSim.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("s", _advShowCursorTouchSim ? "/ENABLED" : string.Empty));
                };
            }

            if (chkAdvWindowTopMost != null)
            {
                chkAdvWindowTopMost.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(chkAdvWindowTopMost, () => _advWindowTopMost = false))
                    {
                        return;
                    }

                    _advWindowTopMost = chkAdvWindowTopMost.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-windowalwaysontop", _advWindowTopMost ? "/ENABLED" : string.Empty));
                };
            }

            if (chkAdvSingleAdapter != null)
            {
                chkAdvSingleAdapter.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(chkAdvSingleAdapter, () => _advSingleAdapter = false))
                    {
                        return;
                    }

                    _advSingleAdapter = chkAdvSingleAdapter.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("graphics-force-single-adapter", _advSingleAdapter ? "/ENABLED" : string.Empty));
                };
            }

            if (chkAdvSubWindowTopMost != null)
            {
                chkAdvSubWindowTopMost.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(chkAdvSubWindowTopMost, () => _advSubWindowTopMost = false))
                    {
                        return;
                    }

                    _advSubWindowTopMost = chkAdvSubWindowTopMost.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sdvxwsubtop", _advSubWindowTopMost ? "/ENABLED" : string.Empty));
                };
            }

            if (chkAdvSubForceRender != null)
            {
                chkAdvSubForceRender.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(chkAdvSubForceRender, () => _advSubForceRender = false))
                    {
                        return;
                    }

                    _advSubForceRender = chkAdvSubForceRender.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-sdvxsubredraw", _advSubForceRender ? "/ENABLED" : string.Empty));
                };
            }

            if (chkAdvNativeTouch != null)
            {
                chkAdvNativeTouch.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(chkAdvNativeTouch, () => _advNativeTouch = false))
                    {
                        return;
                    }

                    _advNativeTouch = chkAdvNativeTouch.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sdvxnativetouch", _advNativeTouch ? "/ENABLED" : string.Empty));
                };
            }

            if (chkAdvCardIo != null)
            {
                chkAdvCardIo.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(chkAdvCardIo, () => _advCardIo = false))
                    {
                        return;
                    }

                    _advCardIo = chkAdvCardIo.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("cardio", _advCardIo ? "/ENABLED" : string.Empty));
                };
            }

            if (chkAdvHidSmartCard != null)
            {
                chkAdvHidSmartCard.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForToggleOrRevert(chkAdvHidSmartCard, () => _advHidSmartCard = false))
                    {
                        return;
                    }

                    _advHidSmartCard = chkAdvHidSmartCard.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("scard", _advHidSmartCard ? "/ENABLED" : string.Empty));
                };
            }

            if (txtAdvWindowSize != null)
            {
                txtAdvWindowSize.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForTextOrRevert(txtAdvWindowSize, "sp2x-windowsize"))
                    {
                        return;
                    }

                    _advWindowSize = txtAdvWindowSize.Text ?? string.Empty;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-windowsize", _advWindowSize));
                };
            }

            if (txtAdvAsioDriver != null)
            {
                txtAdvAsioDriver.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingSpiceToggleUi) return;
                    if (!EnsureSpiceXmlExistsForTextOrRevert(txtAdvAsioDriver, "sp2x-sdvxasio"))
                    {
                        return;
                    }

                    _advAsioDriver = txtAdvAsioDriver.Text ?? string.Empty;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-sdvxasio", _advAsioDriver));
                };
            }

            // 使用预配置文件
            // 服务器设定（并入游戏设定页）
            if (txtServerAddress != null)
            {
                txtServerAddress.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    UpdateSpiceConfig(new OptionUpdate("url", txtServerAddress.Text ?? string.Empty, false));
                    SelectPresetByCurrentFields();
                    SaveServerPresetsToConfig();
                };
            }
            if (txtPcbId != null)
            {
                txtPcbId.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    UpdateSpiceConfig(new OptionUpdate("p", txtPcbId.Text ?? string.Empty, false));
                    SelectPresetByCurrentFields();
                    SaveServerPresetsToConfig();
                };
            }

            InitializeDisplayLayoutControls();

            if (statusLabel != null)
            {
                statusLabel.Text = "就绪";
            }
            this.Closing += Bootstrap_FormClosing;

            UpdateCompatLayerStatus();
        }

        private void InitializeDisplayLayoutControls()
        {
            _displayInfos.Clear();
            _displayInfos.AddRange(DisplayConfigure.GetDisplays());

            if (cmbMainScreen != null && cmbMainScreen.Items.Count == 0)
            {
                if (_displayInfos.Count > 0)
                {
                    foreach (var display in _displayInfos)
                    {
                        cmbMainScreen.Items.Add(display.FriendlyName);
                        if (cmbSubScreen != null) cmbSubScreen.Items.Add(display.FriendlyName);
                    }
                }
                else
                {
                    cmbMainScreen.Items.Add("主显示器");
                    if (cmbSubScreen != null) cmbSubScreen.Items.Add("副显示器");
                }

                cmbMainScreen.SelectedIndex = 0;
                if (cmbSubScreen != null && cmbSubScreen.Items.Count > 0)
                    cmbSubScreen.SelectedIndex = Math.Min(1, cmbSubScreen.Items.Count - 1);
            }

            InitializeRotationCombo(cmbRotation);
            InitializeRotationCombo(cmbSubRotation);

            if (cmbMainScreen != null)
            {
                cmbMainScreen.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    RefreshMainOptions();
                    UpdateDisplayInfoTexts();
                    SaveSettings();
                };
            }

            if (cmbSubScreen != null)
            {
                cmbSubScreen.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    RefreshSubOptions();
                    UpdateDisplayInfoTexts();
                    SaveSettings();
                };
            }

            if (cmbRotation != null)
            {
                cmbRotation.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    RefreshMainOptions(refreshResolutionList: true, refreshRateList: true);
                    UpdateDisplayInfoTexts();
                    SaveSettings();
                };
            }

            if (cmbSubRotation != null)
            {
                cmbSubRotation.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    RefreshSubOptions(refreshResolutionList: true, refreshRateList: true);
                    UpdateDisplayInfoTexts();
                    SaveSettings();
                };
            }

            if (cmbMainResolution != null)
            {
                cmbMainResolution.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    RefreshMainOptions(refreshResolutionList: false, refreshRateList: true);
                    UpdateDisplayInfoTexts();
                    SaveSettings();
                };
            }

            if (cmbSubResolution != null)
            {
                cmbSubResolution.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    RefreshSubOptions(refreshResolutionList: false, refreshRateList: true);
                    UpdateDisplayInfoTexts();
                    SaveSettings();
                };
            }

            if (cmbMainRefreshRate != null)
            {
                cmbMainRefreshRate.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    UpdateDisplayInfoTexts();
                    SaveSettings();
                };
            }

            if (cmbSubRefreshRate != null)
            {
                cmbSubRefreshRate.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    UpdateDisplayInfoTexts();
                    SaveSettings();
                };
            }

            if (tglDisplayConfigEnabled != null)
            {
                tglDisplayConfigEnabled.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    _displayConfigEnabled = tglDisplayConfigEnabled.IsChecked == true;
                    UpdateDisplayLayoutControlsEnabled();
                    SaveSettings();
                };
            }

            if (cmbDisplayMode != null)
            {
                cmbDisplayMode.SelectionChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    _isDualDisplay = cmbDisplayMode.SelectedIndex != 0;
                    UpdateDisplayLayoutControlsEnabled();
                    UpdateDisplayInfoTexts();
                    SaveSettings();
                };
            }

            RefreshMainOptions();
            RefreshSubOptions();
            SelectDisplayTarget(DisplaySelectionTarget.None);
            UpdateDisplayLayoutControlsEnabled();
            StartDisplayPulseAnimation();
        }

        private static void InitializeRotationCombo(ComboBox combo)
        {
            if (combo == null || combo.Items.Count > 0)
            {
                return;
            }

            combo.Items.Add("0");
            combo.Items.Add("90");
            combo.Items.Add("180");
            combo.Items.Add("270");
            combo.SelectedIndex = 0;
        }

        private void UpdateDisplayLayoutControlsEnabled()
        {
            bool enabled = _displayConfigEnabled;
            if (displayConfigDisabledMask != null)
            {
                displayConfigDisabledMask.IsBusy = !enabled;
            }
            if (cmbMainScreen != null) cmbMainScreen.IsEnabled = enabled;
            if (cmbRotation != null) cmbRotation.IsEnabled = enabled;
            if (cmbMainResolution != null) cmbMainResolution.IsEnabled = enabled;
            if (cmbMainRefreshRate != null) cmbMainRefreshRate.IsEnabled = enabled;
            if (btnPreviewDisplaySettings != null) btnPreviewDisplaySettings.IsEnabled = enabled;
            bool subEnabled = enabled && _isDualDisplay;
            if (btnSelectMainScreenArea != null)
            {
                btnSelectMainScreenArea.IsVisible = enabled;
                btnSelectMainScreenArea.IsEnabled = enabled;
            }
            if (btnSelectSubScreenArea != null)
            {
                btnSelectSubScreenArea.IsVisible = enabled && _isDualDisplay;
                btnSelectSubScreenArea.IsEnabled = subEnabled;
            }
            if (dotSubCore != null) dotSubCore.IsVisible = _isDualDisplay;
            if (dotSubGlow != null) dotSubGlow.IsVisible = _isDualDisplay;
            if (dotSubSelectedRing != null) dotSubSelectedRing.IsVisible = _isDualDisplay && _selectedDisplayTarget == DisplaySelectionTarget.Sub;
            if (cmbSubScreen != null) cmbSubScreen.IsEnabled = subEnabled;
            if (cmbSubRotation != null) cmbSubRotation.IsEnabled = subEnabled;
            if (cmbSubResolution != null) cmbSubResolution.IsEnabled = subEnabled;
            if (cmbSubRefreshRate != null) cmbSubRefreshRate.IsEnabled = subEnabled;

            if (!_isDualDisplay && _selectedDisplayTarget == DisplaySelectionTarget.Sub)
            {
                SelectDisplayTarget(DisplaySelectionTarget.None);
            }
        }

        private void RefreshMainOptions(bool refreshResolutionList = true, bool refreshRateList = true)
        {
            RefreshDisplayOptions(cmbMainScreen, cmbRotation, cmbMainResolution, cmbMainRefreshRate, refreshResolutionList, refreshRateList);
        }

        private void RefreshSubOptions(bool refreshResolutionList = true, bool refreshRateList = true)
        {
            RefreshDisplayOptions(cmbSubScreen, cmbSubRotation, cmbSubResolution, cmbSubRefreshRate, refreshResolutionList, refreshRateList);
        }

        private void RefreshDisplayOptions(ComboBox displayCombo, ComboBox rotationCombo, ComboBox resolutionCombo, ComboBox refreshCombo, bool refreshResolutionList, bool refreshRateList)
        {
            if (displayCombo == null || resolutionCombo == null || refreshCombo == null)
            {
                return;
            }

            var displayInfo = GetSelectedDisplayInfo(displayCombo);
            if (displayInfo == null)
            {
                if (refreshResolutionList)
                {
                    resolutionCombo.Items.Clear();
                }
                if (refreshRateList)
                {
                    refreshCombo.Items.Clear();
                }
                return;
            }

            var supportedModes = DisplayConfigure.GetSupportedModes(displayInfo.DeviceName);
            int rotation = ParseRotationValue(rotationCombo);

            string previousResolution = resolutionCombo.SelectedItem?.ToString() ?? string.Empty;
            if (refreshResolutionList)
            {
                var resolutions = supportedModes
                    .Select(m => NormalizeResolutionByRotation(m.Width, m.Height, rotation))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                resolutionCombo.Items.Clear();
                foreach (var resolution in resolutions)
                {
                    resolutionCombo.Items.Add(resolution);
                }

                if (resolutions.Count > 0)
                {
                    if (!string.IsNullOrEmpty(previousResolution) && resolutions.Contains(previousResolution, StringComparer.OrdinalIgnoreCase))
                    {
                        resolutionCombo.SelectedItem = previousResolution;
                    }
                    else
                    {
                        resolutionCombo.SelectedIndex = 0;
                    }
                }
            }

            if (!refreshRateList)
            {
                return;
            }

            string selectedResolution = resolutionCombo.SelectedItem?.ToString() ?? string.Empty;
            string previousRefresh = refreshCombo.SelectedItem?.ToString() ?? string.Empty;

            var rates = supportedModes
                .Where(m => string.Equals(NormalizeResolutionByRotation(m.Width, m.Height, rotation), selectedResolution, StringComparison.OrdinalIgnoreCase))
                .Select(m => m.RefreshRate)
                .Distinct()
                .OrderBy(v => v)
                .Select(v => v.ToString())
                .ToList();

            refreshCombo.Items.Clear();
            foreach (var rate in rates)
            {
                refreshCombo.Items.Add(rate);
            }

            if (rates.Count > 0)
            {
                if (!string.IsNullOrEmpty(previousRefresh) && rates.Contains(previousRefresh, StringComparer.OrdinalIgnoreCase))
                {
                    refreshCombo.SelectedItem = previousRefresh;
                }
                else
                {
                    refreshCombo.SelectedIndex = 0;
                }
            }
        }

        private static string NormalizeResolutionByRotation(int width, int height, int rotation)
        {
            bool vertical = rotation == 90 || rotation == 270;
            int w = width;
            int h = height;

            if (vertical)
            {
                if (w > h)
                {
                    int temp = w;
                    w = h;
                    h = temp;
                }
                return $"{w}x{h}";
            }

            if (w < h)
            {
                int temp = w;
                w = h;
                h = temp;
            }
            return $"{w}x{h}";
        }

        private void StartDisplayPulseAnimation()
        {
            if (_displayPulseTimer != null)
            {
                return;
            }

            _displayPulseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _displayPulseTimer.Tick += (s, e) =>
            {
                _displayPulsePhase += 0.08;
                if (_displayPulsePhase > Math.PI * 2)
                {
                    _displayPulsePhase = 0;
                }

                double t = (Math.Sin(_displayPulsePhase) + 1d) / 2d;
                ApplyPulseVisual(dotMainGlow, 0.18, 0.58, 1.0, 1.45, t);
                ApplyPulseVisual(dotSubGlow, 0.18, 0.58, 1.0, 1.45, t);
            };
            _displayPulseTimer.Start();
        }

        private static void ApplyPulseVisual(Control control, double minOpacity, double maxOpacity, double minScale, double maxScale, double t)
        {
            if (control == null)
            {
                return;
            }

            control.Opacity = minOpacity + (maxOpacity - minOpacity) * t;
            double scale = minScale + (maxScale - minScale) * t;
            control.RenderTransformOrigin = Avalonia.RelativePoint.Center;
            control.RenderTransform = new ScaleTransform(scale, scale);
        }

        private async void btnPreviewDisplaySettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_displayConfigEnabled)
                {
                    ShowWarningToast("显示器预览", "显示器配置未启用，无法预览。");
                    return;
                }

                SaveSettings();
                var backupStates = CaptureCurrentSelectedDisplayStates();

                bool applied = ApplyDisplaySettingsForLaunch();
                if (!applied)
                {
                    ShowWarningToast("显示器预览", "预览应用存在失败项，请检查当前显示器参数。");
                }

                var result = await ShowPreviewDecisionDialogAsync();
                if (result == PreviewDecision.Restore)
                {
                    int restored = RestoreDisplayStates(backupStates);
                    ShowInfoToast("显示器预览", restored > 0 ? $"已还原 {restored} 个显示器设置。" : "未还原任何显示器设置。");
                    UpdateDisplayInfoTexts();
                    return;
                }

                ShowInfoToast("显示器预览", "已保持当前预览设置。");
            }
            catch (Exception ex)
            {
                ShowErrorToast("显示器预览失败", ex.Message);
            }
        }

        private enum PreviewDecision
        {
            Keep,
            Restore
        }

        private Task<PreviewDecision> ShowPreviewDecisionDialogAsync()
        {
            return ShowPreviewDecisionMessageBoxAsync();
        }

        private async Task<PreviewDecision> ShowPreviewDecisionMessageBoxAsync()
        {
            var restoreButton = SukiMessageBoxButtonsFactory.CreateButton("还原", SukiMessageBoxResult.No, "Flat Accent");
            var keepButton = SukiMessageBoxButtonsFactory.CreateButton("保持现状", SukiMessageBoxResult.Yes, "Flat");

            var result = await SukiMessageBox.ShowDialog(new SukiMessageBoxHost
            {
                UseAlternativeHeaderStyle = true,
                IconPreset = SukiMessageBoxIcons.Question,
                Header = "显示器预览",
                Content = "已应用当前预览设置。\n\n点击“还原”将恢复到预览前状态；点击“保持现状”将不做修改并关闭弹窗。",
                ActionButtonsSource = [restoreButton, keepButton]
            });

            if (result is SukiMessageBoxResult messageBoxResult)
            {
                return messageBoxResult switch
                {
                    SukiMessageBoxResult.No => PreviewDecision.Restore,
                    SukiMessageBoxResult.Yes => PreviewDecision.Keep,
                    _ => PreviewDecision.Keep
                };
            }

            return PreviewDecision.Keep;
        }

        private Dictionary<string, DisplayConfigure.DisplayState> CaptureCurrentSelectedDisplayStates()
        {
            var result = new Dictionary<string, DisplayConfigure.DisplayState>(StringComparer.OrdinalIgnoreCase);

            void Capture(ComboBox combo)
            {
                var info = GetSelectedDisplayInfo(combo);
                if (info == null || result.ContainsKey(info.DeviceName))
                {
                    return;
                }

                if (DisplayConfigure.TryGetCurrentState(info.DeviceName, out var state))
                {
                    result[info.DeviceName] = state;
                }
            }

            Capture(cmbMainScreen);
            if (_isDualDisplay)
            {
                Capture(cmbSubScreen);
            }

            return result;
        }

        private static int RestoreDisplayStates(Dictionary<string, DisplayConfigure.DisplayState> states)
        {
            int restored = 0;
            if (states == null)
            {
                return restored;
            }

            foreach (var state in states.Values)
            {
                if (DisplayConfigure.RestoreDisplaySettings(state))
                {
                    restored++;
                }
            }

            return restored;
        }

        private static int ParseRotationValue(ComboBox combo)
        {
            if (combo == null)
            {
                return 0;
            }

            var selected = combo.SelectedItem?.ToString();
            if (int.TryParse(selected, out var value))
            {
                return value;
            }
            return 0;
        }

        private DisplayConfigure.DisplayInfo GetSelectedDisplayInfo(ComboBox combo)
        {
            if (combo == null)
            {
                return null;
            }

            int idx = combo.SelectedIndex;
            if (idx < 0 || idx >= _displayInfos.Count)
            {
                return null;
            }

            return _displayInfos[idx];
        }

        private void SelectDisplayTarget(DisplaySelectionTarget target)
        {
            if (target == DisplaySelectionTarget.Sub && !_isDualDisplay)
            {
                target = DisplaySelectionTarget.None;
            }

            _selectedDisplayTarget = target;

            if (panelNoScreenSelected != null) panelNoScreenSelected.IsVisible = target == DisplaySelectionTarget.None;
            if (panelMainScreenConfig != null) panelMainScreenConfig.IsVisible = target == DisplaySelectionTarget.Main;
            if (panelSubScreenConfig != null) panelSubScreenConfig.IsVisible = target == DisplaySelectionTarget.Sub;

            if (dotMainSelectedRing != null) dotMainSelectedRing.IsVisible = target == DisplaySelectionTarget.Main;
            if (dotSubSelectedRing != null) dotSubSelectedRing.IsVisible = _isDualDisplay && target == DisplaySelectionTarget.Sub;

            UpdateDisplayInfoTexts();
        }

        private void UpdateDisplayInfoTexts()
        {
            UpdateDisplayInfoText(cmbMainScreen, cmbRotation, cmbMainResolution, cmbMainRefreshRate, txtMainOutputInfo, txtMainStartupInfo);
            UpdateDisplayInfoText(cmbSubScreen, cmbSubRotation, cmbSubResolution, cmbSubRefreshRate, txtSubOutputInfo, txtSubStartupInfo);
        }

        private void UpdateDisplayInfoText(ComboBox displayCombo, ComboBox rotationCombo, ComboBox resolutionCombo, ComboBox refreshCombo, TextBlock outputText, TextBlock startupText)
        {
            if (outputText == null || startupText == null)
            {
                return;
            }

            var info = GetSelectedDisplayInfo(displayCombo);
            if (info == null)
            {
                outputText.Text = "未知";
                startupText.Text = "未配置";
                return;
            }

            if (DisplayConfigure.TryGetCurrentState(info.DeviceName, out var current))
            {
                int currentAngle = DisplayConfigure.OrientationToAngle(current.Orientation);
                outputText.Text = $"设备: {info.FriendlyName} ({info.DeviceName})\n当前: {current.Width}x{current.Height} @ {current.RefreshRate}Hz, 旋转 {currentAngle}°";
            }
            else
            {
                outputText.Text = $"设备: {info.FriendlyName} ({info.DeviceName})\n当前: 未知";
            }

            int startupRotation = ParseRotationValue(rotationCombo);
            string startupResolution = resolutionCombo?.SelectedItem?.ToString() ?? "未设置";
            string startupRefresh = refreshCombo?.SelectedItem?.ToString() ?? "未设置";
            startupText.Text = $"旋转: {startupRotation}°\n分辨率: {startupResolution}\n刷新率: {startupRefresh}Hz";
        }

        private void btnSelectMainScreenArea_Click(object sender, RoutedEventArgs e)
        {
            SelectDisplayTarget(DisplaySelectionTarget.Main);
        }

        private void btnSelectSubScreenArea_Click(object sender, RoutedEventArgs e)
        {
            SelectDisplayTarget(DisplaySelectionTarget.Sub);
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
            if (_usePreconfig)
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
            if (settingsBusyArea != null)
            {
                settingsBusyArea.IsBusy = isBusy;
            }

            if (btnEditConfig != null)
            {
                btnEditConfig.IsEnabled = !isBusy;
                btnEditConfig.Content = isBusy ? "编辑 spicecfg（运行中...）" : "编辑 spicecfg";
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

        private void AppendLaunchOutput(string message, NotificationType type = NotificationType.Information)
        {
            if (txtLogOutput == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            void AppendAction()
            {
                var normalized = message.Replace("\r\n", "\n");
                var lines = normalized.Split('\n');
                foreach (var line in lines)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        txtLogOutput.Text += Environment.NewLine;
                        continue;
                    }

                    string prefix = type switch
                    {
                        NotificationType.Error => "[错误] ",
                        NotificationType.Warning => "[警告] ",
                        _ => string.Empty
                    };

                    txtLogOutput.Text += $"[{DateTime.Now:HH:mm:ss}] {prefix}{line}{Environment.NewLine}";
                }

                if (launchLogScrollViewer != null)
                {
                    launchLogScrollViewer.Offset = new Vector(launchLogScrollViewer.Offset.X, double.MaxValue);
                }

                _ = AnimateLaunchLogAppendAsync();
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                AppendAction();
            }
            else
            {
                Dispatcher.UIThread.Post(AppendAction);
            }
        }

        private async Task AnimateLaunchLogAppendAsync()
        {
            if (txtLogOutput == null)
            {
                return;
            }

            if (_isLaunchLogAppendAnimating)
            {
                _isLaunchLogAppendAnimationPending = true;
                return;
            }

            _isLaunchLogAppendAnimating = true;
            try
            {
                do
                {
                    _isLaunchLogAppendAnimationPending = false;
                    txtLogOutput.RenderTransformOrigin = Avalonia.RelativePoint.Center;
                    var scale = txtLogOutput.RenderTransform as ScaleTransform;
                    if (scale == null)
                    {
                        scale = new ScaleTransform(0.985, 0.985);
                        txtLogOutput.RenderTransform = scale;
                    }

                    txtLogOutput.Opacity = 0.55;
                    scale.ScaleX = 0.985;
                    scale.ScaleY = 0.985;

                    const int steps = 6;
                    for (int i = 0; i <= steps; i++)
                    {
                        double t = (double)i / steps;
                        double eased = 1 - Math.Pow(1 - t, 3);
                        txtLogOutput.Opacity = 0.55 + 0.45 * eased;
                        double currentScale = 0.985 + 0.015 * eased;
                        scale.ScaleX = currentScale;
                        scale.ScaleY = currentScale;
                        await Task.Delay(12);
                    }

                    txtLogOutput.Opacity = 1;
                    scale.ScaleX = 1;
                    scale.ScaleY = 1;
                }
                while (_isLaunchLogAppendAnimationPending);
            }
            finally
            {
                _isLaunchLogAppendAnimating = false;
            }
        }

        private async Task ShowLaunchLogAreaWithAnimationAsync()
        {
            if (launchLogContainer == null)
            {
                return;
            }

            if (_isLaunchLogVisible && launchLogContainer.IsVisible)
            {
                return;
            }

            _isLaunchLogVisible = true;
            launchLogContainer.IsVisible = true;
            launchLogContainer.Opacity = 0;
            launchLogContainer.RenderTransformOrigin = Avalonia.RelativePoint.TopLeft;
            UpdateLaunchLogToggleButtonText();

            var scale = launchLogContainer.RenderTransform as ScaleTransform;
            if (scale == null)
            {
                scale = new ScaleTransform(0.12, 0.12);
                launchLogContainer.RenderTransform = scale;
            }

            scale.ScaleX = 0.12;
            scale.ScaleY = 0.12;

            const int steps = 14;
            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                double eased = 1 - Math.Pow(1 - t, 3);
                double currentScale = 0.12 + (0.88 * eased);
                launchLogContainer.Opacity = eased;
                scale.ScaleX = currentScale;
                scale.ScaleY = currentScale;
                await Task.Delay(16);
            }

            launchLogContainer.Opacity = 1;
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }

        private void HideLaunchLogArea(bool clearOutput = false)
        {
            _isLaunchLogVisible = false;
            if (launchLogContainer == null)
            {
                return;
            }

            launchLogContainer.IsVisible = false;
            launchLogContainer.Opacity = 0;
            launchLogContainer.RenderTransformOrigin = Avalonia.RelativePoint.TopLeft;
            launchLogContainer.RenderTransform = new ScaleTransform(0.12, 0.12);
            UpdateLaunchLogToggleButtonText();

            if (clearOutput && txtLogOutput != null)
            {
                txtLogOutput.Text = string.Empty;
            }
        }

        private void UpdateLaunchLogToggleButtonText()
        {
            if (btnToggleLaunchLog == null)
            {
                return;
            }

            btnToggleLaunchLog.Content = _isLaunchLogVisible ? "隐藏启动日志" : "显示启动日志";
        }

        private static string UnquoteTomlString(string rawValue)
        {
            var value = rawValue?.Trim() ?? string.Empty;
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                var inner = value.Substring(1, value.Length - 2);
                return inner
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\")
                    .Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\t", "\t");
            }

            return value;
        }

        private static string EscapeTomlString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private static void WriteInitialConfigToml(string configPath)
        {
            var dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var lines = new List<string>
            {
                "[Setting]",
                "usepreconfig = \"false\"",
                "noasphyxia = \"false\"",
                "compatlayerenabled = \"false\"",
                "rendermode = \"dx9on12\"",
                string.Empty,
                "[Display]",
                "displayconfigure = \"false\"",
                "norestorerotation = \"false\"",
                "mode = \"dual\"",
                "mainscreen = \"0\"",
                "subscreen = \"0\"",
                "subrotation = \"0\"",
                "mainrotation = \"0\"",
                "mainresolution = \"640x480\"",
                "subresolution = \"640x480\"",
                "mainrefresh = \"59\"",
                "subrefresh = \"59\"",
                string.Empty,
                "[Server]",
                "activepreset = \"Asphyxia\"",
                string.Empty,
                "[[Server.Presets]]",
                "name = \"Asphyxia\"",
                "serverurl = \"http://localhost:8083\"",
                "pcbid = \"\""
            };

            File.WriteAllText(configPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
        }

        private void EnsureConfigSchema()
        {
            try
            {
                _configFile.RenameSection(LegacySettingsSectionName, SettingSectionName);
                _configFile.MoveKey(SettingSectionName, DisplaySectionName, "displayconfigure");
                _configFile.MoveKey(SettingSectionName, DisplaySectionName, "norestorerotation");
            }
            catch
            {
            }
        }

        private void LoadServerPresetsFromConfig()
        {
            _serverPresets.Clear();
            _serverPresets.Add(new ServerPresetItem { Name = NonePresetName });

            var configPath = GetConfigTomlPath();
            if (!File.Exists(configPath))
            {
                EnsureAsphyxiaPresetInList();
                _activeServerPreset = NonePresetName;
                SaveServerPresetsToConfig();
                RefreshServerPresetCombo();
                return;
            }

            try
            {
                var lines = File.ReadAllLines(configPath, Encoding.UTF8);
                ServerPresetItem current = null;
                bool hasPresetSection = false;
                bool inServerSection = false;
                string activePreset = string.Empty;

                void CommitCurrent()
                {
                    if (current == null || string.IsNullOrWhiteSpace(current.Name))
                    {
                        return;
                    }

                    if (_serverPresets.Any(p => string.Equals(p.Name, current.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        return;
                    }

                    _serverPresets.Add(current);
                }

                foreach (var raw in lines)
                {
                    var line = raw.Trim();

                    if (line.StartsWith("[Server]", StringComparison.OrdinalIgnoreCase))
                    {
                        inServerSection = true;
                        continue;
                    }

                    if (line.StartsWith("[[Server.Presets]]", StringComparison.OrdinalIgnoreCase)
                        || line.StartsWith("[[ServerPresets]]", StringComparison.OrdinalIgnoreCase))
                    {
                        hasPresetSection = true;
                        inServerSection = false;
                        CommitCurrent();
                        current = new ServerPresetItem();
                        continue;
                    }

                    if (inServerSection)
                    {
                        if (line.StartsWith("[", StringComparison.Ordinal) && !line.StartsWith("[[", StringComparison.Ordinal))
                        {
                            inServerSection = false;
                        }
                        else
                        {
                            int serverEq = line.IndexOf('=');
                            if (serverEq > 0)
                            {
                                string serverKey = line.Substring(0, serverEq).Trim();
                                string serverValue = UnquoteTomlString(line.Substring(serverEq + 1).Trim());
                                if (string.Equals(serverKey, "activepreset", StringComparison.OrdinalIgnoreCase))
                                {
                                    activePreset = serverValue;
                                }
                            }
                            continue;
                        }
                    }

                    if (current == null)
                    {
                        continue;
                    }

                    if (line.StartsWith("[", StringComparison.Ordinal) && !line.StartsWith("[[", StringComparison.Ordinal))
                    {
                        CommitCurrent();
                        current = null;
                        continue;
                    }

                    int eqIndex = line.IndexOf('=');
                    if (eqIndex <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, eqIndex).Trim();
                    string value = UnquoteTomlString(line.Substring(eqIndex + 1).Trim());

                    if (string.Equals(key, "name", StringComparison.OrdinalIgnoreCase))
                    {
                        current.Name = value;
                    }
                    else if (string.Equals(key, "serverurl", StringComparison.OrdinalIgnoreCase))
                    {
                        current.ServerUrl = value;
                    }
                    else if (string.Equals(key, "pcbid", StringComparison.OrdinalIgnoreCase))
                    {
                        current.PcbId = value;
                    }
                }

                CommitCurrent();

                bool presetChanged = EnsureAsphyxiaPresetInList();
                if (!hasPresetSection)
                {
                    presetChanged = true;
                }

                if (presetChanged)
                {
                    SaveServerPresetsToConfig();
                }

                if (!string.IsNullOrWhiteSpace(activePreset))
                {
                    var matched = _serverPresets.FirstOrDefault(p => string.Equals(p.Name, activePreset, StringComparison.OrdinalIgnoreCase));
                    if (matched != null)
                    {
                        _activeServerPreset = matched.Name;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowWarningToast("服务器预设读取异常", ex.Message);
                EnsureAsphyxiaPresetInList();
            }

            RefreshServerPresetCombo();
        }

        private bool EnsureAsphyxiaPresetInList()
        {
            var existing = _serverPresets.FirstOrDefault(p => string.Equals(p.Name, AsphyxiaPresetName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                bool changed = false;
                if (string.IsNullOrWhiteSpace(existing.ServerUrl))
                {
                    existing.ServerUrl = AsphyxiaDefaultUrl;
                    changed = true;
                }

                return changed;
            }

            _serverPresets.Add(new ServerPresetItem
            {
                Name = AsphyxiaPresetName,
                ServerUrl = AsphyxiaDefaultUrl,
                PcbId = string.Empty
            });
            return true;
        }

        private void SaveServerPresetsToConfig()
        {
            var configPath = GetConfigTomlPath();

            var lines = File.Exists(configPath)
                ? File.ReadAllLines(configPath, Encoding.UTF8).ToList()
                : new List<string>();

            var kept = new List<string>();
            bool skippingOldPresets = false;
            bool skippingServerSection = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("[Server]", StringComparison.OrdinalIgnoreCase))
                {
                    skippingServerSection = true;
                    continue;
                }

                if (trimmed.StartsWith("[[Server.Presets]]", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("[[ServerPresets]]", StringComparison.OrdinalIgnoreCase))
                {
                    skippingOldPresets = true;
                    continue;
                }

                if (skippingServerSection || skippingOldPresets)
                {
                    if (trimmed.StartsWith("[", StringComparison.Ordinal) && !trimmed.StartsWith("[[", StringComparison.Ordinal))
                    {
                        skippingServerSection = false;
                        skippingOldPresets = false;
                        kept.Add(line);
                    }
                    continue;
                }

                kept.Add(line);
            }

            while (kept.Count > 0 && string.IsNullOrWhiteSpace(kept[kept.Count - 1]))
            {
                kept.RemoveAt(kept.Count - 1);
            }

            if (kept.Count > 0)
            {
                kept.Add(string.Empty);
            }

            kept.Add("[Server]");
            kept.Add($"activepreset = \"{EscapeTomlString(_activeServerPreset ?? NonePresetName)}\"");

            foreach (var preset in _serverPresets.Where(p => !string.Equals(p.Name, NonePresetName, StringComparison.OrdinalIgnoreCase)))
            {
                kept.Add(string.Empty);
                kept.Add("[[Server.Presets]]");
                kept.Add($"name = \"{EscapeTomlString(preset.Name)}\"");
                kept.Add($"serverurl = \"{EscapeTomlString(preset.ServerUrl)}\"");
                kept.Add($"pcbid = \"{EscapeTomlString(preset.PcbId)}\"");
            }

            for (int i = kept.Count - 1; i >= 0; i--)
            {
                if (!string.IsNullOrWhiteSpace(kept[i]))
                {
                    break;
                }

                kept.RemoveAt(i);
            }

            for (int i = kept.Count - 1; i > 0; i--)
            {
                if (string.IsNullOrWhiteSpace(kept[i]) && string.IsNullOrWhiteSpace(kept[i - 1]))
                {
                    kept.RemoveAt(i);
                }
            }

            File.WriteAllText(configPath, string.Join(Environment.NewLine, kept), new UTF8Encoding(false));
        }

        private void RefreshServerPresetCombo()
        {
            if (cmbServerPreset == null)
            {
                return;
            }

            cmbServerPreset.Items.Clear();
            foreach (var preset in _serverPresets)
            {
                cmbServerPreset.Items.Add(preset);
            }

            if (cmbServerPreset.Items.Count > 0)
            {
                var active = _serverPresets.FirstOrDefault(p => string.Equals(p.Name, _activeServerPreset, StringComparison.OrdinalIgnoreCase));
                cmbServerPreset.SelectedItem = active ?? _serverPresets[0];
            }
        }

        private void SelectPresetByCurrentFields()
        {
            if (cmbServerPreset == null)
            {
                return;
            }

            var serverUrl = txtServerAddress?.Text ?? string.Empty;
            var pcbId = txtPcbId?.Text ?? string.Empty;

            var matched = _serverPresets.FirstOrDefault(p =>
                !string.Equals(p.Name, NonePresetName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.ServerUrl ?? string.Empty, serverUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.PcbId ?? string.Empty, pcbId, StringComparison.OrdinalIgnoreCase));

            _isSyncingModel = true;
            try
            {
                if (matched != null)
                {
                    cmbServerPreset.SelectedItem = matched;
                    _activeServerPreset = matched.Name;
                }
                else
                {
                    cmbServerPreset.SelectedIndex = 0;
                    _activeServerPreset = NonePresetName;
                }
            }
            finally
            {
                _isSyncingModel = false;
            }
        }

        private void cmbServerPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings || _isSyncingModel)
            {
                return;
            }

            if (cmbServerPreset?.SelectedItem is not ServerPresetItem preset)
            {
                return;
            }

            _isSyncingModel = true;
            try
            {
                _activeServerPreset = preset.Name;
            }
            finally
            {
                _isSyncingModel = false;
            }

            if (string.Equals(preset.Name, NonePresetName, StringComparison.OrdinalIgnoreCase))
            {
                if (txtServerAddress != null) txtServerAddress.Text = string.Empty;
                if (txtPcbId != null) txtPcbId.Text = string.Empty;
            }
            else
            {
                if (txtServerAddress != null) txtServerAddress.Text = preset.ServerUrl ?? string.Empty;
                if (txtPcbId != null) txtPcbId.Text = preset.PcbId ?? string.Empty;
            }

            UpdateSpiceConfig(
                new OptionUpdate("url", txtServerAddress?.Text ?? string.Empty, false),
                new OptionUpdate("p", txtPcbId?.Text ?? string.Empty, false));
            SaveServerPresetsToConfig();
        }

        private async void btnAddServerPreset_Click(object sender, RoutedEventArgs e)
        {
            await CreateServerPresetInteractiveAsync();
        }

        private async void btnDeleteServerPreset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cmbServerPreset?.SelectedItem is not ServerPresetItem preset)
                {
                    ShowWarningToast("删除预设", "请先选择要删除的预设。");
                    return;
                }

                if (string.Equals(preset.Name, NonePresetName, StringComparison.OrdinalIgnoreCase))
                {
                    ShowWarningToast("删除预设", "“无”是内置项，不能删除。");
                    return;
                }

                if (string.Equals(preset.Name, AsphyxiaPresetName, StringComparison.OrdinalIgnoreCase))
                {
                    ShowWarningToast("删除预设", "Asphyxia 是内置预设，不能删除。");
                    return;
                }

                var dialogBuilder = _dialogManager
                    .CreateDialog()
                    .OfType(NotificationType.Warning)
                    .WithTitle("删除服务器预设")
                    .WithContent($"确定删除预设“{preset.Name}”吗？")
                    .WithYesNoResult("删除", "取消", "Flat")
                    .Dismiss().ByClickingBackground();
                ApplyDialogNotificationIcon(dialogBuilder, NotificationType.Warning);
                bool confirmed = await dialogBuilder.TryShowAsync();
                if (!confirmed)
                {
                    return;
                }

                _serverPresets.RemoveAll(p => string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));

                if (string.Equals(_activeServerPreset, preset.Name, StringComparison.OrdinalIgnoreCase))
                {
                    _activeServerPreset = NonePresetName;
                }

                RefreshServerPresetCombo();
                if (cmbServerPreset != null)
                {
                    var fallback = _serverPresets.FirstOrDefault(p => string.Equals(p.Name, NonePresetName, StringComparison.OrdinalIgnoreCase));
                    if (fallback != null)
                    {
                        cmbServerPreset.SelectedItem = fallback;
                    }
                    else if (_serverPresets.Count > 0)
                    {
                        cmbServerPreset.SelectedItem = _serverPresets[0];
                    }
                }

                SaveServerPresetsToConfig();
                ShowInfoToast("删除预设", $"已删除预设：{preset.Name}");
            }
            catch (Exception ex)
            {
                ShowErrorToast("删除预设失败", ex.Message);
            }
        }

        private async Task<bool> CreateServerPresetInteractiveAsync()
        {
            try
            {
                var nameBox = new TextBox { Watermark = "预设名" };
                var urlBox = new TextBox { Watermark = "http://SERVERURL:PORT" };
                var pcbBox = new TextBox { Watermark = "PCBID" };

                var content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "请输入预设信息" },
                        nameBox,
                        urlBox,
                        pcbBox
                    }
                };

                var confirmed = await _dialogManager
                    .CreateDialog()
                    .WithTitle("新建服务器预设")
                    .WithContent(content)
                    .WithYesNoResult("保存", "取消", "Flat")
                    .TryShowAsync();

                if (!confirmed)
                {
                    return false;
                }

                var presetName = (nameBox.Text ?? string.Empty).Trim();
                var serverUrl = (urlBox.Text ?? string.Empty).Trim();
                var pcbId = (pcbBox.Text ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(presetName))
                {
                    ShowErrorToast("新建预设失败", "预设名不能为空。");
                    return false;
                }

                if (_serverPresets.Any(p => string.Equals(p.Name, presetName, StringComparison.OrdinalIgnoreCase)))
                {
                    ShowErrorToast("新建预设失败", "已存在同名预设。");
                    return false;
                }

                var preset = new ServerPresetItem
                {
                    Name = presetName,
                    ServerUrl = serverUrl,
                    PcbId = pcbId
                };

                _serverPresets.Add(preset);
                SaveServerPresetsToConfig();
                RefreshServerPresetCombo();
                if (cmbServerPreset != null)
                {
                    cmbServerPreset.SelectedItem = preset;
                }
                ShowInfoToast("预设已保存", $"已新增预设：{presetName}");
                return true;
            }
            catch (Exception ex)
            {
                ShowErrorToast("新建预设失败", ex.Message);
                return false;
            }
        }

        private void LoadSettings()
        {
            try
            {
                _isLoadingSettings = true; // 标记正在加载设置
                // 加载预配置选项
                string usePreconfigStr = _configFile.ReadString(SettingSectionName, "usepreconfig", "false");
                if (!bool.TryParse(usePreconfigStr, out _usePreconfig))
                {
                    _usePreconfig = false;
                }
                if (chkUsePreconfig != null)
                {
                    chkUsePreconfig.IsChecked = _usePreconfig;
                }

                LoadServerPresetsFromConfig();

                // 加载其他启动选项（窗口化与大小核由 XML 驱动，故不再从 config.toml 读取）
                chkNoAsphyxia.IsChecked = bool.TryParse(_configFile.ReadString(SettingSectionName, "noasphyxia", "false"), out var noAsphyxia) && noAsphyxia;
                bool noRestoreRotation = bool.TryParse(_configFile.ReadString(DisplaySectionName, "norestorerotation", "false"), out var noRestore) && noRestore;
                chkNoRestoreRotation.IsChecked = !noRestoreRotation;

                _displayConfigEnabled = bool.TryParse(_configFile.ReadString(DisplaySectionName, "displayconfigure", "false"), out var displayCfg) && displayCfg;
                if (tglDisplayConfigEnabled != null) tglDisplayConfigEnabled.IsChecked = _displayConfigEnabled;
                _isDualDisplay = !string.Equals(_configFile.ReadString(DisplaySectionName, "mode", "dual"), "single", StringComparison.OrdinalIgnoreCase);
                if (cmbDisplayMode != null) cmbDisplayMode.SelectedIndex = _isDualDisplay ? 1 : 0;

                // 渲染模式（兼容层实现）
                try
                {
                    string renderMode = _configFile.ReadString(SettingSectionName, "rendermode", "dx9on12");
                    if (cmbCompatType != null)
                    {
                        // 保证项存在
                        if (cmbCompatType.Items.Count == 0)
                        {
                            cmbCompatType.Items.Add("dx9on12");
                            cmbCompatType.Items.Add("dx9on12_external");
                            cmbCompatType.Items.Add("dxvk");
                        }
                        int idx = 0;
                        if (string.Equals(renderMode, "dx9on12_external", StringComparison.OrdinalIgnoreCase)) idx = 1;
                        else if (string.Equals(renderMode, "dxvk", StringComparison.OrdinalIgnoreCase)) idx = 2;
                        cmbCompatType.SelectedIndex = idx;
                    }
                }
                catch { }

                // 读取机台属性（优先 ea3-ident.xml，回退 ea3-config.xml）
                string machineProperty = ResolveMachineProperty();
                if (txtCurrentVersion != null)
                {
                    txtCurrentVersion.Text = machineProperty;
                }

                // 读取当前游戏版本（bootstrap.xml/release_code）
                string currentGameVersion = ResolveCurrentGameVersion();
                if (txtRevision != null)
                {
                    txtRevision.Text = currentGameVersion;
                }

                string launcherVersion = ResolveLauncherVersion();
                if (txtLauncherVersion != null)
                {
                    txtLauncherVersion.Text = launcherVersion;
                }

                if (cmbMainScreen != null)
                {
                    int.TryParse(_configFile.ReadString(DisplaySectionName, "mainscreen", "0"), out var mainScreenIndex);
                    if (mainScreenIndex >= 0 && mainScreenIndex < cmbMainScreen.Items.Count) cmbMainScreen.SelectedIndex = mainScreenIndex;
                }
                if (cmbSubScreen != null)
                {
                    int.TryParse(_configFile.ReadString(DisplaySectionName, "subscreen", "0"), out var subScreenIndex);
                    if (subScreenIndex >= 0 && subScreenIndex < cmbSubScreen.Items.Count) cmbSubScreen.SelectedIndex = subScreenIndex;
                }
                if (cmbSubRotation != null)
                {
                    int.TryParse(_configFile.ReadString(DisplaySectionName, "subrotation", "0"), out var subRotationIndex);
                    if (subRotationIndex >= 0 && subRotationIndex < cmbSubRotation.Items.Count) cmbSubRotation.SelectedIndex = subRotationIndex;
                }
                if (cmbRotation != null)
                {
                    int.TryParse(_configFile.ReadString(DisplaySectionName, "mainrotation", "0"), out var mainRotationIndex);
                    if (mainRotationIndex >= 0 && mainRotationIndex < cmbRotation.Items.Count) cmbRotation.SelectedIndex = mainRotationIndex;
                }

                RefreshMainOptions();
                RefreshSubOptions();

                if (cmbMainResolution != null)
                {
                    var res = _configFile.ReadString(DisplaySectionName, "mainresolution", "");
                    if (!string.IsNullOrWhiteSpace(res)) cmbMainResolution.SelectedItem = res;
                }
                if (cmbSubResolution != null)
                {
                    var res = _configFile.ReadString(DisplaySectionName, "subresolution", "");
                    if (!string.IsNullOrWhiteSpace(res)) cmbSubResolution.SelectedItem = res;
                }
                RefreshMainOptions(refreshResolutionList: false, refreshRateList: true);
                RefreshSubOptions(refreshResolutionList: false, refreshRateList: true);

                if (cmbMainRefreshRate != null)
                {
                    var refresh = _configFile.ReadString(DisplaySectionName, "mainrefresh", "");
                    if (!string.IsNullOrWhiteSpace(refresh)) cmbMainRefreshRate.SelectedItem = refresh;
                }
                if (cmbSubRefreshRate != null)
                {
                    var refresh = _configFile.ReadString(DisplaySectionName, "subrefresh", "");
                    if (!string.IsNullOrWhiteSpace(refresh)) cmbSubRefreshRate.SelectedItem = refresh;
                }

                UpdateDisplayLayoutControlsEnabled();
                UpdateDisplayInfoTexts();
                SyncCompatModeButtonsFromCombo();
            }
            catch (Exception ex)
            {
                ShowErrorToast("加载配置失败", ex.Message);
                if (txtCurrentVersion != null) txtCurrentVersion.Text = "读取失败";
                if (txtRevision != null) txtRevision.Text = "读取失败";
                if (txtLauncherVersion != null) txtLauncherVersion.Text = "读取失败";
            }
            finally
            {
                _isLoadingSettings = false; // 标记加载完成
            }
        }

        private void SaveSettings()
        {
            if (_isLoadingSettings)
            {
                return;
            }

            try
            {
                _configFile.WriteString(SettingSectionName, "usepreconfig", _usePreconfig.ToString().ToLowerInvariant());

                // 保存仍通过 config.toml 管理的选项
                if (chkNoAsphyxia != null)
                    _configFile.WriteString(SettingSectionName, "noasphyxia", (chkNoAsphyxia.IsChecked == true).ToString().ToLowerInvariant());
                if (chkNoRestoreRotation != null)
                    _configFile.WriteString(DisplaySectionName, "norestorerotation", (chkNoRestoreRotation.IsChecked != true).ToString().ToLowerInvariant());

                // 保存渲染模式（兼容层实现）
                if (cmbCompatType != null && cmbCompatType.SelectedItem != null)
                {
                    string renderMode = cmbCompatType.SelectedItem.ToString();
                    _configFile.WriteString(SettingSectionName, "rendermode", renderMode);
                }

                _configFile.WriteString(DisplaySectionName, "displayconfigure", _displayConfigEnabled.ToString().ToLowerInvariant());
                _configFile.WriteString(DisplaySectionName, "mode", _isDualDisplay ? "dual" : "single");

                if (cmbMainScreen != null) _configFile.WriteString(DisplaySectionName, "mainscreen", cmbMainScreen.SelectedIndex.ToString());
                if (cmbSubScreen != null) _configFile.WriteString(DisplaySectionName, "subscreen", cmbSubScreen.SelectedIndex.ToString());
                if (cmbSubRotation != null) _configFile.WriteString(DisplaySectionName, "subrotation", cmbSubRotation.SelectedIndex.ToString());
                if (cmbRotation != null) _configFile.WriteString(DisplaySectionName, "mainrotation", cmbRotation.SelectedIndex.ToString());
                if (cmbMainResolution != null && cmbMainResolution.SelectedItem != null) _configFile.WriteString(DisplaySectionName, "mainresolution", cmbMainResolution.SelectedItem.ToString());
                if (cmbSubResolution != null && cmbSubResolution.SelectedItem != null) _configFile.WriteString(DisplaySectionName, "subresolution", cmbSubResolution.SelectedItem.ToString());
                if (cmbMainRefreshRate != null && cmbMainRefreshRate.SelectedItem != null) _configFile.WriteString(DisplaySectionName, "mainrefresh", cmbMainRefreshRate.SelectedItem.ToString());
                if (cmbSubRefreshRate != null && cmbSubRefreshRate.SelectedItem != null) _configFile.WriteString(DisplaySectionName, "subrefresh", cmbSubRefreshRate.SelectedItem.ToString());
            }
            catch (Exception ex)
            {
                ShowErrorToast("保存配置失败", ex.Message);
            }
        }

        private string ResolveMachineProperty()
        {
            var identPath = Path.Combine(_contentsDir, "prop", "ea3-ident.xml");
            var result = TryReadMachinePropertyFromEa3(identPath);
            if (!string.IsNullOrWhiteSpace(result))
            {
                return result;
            }

            var configPath = Path.Combine(_contentsDir, "prop", "ea3-config.xml");
            result = TryReadMachinePropertyFromEa3(configPath);
            if (!string.IsNullOrWhiteSpace(result))
            {
                return result;
            }

            return "未知";
        }

        private static string TryReadMachinePropertyFromEa3(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var doc = XDocument.Load(filePath);
                var softNode = doc.Root?.Element("soft");
                if (softNode == null)
                {
                    return null;
                }

                var model = softNode.Element("model")?.Value?.Trim();
                var dest = softNode.Element("dest")?.Value?.Trim();
                var spec = softNode.Element("spec")?.Value?.Trim();
                var rev = softNode.Element("rev")?.Value?.Trim();

                if (string.IsNullOrWhiteSpace(model) ||
                    string.IsNullOrWhiteSpace(dest) ||
                    string.IsNullOrWhiteSpace(spec) ||
                    string.IsNullOrWhiteSpace(rev))
                {
                    return null;
                }

                return $"{model}:{dest}:{spec}:{rev}";
            }
            catch
            {
                return null;
            }
        }

        private string ResolveCurrentGameVersion()
        {
            try
            {
                var bootstrapPath = Path.Combine(_contentsDir, "prop", "bootstrap.xml");
                if (!File.Exists(bootstrapPath))
                {
                    return "未知";
                }

                var doc = XDocument.Load(bootstrapPath);
                var releaseCode = doc.Root?.Element("release_code")?.Value?.Trim();
                return string.IsNullOrWhiteSpace(releaseCode) ? "未知" : releaseCode;
            }
            catch
            {
                return "未知";
            }
        }

        private string ResolveLauncherVersion()
        {
            try
            {
                var launcherExe = Path.Combine(_baseDir, "launcher", "LazyBootstrap.exe");
                if (File.Exists(launcherExe))
                {
                    var fileVersion = FileVersionInfo.GetVersionInfo(launcherExe);
                    if (!string.IsNullOrWhiteSpace(fileVersion.FileVersion))
                    {
                        return fileVersion.FileVersion;
                    }

                    if (!string.IsNullOrWhiteSpace(fileVersion.ProductVersion))
                    {
                        return fileVersion.ProductVersion;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return "未知";
        }

        // 使用预配置文件复选框事件
        private void chkUsePreconfig_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings || _isUpdatingPreconfigUi)
            {
                return; // 如果正在加载设置，不显示警告
            }

            bool newUsePreconfig = chkUsePreconfig.IsChecked == true;

            if (!newUsePreconfig)
            {
                var dialogBuilder = _dialogManager.CreateDialog()
                    .OfType(NotificationType.Warning)
                    .WithTitle("预配置提示")
                    .WithContent("如果不使用预配置文件，你需要重新手动设置所有选项与补丁（等同于纯净版），请确保你拥有相关知识！")
                    .WithActionButton("我知道了", _ => { }, true, "Flat")
                    .Dismiss().ByClickingBackground();
                ApplyDialogNotificationIcon(dialogBuilder, NotificationType.Warning);
                dialogBuilder.TryShow();
            }

            _usePreconfig = newUsePreconfig;
            var activeXmlPath = GetSpiceXmlPath();
            if (!File.Exists(activeXmlPath))
            {
                ShowErrorToast("切换失败", "未找到 spicetools.xml，已自动关闭该选项。");

                _usePreconfig = false;
                ApplyPreconfigToggleState(false);
                RefreshSettingsPanelAfterPreconfigSwitch();

                SaveSettings();
                return;
            }

            SaveSettings();
            ShowInfoToast("预配置切换", _usePreconfig
                ? $"当前使用预配置 XML: {activeXmlPath}"
                : $"当前使用系统 XML: {activeXmlPath}");

            RefreshSettingsPanelAfterPreconfigSwitch();
        }

        private void RefreshSettingsPanelAfterPreconfigSwitch()
        {
            LoadSpiceConfig();
            LoadServerPresetsFromConfig();
            SelectPresetByCurrentFields();
            UpdateCompatLayerStatus();
            SyncCompatModeButtonsFromCombo();
        }

        private void ApplyPreconfigToggleState(bool enabled)
        {
            _isUpdatingPreconfigUi = true;
            try
            {
                if (chkUsePreconfig != null)
                {
                    chkUsePreconfig.IsChecked = enabled;
                }
            }
            finally
            {
                _isUpdatingPreconfigUi = false;
            }

            Dispatcher.UIThread.Post(() =>
            {
                _isUpdatingPreconfigUi = true;
                try
                {
                    if (chkUsePreconfig != null)
                    {
                        chkUsePreconfig.IsChecked = enabled;
                    }
                }
                finally
                {
                    _isUpdatingPreconfigUi = false;
                }
            }, DispatcherPriority.Render);
        }

        private bool EnsureSpiceXmlExistsForToggleOrRevert(ToggleSwitch toggle, Action onReverted)
        {
            var xmlPath = GetSpiceXmlPath();
            if (File.Exists(xmlPath))
            {
                return true;
            }

            ShowErrorToast("保存设定失败", "未找到 spicetools.xml，已自动关闭该选项。");

            _isUpdatingSpiceToggleUi = true;
            try
            {
                if (onReverted != null)
                {
                    onReverted();
                }

                if (toggle != null)
                {
                    toggle.IsChecked = false;
                }
            }
            finally
            {
                _isUpdatingSpiceToggleUi = false;
            }

            Dispatcher.UIThread.Post(() =>
            {
                _isUpdatingSpiceToggleUi = true;
                try
                {
                    if (toggle != null)
                    {
                        toggle.IsChecked = false;
                    }
                }
                finally
                {
                    _isUpdatingSpiceToggleUi = false;
                }
            }, DispatcherPriority.Render);

            return false;
        }

        // 检查兼容层文件数量
        private int GetCompatLayerFileCount()
        {
            string modulesDir = Path.Combine(_contentsDir, "modules");
            string[] compatFiles = { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll" };

            int foundCount = 0;
            foreach (var fileName in compatFiles)
            {
                string filePath = Path.Combine(modulesDir, fileName);
                if (File.Exists(filePath))
                {
                    foundCount++;
                }
            }

            return foundCount;
        }

        private bool IsCompatLayerEnabledConfigured()
        {
            try
            {
                var s = _configFile.ReadString(SettingSectionName, "compatlayerenabled", "false");
                bool enabled;
                return bool.TryParse(s, out enabled) && enabled;
            }
            catch { return false; }
        }

        // 更新兼容层状态指示器
        private void UpdateCompatLayerStatus()
        {
            int fileCount = GetCompatLayerFileCount();

            // 启用状态下禁用兼容层实现下拉，需先关闭再更改；同时"启用"按钮禁用、"关闭"按钮启用
            bool effectiveEnabled = fileCount >= 1 || IsCompatLayerEnabledConfigured();
            UpdateCompatRenderModeBusyState(effectiveEnabled);

            if (lblCompatStatus != null)
            {
                lblCompatStatus.Text = string.Empty;
                lblCompatStatus.IsVisible = false;
            }

            if (cmbCompatType != null)
            {
                cmbCompatType.IsEnabled = !effectiveEnabled;
                if (effectiveEnabled)
                {
                    ToolTip.SetTip(cmbCompatType, null);
                }
                else
                {
                    if (!string.IsNullOrEmpty(_compatTypeTooltipCache))
                        ToolTip.SetTip(cmbCompatType, _compatTypeTooltipCache);
                }
            }
            if (btnLoadCompat != null) // 启用按钮
            {
                btnLoadCompat.IsEnabled = !effectiveEnabled;
            }
            if (btnUnloadCompat != null) // 关闭按钮
            {
                btnUnloadCompat.IsEnabled = effectiveEnabled;
            }

            _isUpdatingCompatUi = true;
            try
            {
                if (tglCompatLayer != null)
                {
                    tglCompatLayer.IsChecked = effectiveEnabled;
                }

                bool chipsEnabled = !effectiveEnabled;
                if (rbCompatDx9on12 != null) rbCompatDx9on12.IsEnabled = chipsEnabled;
                if (rbCompatDx9on12External != null) rbCompatDx9on12External.IsEnabled = chipsEnabled;
                if (rbCompatDxvk != null) rbCompatDxvk.IsEnabled = chipsEnabled;
                SyncCompatModeButtonsFromCombo();
            }
            finally
            {
                _isUpdatingCompatUi = false;
            }
        }

        private void UpdateCompatRenderModeBusyState(bool isBusy)
        {
            if (compatRenderModeBusyArea != null)
            {
                compatRenderModeBusyArea.IsBusy = isBusy;
            }
        }

        private void SyncCompatModeButtonsFromCombo()
        {
            var mode = cmbCompatType?.SelectedItem?.ToString() ?? "dx9on12";
            if (rbCompatDxvk != null) rbCompatDxvk.IsChecked = string.Equals(mode, "dxvk", StringComparison.OrdinalIgnoreCase);
            if (rbCompatDx9on12External != null) rbCompatDx9on12External.IsChecked = string.Equals(mode, "dx9on12_external", StringComparison.OrdinalIgnoreCase);
            if (rbCompatDx9on12 != null)
            {
                rbCompatDx9on12.IsChecked = !string.Equals(mode, "dxvk", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(mode, "dx9on12_external", StringComparison.OrdinalIgnoreCase);
            }
        }

        // 环境扫描（启动时执行）
        private async Task RunEnvironmentScanAsync()
        {
            try
            {
                SetControlsEnabled(false);
                if (statusLabel != null) statusLabel.Text = "正在进行环境检测...";
                if (statusProgress != null)
                {
                    statusProgress.IsVisible = true;
                    statusProgress.Value = 0;
                    statusProgress.Minimum = 0;
                    statusProgress.Maximum = 100;
                }

                // 执行检测
                await EnvironmentScan.RunAsync((progress, message) =>
                {
                    int value = progress;
                    if (value < 0) value = 0;
                    if (value > 100) value = 100;
                    try
                    {
                        if (statusProgress != null)
                        {
                            Dispatcher.UIThread.Post(() => { statusProgress.Value = value; });
                        }
                    }
                    catch { }
                });

                RefreshEnvironmentScanResultCard();

                // 检测异常弹窗
                if (EnvironmentScan.LastHadError)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("(｡>﹏<｡) 啊哇哇，Near检测到你的系统可能缺少必要的运行组件！");
                    sb.AppendLine();
                    sb.AppendLine("有这些东西异常！");
                    sb.AppendLine(EnvironmentScan.LastErrorSummary);
                    sb.AppendLine("(* ^∇^)ﾉ Noah给出的解决方法：");
                    sb.AppendLine("- 在工具页点击“安装运行库”按钮安装必要运行组件");
                    sb.AppendLine("- 确保已安装最新的显卡驱动程序");
                    sb.AppendLine("- 若为 AMD/Intel 显卡，请启用\u201c显卡兼容层\u201d后重试");
                    sb.AppendLine();
                    sb.AppendLine("如果\u201c系统媒体功能包\u201d异常：");
                    sb.AppendLine("- 检查\u201cWindows 功能\u201d中是否启用了\u201c媒体功能包\u201d");
                    sb.AppendLine();
                    sb.AppendLine("请注意！由于环境不同，这个提示可能会误报！");
                    sb.AppendLine("您可先行尝试启动游戏，若出现问题，再寻求周围帮助！");
                    sb.AppendLine();

                    var dialogBuilder = _dialogManager.CreateDialog()
                        .OfType(NotificationType.Error)
                        .WithTitle("环境检测提示")
                        .WithContent(sb.ToString())
                        .WithActionButton("关闭", _ => { }, true, "Flat")
                        .Dismiss().ByClickingBackground();
                    ApplyDialogNotificationIcon(dialogBuilder, NotificationType.Error);
                    dialogBuilder.TryShow();
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast("环境检测失败", ex.Message);
            }
            finally
            {
                if (statusLabel != null) statusLabel.Text = "就绪";
                // 检测完成后隐藏进度条并复位
                if (statusProgress != null)
                {
                    try { statusProgress.Value = 0; } catch { }
                    statusProgress.IsVisible = false;
                }
                SetControlsEnabled(true);
            }
        }

        private void RefreshEnvironmentScanResultCard()
        {
            if (panelEnvScanResults == null)
            {
                return;
            }

            panelEnvScanResults.Children.Clear();

            var rootItems = new List<EnvironmentScan.ScanResultItem>();
            var groupedItems = new Dictionary<string, List<EnvironmentScan.ScanResultItem>>(StringComparer.Ordinal);

            foreach (var item in EnvironmentScan.LastItems)
            {
                var slashIndex = item.Item.IndexOf('/');
                if (slashIndex <= 0 || slashIndex >= item.Item.Length - 1)
                {
                    rootItems.Add(item);
                    continue;
                }

                var groupName = item.Item.Substring(0, slashIndex).Trim();
                if (!groupedItems.TryGetValue(groupName, out var list))
                {
                    list = new List<EnvironmentScan.ScanResultItem>();
                    groupedItems[groupName] = list;
                }

                list.Add(item);
            }

            static string ResolveStatusText(EnvironmentScan.ScanResultItem item)
            {
                if (!string.IsNullOrWhiteSpace(item.Detail)
                    && item.Detail.IndexOf("虚拟机", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "虚拟机";
                }

                return item.Level switch
                {
                    EnvironmentScan.ScanResultLevel.Success => "通过",
                    EnvironmentScan.ScanResultLevel.Warning => "警告",
                    _ => "失败"
                };
            }

            static IBrush ResolveStatusBrush(EnvironmentScan.ScanResultItem item)
            {
                if (!string.IsNullOrWhiteSpace(item.Detail)
                    && item.Detail.IndexOf("虚拟机", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return Brushes.Goldenrod;
                }

                return item.Level switch
                {
                    EnvironmentScan.ScanResultLevel.Success => Brushes.LightGreen,
                    EnvironmentScan.ScanResultLevel.Warning => Brushes.Orange,
                    _ => Brushes.IndianRed
                };
            }

            void AddRow(string labelText, EnvironmentScan.ScanResultItem sourceItem, bool showStatus, double indentLeft)
            {
                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("300,80,*"),
                    ColumnSpacing = 8,
                    Margin = new Thickness(indentLeft, 0, 0, 0)
                };

                var label = new TextBlock
                {
                    Text = labelText,
                    TextWrapping = TextWrapping.Wrap
                };
                row.Children.Add(label);

                if (showStatus)
                {
                    var status = new TextBlock
                    {
                        Text = ResolveStatusText(sourceItem),
                        Foreground = ResolveStatusBrush(sourceItem),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };
                    Grid.SetColumn(status, 1);
                    row.Children.Add(status);
                }

                panelEnvScanResults.Children.Add(row);
            }

            foreach (var item in rootItems)
            {
                AddRow(item.Item, item, true, 0);
            }

            foreach (var group in groupedItems)
            {
                var groupLevel = group.Value.Any(x => x.Level == EnvironmentScan.ScanResultLevel.Error)
                    ? EnvironmentScan.ScanResultLevel.Error
                    : (group.Value.Any(x => x.Level == EnvironmentScan.ScanResultLevel.Warning)
                        ? EnvironmentScan.ScanResultLevel.Warning
                        : EnvironmentScan.ScanResultLevel.Success);

                var groupItem = new EnvironmentScan.ScanResultItem
                {
                    Item = group.Key,
                    Level = groupLevel
                };

                AddRow(group.Key, groupItem, false, 0);

                bool noStatusGroup = string.Equals(group.Key, "CPU", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(group.Key, "GPU", StringComparison.OrdinalIgnoreCase);

                foreach (var child in group.Value)
                {
                    var slashIndex = child.Item.IndexOf('/');
                    var childSuffix = slashIndex >= 0 && slashIndex < child.Item.Length - 1
                        ? child.Item.Substring(slashIndex + 1)
                        : child.Item;
                    bool isVm = !string.IsNullOrWhiteSpace(child.Detail)
                        && child.Detail.IndexOf("虚拟机", StringComparison.OrdinalIgnoreCase) >= 0;

                    string childLabel;
                    if (noStatusGroup)
                    {
                        if (string.Equals(group.Key, "CPU", StringComparison.OrdinalIgnoreCase))
                        {
                            childLabel = string.IsNullOrWhiteSpace(child.Detail) ? childSuffix : child.Detail;
                        }
                        else
                        {
                            childLabel = childSuffix;
                        }
                    }
                    else
                    {
                        childLabel = string.IsNullOrWhiteSpace(child.Detail) ? childSuffix : $"{childSuffix} - {child.Detail}";
                    }

                    AddRow(childLabel, child, noStatusGroup ? isVm : true, 28);
                }
            }
        }

        // Boot
        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            // Lock Element
            SetControlsEnabled(false);
            if (statusLabel != null) statusLabel.Text = "正在启动...";
            await ShowLaunchLogAreaWithAnimationAsync();
            if (txtLogOutput != null) txtLogOutput.Text = string.Empty;
            AppendLaunchOutput("正在启动...");

            try
            {
                string spicePath = GetSpicePath();
                string asphyxiaPath = GetAsphyxiaPath();

                if (!File.Exists(spicePath))
                {
                    ShowErrorToast("启动失败", $"未找到 spice64.exe: {spicePath}");
                    AppendLaunchOutput($"未找到游戏主程序：{spicePath}", NotificationType.Error);
                    SetControlsEnabled(true);
                    if (statusLabel != null) statusLabel.Text = "启动失败";
                    return;
                }

                bool startAsphyxia = chkNoAsphyxia?.IsChecked != true;
                if (startAsphyxia && !File.Exists(asphyxiaPath))
                {
                    ShowErrorToast("启动失败", $"未找到 asphyxia-core-x64.exe: {asphyxiaPath}");
                    AppendLaunchOutput($"未找到 Asphyxia Core：{asphyxiaPath}", NotificationType.Error);
                    SetControlsEnabled(true);
                    if (statusLabel != null) statusLabel.Text = "启动失败";
                    return;
                }

                if (_displayConfigEnabled)
                {
                    AppendLaunchOutput("正在应用显示器配置...");
                    bool displayApplySuccess = ApplyDisplaySettingsForLaunch();
                    AppendLaunchOutput(displayApplySuccess ? "显示器配置应用完成。" : "显示器配置部分失败，游戏将继续启动。", displayApplySuccess ? NotificationType.Information : NotificationType.Warning);
                    await Task.Delay(5000);
                }

                // Launch Asphyxia
                if (startAsphyxia)
                {
                    AppendLaunchOutput("正在启动 Asphyxia Core...");
                    var asphyxiaStartInfo = new ProcessStartInfo
                    {
                        FileName = asphyxiaPath,
                        Arguments = _dbgAsphyxiaDebug ? "--dev" : string.Empty,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(asphyxiaPath)
                    };

                    var asphyxiaProcess = Process.Start(asphyxiaStartInfo);
                    if (asphyxiaProcess == null)
                    {
                        ShowErrorToast("启动失败", "Asphyxia 启动失败：进程未创建。");
                        AppendLaunchOutput("Asphyxia 启动失败：进程未创建。", NotificationType.Error);
                        SetControlsEnabled(true);
                        if (statusLabel != null) statusLabel.Text = "启动失败";
                        return;
                    }

                    AppendLaunchOutput("Asphyxia Core 已启动。");
                }
                else
                {
                    AppendLaunchOutput("已跳过启动 Asphyxia Core。");
                }

                // 在启动前写入 XML 选项，替代命令行参数
                UpdateSpiceConfig();

                var argsBuilder = new StringBuilder();
                if (_usePreconfig)
                {
                    // Spice2x launch args（使用预配置）
                    argsBuilder.Append("-cfgpath lazy/spicetools.xml ");
                    argsBuilder.Append("-patchcfgpath lazy/spicetools_patch_manager.json ");
                    argsBuilder.Append("-modules modules ");
                }

                // Booting
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
                    ShowErrorToast("启动失败", "spice64 启动失败：进程未创建。");
                    AppendLaunchOutput("spice64 启动失败：进程未创建。", NotificationType.Error);
                    SetControlsEnabled(true);
                    if (statusLabel != null) statusLabel.Text = "启动失败";
                    _gameProcess.Dispose();
                    _gameProcess = null;
                    return;
                }

                _gameProcess.Exited += GameProcess_Exited;

                if (statusLabel != null) statusLabel.Text = "游戏已启动";
                AppendLaunchOutput("游戏进程已启动。");
            }
            catch (Exception ex)
            {
                ShowErrorToast("启动失败", ex.Message);
                AppendLaunchOutput($"启动过程中发生严重错误：{ex.Message}", NotificationType.Error);
                SetControlsEnabled(true);
                if (statusLabel != null) statusLabel.Text = "启动失败";
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
            allOk &= ApplyDisplayTarget(cmbMainScreen, cmbRotation, cmbMainResolution, cmbMainRefreshRate, "主屏");

            if (_isDualDisplay)
            {
                allOk &= ApplyDisplayTarget(cmbSubScreen, cmbSubRotation, cmbSubResolution, cmbSubRefreshRate, "副屏");
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

        // Status
        private void GameProcess_Exited(object sender, EventArgs e)
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
                // ignored
            }

            Dispatcher.UIThread.Post(() =>
            {
                AppendLaunchOutput(abnormalExit
                    ? $"游戏进程异常退出（ExitCode: {exitCode}）。"
                    : "游戏进程已退出。", abnormalExit ? NotificationType.Warning : NotificationType.Information);

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

                if (chkNoRestoreRotation?.IsChecked == true)
                {
                    AppendLaunchOutput("正在恢复显示器参数...");
                    int restoredCount = 0;
                    foreach (var kv in _displayRestoreStates)
                    {
                        if (DisplayConfigure.RestoreDisplaySettings(kv.Value))
                        {
                            restoredCount++;
                        }
                    }
                    AppendLaunchOutput(restoredCount > 0
                        ? $"已恢复 {restoredCount} 个显示器参数。"
                        : "未恢复任何显示器参数。", restoredCount > 0 ? NotificationType.Information : NotificationType.Warning);
                }

                if (statusLabel != null) statusLabel.Text = "就绪";
                SetControlsEnabled(true);
                if (_gameProcess != null)
                {
                    _gameProcess.Dispose();
                    _gameProcess = null;
                }
            });
        }

        // kill process
        private void btnKillProcesses_Click(object sender, RoutedEventArgs e)
        {
            int killedSpice = KillProcessesByName("spice64");
            int killedAsphyxia = KillProcessesByName("asphyxia-core-x64");
            ShowInfoToast("结束进程", $"处理完成：spice64 {killedSpice} 个，asphyxia-core-x64 {killedAsphyxia} 个。");
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
                ShowErrorToast("结束进程失败", $"获取进程列表 {processName} 时出错：{ex.Message}");
                return 0;
            }

            foreach (Process p in processes)
            {
                try
                {
                    int pid = p.Id;

                    // 先尝试正常终止
                    p.Kill();

                    // 等待进程退出，最多等待3秒
                    if (!p.WaitForExit(3000))
                    {
                        // 如果3秒后还没退出，使用强制终止
                        ShowWarningToast("进程未响应", $"{processName}.exe (PID: {pid}) 未响应，尝试强制终止。");

                        try
                        {
                            // 使用 taskkill /F 强制终止
                            ProcessStartInfo taskKillInfo = new ProcessStartInfo
                            {
                                FileName = "taskkill",
                                Arguments = $"/F /PID {pid}",
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            };

                            using (Process taskKillProcess = Process.Start(taskKillInfo))
                            {
                                taskKillProcess.WaitForExit(2000);
                            }
                        }
                        catch (Exception ex)
                        {
                            ShowErrorToast("强制终止失败", ex.Message);
                        }
                    }

                    count++;
                }
                catch (InvalidOperationException)
                {
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    ShowErrorToast("结束进程权限不足", ex.Message);

                    // 尝试使用管理员权限的 taskkill
                    try
                    {
                        ProcessStartInfo taskKillInfo = new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = $"/F /IM {processName}.exe",
                            CreateNoWindow = true,
                            UseShellExecute = true,
                            Verb = "runas" // 请求管理员权限
                        };

                        Process.Start(taskKillInfo);
                    }
                    catch (Exception ex2)
                    {
                        ShowErrorToast("管理员终止失败", ex2.Message);
                    }
                }
                catch (Exception ex)
                {
                    ShowErrorToast("结束进程失败", ex.Message);
                }
                finally
                {
                    try
                    {
                        p.Dispose();
                    }
                    catch { }
                }
            }

            return count;
        }

        // Clear ifs_hook cache
        private void btnClearCache_Click(object sender, RoutedEventArgs e)
        {
            string cachePath = Path.Combine(_contentsDir, "data_mods", "_cache");
            try
            {
                if (Directory.Exists(cachePath))
                {
                    Directory.Delete(cachePath, true);
                    ShowInfoToast("缓存清理", "缓存已成功清除！");
                }
                else
                {
                    ShowWarningToast("缓存清理", "缓存文件不存在。");
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast("缓存清理失败", ex.Message);
            }
        }

        // Edit spicecfg
        private async void btnEditConfig_Click(object sender, RoutedEventArgs e)
        {
            string cfgToolPath = Path.Combine(_contentsDir, "spicecfg.exe");
            string arguments = "";
            if (_usePreconfig)
            {
                arguments = "-cmdoverride -cfgpath lazy/spicetools.xml -patchcfgpath lazy/spicetools_patch_manager.json -modules modules";
            }

            string xmlPath = GetSpiceXmlPath();
            string configPath = GetConfigTomlPath();

            try
            {
                if (!File.Exists(cfgToolPath))
                {
                    ShowErrorToast("无法启动 spicecfg", $"未找到程序: {cfgToolPath}");
                    return;
                }
                if (!File.Exists(xmlPath))
                {
                    ShowErrorToast("无法启动 spicecfg", $"未找到配置文件: {xmlPath}");
                    return;
                }
                if (!File.Exists(configPath))
                {
                    ShowErrorToast("无法启动 spicecfg", $"未找到配置文件: {configPath}");
                    return;
                }

                SetSettingsBusy(true);
                var startInfo = new ProcessStartInfo
                {
                    FileName = cfgToolPath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(cfgToolPath),
                };

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    ShowErrorToast("无法启动 spicecfg", "进程启动失败。");
                    return;
                }

                await process.WaitForExitAsync();

                bool prev = _isLoadingSettings;
                _isLoadingSettings = true;
                try
                {
                    LoadSettings();
                    LoadSpiceConfig();
                    SelectPresetByCurrentFields();
                }
                finally
                {
                    _isLoadingSettings = prev;
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast("启动 spicecfg 失败", ex.Message);
            }
            finally
            {
                SetSettingsBusy(false);
            }
        }

        // Install Runtime
        private void btnInstallRuntime_Click(object sender, RoutedEventArgs e)
        {
            string runtimePath = Path.Combine(_baseDir, "runtime");
            string installBatPath = Path.Combine(runtimePath, "install.bat");

            try
            {
                if (!File.Exists(installBatPath))
                {
                    ShowErrorToast("安装运行库失败", "未找到 runtime/install.bat");
                    return;
                }

                ShowInfoToast("安装运行库", "正在启动 Runtime 安装脚本（可能会弹出 UAC）。");

                var startInfo = new ProcessStartInfo
                {
                    FileName = installBatPath,
                    WorkingDirectory = runtimePath,
                    UseShellExecute = true,
                    Verb = "runas" // 以管理员权限运行
                };

                Process installProcess = Process.Start(startInfo);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                {
                    ShowWarningToast("安装运行库", "用户已取消操作。");
                }
                else
                {
                    ShowErrorToast("安装运行库失败", ex.Message);
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast("安装运行库失败", ex.Message);
            }
        }

        // NVIDIA API,dxvk Library Load
        private void btnLoadCompat_Click(object sender, RoutedEventArgs e)
        {
            if (!ToggleCompatLayer(true, out var error))
            {
                ShowErrorToast("兼容层切换失败", string.IsNullOrWhiteSpace(error) ? "未知错误" : error);
            }
        }

        private void btnUnloadCompat_Click(object sender, RoutedEventArgs e)
        {
            if (!ToggleCompatLayer(false, out var error))
            {
                ShowErrorToast("兼容层切换失败", string.IsNullOrWhiteSpace(error) ? "未知错误" : error);
            }
        }

        private bool ToggleCompatLayer(bool enable, out string error)
        {
            error = string.Empty;
            if (enable)
            {
                if (!ApplyCompatLayerFilesByMode(out error))
                {
                    UpdateCompatLayerStatus();
                    return false;
                }
            }
            else
            {
                if (!RemoveCompatLayerFilesFromModules(out error))
                {
                    UpdateCompatLayerStatus();
                    return false;
                }
            }

            try
            {
                _configFile.WriteString(SettingSectionName, "compatlayerenabled", enable ? "true" : "false");
            }
            catch (Exception ex)
            {
                error = ex.Message;
                UpdateCompatLayerStatus();
                return false;
            }

            UpdateCompatLayerStatus();
            try { UpdateSpiceConfig(new OptionUpdate("sp2x-dx9on12", ResolveDxModeValue(), false)); } catch { }
            return true;
        }

        private bool ApplyCompatLayerFilesByMode(out string error)
        {
            error = string.Empty;
            string stubsDir = Path.Combine(_contentsDir, "lazy", "stubs");
            string modulesDir = Path.Combine(_contentsDir, "modules");
            if (!Directory.Exists(stubsDir))
            {
                error = "未找到 contents/lazy/stubs";
                return false;
            }

            Directory.CreateDirectory(modulesDir);

            string mode = "dx9on12";
            try
            {
                mode = cmbCompatType != null && cmbCompatType.SelectedItem != null
                    ? cmbCompatType.SelectedItem.ToString()
                    : "dx9on12";
            }
            catch { }

            try
            {
                var baseFiles = new[] { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll" };
                foreach (var file in baseFiles)
                {
                    string src = Path.Combine(stubsDir, file);
                    string dst = Path.Combine(modulesDir, file);
                    if (!File.Exists(src))
                    {
                        error = $"缺少文件: {file}";
                        return false;
                    }
                    File.Copy(src, dst, true);
                }

                string d3d9Path = Path.Combine(modulesDir, "d3d9.dll");
                if (File.Exists(d3d9Path))
                {
                    File.Delete(d3d9Path);
                }

                if (string.Equals(mode, "dxvk", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mode, "dx9on12_external", StringComparison.OrdinalIgnoreCase))
                {
                    string stubName = string.Equals(mode, "dxvk", StringComparison.OrdinalIgnoreCase)
                        ? "d3d9.dll.dxvk"
                        : "d3d9.dll.dx9on12";
                    string src = Path.Combine(stubsDir, stubName);
                    if (!File.Exists(src))
                    {
                        error = $"缺少文件: {stubName}";
                        return false;
                    }

                    File.Copy(src, d3d9Path, true);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool RemoveCompatLayerFilesFromModules(out string error)
        {
            error = string.Empty;
            string modulesDir = Path.Combine(_contentsDir, "modules");
            try
            {
                var files = new[] { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll", "d3d9.dll" };
                foreach (var file in files)
                {
                    string path = Path.Combine(modulesDir, file);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private async void tglCompatLayer_IsCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings || _isUpdatingCompatUi)
            {
                return;
            }

            bool enable = tglCompatLayer?.IsChecked == true;
            if (!ToggleCompatLayer(enable, out var error))
            {
                ShowErrorToast("兼容层切换失败", string.IsNullOrWhiteSpace(error) ? "未知错误" : error);
                return;
            }

            ShowInfoToast("兼容层状态已更新", enable ? "已启用 AMD/Intel 显卡兼容层。" : "已关闭 AMD/Intel 显卡兼容层。");
            SaveSettings();
            await Task.CompletedTask;
        }

        private void rbCompatMode_IsCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings || _isUpdatingCompatUi)
            {
                return;
            }

            if (cmbCompatType == null)
            {
                return;
            }

            string selected = "dx9on12";
            if (rbCompatDxvk?.IsChecked == true)
            {
                selected = "dxvk";
            }
            else if (rbCompatDx9on12External?.IsChecked == true)
            {
                selected = "dx9on12_external";
            }
            _isSyncingModel = true;
            try
            {
                cmbCompatType.SelectedItem = selected;
            }
            finally
            {
                _isSyncingModel = false;
            }

            UpdateSpiceConfig(new OptionUpdate("sp2x-dx9on12", ResolveDxModeValue(), false));
            SaveSettings();
        }

        // Kill process when exit bootstrap
        private void Bootstrap_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
            try
            {
                if (_gameProcess != null && !_gameProcess.HasExited)
                    _gameProcess.Kill();
            }
            catch (Exception) { /* ignored */ }
        }

        private void SetControlsEnabled(bool enabled)
        {
            if (btnStart != null) btnStart.IsEnabled = enabled;
            if (btnLoadCompat != null) btnLoadCompat.IsEnabled = enabled;
            if (btnUnloadCompat != null) btnUnloadCompat.IsEnabled = enabled;
            if (cmbCompatType != null) cmbCompatType.IsEnabled = enabled;

            // Options group controls
            if (chkUsePreconfig != null) chkUsePreconfig.IsEnabled = enabled;
            if (chkWindowed != null) chkWindowed.IsEnabled = enabled;
            if (chkNoAsphyxia != null) chkNoAsphyxia.IsEnabled = enabled;
            if (chkNoRestoreRotation != null) chkNoRestoreRotation.IsEnabled = enabled;
            if (btnEditConfig != null) btnEditConfig.IsEnabled = enabled;
            if (cmbServerPreset != null) cmbServerPreset.IsEnabled = enabled;
            if (btnAddServerPreset != null) btnAddServerPreset.IsEnabled = enabled;
            if (btnDeleteServerPreset != null) btnDeleteServerPreset.IsEnabled = enabled;
            if (tglCompatLayer != null) tglCompatLayer.IsEnabled = enabled;
            if (rbCompatDx9on12 != null) rbCompatDx9on12.IsEnabled = enabled;
            if (rbCompatDx9on12External != null) rbCompatDx9on12External.IsEnabled = enabled;
            if (rbCompatDxvk != null) rbCompatDxvk.IsEnabled = enabled;
            if (btnOpenLog != null) btnOpenLog.IsEnabled = enabled;
            if (btnTouchPanel != null) btnTouchPanel.IsEnabled = enabled;
            if (btnGotoGameSettings != null) btnGotoGameSettings.IsEnabled = enabled;
            if (chkAdvNetDump != null) chkAdvNetDump.IsEnabled = enabled;
            if (chkAdvAsphyxiaDebug != null) chkAdvAsphyxiaDebug.IsEnabled = enabled;
            if (chkAdvDisableSubDisplay != null) chkAdvDisableSubDisplay.IsEnabled = enabled;
            if (cmbAdvWindowMode != null) cmbAdvWindowMode.IsEnabled = enabled;
            if (chkAdvPCoreOptimization != null) chkAdvPCoreOptimization.IsEnabled = enabled;
            if (chkAdvSubBorderless != null) chkAdvSubBorderless.IsEnabled = enabled;
            if (chkAdvShowCursorTouchSim != null) chkAdvShowCursorTouchSim.IsEnabled = enabled;
            if (chkAdvWindowTopMost != null) chkAdvWindowTopMost.IsEnabled = enabled;
            if (txtAdvWindowSize != null) txtAdvWindowSize.IsEnabled = enabled;
            if (chkAdvSingleAdapter != null) chkAdvSingleAdapter.IsEnabled = enabled;
            if (chkAdvSubWindowTopMost != null) chkAdvSubWindowTopMost.IsEnabled = enabled;
            if (chkAdvSubForceRender != null) chkAdvSubForceRender.IsEnabled = enabled;
            if (chkAdvNativeTouch != null) chkAdvNativeTouch.IsEnabled = enabled;
            if (txtAdvAsioDriver != null) txtAdvAsioDriver.IsEnabled = enabled;
            if (chkAdvCardIo != null) chkAdvCardIo.IsEnabled = enabled;
            if (chkAdvHidSmartCard != null) chkAdvHidSmartCard.IsEnabled = enabled;
            if (txtServerAddress != null) txtServerAddress.IsEnabled = enabled;
            if (txtPcbId != null) txtPcbId.IsEnabled = enabled;
            if (tglDisplayConfigEnabled != null) tglDisplayConfigEnabled.IsEnabled = enabled;
            if (cmbDisplayMode != null) cmbDisplayMode.IsEnabled = enabled;
            if (cmbMainScreen != null) cmbMainScreen.IsEnabled = enabled;
            if (cmbMainResolution != null) cmbMainResolution.IsEnabled = enabled;
            if (cmbMainRefreshRate != null) cmbMainRefreshRate.IsEnabled = enabled;
            if (cmbSubScreen != null) cmbSubScreen.IsEnabled = enabled;
            if (cmbSubRotation != null) cmbSubRotation.IsEnabled = enabled;
            if (cmbSubResolution != null) cmbSubResolution.IsEnabled = enabled;
            if (cmbSubRefreshRate != null) cmbSubRefreshRate.IsEnabled = enabled;
            if (cmbRotation != null) cmbRotation.IsEnabled = enabled;
            if (btnPreviewDisplaySettings != null) btnPreviewDisplaySettings.IsEnabled = enabled;
            if (btnSelectMainScreenArea != null) btnSelectMainScreenArea.IsEnabled = enabled;
            if (btnSelectSubScreenArea != null) btnSelectSubScreenArea.IsEnabled = enabled && _isDualDisplay;

            if (btnClearCache != null) btnClearCache.IsEnabled = enabled;
            if (btnInstallRuntime != null) btnInstallRuntime.IsEnabled = enabled;
            if (btnAddFirewallRule != null) btnAddFirewallRule.IsEnabled = enabled;
            if (btnAudioPanel != null) btnAudioPanel.IsEnabled = enabled;
            if (btnKillProcesses != null) btnKillProcesses.IsEnabled = true; // 始终启用

            // After enabling, re-apply compat layer status logic
            if (enabled)
            {
                UpdateCompatLayerStatus();
                UpdateDisplayLayoutControlsEnabled();
            }
        }

        private async void btnAddFirewallRule_Click(object sender, RoutedEventArgs e)
        {
            const string ruleName = "SpiceTools";
            string spicePath = GetSpicePath();

            var dialogBuilder = _dialogManager
                .CreateDialog()
                .OfType(NotificationType.Warning)
                .WithTitle("添加防火墙规则")
                .WithContent("确定要执行吗？\n如果之前已经添加过，请勿重复添加")
                .WithYesNoResult("确定", "取消", "Flat")
                .Dismiss().ByClickingBackground();
            ApplyDialogNotificationIcon(dialogBuilder, NotificationType.Warning);
            var confirmed = await dialogBuilder.TryShowAsync();

            if (!confirmed)
            {
                return;
            }

            if (!File.Exists(spicePath))
            {
                ShowErrorToast("添加防火墙规则失败", $"未找到目标程序：{spicePath}");
                return;
            }

            try
            {
                var addProcessInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow program=\"{spicePath}\" enable=yes profile=public,private",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };

                using (var addProcess = Process.Start(addProcessInfo))
                {
                    addProcess.WaitForExit();
                    ShowInfoToast("防火墙规则", "防火墙规则添加完成。");
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                {
                    ShowWarningToast("防火墙规则", "用户取消了 UAC 提示，防火墙规则未添加。");
                }
                else
                {
                    ShowErrorToast("防火墙规则失败", ex.Message);
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast("防火墙规则失败", ex.Message);
            }
        }

        private async void btnOpenLog_Click(object sender, RoutedEventArgs e)
        {
            await ShowLogDialogAsync();
        }

        private async void btnToggleLaunchLog_Click(object sender, RoutedEventArgs e)
        {
            if (_isLaunchLogVisible)
            {
                HideLaunchLogArea();
                return;
            }

            await ShowLaunchLogAreaWithAnimationAsync();
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

        private async Task ShowLogDialogAsync()
        {
            try
            {
                string logPath = Path.Combine(_contentsDir, "log.txt");
                if (!File.Exists(logPath))
                {
                    ShowErrorToast("查看 log 失败", $"未找到日志文件: {logPath}");
                    return;
                }

                string content = await File.ReadAllTextAsync(logPath, Encoding.UTF8);
                if (string.IsNullOrEmpty(content))
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
                    OpenLogFolderAndSelectFile(logPath);
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast("查看 log 失败", ex.Message);
            }
        }

        private static void OpenLogFolderAndSelectFile(string logPath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{logPath}\"",
                UseShellExecute = true
            });
        }

        private void btnAudioPanel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "control.exe",
                    Arguments = "mmsys.cpl",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                ShowErrorToast("打开音频控制面板失败", ex.Message);
            }
        }

        private void btnTouchPanel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "control.exe",
                    Arguments = "/name Microsoft.TabletPCSettings",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                ShowErrorToast("打开触控屏设置失败", ex.Message);
            }
        }

        private void btnGotoGameSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (mainSideMenu != null)
                {
                    var target = mainSideMenu.Items?.OfType<object>().Skip(1).FirstOrDefault();
                    if (target != null)
                    {
                        mainSideMenu.SelectedItem = target;
                    }
                }
            }
            catch { }
        }

        private void UpdateSpiceConfig(params OptionUpdate[] updates)
        {
            try
            {
                if (updates == null || updates.Length == 0)
                {
                    updates = BuildDefaultOptionUpdates().ToArray();
                }

                if (updates.Length == 0) return;

                string spiceXmlPath = GetSpiceXmlPath();
                if (!File.Exists(spiceXmlPath))
                {
                    ShowErrorToast("保存设定失败", "未找到 spicetools.xml，已恢复到上一次状态。");
                    RestoreUiFromLastKnownSpiceValues();
                    return;
                }

                if (!TryGetSpiceOptionsContext(LoadOptions.PreserveWhitespace, true, out var context))
                {
                    ShowErrorToast("保存设定失败", "配置写入失败，已恢复到上一次状态。");
                    RestoreUiFromLastKnownSpiceValues();
                    return;
                }

                var doc = context.Document;
                var soundVoltex = context.SoundVoltex;
                var options = context.OptionsElement;

                string newline = "\r\n";
                string optionsIndent = ExtractIndentation(options.PreviousNode as XText, ref newline) ?? string.Empty;
                string indentStep = DetermineIndentStep(soundVoltex, ref newline) ?? new string(' ', 4);

                string optionIndent = ExtractIndentation(options.Elements("option").FirstOrDefault()?.PreviousNode as XText, ref newline);
                if (string.IsNullOrEmpty(optionIndent))
                {
                    optionIndent = optionsIndent + indentStep;
                }

                string optionLinePrefix = newline + optionIndent;
                string closingLinePrefix = newline + optionsIndent;
                var closingWhitespace = EnsureClosingWhitespace(options, closingLinePrefix);

                foreach (var update in updates)
                {
                    if (update == null || string.IsNullOrEmpty(update.Name)) continue;

                    context.OptionLookup.TryGetValue(update.Name, out var existing);

                    if (existing == null)
                    {
                        if (update.ShouldRemove || string.IsNullOrEmpty(update.Value))
                        {
                            continue;
                        }

                        if (closingWhitespace == null)
                        {
                            closingWhitespace = EnsureClosingWhitespace(options, closingLinePrefix);
                        }

                        closingWhitespace.AddBeforeSelf(new XText(optionLinePrefix));
                        var newOpt = new XElement("option",
                            new XAttribute("name", update.Name),
                            new XAttribute("value", update.Value ?? string.Empty));
                        closingWhitespace.AddBeforeSelf(newOpt);
                        context.OptionLookup[update.Name] = newOpt;
                        continue;
                    }

                    if (update.ShouldRemove)
                    {
                        var whitespace = existing.PreviousNode as XText;
                        existing.Remove();
                        if (whitespace != null && string.IsNullOrWhiteSpace(whitespace.Value))
                        {
                            whitespace.Remove();
                        }
                        context.OptionLookup.Remove(update.Name);
                        continue;
                    }

                    existing.SetAttributeValue("value", update.Value ?? string.Empty);
                }

                var settings = new XmlWriterSettings
                {
                    Indent = false,
                    NewLineHandling = NewLineHandling.None,
                    Encoding = new System.Text.UTF8Encoding(false),
                    OmitXmlDeclaration = false,
                    NewLineChars = newline,
                    NewLineOnAttributes = false
                };
                using (var writer = XmlWriter.Create(context.FilePath, settings))
                {
                    doc.Save(writer);
                }

                NormalizeSelfClosingTags(context.FilePath);
            }
            catch (Exception ex)
            {
                ShowErrorToast("保存设定失败", ex.Message);
            }
        }

        private void LoadSpiceConfig()
        {
            bool previousLoadingState = _isLoadingSettings;
            _isLoadingSettings = true;
            try
            {
                if (!TryGetSpiceOptionsContext(LoadOptions.PreserveWhitespace, false, out var context))
                {
                    return;
                }

                string GetValue(string name) => context.GetOptionValue(name);

                CacheLastKnownSpiceValue("w", GetValue("w"));
                CacheLastKnownSpiceValue("sp2x-processefficiency", GetValue("sp2x-processefficiency"));
                CacheLastKnownSpiceValue("sp2x-sdvxnosub", GetValue("sp2x-sdvxnosub"));
                CacheLastKnownSpiceValue("sp2x-windowborder", GetValue("sp2x-windowborder"));
                CacheLastKnownSpiceValue("sdvxwsubborderless", GetValue("sdvxwsubborderless"));
                CacheLastKnownSpiceValue("s", GetValue("s"));
                CacheLastKnownSpiceValue("sp2x-windowalwaysontop", GetValue("sp2x-windowalwaysontop"));
                CacheLastKnownSpiceValue("sp2x-windowsize", GetValue("sp2x-windowsize"));
                CacheLastKnownSpiceValue("graphics-force-single-adapter", GetValue("graphics-force-single-adapter"));
                CacheLastKnownSpiceValue("sdvxwsubtop", GetValue("sdvxwsubtop"));
                CacheLastKnownSpiceValue("sp2x-sdvxsubredraw", GetValue("sp2x-sdvxsubredraw"));
                CacheLastKnownSpiceValue("sdvxnativetouch", GetValue("sdvxnativetouch"));
                CacheLastKnownSpiceValue("sp2x-sdvxasio", GetValue("sp2x-sdvxasio"));
                CacheLastKnownSpiceValue("cardio", GetValue("cardio"));
                CacheLastKnownSpiceValue("scard", GetValue("scard"));
                CacheLastKnownSpiceValue("netdump", GetValue("netdump"));
                CacheLastKnownSpiceValue("url", GetValue("url"));
                CacheLastKnownSpiceValue("p", GetValue("p"));

                // 游戏相关复选项从 XML 读取
                var wVal = GetValue("w");
                bool windowed = string.Equals(wVal, "/ENABLED", StringComparison.OrdinalIgnoreCase);
                if (chkWindowed != null) chkWindowed.IsChecked = windowed;

                var peVal = GetValue("sp2x-processefficiency");
                _advPCoreOptimization = string.Equals(peVal, "pcores", StringComparison.OrdinalIgnoreCase);

                // 高级选项缓存
                _advDisableSubDisplay = string.Equals(GetValue("sp2x-sdvxnosub"), "/ENABLED", StringComparison.Ordinal);
                var wborder = GetValue("sp2x-windowborder");
                if (string.Equals(wborder, "1", StringComparison.Ordinal)) _advWindowModeIndex = 1;
                else if (string.Equals(wborder, "2", StringComparison.Ordinal)) _advWindowModeIndex = 2;
                else _advWindowModeIndex = 0;
                _advSubBorderless = string.Equals(GetValue("sdvxwsubborderless"), "/ENABLED", StringComparison.Ordinal);
                _advShowCursorTouchSim = string.Equals(GetValue("s"), "/ENABLED", StringComparison.Ordinal);
                _advWindowTopMost = string.Equals(GetValue("sp2x-windowalwaysontop"), "/ENABLED", StringComparison.Ordinal);
                _advWindowSize = GetValue("sp2x-windowsize") ?? string.Empty;
                _advSingleAdapter = string.Equals(GetValue("graphics-force-single-adapter"), "/ENABLED", StringComparison.Ordinal);
                _advSubWindowTopMost = string.Equals(GetValue("sdvxwsubtop"), "/ENABLED", StringComparison.Ordinal);
                _advSubForceRender = string.Equals(GetValue("sp2x-sdvxsubredraw"), "/ENABLED", StringComparison.Ordinal);
                _advNativeTouch = string.Equals(GetValue("sdvxnativetouch"), "/ENABLED", StringComparison.Ordinal);
                _advAsioDriver = GetValue("sp2x-sdvxasio") ?? string.Empty;
                _advCardIo = string.Equals(GetValue("cardio"), "/ENABLED", StringComparison.Ordinal);
                _advHidSmartCard = string.Equals(GetValue("scard"), "/ENABLED", StringComparison.Ordinal);
                _dbgNetDump = string.Equals(GetValue("netdump"), "/ENABLED", StringComparison.Ordinal);
                if (txtServerAddress != null) txtServerAddress.Text = GetValue("url");
                if (txtPcbId != null) txtPcbId.Text = GetValue("p");

                // 回填高级选项页面控件
                if (chkAdvNetDump != null) chkAdvNetDump.IsChecked = _dbgNetDump;
                if (chkAdvAsphyxiaDebug != null) chkAdvAsphyxiaDebug.IsChecked = _dbgAsphyxiaDebug;
                if (chkAdvDisableSubDisplay != null) chkAdvDisableSubDisplay.IsChecked = _advDisableSubDisplay;
                if (cmbAdvWindowMode != null) cmbAdvWindowMode.SelectedIndex = _advWindowModeIndex;
                if (chkAdvPCoreOptimization != null) chkAdvPCoreOptimization.IsChecked = _advPCoreOptimization;
                if (chkAdvSubBorderless != null) chkAdvSubBorderless.IsChecked = _advSubBorderless;
                if (chkAdvShowCursorTouchSim != null) chkAdvShowCursorTouchSim.IsChecked = _advShowCursorTouchSim;
                if (chkAdvWindowTopMost != null) chkAdvWindowTopMost.IsChecked = _advWindowTopMost;
                if (txtAdvWindowSize != null) txtAdvWindowSize.Text = _advWindowSize;
                if (chkAdvSingleAdapter != null) chkAdvSingleAdapter.IsChecked = _advSingleAdapter;
                if (chkAdvSubWindowTopMost != null) chkAdvSubWindowTopMost.IsChecked = _advSubWindowTopMost;
                if (chkAdvSubForceRender != null) chkAdvSubForceRender.IsChecked = _advSubForceRender;
                if (chkAdvNativeTouch != null) chkAdvNativeTouch.IsChecked = _advNativeTouch;
                if (txtAdvAsioDriver != null) txtAdvAsioDriver.Text = _advAsioDriver;
                if (chkAdvCardIo != null) chkAdvCardIo.IsChecked = _advCardIo;
                if (chkAdvHidSmartCard != null) chkAdvHidSmartCard.IsChecked = _advHidSmartCard;

                SelectPresetByCurrentFields();
            }
            catch (Exception ex)
            {
                ShowErrorToast("读取配置失败", ex.Message);
            }
            finally
            {
                _isLoadingSettings = previousLoadingState;
            }
        }

        private void CacheLastKnownSpiceValue(string key, string value)
        {
            _lastKnownSpiceValues[key] = value ?? string.Empty;
        }

        private string GetLastKnownSpiceValue(string key)
        {
            return _lastKnownSpiceValues.TryGetValue(key, out var value) ? value : string.Empty;
        }

        private void RestoreUiFromLastKnownSpiceValues()
        {
            if (_lastKnownSpiceValues.Count == 0)
            {
                return;
            }

            bool previousLoadingState = _isLoadingSettings;
            _isLoadingSettings = true;
            try
            {
                if (chkWindowed != null)
                {
                    chkWindowed.IsChecked = string.Equals(GetLastKnownSpiceValue("w"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                }

                _advPCoreOptimization = string.Equals(GetLastKnownSpiceValue("sp2x-processefficiency"), "pcores", StringComparison.OrdinalIgnoreCase);
                _advDisableSubDisplay = string.Equals(GetLastKnownSpiceValue("sp2x-sdvxnosub"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _advSubBorderless = string.Equals(GetLastKnownSpiceValue("sdvxwsubborderless"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _advShowCursorTouchSim = string.Equals(GetLastKnownSpiceValue("s"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _advWindowTopMost = string.Equals(GetLastKnownSpiceValue("sp2x-windowalwaysontop"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _advWindowSize = GetLastKnownSpiceValue("sp2x-windowsize");
                _advSingleAdapter = string.Equals(GetLastKnownSpiceValue("graphics-force-single-adapter"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _advSubWindowTopMost = string.Equals(GetLastKnownSpiceValue("sdvxwsubtop"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _advSubForceRender = string.Equals(GetLastKnownSpiceValue("sp2x-sdvxsubredraw"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _advNativeTouch = string.Equals(GetLastKnownSpiceValue("sdvxnativetouch"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _advAsioDriver = GetLastKnownSpiceValue("sp2x-sdvxasio");
                _advCardIo = string.Equals(GetLastKnownSpiceValue("cardio"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _advHidSmartCard = string.Equals(GetLastKnownSpiceValue("scard"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _dbgNetDump = string.Equals(GetLastKnownSpiceValue("netdump"), "/ENABLED", StringComparison.OrdinalIgnoreCase);

                var wborder = GetLastKnownSpiceValue("sp2x-windowborder");
                if (string.Equals(wborder, "1", StringComparison.Ordinal)) _advWindowModeIndex = 1;
                else if (string.Equals(wborder, "2", StringComparison.Ordinal)) _advWindowModeIndex = 2;
                else _advWindowModeIndex = 0;

                if (chkAdvDisableSubDisplay != null) chkAdvDisableSubDisplay.IsChecked = _advDisableSubDisplay;
                if (chkAdvNetDump != null) chkAdvNetDump.IsChecked = _dbgNetDump;
                if (chkAdvPCoreOptimization != null) chkAdvPCoreOptimization.IsChecked = _advPCoreOptimization;
                if (chkAdvSubBorderless != null) chkAdvSubBorderless.IsChecked = _advSubBorderless;
                if (chkAdvShowCursorTouchSim != null) chkAdvShowCursorTouchSim.IsChecked = _advShowCursorTouchSim;
                if (chkAdvWindowTopMost != null) chkAdvWindowTopMost.IsChecked = _advWindowTopMost;
                if (txtAdvWindowSize != null) txtAdvWindowSize.Text = _advWindowSize;
                if (chkAdvSingleAdapter != null) chkAdvSingleAdapter.IsChecked = _advSingleAdapter;
                if (chkAdvSubWindowTopMost != null) chkAdvSubWindowTopMost.IsChecked = _advSubWindowTopMost;
                if (chkAdvSubForceRender != null) chkAdvSubForceRender.IsChecked = _advSubForceRender;
                if (chkAdvNativeTouch != null) chkAdvNativeTouch.IsChecked = _advNativeTouch;
                if (txtAdvAsioDriver != null) txtAdvAsioDriver.Text = _advAsioDriver;
                if (chkAdvCardIo != null) chkAdvCardIo.IsChecked = _advCardIo;
                if (chkAdvHidSmartCard != null) chkAdvHidSmartCard.IsChecked = _advHidSmartCard;
                if (cmbAdvWindowMode != null) cmbAdvWindowMode.SelectedIndex = _advWindowModeIndex;

                if (txtServerAddress != null) txtServerAddress.Text = GetLastKnownSpiceValue("url");
                if (txtPcbId != null) txtPcbId.Text = GetLastKnownSpiceValue("p");

                SelectPresetByCurrentFields();
            }
            finally
            {
                _isLoadingSettings = previousLoadingState;
            }
        }

        private IEnumerable<OptionUpdate> BuildDefaultOptionUpdates()
        {
            yield return new OptionUpdate("w", chkWindowed != null && chkWindowed.IsChecked == true ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sp2x-processefficiency", _advPCoreOptimization ? "pcores" : string.Empty);
            yield return new OptionUpdate("sp2x-dx9on12", ResolveDxModeValue(), false);
            yield return new OptionUpdate("sp2x-sdvxnosub", _advDisableSubDisplay ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sp2x-windowborder", ResolveWindowBorderValue());
            yield return new OptionUpdate("sdvxwsubborderless", _advSubBorderless ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("s", _advShowCursorTouchSim ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sp2x-windowalwaysontop", _advWindowTopMost ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sp2x-windowsize", _advWindowSize ?? string.Empty);
            yield return new OptionUpdate("graphics-force-single-adapter", _advSingleAdapter ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sdvxwsubtop", _advSubWindowTopMost ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sp2x-sdvxsubredraw", _advSubForceRender ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sdvxnativetouch", _advNativeTouch ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sp2x-sdvxasio", _advAsioDriver ?? string.Empty);
            yield return new OptionUpdate("cardio", _advCardIo ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("scard", _advHidSmartCard ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("netdump", _dbgNetDump ? "/ENABLED" : string.Empty);
            if (txtServerAddress != null) yield return new OptionUpdate("url", txtServerAddress.Text ?? string.Empty, false);
            if (txtPcbId != null) yield return new OptionUpdate("p", txtPcbId.Text ?? string.Empty, false);
        }

        private bool EnsureSpiceXmlExistsForTextOrRevert(TextBox textBox, string optionName)
        {
            var xmlPath = GetSpiceXmlPath();
            if (File.Exists(xmlPath))
            {
                return true;
            }

            ShowErrorToast("保存设定失败", "未找到 spicetools.xml，已恢复到上一次状态。");

            _isUpdatingSpiceToggleUi = true;
            try
            {
                if (textBox != null)
                {
                    textBox.Text = GetLastKnownSpiceValue(optionName);
                }
            }
            finally
            {
                _isUpdatingSpiceToggleUi = false;
            }

            return false;
        }

        private string ResolveWindowBorderValue()
        {
            switch (_advWindowModeIndex)
            {
                case 1:
                    return "1";
                case 2:
                    return "2";
                default:
                    return string.Empty;
            }
        }

        private string ResolveDxModeValue()
        {
            try
            {
                bool compatEnabled = IsCompatLayerEffectivelyEnabled();
                if (!compatEnabled) return "0";

                var compat = cmbCompatType != null && cmbCompatType.SelectedItem != null
                    ? cmbCompatType.SelectedItem.ToString()
                    : "dx9on12";
                return string.Equals(compat, "dx9on12", StringComparison.OrdinalIgnoreCase) ? "1" : "0";
            }
            catch { return "0"; }
        }

        private bool IsCompatLayerEffectivelyEnabled()
        {
            try
            {
                int fileCount = GetCompatLayerFileCount();
                return fileCount >= 1 || IsCompatLayerEnabledConfigured();
            }
            catch { return IsCompatLayerEnabledConfigured(); }
        }

        private bool TryGetSpiceOptionsContext(LoadOptions loadOptions, bool createOptionsWhenMissing, out SpiceOptionsContext context)
        {
            context = null;
            string spiceXmlPath = GetSpiceXmlPath();
            if (!File.Exists(spiceXmlPath))
            {
                return false;
            }

            var doc = XDocument.Load(spiceXmlPath, loadOptions);
            var root = doc.Root;
            if (root == null)
            {
                ShowErrorToast("读取配置失败", "SpiceTools 配置 XML 根节点为空。");
                return false;
            }

            var soundVoltex = root.Elements("game").FirstOrDefault(g =>
            {
                var nameAttr = g.Attribute("name");
                return nameAttr != null && string.Equals(nameAttr.Value, "Sound Voltex", StringComparison.OrdinalIgnoreCase);
            });
            if (soundVoltex == null)
            {
                ShowWarningToast("读取配置异常", "未找到游戏条目: Sound Voltex。");
                return false;
            }

            var options = soundVoltex.Element("options");
            if (options == null)
            {
                if (createOptionsWhenMissing)
                {
                    options = new XElement("options");
                    soundVoltex.Add(options);
                }
                else
                {
                    return false;
                }
            }

            var lookup = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (var option in options.Elements("option"))
            {
                var nameAttr = option.Attribute("name");
                if (nameAttr == null) continue;
                var key = nameAttr.Value;
                if (!lookup.ContainsKey(key))
                {
                    lookup[key] = option;
                }
            }

            context = new SpiceOptionsContext(spiceXmlPath, doc, soundVoltex, options, lookup);
            return true;
        }

        private string ExtractIndentation(XText textNode, ref string newlineChars)
        {
            if (textNode == null) return null;

            var text = textNode.Value;
            if (text.Contains("\r\n")) newlineChars = "\r\n";
            else if (text.Contains("\n")) newlineChars = "\n";
            else if (text.Contains("\r")) newlineChars = "\r";

            int lastNewlineIndex = text.LastIndexOf('\n');
            if (lastNewlineIndex < text.LastIndexOf('\r'))
            {
                lastNewlineIndex = text.LastIndexOf('\r');
            }

            if (lastNewlineIndex >= 0 && lastNewlineIndex + 1 < text.Length)
            {
                int start = lastNewlineIndex + 1;
                while (start < text.Length && (text[start] == '\r' || text[start] == '\n'))
                {
                    start++;
                }
                return text.Substring(start);
            }

            return text;
        }

        private string DetermineIndentStep(XElement parentElement, ref string newlineChars)
        {
            if (parentElement == null) return null;

            foreach (var container in parentElement.Elements())
            {
                if (!container.HasElements) continue;

                var containerIndent = ExtractIndentation(container.PreviousNode as XText, ref newlineChars);
                var child = container.Elements().FirstOrDefault();
                var childIndent = ExtractIndentation(child?.PreviousNode as XText, ref newlineChars);

                if (!string.IsNullOrEmpty(containerIndent) && !string.IsNullOrEmpty(childIndent) && childIndent.StartsWith(containerIndent))
                {
                    return childIndent.Substring(containerIndent.Length);
                }
            }

            return null;
        }

        private XText EnsureClosingWhitespace(XElement optionsElement, string desiredValue)
        {
            var lastNode = optionsElement.Nodes().LastOrDefault();
            if (lastNode is XText textNode)
            {
                textNode.Value = desiredValue;
                return textNode;
            }

            var newTextNode = new XText(desiredValue);
            optionsElement.Add(newTextNode);
            return newTextNode;
        }

        private void NormalizeSelfClosingTags(string filePath)
        {
            try
            {
                var original = File.ReadAllText(filePath, Encoding.UTF8);
                var normalized = Regex.Replace(original, "(?<=\\S)[ \\\\\\t]+/>", "/>");
                if (!string.Equals(original, normalized, StringComparison.Ordinal))
                {
                    File.WriteAllText(filePath, normalized, new UTF8Encoding(false));
                }
            }
            catch (Exception ex)
            {
                ShowWarningToast("配置格式修复失败", ex.Message);
            }
        }

        private sealed class SpiceOptionsContext
        {
            public string FilePath { get; }
            public XDocument Document { get; }
            public XElement SoundVoltex { get; }
            public XElement OptionsElement { get; }
            public Dictionary<string, XElement> OptionLookup { get; }

            public SpiceOptionsContext(string filePath, XDocument document, XElement soundVoltex, XElement optionsElement, Dictionary<string, XElement> optionLookup)
            {
                FilePath = filePath;
                Document = document;
                SoundVoltex = soundVoltex;
                OptionsElement = optionsElement;
                OptionLookup = optionLookup;
            }

            public string GetOptionValue(string name)
            {
                if (OptionLookup.TryGetValue(name, out var element))
                {
                    return element.Attribute("value")?.Value ?? string.Empty;
                }
                return string.Empty;
            }
        }

        private sealed class OptionUpdate
        {
            public string Name { get; }
            public string Value { get; }
            public bool RemoveWhenEmpty { get; }

            public OptionUpdate(string name, string value, bool removeWhenEmpty = false)
            {
                Name = name;
                Value = value ?? string.Empty;
                RemoveWhenEmpty = removeWhenEmpty;
            }

            public bool ShouldRemove => RemoveWhenEmpty && string.IsNullOrEmpty(Value);
        }
    }
}
