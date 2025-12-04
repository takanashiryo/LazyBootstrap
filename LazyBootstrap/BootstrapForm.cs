// written by Arkito aka Takanashi Ryo, only release in SDVX Lazy Pack.
// Encoding: UTF-8 with BOM
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LazyBootstrap
{
    public partial class BootstrapForm : Form
    {
        private Process _gameProcess;
        private readonly ConfigHandler _configFile;
        private readonly ConfigHandler _versionFile;
        private bool _usePreconfig = true; // 是否使用预配置文件（默认勾选）
        private bool _isLoadingSettings = false; // 标志：是否正在加载设置

        // 统一路径前缀
        private readonly string _baseDir;
        private readonly string _contentsDir;

        // 缓存兼容层实现下拉框原始 tooltip，避免禁用时闪烁
        private string _compatTypeTooltipCache;

        // 高级选项选择的缓存（不改动原有布局与配置文件结构）
        private bool _advDisableSubDisplay = false;
        private int _advWindowModeIndex = 0; // 0: 默认, 1: 无边框, 2: 可变窗口
        private bool _advSubBorderless = false;
        private bool _advShowCursorTouchSim = false;

        // 调试与 NetDump 选项缓存（不依赖隐藏控件）
        private bool _dbgNetDump = false;
        private bool _dbgAsphyxiaDebug = false;

        public BootstrapForm()
        {
            InitializeComponent();

            _baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _contentsDir = Path.Combine(_baseDir, "contents");

            string configFilePath = Path.Combine(_baseDir, "config.ini");
            string versionFilePath = Path.Combine(_baseDir, "version.ini");
            bool newConfigCreated = !System.IO.File.Exists(configFilePath);
            bool newVersionCreated = !System.IO.File.Exists(versionFilePath);
            _configFile = new ConfigHandler(configFilePath);
            _versionFile = new ConfigHandler(versionFilePath);

            if (newVersionCreated)
            {
                // 创建版本文件并写入默认版本与修订号
                _versionFile.WriteString("Version", "version", "YYYYMMDD");
                _versionFile.WriteString("Version", "revision", "0");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_versionFile.ReadString("Version", "version", "")))
                {
                    _versionFile.WriteString("Version", "version", "YYYYMMDD");
                }
                if (string.IsNullOrWhiteSpace(_versionFile.ReadString("Version", "revision", "")))
                {
                    _versionFile.WriteString("Version", "revision", "0");
                }
            }

            if (newConfigCreated)
            {
                _configFile.WriteString("Settings", "usepreconfig", "true");
                _configFile.WriteString("Settings", "windowed", "false");
                _configFile.WriteString("Settings", "pcoreopt", "false");
                _configFile.WriteString("Settings", "noasphyxia", "false");
                _configFile.WriteString("Settings", "norestorerotation", "false");
                _configFile.WriteString("Settings", "compatlayerenabled", "false");
            }

            InitializeCustomComponents();
            LogSystem.Log("本包体免费，如果你是付费获取的，请窒息");
            LoadSettings();

            // 绑定高级选项菜单项与复选框的同步
            try
            {
                if (menuItemNetDump != null) menuItemNetDump.Checked = chkNetDump != null && chkNetDump.Checked;
                if (menuItemAsphyxiaDebug != null) menuItemAsphyxiaDebug.Checked = chkAsphyxiaDebug != null && chkAsphyxiaDebug.Checked;
            }
            catch { }

            // 窗体显示后进行环境检测
            this.Shown += async (s, e) =>
            {
                await RunEnvironmentScanAsync();
            };
        }

        private void btnAdvancedOptions_Click(object sender, EventArgs e)
        {
            try
            {
                var dlg = new AdvancedOptionsForm
                {
                    NetDumpEnabled = _dbgNetDump,
                    AsphyxiaDebugEnabled = _dbgAsphyxiaDebug,
                    DisableSubDisplay = _advDisableSubDisplay,
                    WindowModeIndex = _advWindowModeIndex,
                    SubBorderless = _advSubBorderless,
                    ShowCursorAndTouchSim = _advShowCursorTouchSim
                };
                var result = dlg.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    // 读取对话框选择并缓存
                    _dbgNetDump = dlg.NetDumpEnabled;
                    _dbgAsphyxiaDebug = dlg.AsphyxiaDebugEnabled;

                    // 同步菜单项勾选状态
                    if (menuItemNetDump != null) menuItemNetDump.Checked = _dbgNetDump;
                    if (menuItemAsphyxiaDebug != null) menuItemAsphyxiaDebug.Checked = _dbgAsphyxiaDebug;

                    // 缓存高级选项选择
                    _advDisableSubDisplay = dlg.DisableSubDisplay;
                    _advWindowModeIndex = dlg.WindowModeIndex;
                    _advSubBorderless = dlg.SubBorderless;
                    _advShowCursorTouchSim = dlg.ShowCursorAndTouchSim;
                }
            }
            catch { }
        }

        private void menuItemNetDump_Click(object sender, EventArgs e)
        {
            if (menuItemNetDump != null)
            {
                _dbgNetDump = menuItemNetDump.Checked;
            }
        }

        private void menuItemAsphyxiaDebug_Click(object sender, EventArgs e)
        {
            if (menuItemAsphyxiaDebug != null)
            {
                _dbgAsphyxiaDebug = menuItemAsphyxiaDebug.Checked;
            }
        }

        private void InitializeCustomComponents()
        {
            // 设置默认值和下拉列表项
            cmbRotation.Items.AddRange(new object[] { "0", "90", "180", "270" });
            cmbRotation.SelectedIndex = 0;

            // 兼容层类型默认值
            if (cmbCompatType != null)
            {
                if (cmbCompatType.Items.Count == 0)
                {
                    cmbCompatType.Items.AddRange(new object[] { "dx9on12", "dxvk" });
                }
                cmbCompatType.SelectedIndex = 0;
                if (toolTip0 != null)
                {
                    _compatTypeTooltipCache = toolTip0.GetToolTip(cmbCompatType);
                }
            }

            LogSystem.Initialize(txtLogOutput);

            if (this.Controls.Find("statusStrip1", true).Length > 0 && statusLabel != null)
            {
                statusLabel.Text = "就绪";
            }
            this.FormClosing += Bootstrap_FormClosing;

            // 初始时将菜单项与缓存同步
            try
            {
                if (menuItemNetDump != null) menuItemNetDump.Checked = _dbgNetDump;
                if (menuItemAsphyxiaDebug != null) menuItemAsphyxiaDebug.Checked = _dbgAsphyxiaDebug;
            }
            catch { }

            UpdateCompatLayerStatus();
        }

        private bool TryGetRotationAngle(out int angle)
        {
            angle = 0;
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
                chkUsePreconfig.Checked = _usePreconfig;

                // 加载其他启动选项
                chkWindowed.Checked = bool.Parse(_configFile.ReadString("Settings", "windowed", "false"));
                chkPCoreOptimization.Checked = bool.Parse(_configFile.ReadString("Settings", "pcoreopt", "false"));
                chkNoAsphyxia.Checked = bool.Parse(_configFile.ReadString("Settings", "noasphyxia", "false"));
                chkNoRestoreRotation.Checked = bool.Parse(_configFile.ReadString("Settings", "norestorerotation", "false"));

                // 渲染模式（兼容层实现）
                try
                {
                    string renderMode = _configFile.ReadString("Settings", "rendermode", "dx9on12");
                    if (cmbCompatType != null)
                    {
                        // 保证项存在
                        if (cmbCompatType.Items.Count == 0)
                        {
                            cmbCompatType.Items.AddRange(new object[] { "dx9on12", "dxvk" });
                        }
                        int idx = 0;
                        if (string.Equals(renderMode, "dxvk", StringComparison.OrdinalIgnoreCase)) idx = 1;
                        cmbCompatType.SelectedIndex = idx;
                    }
                }
                catch { }

                // 读取调试和 NetDump 的缓存默认（不从配置文件持久化，保持会话内状态）
                if (menuItemNetDump != null) menuItemNetDump.Checked = _dbgNetDump;
                if (menuItemAsphyxiaDebug != null) menuItemAsphyxiaDebug.Checked = _dbgAsphyxiaDebug;

                // 读取当前版本
                string version = _versionFile.ReadString("Version", "version", "Unknown");
                if (txtCurrentVersion != null)
                {
                    txtCurrentVersion.Text = version;
                }

                // 读取懒人包修订号
                string revision = _versionFile.ReadString("Version", "revision", "Unknown");
                if (txtRevision != null)
                {
                    txtRevision.Text = revision;
                }
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
                _isLoadingSettings = false;
            }
        }

        private void SaveSettings()
        {
            try
            {
                _configFile.WriteString("Settings", "usepreconfig", _usePreconfig.ToString().ToLowerInvariant());

                // 保存其他启动选项
                _configFile.WriteString("Settings", "windowed", chkWindowed.Checked.ToString().ToLowerInvariant());
                _configFile.WriteString("Settings", "pcoreopt", chkPCoreOptimization.Checked.ToString().ToLowerInvariant());
                _configFile.WriteString("Settings", "noasphyxia", chkNoAsphyxia.Checked.ToString().ToLowerInvariant());
                _configFile.WriteString("Settings", "norestorerotation", chkNoRestoreRotation.Checked.ToString().ToLowerInvariant());

                // 保存渲染模式（兼容层实现）
                if (cmbCompatType != null && cmbCompatType.SelectedItem != null)
                {
                    string renderMode = cmbCompatType.SelectedItem.ToString();
                    _configFile.WriteString("Settings", "rendermode", renderMode);
                }

                LogSystem.Log("Saved to config.ini");
            }
            catch (Exception ex)
            {
                LogSystem.Log($"保存出错: {ex.Message}", LogSystem.LogLevel.Error);
            }
        }

        // 使用预配置文件复选框事件
        private void chkUsePreconfig_CheckedChanged(object sender, EventArgs e)
        {
            if (_isLoadingSettings)
            {
                return; // 如果正在加载设置，不显示警告
            }

            if (!chkUsePreconfig.Checked)
            {
                // 用户取消勾选，显示警告
                DialogResult result = MessageBox.Show(
                    "如果不使用预配置文件，你需要重新手动设置所有选项与补丁（等同于纯净版），请确保你拥有相关知识！",
                    "警告",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Cancel)
                {
                    // 用户点击取消，恢复勾选状态
                    chkUsePreconfig.Checked = true;
                    return;
                }
            }

            _usePreconfig = chkUsePreconfig.Checked;
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
                lblCompatStatus.ForeColor = System.Drawing.Color.Green;
            }
            else if (fileCount >= 1 && fileCount <= 2)
            {
                // 部分文件存在
                lblCompatStatus.Text = "● 已启用，但可能不完整";
                lblCompatStatus.ForeColor = System.Drawing.Color.Orange;
            }
            else
            {
                // 没有文件
                lblCompatStatus.Text = "● 未启用";
                lblCompatStatus.ForeColor = System.Drawing.Color.Red;
            }

            // 启用状态下禁用兼容层实现下拉，需先关闭再更改；同时“启用”按钮禁用、“关闭”按钮启用
            bool effectiveEnabled = fileCount >= 1 || IsCompatLayerEnabledConfigured();
            if (cmbCompatType != null)
            {
                cmbCompatType.Enabled = !effectiveEnabled;
                if (toolTip0 != null)
                {
                    if (effectiveEnabled)
                    {
                        // 禁用时移除 tooltip，避免持续闪烁
                        toolTip0.SetToolTip(cmbCompatType, string.Empty);
                    }
                    else
                    {
                        // 恢复原始 tooltip
                        if (!string.IsNullOrEmpty(_compatTypeTooltipCache))
                            toolTip0.SetToolTip(cmbCompatType, _compatTypeTooltipCache);
                    }
                }
            }
            if (button1 != null) // 启用按钮
            {
                button1.Enabled = !effectiveEnabled;
            }
            if (btnUnloadCompat != null) // 关闭按钮
            {
                btnUnloadCompat.Enabled = effectiveEnabled;
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
                    statusProgress.Visible = true;
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

                    if (statusProgress != null)
                    {
                        int value = progress;
                        if (value < 0) value = 0;
                        if (value > 100) value = 100;
                        try
                        {
                            if (this.IsHandleCreated)
                            {
                                this.BeginInvoke((MethodInvoker)(() => { statusProgress.Value = value; }));
                            }
                        }
                        catch { }
                    }
                });

                LogSystem.Log("环境检测完成。\r\n");

                // 检测异常弹窗
                if (EnvironmentScan.LastHadError)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("(｡>﹏<｡) 啊哇哇，Near检测到你的系统可能缺少必要的运行组件，可能导致启动失败或功能异常。");
                    sb.AppendLine();
                    sb.AppendLine("٩(ˊᗜˋ*)و Noah给出的解决方法：");
                    sb.AppendLine("- 点击启动器‘安装运行库’按钮安装必要组件");
                    sb.AppendLine("- 确保已安装最新的显卡驱动程序");
                    sb.AppendLine("- 若为 AMD/Intel 显卡，启用‘兼容层’后重试");
                    sb.AppendLine();
                    sb.AppendLine("如果‘系统媒体功能包’异常：");
                    sb.AppendLine("- 检查‘Windows 功能’中是否启用了‘媒体功能包’");
                    sb.AppendLine();
                    sb.AppendLine("您可先行尝试启动游戏，若出现问题，再寻求周围帮助。");

                    MessageBox.Show(this, sb.ToString(), "环境检测发现问题", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    statusProgress.Visible = false;
                }
                SetControlsEnabled(true);
            }
        }

        // Boot
        private async void btnStart_Click(object sender, EventArgs e)
        {
            // Lock Element
            SetControlsEnabled(false);
            if (statusLabel != null) statusLabel.Text = "正在启动...";
            if (txtLogOutput != null)
            {
                try { txtLogOutput.Clear(); } catch { }
            }

            try
            {
                // Screen Rotate
                LogSystem.Log("正在旋转屏幕...");
                if (!TryGetRotationAngle(out int rotationAngle)) rotationAngle = 0;
                string deviceName = null;
                try { deviceName = Screen.PrimaryScreen != null ? Screen.PrimaryScreen.DeviceName : null; } catch { deviceName = null; }
                bool rotationSuccess = false;
                if (!string.IsNullOrEmpty(deviceName))
                {
                    rotationSuccess = ScreenRotate.Rotate(deviceName, rotationAngle);
                }
                LogSystem.Log(rotationSuccess ? $"屏幕已旋转至 {rotationAngle} 度。" : "屏幕旋转失败或无需旋转。");
                await Task.Delay(500); // 等待旋转生效

                // Launch Asphyxia
                if (!chkNoAsphyxia.Checked)
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

                var argsBuilder = new StringBuilder();
                if (_usePreconfig)
                {
                    // Spice2x launch args（使用预配置）
// Spice2x launch args（使用预配置）
argsBuilder.Append("-cmdoverride ");
argsBuilder.Append("-cfgpath lazy/spicetools.xml ");
argsBuilder.Append("-patchcfgpath lazy/spicetools_patch_manager.json ");
argsBuilder.Append("-modules modules ");
                }

                if (chkWindowed.Checked)
                {
                    argsBuilder.Append("-w ");
                }
                // NetDump
                if (_dbgNetDump)
                {
                    argsBuilder.Append("-netdump ");
                }
                if (chkPCoreOptimization.Checked)
                {
                    argsBuilder.Append("-processefficiency pcores ");
                }

                // 根据高级选项拼接参数
                if (_advDisableSubDisplay)
                {
                    argsBuilder.Append("-sdvxnosub ");
                }
                switch (_advWindowModeIndex)
                {
                    case 1:
                        argsBuilder.Append("-windowborder 1 ");
                        break;
                    case 2:
                        argsBuilder.Append("-windowborder 2 ");
                        break;
                }
                if (_advSubBorderless)
                {
                    argsBuilder.Append("-sdvxwsubborderless ");
                }
                if (_advShowCursorTouchSim)
                {
                    argsBuilder.Append("-s ");
                }

                // 根据兼容层选择添加 -dx9on12 参数
                try
                {
                    string compat = cmbCompatType != null && cmbCompatType.SelectedItem != null
                        ? cmbCompatType.SelectedItem.ToString()
                        : "dx9on12";

                    if (string.Equals(compat, "dxvk", StringComparison.OrdinalIgnoreCase))
                    {
                        argsBuilder.Append("-dx9on12 0 ");
                    }
                    else
                    {
                        if (GetCompatLayerFileCount() == 3)
                        {
                            argsBuilder.Append("-dx9on12 1 ");
                        }
                    }
                }
                catch { }

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

        // Status
        private void GameProcess_Exited(object sender, EventArgs e)
        {
            if (!this.IsDisposed && !this.Disposing)
            {
                BeginInvoke((MethodInvoker)delegate
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

                    if (!chkNoRestoreRotation.Checked)
                    {
                        LogSystem.Log("正在还原屏幕旋转...");
                        string deviceName = null;
                        try { deviceName = Screen.PrimaryScreen != null ? Screen.PrimaryScreen.DeviceName : null; } catch { deviceName = null; }
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
        }

        // kill process
        private void btnKillProcesses_Click(object sender, EventArgs e)
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

        // ScreenRotate manually
        private void btnSwitchRotation_Click(object sender, EventArgs e)
        {
            try
            {
                if (!TryGetRotationAngle(out int angle)) angle = 0;
                bool success = ScreenRotate.Rotate(Screen.PrimaryScreen.DeviceName, angle);
                MessageBox.Show(success ? $"屏幕旋转至 {angle} 度成功。" : "屏幕旋转失败。", "提示", MessageBoxButtons.OK,
                    success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"旋转时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Clear ifs_hook cache
        private void btnClearCache_Click(object sender, EventArgs e)
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
        private void btnEditConfig_Click(object sender, EventArgs e)
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
                    MessageBox.Show($"未找到编辑器: {cfgToolPath}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show($"启动编辑器失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Install Runtime
        private void btnInstallRuntime_Click(object sender, EventArgs e)
        {
            string runtimePath = Path.Combine(_baseDir, "runtime");
            string installBatPath = Path.Combine(runtimePath, "install.bat");

            try
            {
                if (!File.Exists(installBatPath))
                {
                    //MessageBox.Show($"未找到安装脚本: {installBatPath}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        private void btnLoadCompat_Click(object sender, EventArgs e)
        {
            MoveCompatFiles(Path.Combine("contents", "lazy", "stubs"), Path.Combine("contents", "modules"), "载入");
            // 记录启用状态
            try { _configFile.WriteString("Settings", "compatlayerenabled", "true"); LogSystem.Log("已记录兼容层状态: 启用"); } catch { }
            UpdateCompatLayerStatus();
        }

        private void btnUnloadCompat_Click(object sender, EventArgs e)
        {
            MoveCompatFiles(Path.Combine("contents", "modules"), Path.Combine("contents", "lazy", "stubs"), "卸载");
            // 记录关闭状态
            try { _configFile.WriteString("Settings", "compatlayerenabled", "false"); LogSystem.Log("已记录兼容层状态: 关闭"); } catch { }
            UpdateCompatLayerStatus();
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
                Directory.CreateDirectory(destDir); // make sure destination folder has been created

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
                    MessageBox.Show($"操作'{operationName}'完成，但所有源文件都不存在，未移动任何文件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    string resultMessage = $"{operationName}完成。成功移动 {successCount}/{filesToMove.Length} 个文件。";
                    MessageBox.Show(resultMessage, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{operationName}失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UpdateCompatLayerStatus();
            }
        }

        // Kill process when exit bootstrap
        private void Bootstrap_FormClosing(object sender, FormClosingEventArgs e)
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
            btnStart.Enabled = enabled;
            if (groupBoxCompatLayer != null)
            {
                groupBoxCompatLayer.Enabled = enabled;
            }
            foreach (Control ctrl in groupBoxOptions.Controls)
            {
                if (ctrl != btnKillProcesses)
                {
                    ctrl.Enabled = enabled;
                }
            }
            btnClearCache.Enabled = enabled;
            btnInstallRuntime.Enabled = enabled;
            btnAddFirewallRule.Enabled = enabled;
            btnKillProcesses.Enabled = true; // 始终启用
        }

        private void btnAddFirewallRule_Click(object sender, EventArgs e)
        {
            const string ruleName = "SpiceTools";
            string spicePath = GetSpicePath();
            FirewallHelper.EnsureFirewallRule(ruleName, spicePath, LogSystem.Log);
        }

        private void btnOpenLog_Click(object sender, EventArgs e)
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
    }
}