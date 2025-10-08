// written by Arkito aka Takanashi Ryo with Gemini, only release in SDVX Lazy Pack.
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
        private bool _usePreconfig = true; // 是否使用预配置文件（默认勾选）
        private const int MAX_HISTORY_ITEMS = 10; // 最大历史记录数量

        public BootstrapForm()
        {
            InitializeComponent();

            string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            _configFile = new ConfigHandler(configFilePath);

            InitializeCustomComponents();
            Log("本包体免费，如果你是付费获取的，请窒息");
            LoadSettings();
        }

        private void InitializeCustomComponents()
        {
            // 设置默认值和下拉列表项
            cmbRotation.Items.AddRange(new object[] { "0", "90", "180", "270" });
            cmbRotation.SelectedIndex = 0;

            if (this.Controls.Find("statusStrip1", true).Length > 0 && statusLabel != null)
            {
                statusLabel.Text = "就绪";
            }
            this.FormClosing += Bootstrap_FormClosing;

            // 初始化兼容层状态
            UpdateCompatLayerStatus();
        }


        private void LoadSettings()
        {
            try
            {
                // 加载预配置选项
                string usePreconfigStr = _configFile.ReadString("Settings", "usepreconfig", "true");
                if (!bool.TryParse(usePreconfigStr, out _usePreconfig))
                {
                    _usePreconfig = true;
                }
                chkUsePreconfig.Checked = _usePreconfig;
                UpdateUiForPreconfigMode();

                // 加载其他启动选项
                chkWindowed.Checked = bool.Parse(_configFile.ReadString("Settings", "windowed", "false"));
                chkPCoreOptimization.Checked = bool.Parse(_configFile.ReadString("Settings", "pcoreopt", "false"));
                chkNoAsphyxia.Checked = bool.Parse(_configFile.ReadString("Settings", "noasphyxia", "false"));
                chkNoRestoreRotation.Checked = bool.Parse(_configFile.ReadString("Settings", "norestorerotation", "false"));

                Log("Load config.ini");
            }
            catch (Exception ex)
            {
                Log($"加载配置文件时出错: {ex.Message}");
            }
        }

        private void SaveSettings()
        {
            try
            {
                _configFile.WriteString("Settings", "usepreconfig", _usePreconfig.ToString());

                // 保存其他启动选项
                _configFile.WriteString("Settings", "windowed", chkWindowed.Checked.ToString());
                _configFile.WriteString("Settings", "pcoreopt", chkPCoreOptimization.Checked.ToString());
                _configFile.WriteString("Settings", "noasphyxia", chkNoAsphyxia.Checked.ToString());
                _configFile.WriteString("Settings", "norestorerotation", chkNoRestoreRotation.Checked.ToString());

                Log("Saved to config.ini");
            }
            catch (Exception ex)
            {
                Log($"保存出错: {ex.Message}");
            }
        }

        // 使用预配置文件复选框事件
        private void chkUsePreconfig_CheckedChanged(object sender, EventArgs e)
        {
            _usePreconfig = chkUsePreconfig.Checked;
            UpdateUiForPreconfigMode();
        }

        // 根据预配置模式更新UI
        private void UpdateUiForPreconfigMode()
        {
            // 无需调整UI位置
        }

        // 检查兼容层文件数量
        private int GetCompatLayerFileCount()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string modulesDir = Path.Combine(baseDir, "contents", "modules");
            string[] compatFiles = { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll", "d3d9.dll" };

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

            if (fileCount == 4)
            {
                // 所有文件都存在
                lblCompatStatus.Text = "● 已载入";
                lblCompatStatus.ForeColor = System.Drawing.Color.Green;
            }
            else if (fileCount >= 1 && fileCount <= 3)
            {
                // 部分文件存在
                lblCompatStatus.Text = "● 已载入，但可能不完整";
                lblCompatStatus.ForeColor = System.Drawing.Color.Orange;
            }
            else
            {
                // 没有文件
                lblCompatStatus.Text = "● 未载入";
                lblCompatStatus.ForeColor = System.Drawing.Color.Red;
            }
        }

        // Boot
        private async void btnStart_Click(object sender, EventArgs e)
        {
            // Lock Element
            SetControlsEnabled(false);
            statusLabel.Text = "正在启动...";
            txtLogOutput.Clear();

            try
            {
                // Screen Rotate
                Log("正在旋转屏幕...");
                int rotationAngle = int.Parse(cmbRotation.SelectedItem.ToString());
                bool rotationSuccess = ScreenRotate.Rotate(Screen.PrimaryScreen.DeviceName, rotationAngle);
                Log(rotationSuccess ? $"屏幕已旋转至 {rotationAngle} 度。" : "屏幕旋转失败或无需旋转。");
                await Task.Delay(500); // 等待旋转生效

                // Launch Asphyxia
                if (!chkNoAsphyxia.Checked)
                {
                    Log("\n正在启动 Asphyxia Core...");
                    string asphyxiaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "asphyxia", "asphyxia-core-x64.exe");
                    if (File.Exists(asphyxiaPath))
                    {
                        // 如果勾选了调试模式，使用 cmd.exe 启动以保持窗口打开
                        if (chkAsphyxiaDebug.Checked)
                        {
                            var asphyxiaStartInfo = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = $"/k \"\"{asphyxiaPath}\" --dev\"",
                                WorkingDirectory = Path.GetDirectoryName(asphyxiaPath)
                            };
                            Process.Start(asphyxiaStartInfo);
                            Log("  - 使用调试模式启动（控制台窗口将保持打开）");
                        }
                        else
                        {
                            var asphyxiaStartInfo = new ProcessStartInfo
                            {
                                FileName = asphyxiaPath,
                            };
                            Process.Start(asphyxiaStartInfo);
                        }

                        Log("Asphyxia Core 已启动。");
                    }
                    else
                    {
                        Log($"错误: 未找到 Asphyxia Core, 路径: {asphyxiaPath}");
                        SetControlsEnabled(true);
                        if (statusLabel != null) statusLabel.Text = "启动失败";
                        return;
                    }
                }
                else
                {
                    Log("\n已跳过启动 Asphyxia Core。");
                }

                var argsBuilder = new StringBuilder();
                if (_usePreconfig)
                {
                    // Spice2x launch args（使用预配置）
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

                // Booting
                Log("\n正在启动游戏...");
                Log($"  - 启动参数: {argsBuilder.ToString()}");

                string spicePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "contents", "spice64.exe");
                if (!File.Exists(spicePath))
                {
                    Log($"\n错误: 未找到游戏主程序, 路径: {spicePath}");
                    SetControlsEnabled(true);
                    statusLabel.Text = "启动失败";
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

                statusLabel.Text = "游戏已启动";
                Log("\n游戏进程已启动。");
            }
            catch (Exception ex)
            {
                Log($"\n启动过程中发生严重错误: {ex.Message}");
                SetControlsEnabled(true);
                statusLabel.Text = "启动失败";
            }
        }

        // Status
        private void GameProcess_Exited(object sender, EventArgs e)
        {
            if (!this.IsDisposed && !this.Disposing)
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    Log("\n游戏进程已退出。");

                    Log("正在关闭 Asphyxia Core...");
                    try
                    {
                        KillProcessesByName("asphyxia-core-x64");
                        Log("Asphyxia Core 已关闭");
                    }
                    catch (Exception ex)
                    {
                        Log($"未找到正在运行的 Asphyxia Core 进程。{ex.Message}");
                    }

                    if (!chkNoRestoreRotation.Checked)
                    {
                        Log("正在还原屏幕旋转...");
                        bool restored = ScreenRotate.Rotate(Screen.PrimaryScreen.DeviceName, 0);
                        Log(restored ? "屏幕旋转已还原为 0 度。" : "屏幕旋转还原失败。");
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
            Log("\n正在尝试结束所有相关进程...");
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
                Log($"  - 获取进程列表 {processName} 时出错: {ex.Message}");
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
                        Log($"  - 进程 {processName} (PID: {pid}) 未响应，尝试强制终止...");

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

                            Log($"  - 已强制结束进程: {processName}.exe (PID: {pid})");
                        }
                        catch (Exception ex)
                        {
                            Log($"  - 强制终止进程失败: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log($"  - 已结束进程: {processName}.exe (PID: {pid})");
                    }

                    count++;
                }
                catch (InvalidOperationException)
                {
                    Log($"  - 进程 {processName} 已退出，无需结束。");
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    Log($"  - 结束进程 {processName} 时权限不足: {ex.Message}");

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
                        Log($"  - 已使用管理员权限尝试终止 {processName}");
                    }
                    catch (Exception ex2)
                    {
                        Log($"  - 管理员权限终止失败: {ex2.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"  - 结束进程 {processName} 时出错: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        if (p != null && !p.HasExited)
                        {
                            p.Dispose();
                        }
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
                int angle = int.Parse(cmbRotation.SelectedItem.ToString());
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
            string cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "contents", "data_mods", "_cache");
            try
            {
                if (Directory.Exists(cachePath))
                {
                    Directory.Delete(cachePath, true);
                    MessageBox.Show("缓存已成功清除！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("缓存文件不存在", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清除缓存失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Edit spicecfg
        private void btnEditConfig_Click(object sender, EventArgs e)
        {
            string cfgToolPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "contents", "spicecfg.exe");
            string arguments = "";
            if (_usePreconfig)
            {
                arguments = "-cfgpath lazy/spicetools.xml -patchcfgpath lazy/spicetools_patch_manager.json -modules modules";
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
            string runtimePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime");
            string installBatPath = Path.Combine(runtimePath, "install.bat");

            try
            {
                if (!File.Exists(installBatPath))
                {
                    MessageBox.Show($"未找到安装脚本: {installBatPath}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Log($"错误: 未找到 runtime/install.bat");
                    return;
                }

                Log("\n正在安装 Runtime 组件...");
                Log($"  - 执行脚本: {installBatPath}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = installBatPath,
                    WorkingDirectory = runtimePath,
                    UseShellExecute = true,
                    Verb = "runas" // 以管理员权限运行
                };

                Process installProcess = Process.Start(startInfo);

                //if (installProcess != null)
                //{
                //    Log("Runtime 安装程序已启动，请按照提示完成安装。");
                //    MessageBox.Show("Runtime 安装程序已启动，请按照提示完成安装。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //}
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // 用户取消了UAC提示
                if (ex.NativeErrorCode == 1223)
                {
                    Log("用户取消了 Runtime 安装。");
                    MessageBox.Show("已取消安装。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    Log($"启动 Runtime 安装失败: {ex.Message}");
                    MessageBox.Show($"启动安装失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Log($"启动 Runtime 安装时发生错误: {ex.Message}");
                MessageBox.Show($"启动安装失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            Log($"NVIDIA API, DXVK准备{operationName}...");
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string sourceDir = Path.Combine(baseDir, sourceDirRel);
            string destDir = Path.Combine(baseDir, destDirRel);
            string[] filesToMove = { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll", "d3d9.dll" };

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
                            Log($"已删除旧的目标文件: {destPath}");
                        }
                        File.Move(sourcePath, destPath);
                        Log($"成功移动: {fileName}");
                        successCount++;
                    }
                    else
                    {
                        Log($"源文件不存在，跳过: {sourcePath}");
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

        // Log output
        private void Log(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (txtLogOutput.InvokeRequired)
            {
                txtLogOutput.Invoke((MethodInvoker)delegate { Log(message); });
            }
            else
            {
                txtLogOutput.AppendText(message + Environment.NewLine);
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            btnStart.Enabled = enabled;
            groupBoxTools.Enabled = enabled;
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
            btnKillProcesses.Enabled = true;
        }

        private void groupBoxCompatLayer_Enter(object sender, EventArgs e)
        {

        }
    }
}