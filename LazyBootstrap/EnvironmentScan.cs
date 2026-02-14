using Microsoft.Win32; // 可移除如不再需要 Registry
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LazyBootstrap
{
    public static class EnvironmentScan
    {
        private const int StepCount = 7; // CPU / GPU / NVIDIA API / DirectX9.0c / 系统媒体功能包 / VC2010 x86 / VC2010 x64

        // 最近一次扫描的错误状态与摘要
        private static bool _lastHadError;
        private static string _lastErrorSummary;
        public static bool LastHadError => _lastHadError;
        public static string LastErrorSummary => _lastErrorSummary ?? string.Empty;

        public static async Task RunAsync(Action<int, string> progress)
        {
            // 统一的进度上报（不传递 message 内容，仅数值）
            void Report(int stepIndex) { try { progress?.Invoke((int)Math.Round((double)stepIndex * 100 / StepCount), ""); } catch { } }

            bool hadIssue = false; // Warning 或 Error
            bool hadError = false; // 仅 Error
            var errorSummary = new StringBuilder();

            void LogInfo(string msg) => LogSystem.Log(msg);
            void LogWarn(string msg) { hadIssue = true; LogSystem.Log(msg, LogSystem.LogLevel.Warning); }
            void LogError(string msg) { hadIssue = true; hadError = true; errorSummary.AppendLine(msg); LogSystem.Log(msg, LogSystem.LogLevel.Error); }

            LogInfo("初始化环境检测...");
            Report(0);
            await Task.Delay(120);

            // 局部函数：无对外暴露，保持单方法结构
            string GetCpuName()
            {
                try
                {
                    using (var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0"))
                    {
                        var name = k?.GetValue("ProcessorNameString") as string;
                        if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
                    }
                }
                catch { }
                return "未知处理器";
            }

            List<string> GetGpuNames()
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    using (var root = Registry.LocalMachine.OpenSubKey(@"SYSTEM\\CurrentControlSet\\Control\\Video"))
                    {
                        if (root == null) return set.ToList();
                        foreach (var guid in root.GetSubKeyNames())
                        {
                            using (var adapterKey = root.OpenSubKey(guid))
                            {
                                if (adapterKey == null) continue;
                                foreach (var sub in new[] { "0000", "0001", "0002" })
                                {
                                    using (var conf = adapterKey.OpenSubKey(sub))
                                    {
                                        var desc = conf?.GetValue("DriverDesc") as string;
                                        if (!string.IsNullOrWhiteSpace(desc)) set.Add(desc.Trim());
                                        var adapterStr = conf?.GetValue("HardwareInformation.AdapterString") as string;
                                        if (!string.IsNullOrWhiteSpace(adapterStr)) set.Add(adapterStr.Trim());
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
                return set.ToList();
            }

            bool IsCompatLayerEnabled()
            {
                try
                {
                    var envBaseDir = Environment.GetEnvironmentVariable("LAZYBOOTSTRAP_BASEDIR");
                    var baseDir = !string.IsNullOrWhiteSpace(envBaseDir) ? envBaseDir : AppDomain.CurrentDomain.BaseDirectory;
                    var cfgPath = Path.Combine(baseDir, "config.toml");
                    if (!File.Exists(cfgPath)) return false;
                    var cfg = new ConfigHandler(cfgPath);
                    var s = cfg.ReadString("Settings", "compatlayerenabled", "false");
                    bool enabled; return bool.TryParse(s, out enabled) && enabled;
                }
                catch { return false; }
            }

            void LogCpu()
            {
                LogInfo("CPU:");
                LogInfo("  - " + GetCpuName());
            }

            void LogGpu()
            {
                LogInfo("GPU:");
                var gpus = GetGpuNames();
                if (gpus.Count == 0)
                    LogWarn("  - 未检测到");
                else
                    foreach (var g in gpus) LogInfo("  - " + g);
            }

            void LogNvidia()
            {
                LogInfo("NVIDIA API (System32):");
                if (IsCompatLayerEnabled())
                {
                    LogInfo("  - 已启用兼容层，自动跳过系统库检测");
                    return;
                }
                try
                {
                    var sys32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
                    foreach (var f in new[] { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll" })
                    {
                        bool ok = File.Exists(Path.Combine(sys32, f));
                        if (ok) LogInfo($"  - {f}: 已检测到"); else LogError($"  - {f}: 未检测到");
                    }
                }
                catch (Exception ex)
                {
                    LogError($"  - 检测失败: {ex.Message}");
                }
            }

            void LogDirectX()
            {
                LogInfo("DirectX 9.0c:");
                try
                {
                    var sys32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
                    bool hasCore = File.Exists(Path.Combine(sys32, "d3d9.dll"));
                    bool hasJun = File.Exists(Path.Combine(sys32, "d3dx9_43.dll"));
                    if (hasCore) LogInfo("  - d3d9.dll: 已检测到"); else LogError("  - d3d9.dll: 未检测到");
                    if (hasJun) LogInfo("  - d3dx9_43.dll: 已检测到"); else LogError("  - d3dx9_43.dll: 未检测到");
                }
                catch (Exception ex)
                {
                    LogError($"  - 检测失败: {ex.Message}");
                }
            }

            // 新增：系统媒体功能包（MF.dll、MFPLAT.dll、WMVCore.dll）
            void LogMediaFeaturePack()
            {
                LogInfo("系统媒体功能包:");
                try
                {
                    var sys32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
                    foreach (var f in new[] { "MF.dll", "MFPLAT.dll", "WMVCore.dll" })
                    {
                        bool ok = File.Exists(Path.Combine(sys32, f));
                        if (ok) LogInfo($"  - {f}: 已检测到"); else LogError($"  - {f}: 未检测到");
                    }
                }
                catch (Exception ex)
                {
                    LogError($"  - 检测失败: {ex.Message}");
                }
            }

            void LogVc2010(bool x64)
            {
                string arch = x64 ? "x64" : "x86";
                LogInfo($"VC++ 2010 {arch}:");
                try
                {
                    var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                    string dllDir = x64 ? Path.Combine(winDir, "System32") : (Environment.Is64BitOperatingSystem ? Path.Combine(winDir, "SysWOW64") : Path.Combine(winDir, "System32"));
                    var dlls = new[] { "msvcr100.dll", "msvcp100.dll" };
                    foreach (var d in dlls)
                    {
                        bool ok = File.Exists(Path.Combine(dllDir, d));
                        if (ok) LogInfo($"  - {d}: 已检测到"); else LogError($"  - {d}: 未检测到");
                    }
                }
                catch (Exception ex)
                {
                    LogError($"  - 检测失败: {ex.Message}");
                }
            }

            // 步骤定义数组，循环执行
            var steps = new Action[]
            {
                LogCpu,
                LogGpu,
                LogNvidia,
                LogDirectX,
                LogMediaFeaturePack,
                () => LogVc2010(false),
                () => LogVc2010(true)
            };

            for (int i = 0; i < steps.Length; i++)
            {
                try { steps[i](); }
                catch (Exception ex) { LogError($"步骤 {i+1} 未预期异常: {ex.Message}"); }
                Report(i + 1);
                await Task.Delay(160);
            }

            if (hadIssue)
            {
                LogError("环境监测异常！");
            }

            // 保存结果供 UI 使用
            _lastHadError = hadError;
            _lastErrorSummary = errorSummary.ToString();

            Report(StepCount);
        }
    }
}
