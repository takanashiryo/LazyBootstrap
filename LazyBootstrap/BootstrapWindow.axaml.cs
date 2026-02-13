// written by Arkito aka Takanashi Ryo, only release in SDVX Lazy Pack.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SukiUI.Controls;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace LazyBootstrap
{
    public partial class BootstrapWindow : SukiWindow
    {
        private Process _gameProcess;
        private readonly ConfigHandler _configFile;
        private bool _usePreconfig = true; // 是否使用预配置文件
        private bool _isLoadingSettings = false; // 标志：是否正在加载设置

        // 统一路径前缀
        private readonly string _baseDir;
        private readonly string _contentsDir;

        private string _compatTypeTooltipCache;

        private bool _advDisableSubDisplay = false;
        private int _advWindowModeIndex = 0; // 0: 默认, 1: 无边框, 2: 可变窗口
        private bool _advSubBorderless = false;
        private bool _advShowCursorTouchSim = false;
        private bool _advPCoreOptimization = false;

        private bool _dbgNetDump = false;
        private bool _dbgAsphyxiaDebug = false;
        private bool _displayConfigEnabled = false;
        private bool _isDualDisplay = true;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        private const int DISPLAY_DEVICE_ACTIVE = 0x1;
        private const int DISPLAY_DEVICE_MIRRORING_DRIVER = 0x8;

        public BootstrapWindow()
        {
            InitializeComponent();

            if (Design.IsDesignMode)
            {
                return;
            }

            // 优先使用启动器传递的根目录，否则使用当前程序所在目录
            var envBaseDir = Environment.GetEnvironmentVariable("LAZYBOOTSTRAP_BASEDIR");
            _baseDir = !string.IsNullOrEmpty(envBaseDir) ? envBaseDir : AppDomain.CurrentDomain.BaseDirectory;
            _contentsDir = Path.Combine(_baseDir, "contents");

            string configFilePath = Path.Combine(_baseDir, "config.toml");
            bool newConfigCreated = !System.IO.File.Exists(configFilePath);
            _configFile = new ConfigHandler(configFilePath);

            if (newConfigCreated)
            {
                _configFile.WriteString("Settings", "usepreconfig", "true");
                _configFile.WriteString("Settings", "noasphyxia", "false");
                _configFile.WriteString("Settings", "norestorerotation", "false");
                _configFile.WriteString("Settings", "compatlayerenabled", "false");
            }

            InitializeCustomComponents();
            LogSystem.Log("本包体免费，如果你是付费获取的，请窒息");
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
                        new OptionUpdate("s", _advShowCursorTouchSim ? "/ENABLED" : string.Empty)
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
                    LogSystem.Log("服务器配置已更新。");
                }
            }
            catch (Exception ex)
            {
                LogSystem.Log($"打开服务器管理对话框时出错: {ex.Message}", LogSystem.LogLevel.Error);
            }
        }

        private void InitializeCustomComponents()
        {
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

            // 勾选项实时更新：窗口化
            if (chkWindowed != null)
            {
                chkWindowed.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
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
                    if (_isLoadingSettings) return;
                    _dbgNetDump = chkAdvNetDump.IsChecked == true;
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
                    if (_isLoadingSettings) return;
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
                    if (_isLoadingSettings) return;
                    _advPCoreOptimization = chkAdvPCoreOptimization.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sp2x-processefficiency", _advPCoreOptimization ? "pcores" : string.Empty));
                };
            }
            if (chkAdvSubBorderless != null)
            {
                chkAdvSubBorderless.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    _advSubBorderless = chkAdvSubBorderless.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("sdvxwsubborderless", _advSubBorderless ? "/ENABLED" : string.Empty));
                };
            }
            if (chkAdvShowCursorTouchSim != null)
            {
                chkAdvShowCursorTouchSim.IsCheckedChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    _advShowCursorTouchSim = chkAdvShowCursorTouchSim.IsChecked == true;
                    UpdateSpiceConfig(new OptionUpdate("s", _advShowCursorTouchSim ? "/ENABLED" : string.Empty));
                };
            }

            // 使用预配置文件
            if (chkUsePreconfig != null)
            {
                chkUsePreconfig.IsCheckedChanged += chkUsePreconfig_CheckedChanged;
            }

            // 服务器设定（并入游戏设定页）
            if (txtServerAddress != null)
            {
                txtServerAddress.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    UpdateSpiceConfig(new OptionUpdate("url", txtServerAddress.Text ?? string.Empty, false));
                };
            }
            if (txtPcbId != null)
            {
                txtPcbId.TextChanged += (s, e) =>
                {
                    if (_isLoadingSettings) return;
                    UpdateSpiceConfig(new OptionUpdate("p", txtPcbId.Text ?? string.Empty, false));
                };
            }

            if (txtLogOutput != null)
            {
                LogSystem.Initialize(txtLogOutput);
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
            List<string> monitorNames = GetMonitorNames();

            if (cmbMainScreen != null && cmbMainScreen.Items.Count == 0)
            {
                if (monitorNames.Count > 0)
                {
                    for (int i = 0; i < monitorNames.Count; i++)
                    {
                        cmbMainScreen.Items.Add(monitorNames[i]);
                        if (cmbSubScreen != null) cmbSubScreen.Items.Add(monitorNames[i]);
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

            if (cmbSubRotation != null && cmbSubRotation.Items.Count == 0)
            {
                cmbSubRotation.Items.Add("0");
                cmbSubRotation.Items.Add("90");
                cmbSubRotation.Items.Add("180");
                cmbSubRotation.Items.Add("270");
                cmbSubRotation.SelectedIndex = 0;
            }

            void FillResolution(ComboBox combo)
            {
                if (combo == null || combo.Items.Count > 0) return;
                combo.Items.Add("1920x1080");
                combo.Items.Add("1280x720");
                combo.Items.Add("2560x1440");
                combo.Items.Add("3840x2160");
                combo.SelectedIndex = 0;
            }

            FillResolution(cmbMainResolution);
            FillResolution(cmbSubResolution);

            if (txtMainRefreshRate != null && string.IsNullOrWhiteSpace(txtMainRefreshRate.Text)) txtMainRefreshRate.Text = "60";
            if (txtSubRefreshRate != null && string.IsNullOrWhiteSpace(txtSubRefreshRate.Text)) txtSubRefreshRate.Text = "60";

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
                    SaveSettings();
                };
            }

            UpdateDisplayLayoutControlsEnabled();
        }

        private void UpdateDisplayLayoutControlsEnabled()
        {
            bool enabled = _displayConfigEnabled;
            if (cmbMainScreen != null) cmbMainScreen.IsEnabled = enabled;
            if (cmbRotation != null) cmbRotation.IsEnabled = enabled;
            if (cmbMainResolution != null) cmbMainResolution.IsEnabled = enabled;
            if (txtMainRefreshRate != null) txtMainRefreshRate.IsEnabled = enabled;
            bool subEnabled = enabled && _isDualDisplay;
            if (cardSubScreen != null) cardSubScreen.IsVisible = _isDualDisplay;
            if (cmbSubScreen != null) cmbSubScreen.IsEnabled = subEnabled;
            if (cmbSubRotation != null) cmbSubRotation.IsEnabled = subEnabled;
            if (cmbSubResolution != null) cmbSubResolution.IsEnabled = subEnabled;
            if (txtSubRefreshRate != null) txtSubRefreshRate.IsEnabled = subEnabled;
            if (btnApplyDisplaySettings != null) btnApplyDisplaySettings.IsEnabled = enabled;
        }

        private List<string> GetMonitorNames()
        {
            var names = new List<string>();
            try
            {
                var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                uint adapterIndex = 0;
                while (true)
                {
                    var adapter = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                    if (!EnumDisplayDevices(null, adapterIndex, ref adapter, 0))
                    {
                        break;
                    }

                    bool adapterActive = (adapter.StateFlags & DISPLAY_DEVICE_ACTIVE) != 0;
                    bool adapterMirroring = (adapter.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER) != 0;
                    if (!adapterActive || adapterMirroring)
                    {
                        adapterIndex++;
                        continue;
                    }

                    uint monitorIndex = 0;
                    while (true)
                    {
                        var monitor = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                        if (!EnumDisplayDevices(adapter.DeviceName, monitorIndex, ref monitor, 0))
                        {
                            break;
                        }

                        bool monitorActive = (monitor.StateFlags & DISPLAY_DEVICE_ACTIVE) != 0;
                        if (monitorActive)
                        {
                            var name = string.IsNullOrWhiteSpace(monitor.DeviceString)
                                ? monitor.DeviceName?.Trim()
                                : monitor.DeviceString.Trim();

                            if (!string.IsNullOrWhiteSpace(name) && dedup.Add(name))
                            {
                                names.Add(name);
                            }
                        }

                        monitorIndex++;
                    }

                    adapterIndex++;
                }
            }
            catch
            {
            }

            return names;
        }

        private bool TryGetRotationAngle(out int angle)
        {
            angle = 0;
            if (cmbRotation == null) return true;
            var selected = cmbRotation.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selected)) return true; // 不选则按 0 处理
            return int.TryParse(selected, out angle);
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

        private void LoadSettings()
        {
            try
            {
                _isLoadingSettings = true; // 标记正在加载设置
                // 加载预配置选项
                string usePreconfigStr = _configFile.ReadString("Settings", "usepreconfig", "true");
                if (!bool.TryParse(usePreconfigStr, out _usePreconfig))
                {
                    _usePreconfig = true;
                }
                if (chkUsePreconfig != null)
                {
                    chkUsePreconfig.IsChecked = _usePreconfig;
                }

                // 加载其他启动选项（窗口化与大小核由 XML 驱动，故不再从 config.toml 读取）
                chkNoAsphyxia.IsChecked = bool.TryParse(_configFile.ReadString("Settings", "noasphyxia", "false"), out var noAsphyxia) && noAsphyxia;
                bool noRestoreRotation = bool.TryParse(_configFile.ReadString("Settings", "norestorerotation", "false"), out var noRestore) && noRestore;
                chkNoRestoreRotation.IsChecked = !noRestoreRotation;

                _displayConfigEnabled = bool.TryParse(_configFile.ReadString("Settings", "displayconfigure", "false"), out var displayCfg) && displayCfg;
                if (tglDisplayConfigEnabled != null) tglDisplayConfigEnabled.IsChecked = _displayConfigEnabled;
                _isDualDisplay = !string.Equals(_configFile.ReadString("Display", "mode", "dual"), "single", StringComparison.OrdinalIgnoreCase);
                if (cmbDisplayMode != null) cmbDisplayMode.SelectedIndex = _isDualDisplay ? 1 : 0;

                // 渲染模式（兼容层实现）
                try
                {
                    string renderMode = _configFile.ReadString("Settings", "rendermode", "dx9on12");
                    if (cmbCompatType != null)
                    {
                        // 保证项存在
                        if (cmbCompatType.Items.Count == 0)
                        {
                            cmbCompatType.Items.Add("dx9on12");
                            cmbCompatType.Items.Add("dxvk");
                        }
                        int idx = 0;
                        if (string.Equals(renderMode, "dxvk", StringComparison.OrdinalIgnoreCase)) idx = 1;
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

                if (cmbMainScreen != null)
                {
                    int.TryParse(_configFile.ReadString("Display", "mainscreen", "0"), out var mainScreenIndex);
                    if (mainScreenIndex >= 0 && mainScreenIndex < cmbMainScreen.Items.Count) cmbMainScreen.SelectedIndex = mainScreenIndex;
                }
                if (cmbSubScreen != null)
                {
                    int.TryParse(_configFile.ReadString("Display", "subscreen", "0"), out var subScreenIndex);
                    if (subScreenIndex >= 0 && subScreenIndex < cmbSubScreen.Items.Count) cmbSubScreen.SelectedIndex = subScreenIndex;
                }
                if (cmbSubRotation != null)
                {
                    int.TryParse(_configFile.ReadString("Display", "subrotation", "0"), out var subRotationIndex);
                    if (subRotationIndex >= 0 && subRotationIndex < cmbSubRotation.Items.Count) cmbSubRotation.SelectedIndex = subRotationIndex;
                }
                if (cmbMainResolution != null)
                {
                    var res = _configFile.ReadString("Display", "mainresolution", "");
                    if (!string.IsNullOrWhiteSpace(res)) cmbMainResolution.SelectedItem = res;
                }
                if (cmbSubResolution != null)
                {
                    var res = _configFile.ReadString("Display", "subresolution", "");
                    if (!string.IsNullOrWhiteSpace(res)) cmbSubResolution.SelectedItem = res;
                }
                if (txtMainRefreshRate != null)
                {
                    txtMainRefreshRate.Text = _configFile.ReadString("Display", "mainrefresh", txtMainRefreshRate.Text ?? "60");
                }
                if (txtSubRefreshRate != null)
                {
                    txtSubRefreshRate.Text = _configFile.ReadString("Display", "subrefresh", txtSubRefreshRate.Text ?? "60");
                }

                UpdateDisplayLayoutControlsEnabled();
            }
            catch (Exception ex)
            {
                LogSystem.Log($"加载配置文件时出错: {ex.Message}", LogSystem.LogLevel.Error);
                if (txtCurrentVersion != null) txtCurrentVersion.Text = "读取失败";
                if (txtRevision != null) txtRevision.Text = "读取失败";
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
                _configFile.WriteString("Settings", "usepreconfig", _usePreconfig.ToString().ToLowerInvariant());

                // 保存仍通过 config.toml 管理的选项
                if (chkNoAsphyxia != null)
                    _configFile.WriteString("Settings", "noasphyxia", (chkNoAsphyxia.IsChecked == true).ToString().ToLowerInvariant());
                if (chkNoRestoreRotation != null)
                    _configFile.WriteString("Settings", "norestorerotation", (chkNoRestoreRotation.IsChecked != true).ToString().ToLowerInvariant());

                // 保存渲染模式（兼容层实现）
                if (cmbCompatType != null && cmbCompatType.SelectedItem != null)
                {
                    string renderMode = cmbCompatType.SelectedItem.ToString();
                    _configFile.WriteString("Settings", "rendermode", renderMode);
                }

                _configFile.WriteString("Settings", "displayconfigure", _displayConfigEnabled.ToString().ToLowerInvariant());
                _configFile.WriteString("Display", "mode", _isDualDisplay ? "dual" : "single");

                if (cmbMainScreen != null) _configFile.WriteString("Display", "mainscreen", cmbMainScreen.SelectedIndex.ToString());
                if (cmbSubScreen != null) _configFile.WriteString("Display", "subscreen", cmbSubScreen.SelectedIndex.ToString());
                if (cmbSubRotation != null) _configFile.WriteString("Display", "subrotation", cmbSubRotation.SelectedIndex.ToString());
                if (cmbMainResolution != null && cmbMainResolution.SelectedItem != null) _configFile.WriteString("Display", "mainresolution", cmbMainResolution.SelectedItem.ToString());
                if (cmbSubResolution != null && cmbSubResolution.SelectedItem != null) _configFile.WriteString("Display", "subresolution", cmbSubResolution.SelectedItem.ToString());
                if (txtMainRefreshRate != null) _configFile.WriteString("Display", "mainrefresh", txtMainRefreshRate.Text ?? "");
                if (txtSubRefreshRate != null) _configFile.WriteString("Display", "subrefresh", txtSubRefreshRate.Text ?? "");
            }
            catch (Exception ex)
            {
                LogSystem.Log($"保存出错: {ex.Message}", LogSystem.LogLevel.Error);
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

        // 使用预配置文件复选框事件
        private void chkUsePreconfig_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings)
            {
                return; // 如果正在加载设置，不显示警告
            }

            if (chkUsePreconfig.IsChecked != true)
            {
                // 用户取消勾选，日志记录警告
                LogSystem.Log("警告：如果不使用预配置文件，你需要重新手动设置所有选项与补丁（等同于纯净版），请确保你拥有相关知识！", LogSystem.LogLevel.Warning);
            }

            _usePreconfig = chkUsePreconfig.IsChecked == true;
            SaveSettings();

            var activeXmlPath = GetSpiceXmlPath();
            LogSystem.Log(_usePreconfig
                ? $"当前使用预配置 XML: {activeXmlPath}"
                : $"当前使用系统 XML: {activeXmlPath}");

            LoadSpiceConfig();
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
                var s = _configFile.ReadString("Settings", "compatlayerenabled", "false");
                bool enabled;
                return bool.TryParse(s, out enabled) && enabled;
            }
            catch { return false; }
        }

        // 更新兼容层状态指示器
        private void UpdateCompatLayerStatus()
        {
            if (lblCompatStatus == null) return;

            int fileCount = GetCompatLayerFileCount();

            if (fileCount == 3)
            {
                // 所有文件都存在
                lblCompatStatus.Text = "● 已启用";
                lblCompatStatus.Foreground = Avalonia.Media.Brushes.Green;
            }
            else if (fileCount >= 1 && fileCount <= 2)
            {
                // 部分文件存在
                lblCompatStatus.Text = "● 已启用，但可能不完整";
                lblCompatStatus.Foreground = Avalonia.Media.Brushes.Orange;
            }
            else
            {
                // 没有文件
                lblCompatStatus.Text = "● 未启用";
                lblCompatStatus.Foreground = Avalonia.Media.Brushes.Red;
            }

            // 启用状态下禁用兼容层实现下拉，需先关闭再更改；同时"启用"按钮禁用、"关闭"按钮启用
            bool effectiveEnabled = fileCount >= 1 || IsCompatLayerEnabledConfigured();
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

                // 记录开始
                LogSystem.Log("开始环境检测...");

                // 执行检测
                await EnvironmentScan.RunAsync((progress, message) =>
                {
                    if (!string.IsNullOrEmpty(message))
                    {
                        LogSystem.Log(message);
                    }

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

                LogSystem.Log("环境检测完成。\r\n");

                // 检测异常弹窗
                if (EnvironmentScan.LastHadError)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("(｡>﹏<｡) 啊哇哇，Near检测到你的系统可能缺少必要的运行组件！");
                    sb.AppendLine();
                    sb.AppendLine("(* ^∇^)ﾉ Noah给出的解决方法：");
                    sb.AppendLine("- 点击启动器\u201c安装运行库\u201d按钮安装必要运行组件");
                    sb.AppendLine("- 确保已安装最新的显卡驱动程序");
                    sb.AppendLine("- 若为 AMD/Intel 显卡，请启用\u201c显卡兼容层\u201d后重试");
                    sb.AppendLine();
                    sb.AppendLine("如果\u201c系统媒体功能包\u201d异常：");
                    sb.AppendLine("- 检查\u201cWindows 功能\u201d中是否启用了\u201c媒体功能包\u201d");
                    sb.AppendLine();
                    sb.AppendLine("请注意！由于环境不同，这个提示可能会误报！");
                    sb.AppendLine("您可先行尝试启动游戏，若出现问题，再寻求周围帮助。");

                    LogSystem.Log(sb.ToString(), LogSystem.LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                LogSystem.Log($"环境检测时发生错误: {ex.Message}", LogSystem.LogLevel.Error);
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

        // Boot
        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            // Lock Element
            SetControlsEnabled(false);
            if (statusLabel != null) statusLabel.Text = "正在启动...";
            if (txtLogOutput != null)
            {
                try
                {
                    txtLogOutput.Text = string.Empty;
                }
                catch { }
            }

            try
            {
                // Screen Rotate
                LogSystem.Log("正在旋转屏幕...");
                if (!TryGetRotationAngle(out int rotationAngle)) rotationAngle = 0;
                string deviceName = null;
                try
                {
                    deviceName = GetPrimaryScreenDeviceName();
                }
                catch { deviceName = null; }
                bool rotationSuccess = false;
                if (!string.IsNullOrEmpty(deviceName))
                {
                    rotationSuccess = ScreenRotate.Rotate(deviceName, rotationAngle);
                }
                LogSystem.Log(rotationSuccess ? $"屏幕已旋转至 {rotationAngle} 度。" : "屏幕旋转失败或无需旋转。");
                await Task.Delay(500); // 等待旋转生效

                // Launch Asphyxia
                if (chkNoAsphyxia?.IsChecked != true)
                {
                    LogSystem.Log("\n正在启动 Asphyxia Core...");
                    string asphyxiaPath = GetAsphyxiaPath();
                    if (File.Exists(asphyxiaPath))
                    {
                        if (_dbgAsphyxiaDebug)
                        {
                            var asphyxiaStartInfo = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = $"/k \"\"{asphyxiaPath}\" --dev\"",
                                WorkingDirectory = Path.GetDirectoryName(asphyxiaPath)
                            };
                            Process.Start(asphyxiaStartInfo);
                            LogSystem.Log("  - 使用调试模式启动（控制台窗口将保持打开）");
                        }
                        else
                        {
                            var asphyxiaStartInfo = new ProcessStartInfo
                            {
                                FileName = asphyxiaPath,
                                WorkingDirectory = Path.GetDirectoryName(asphyxiaPath)
                            };
                            Process.Start(asphyxiaStartInfo);
                        }

                        LogSystem.Log("Asphyxia Core 已启动。");
                    }
                    else
                    {
                        LogSystem.Log($"错误: 未找到 Asphyxia Core, 路径: {asphyxiaPath}", LogSystem.LogLevel.Error);
                        SetControlsEnabled(true);
                        if (statusLabel != null) statusLabel.Text = "启动失败";
                        return;
                    }
                }
                else
                {
                    LogSystem.Log("\n已跳过启动 Asphyxia Core。");
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

                // NetDump
                if (_dbgNetDump)
                {
                    argsBuilder.Append("-netdump ");
                }

                // Booting
                LogSystem.Log("\n正在启动游戏...");
                LogSystem.Log($"  - 启动参数: {argsBuilder.ToString()}");

                string spicePath = GetSpicePath();
                if (!File.Exists(spicePath))
                {
                    LogSystem.Log($"\n错误: 未找到游戏主程序, 路径: {spicePath}", LogSystem.LogLevel.Error);
                    SetControlsEnabled(true);
                    if (statusLabel != null) statusLabel.Text = "启动失败";
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = spicePath,
                    Arguments = argsBuilder.ToString(),
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(spicePath)
                };

                _gameProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                _gameProcess.Exited += GameProcess_Exited;

                _gameProcess.Start();

                if (statusLabel != null) statusLabel.Text = "游戏已启动";
                LogSystem.Log("\n游戏进程已启动。");
            }
            catch (Exception ex)
            {
                LogSystem.Log($"\n启动过程中发生严重错误: {ex.Message}", LogSystem.LogLevel.Error);
                SetControlsEnabled(true);
                if (statusLabel != null) statusLabel.Text = "启动失败";
            }
        }

        // 获取主屏幕设备名
        private string GetPrimaryScreenDeviceName()
        {
            var devMode = new ScreenRotate.DEVMODE();
            devMode.dmSize = (short)System.Runtime.InteropServices.Marshal.SizeOf<ScreenRotate.DEVMODE>();
            for (int i = 0; i < 16; i++)
            {
                string name = $"\\\\.\\DISPLAY{i + 1}";
                if (ScreenRotate.EnumDisplaySettings(name, ScreenRotate.ENUM_CURRENT_SETTINGS, ref devMode))
                {
                    return name;
                }
            }
            return null;
        }

        // Status
        private void GameProcess_Exited(object sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                LogSystem.Log("\n游戏进程已退出。");

                LogSystem.Log("正在关闭 Asphyxia Core...");
                try
                {
                    KillProcessesByName("asphyxia-core-x64");
                    LogSystem.Log("Asphyxia Core 已关闭");
                }
                catch (Exception ex)
                {
                    LogSystem.Log($"未找到正在运行的 Asphyxia Core 进程。{ex.Message}", LogSystem.LogLevel.Warning);
                }

                if (chkNoRestoreRotation?.IsChecked == true)
                {
                    LogSystem.Log("正在还原屏幕旋转...");
                    string deviceName = null;
                    try { deviceName = GetPrimaryScreenDeviceName(); } catch { deviceName = null; }
                    bool restored = false;
                    if (!string.IsNullOrEmpty(deviceName))
                    {
                        restored = ScreenRotate.Rotate(deviceName, 0);
                    }
                    LogSystem.Log(restored ? "屏幕旋转已还原为 0 度。" : "屏幕旋转还原失败。");
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
            LogSystem.Log("\n正在尝试结束所有相关进程...");
            KillProcessesByName("spice64");
            KillProcessesByName("asphyxia-core-x64");
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
                LogSystem.Log($"  - 获取进程列表 {processName} 时出错: {ex.Message}", LogSystem.LogLevel.Error);
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
                        LogSystem.Log($"  - 进程 {processName} (PID: {pid}) 未响应，尝试强制终止...", LogSystem.LogLevel.Warning);

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

                            LogSystem.Log($"  - 已强制结束进程: {processName}.exe (PID: {pid})");
                        }
                        catch (Exception ex)
                        {
                            LogSystem.Log($"  - 强制终止进程失败: {ex.Message}", LogSystem.LogLevel.Error);
                        }
                    }
                    else
                    {
                        LogSystem.Log($"  - 已结束进程: {processName}.exe (PID: {pid})");
                    }

                    count++;
                }
                catch (InvalidOperationException)
                {
                    LogSystem.Log($"  - 进程 {processName} 已退出，无需结束。");
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    LogSystem.Log($"  - 结束进程 {processName} 时权限不足: {ex.Message}", LogSystem.LogLevel.Error);

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
                        LogSystem.Log($"  - 已使用管理员权限尝试终止 {processName}");
                    }
                    catch (Exception ex2)
                    {
                        LogSystem.Log($"  - 管理员权限终止失败: {ex2.Message}", LogSystem.LogLevel.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogSystem.Log($"  - 结束进程 {processName} 时出错: {ex.Message}", LogSystem.LogLevel.Error);
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

        private bool ApplyCurrentRotation()
        {
            try
            {
                if (!TryGetRotationAngle(out int angle)) angle = 0;
                string deviceName = null;
                try { deviceName = GetPrimaryScreenDeviceName(); } catch { deviceName = null; }
                if (string.IsNullOrEmpty(deviceName))
                {
                    LogSystem.Log("无法获取主显示器信息，已取消旋转。", LogSystem.LogLevel.Error);
                    return false;
                }

                bool success = ScreenRotate.Rotate(deviceName, angle);
                LogSystem.Log(success ? $"屏幕旋转至 {angle} 度成功。" : "屏幕旋转失败。",
                    success ? LogSystem.LogLevel.Info : LogSystem.LogLevel.Error);
                return success;
            }
            catch (Exception ex)
            {
                LogSystem.Log($"旋转时发生错误: {ex.Message}", LogSystem.LogLevel.Error);
                return false;
            }
        }

        // ScreenRotate manually
        private void btnSwitchRotation_Click(object sender, RoutedEventArgs e)
        {
            ApplyCurrentRotation();
        }

        private void btnApplyDisplaySettings_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            ApplyCurrentRotation();
            LogSystem.Log("显示配置已应用。", LogSystem.LogLevel.Info);
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
                    LogSystem.Log("缓存已成功清除！");
                }
                else
                {
                    LogSystem.Log("缓存文件不存在。", LogSystem.LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                LogSystem.Log($"清除缓存失败: {ex.Message}", LogSystem.LogLevel.Error);
            }
        }

        // Edit spicecfg
        private void btnEditConfig_Click(object sender, RoutedEventArgs e)
        {
            string cfgToolPath = Path.Combine(_contentsDir, "spicecfg.exe");
            string arguments = "";
            if (_usePreconfig)
            {
                arguments = "-cmdoverride -cfgpath lazy/spicetools.xml -patchcfgpath lazy/spicetools_patch_manager.json -modules modules";
            }

            try
            {
                if (!File.Exists(cfgToolPath))
                {
                    LogSystem.Log($"未找到编辑器: {cfgToolPath}", LogSystem.LogLevel.Error);
                    return;
                }
                var startInfo = new ProcessStartInfo
                {
                    FileName = cfgToolPath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(cfgToolPath),
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                LogSystem.Log($"启动编辑器失败: {ex.Message}", LogSystem.LogLevel.Error);
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
                    LogSystem.Log($"错误: 未找到 runtime/install.bat", LogSystem.LogLevel.Error);
                    return;
                }

                LogSystem.Log("\n正在安装 Runtime 组件...");
                LogSystem.Log($"  - 执行脚本: {installBatPath}");

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
                    LogSystem.Log("用户已取消。", LogSystem.LogLevel.Warning);
                }
                else
                {
                    LogSystem.Log($"启动 Runtime 安装失败: {ex.Message}", LogSystem.LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                LogSystem.Log($"启动 Runtime 安装时发生错误: {ex.Message}", LogSystem.LogLevel.Error);
            }
        }

        // NVIDIA API,dxvk Library Load
        private void btnLoadCompat_Click(object sender, RoutedEventArgs e)
        {
            ToggleCompatLayer(true);
        }

        private void btnUnloadCompat_Click(object sender, RoutedEventArgs e)
        {
            ToggleCompatLayer(false);
        }

        private void ToggleCompatLayer(bool enable)
        {
            string source = enable
                ? Path.Combine("contents", "lazy", "stubs")
                : Path.Combine("contents", "modules");
            string dest = enable
                ? Path.Combine("contents", "modules")
                : Path.Combine("contents", "lazy", "stubs");

            MoveCompatFiles(source, dest, enable ? "载入" : "卸载");
            try
            {
                _configFile.WriteString("Settings", "compatlayerenabled", enable ? "true" : "false");
                LogSystem.Log($"已记录兼容层状态: {(enable ? "启用" : "关闭")}");
            }
            catch { }

            UpdateCompatLayerStatus();
            try { UpdateSpiceConfig(new OptionUpdate("sp2x-dx9on12", ResolveDxModeValue(), false)); } catch { }
        }

        private void MoveCompatFiles(string sourceDirRel, string destDirRel, string operationName)
        {
            LogSystem.Log($"NVIDIA API 准备{operationName}...");
            string sourceDir = Path.Combine(_baseDir, sourceDirRel);
            string destDir = Path.Combine(_baseDir, destDirRel);
            List<string> filesToMoveList = new List<string> { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll" };

            // dxvk d3d9.dll if needed
            try
            {
                string compat = cmbCompatType != null && cmbCompatType.SelectedItem != null
                    ? cmbCompatType.SelectedItem.ToString()
                    : "dx9on12";
                if (string.Equals(compat, "dxvk", StringComparison.OrdinalIgnoreCase))
                {
                    LogSystem.Log($"DXVK 准备{operationName}...");
                    filesToMoveList.Add("d3d9.dll");
                }
            }
            catch { }

            string[] filesToMove = filesToMoveList.ToArray();

            int successCount = 0;
            int skippedCount = 0;
            try
            {
                if (!Directory.Exists(sourceDir))
                {
                    LogSystem.Log($"源文件夹 '{sourceDirRel}' 不存在!", LogSystem.LogLevel.Error);
                    return;
                }
                Directory.CreateDirectory(destDir);

                foreach (var fileName in filesToMove)
                {
                    string sourcePath = Path.Combine(sourceDir, fileName);
                    string destPath = Path.Combine(destDir, fileName);

                    if (File.Exists(sourcePath))
                    {
                        if (File.Exists(destPath))
                        {
                            File.Delete(destPath);
                            LogSystem.Log($"已删除旧的目标文件: {destPath}");
                        }
                        File.Move(sourcePath, destPath);
                        LogSystem.Log($"成功移动: {fileName}");
                        successCount++;
                    }
                    else
                    {
                        LogSystem.Log($"源文件不存在，跳过: {sourcePath}", LogSystem.LogLevel.Warning);
                        skippedCount++;
                    }
                }

                if (skippedCount == filesToMove.Length)
                {
                    LogSystem.Log($"操作'{operationName}'完成，但所有源文件都不存在，未移动任何文件。", LogSystem.LogLevel.Warning);
                }
                else
                {
                    string resultMessage = $"{operationName}完成。成功移动 {successCount}/{filesToMove.Length} 个文件。";
                    LogSystem.Log(resultMessage);
                }
            }
            catch (Exception ex)
            {
                LogSystem.Log($"{operationName}失败: {ex.Message}", LogSystem.LogLevel.Error);
            }
            finally
            {
                UpdateCompatLayerStatus();
            }
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
            if (txtServerAddress != null) txtServerAddress.IsEnabled = enabled;
            if (txtPcbId != null) txtPcbId.IsEnabled = enabled;
            if (tglDisplayConfigEnabled != null) tglDisplayConfigEnabled.IsEnabled = enabled;
            if (cmbDisplayMode != null) cmbDisplayMode.IsEnabled = enabled;
            if (cmbMainScreen != null) cmbMainScreen.IsEnabled = enabled;
            if (cmbMainResolution != null) cmbMainResolution.IsEnabled = enabled;
            if (txtMainRefreshRate != null) txtMainRefreshRate.IsEnabled = enabled;
            if (cmbSubScreen != null) cmbSubScreen.IsEnabled = enabled;
            if (cmbSubRotation != null) cmbSubRotation.IsEnabled = enabled;
            if (cmbSubResolution != null) cmbSubResolution.IsEnabled = enabled;
            if (txtSubRefreshRate != null) txtSubRefreshRate.IsEnabled = enabled;
            if (cmbRotation != null) cmbRotation.IsEnabled = enabled;
            if (btnApplyDisplaySettings != null) btnApplyDisplaySettings.IsEnabled = enabled;

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

        private void btnAddFirewallRule_Click(object sender, RoutedEventArgs e)
        {
            const string ruleName = "SpiceTools";
            string spicePath = GetSpicePath();
            FirewallHelper.EnsureFirewallRule(ruleName, spicePath, LogSystem.Log);
        }

        private void btnOpenLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string logPath = Path.Combine(_contentsDir, "log.txt");
                string folderPath = _contentsDir;
                if (Directory.Exists(folderPath))
                {
                    if (File.Exists(logPath))
                    {
                        // 打开资源管理器并选中 log.txt
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{logPath}\"",
                            UseShellExecute = true
                        });
                        LogSystem.Log($"已打开日志所在文件夹并选中: {logPath}");
                    }
                    else
                    {
                        LogSystem.Log($"未找到日志文件: {folderPath}", LogSystem.LogLevel.Warning);
                    }
                }
                else
                {
                    LogSystem.Log($"未找到日志文件夹: {folderPath}", LogSystem.LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                LogSystem.Log($"打开文件夹失败: {ex.Message}", LogSystem.LogLevel.Error);
            }
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
                LogSystem.Log($"打开音频控制面板失败: {ex.Message}", LogSystem.LogLevel.Error);
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
                LogSystem.Log($"打开触控屏设置失败: {ex.Message}", LogSystem.LogLevel.Error);
            }
        }

        private void btnGotoGameSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (mainSideMenu != null)
                {
                    var target = mainSideMenu.Items?.OfType<object>().Skip(2).FirstOrDefault();
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

                if (!TryGetSpiceOptionsContext(LoadOptions.PreserveWhitespace, true, out var context))
                {
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
                LogSystem.Log($"更新 SpiceTools 配置失败: {ex.Message}", LogSystem.LogLevel.Error);
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
            }
            catch (Exception ex)
            {
                LogSystem.Log($"读取 XML 失败: {ex.Message}", LogSystem.LogLevel.Error);
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
            if (txtServerAddress != null) yield return new OptionUpdate("url", txtServerAddress.Text ?? string.Empty, false);
            if (txtPcbId != null) yield return new OptionUpdate("p", txtPcbId.Text ?? string.Empty, false);
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
                return string.Equals(compat, "dxvk", StringComparison.OrdinalIgnoreCase) ? "0" : "1";
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
                LogSystem.Log("未找到 SpiceTools 配置文件，无法读取或更新选项。", LogSystem.LogLevel.Warning);
                return false;
            }

            var doc = XDocument.Load(spiceXmlPath, loadOptions);
            var root = doc.Root;
            if (root == null)
            {
                LogSystem.Log("SpiceTools 配置 XML 根节点为空。", LogSystem.LogLevel.Error);
                return false;
            }

            var soundVoltex = root.Elements("game").FirstOrDefault(g =>
            {
                var nameAttr = g.Attribute("name");
                return nameAttr != null && string.Equals(nameAttr.Value, "Sound Voltex", StringComparison.OrdinalIgnoreCase);
            });
            if (soundVoltex == null)
            {
                LogSystem.Log("未找到游戏条目: Sound Voltex。", LogSystem.LogLevel.Warning);
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
                LogSystem.Log($"规范化自闭合标签格式失败: {ex.Message}", LogSystem.LogLevel.Warning);
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
