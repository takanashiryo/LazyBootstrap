using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LazyBootstrap
{
    public partial class BootstrapForm : Form
    {
        private Process _gameProcess;
        private readonly ConfigHandler _configFile;

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
            txtEaServer.Text = "http://localhost:8083";
            cmbRotation.Items.AddRange(new object[] { "0", "90", "180", "270" });
            cmbRotation.SelectedIndex = 0;
            statusLabel.Text = "就绪";
            this.FormClosing += Bootstrap_FormClosing;
        }
        private void LoadSettings()
        {
            try
            {
                txtEaServer.Text = _configFile.ReadString("Settings", "eaURL", "http://localhost:8083");
                txtPcbId.Text = _configFile.ReadString("Settings", "pcbid", "");
                txtNetworkIp.Text = _configFile.ReadString("Settings", "networkip", "");
                txtSubnetMask.Text = _configFile.ReadString("Settings", "subnet", "");
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
                _configFile.WriteString("Settings", "eaURL", txtEaServer.Text);
                _configFile.WriteString("Settings", "pcbid", txtPcbId.Text);
                _configFile.WriteString("Settings", "networkip", txtNetworkIp.Text);
                _configFile.WriteString("Settings", "subnet", txtSubnetMask.Text);
            }
            catch(Exception ex)
            {
                Log($"加载出错{ex.Message}");
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
                    string asphyxiaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "asphyxia","asphyxia-core-x64.exe");
                    if (File.Exists(asphyxiaPath))
                    {
                        var asphyxiaStartInfo = new ProcessStartInfo
                        {
                            FileName = asphyxiaPath,
                            WindowStyle = ProcessWindowStyle.Minimized
                        };
                        Process.Start(asphyxiaStartInfo);
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

                // Spice2x launch args
                var argsBuilder = new StringBuilder();
                argsBuilder.Append("-cfgpath lazy/spicetools.xml ");
                argsBuilder.Append("-patchcfgpath lazy/spicetools_patch_manager.json ");
                argsBuilder.Append("-modules modules ");
                argsBuilder.Append("-cmdoverride ");

                string eaURL = string.IsNullOrWhiteSpace(txtEaServer.Text) ? "http://localhost:8083" : txtEaServer.Text;
                argsBuilder.Append($"-url {eaURL} ");

                if (this.Controls.ContainsKey("txtPcbId") && !string.IsNullOrWhiteSpace(txtPcbId.Text))
                {
                    argsBuilder.Append($"-p {txtPcbId.Text.Trim()} ");
                }
                if (!string.IsNullOrWhiteSpace(txtNetworkIp.Text))
                {
                    argsBuilder.Append($"-network {txtNetworkIp.Text} ");
                }
                if (!string.IsNullOrWhiteSpace(txtSubnetMask.Text))
                {
                    argsBuilder.Append($"-subnet {txtSubnetMask.Text} ");
                }
                if (chkWindowed.Checked)
                {
                    argsBuilder.Append("-w ");
                }
                if (chkNetDump.Checked)
                {
                    argsBuilder.Append("-netdump ");
                }

                // Booting
                Log("\n正在启动游戏...");
                Log($"  - 启动参数: {argsBuilder.ToString()}");

                string spicePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "contents","spice64.exe");
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
                using (p)
                {
                    try
                    {
                        int pid = p.Id;
                        p.Kill();
                        p.WaitForExit(5000); // 5 seconds
                        count++;
                        Log($"  - 已结束进程: {processName}.exe (PID: {pid})");
                    }
                    catch (Exception)
                    {
                        Log($"  - 进程 {processName} 已退出，无需结束。");
                    }
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
            string cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "contents","data_mods","_cache");
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
            string arguments = "-cfgpath lazy/spicetools.xml -patchcfgpath lazy/spicetools_patch_manager.json -modules modules";

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
                    WorkingDirectory = Path.GetDirectoryName(cfgToolPath)
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动编辑器失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // NVIDIA API,dxvk Library Load
        private void btnLoadCompat_Click(object sender, EventArgs e)
        {
            MoveCompatFiles(Path.Combine("contents","lazy", "stubs"), Path.Combine("contents", "modules"), "载入");
        }

        private void btnUnloadCompat_Click(object sender, EventArgs e)
        {
            MoveCompatFiles(Path.Combine("contents", "modules"), Path.Combine("contents","lazy", "stubs"), "卸载");
        }

        private void MoveCompatFiles(string sourceDirRel, string destDirRel, string operationName)
        {
            Log($"NVIDIA API, DirectX to Vulkan准备{operationName}...");
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
            if (this.Controls.ContainsKey("groupBoxCompatLayer"))
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
    }
}