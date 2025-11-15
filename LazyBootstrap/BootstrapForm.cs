// written by Arkito aka Takanashi Ryo, only release in SDVX Lazy Pack.
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
            }

            InitializeCustomComponents();
            LogSystem.Log("本包体免费，如果你是付费获取的，请窒息");
            LoadSettings();
        }

        private void InitializeCustomComponents()
        {
            // 设置默认值和下拉列表项
            cmbRotation.Items.AddRange(new object[] { "0", "90", "180", "270" });
            cmbRotation.SelectedIndex = 0;

            // 初始化日志输出控件
            LogSystem.Initialize(txtLogOutput);

            if (this.Controls.Find("statusStrip1", true).Length > 0 && statusLabel != null)
            {
                statusLabel.Text = "就绪";
            }
            this.FormClosing += Bootstrap_FormClosing;

            // 初始化兼容层状态
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
        }

        // Boot
        private async void btnStart_Click(object sender, EventArgs e)
        {
            // Lock Element
            SetControlsEnabled(false);
            if (statusLabel != null) statusLabel.Text = "正在启动...";
            txtLogOutput.Clear();

            try
            {
                // Screen Rotate
                LogSystem.Log("正在旋转屏幕...");
                if (!TryGetRotationAngle(out int rotationAngle)) rotationAngle = 0;
                bool rotationSuccess = ScreenRotate.Rotate(Screen.PrimaryScreen.DeviceName, rotationAngle);
                LogSystem.Log(rotationSuccess ? $"屏幕已旋转至 {rotationAngle} 度。" : "屏幕旋转失败或无需旋转。");
                await Task.Delay(500); // 等待旋转生效

                // Launch Asphyxia
                if (!chkNoAsphyxia.Checked)
                {
                    LogSystem.Log("\n正在启动 Asphyxia Core...");
                    string asphyxiaPath = GetAsphyxiaPath();
                    if (File.Exists(asphyxiaPath))
                    {
                        if (chkAsphyxiaDebug.Checked)
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
                    argsBuilder.Append("-cmdoverride ");
                    argsBuilder.Append("-cfgpath lazy/spicetools.xml ");
                    argsBuilder.Append("-patchcfgpath lazy/spicetools_patch_manager.json ");
                    argsBuilder.Append("-modules modules ");
                }

                if (chkWindowed.Checked)
                {
                    argsBuilder.Append("-w ");
                }
                if (chkNetDump.Checked)
                {
                    argsBuilder.Append("-netdump ");
                }
                if (chkPCoreOptimization.Checked)
                {
                    argsBuilder.Append("-processefficiency pcores ");
                }

                // add "-dx9on12 1" when detect NVIDIA API in modules
                try
                {
                    if (GetCompatLayerFileCount() == 3)
                    {
                        argsBuilder.Append("-dx9on12 1 ");
                        LogSystem.Log("检测到 NVIDIA API 兼容层，已添加启动参数: -dx9on12 1");
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
                    WorkingDirectory = Path.GetDirectoryName(spicePath) // set spice2x working directory to pass folders 
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
                        bool restored = ScreenRotate.Rotate(Screen.PrimaryScreen.DeviceName, 0);
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
                    MessageBox.Show($"未找到安装脚本: {installBatPath}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                // 用户取消了UAC提示
                if (ex.NativeErrorCode == 1223)
                {
                    LogSystem.Log("用户取消了 Runtime 安装。", LogSystem.LogLevel.Warning);
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
            UpdateCompatLayerStatus();
        }

        private void btnUnloadCompat_Click(object sender, EventArgs e)
        {
            MoveCompatFiles(Path.Combine("contents", "modules"), Path.Combine("contents", "lazy", "stubs"), "卸载");
            UpdateCompatLayerStatus();
        }

        private void MoveCompatFiles(string sourceDirRel, string destDirRel, string operationName)
        {
            LogSystem.Log($"NVIDIA API 准备{operationName}...");
            string sourceDir = Path.Combine(_baseDir, sourceDirRel);
            string destDir = Path.Combine(_baseDir, destDirRel);
            string[] filesToMove = { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll" };

            int successCount = 0;
            int skippedCount = 0;
            try
            {
                if (!Directory.Exists(sourceDir))
                {
                    MessageBox.Show($"源文件夹 '{sourceDirRel}' 不存在!", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
    }
}