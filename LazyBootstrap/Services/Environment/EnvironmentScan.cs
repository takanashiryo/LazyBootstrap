using Microsoft.Win32;
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

        public enum ScanResultLevel
        {
            Success,
            Warning,
            Error
        }

        public sealed class ScanResultItem
        {
            public string Item { get; set; } = string.Empty;
            public string Detail { get; set; } = string.Empty;
            public ScanResultLevel Level { get; set; } = ScanResultLevel.Success;
        }

        public sealed class ScanSummary
        {
            public static ScanSummary Empty { get; } = new(false, string.Empty, Array.Empty<ScanResultItem>());

            public ScanSummary(bool hadError, string errorSummary, IReadOnlyList<ScanResultItem> items)
            {
                HadError = hadError;
                ErrorSummary = errorSummary ?? string.Empty;
                Items = items ?? Array.Empty<ScanResultItem>();
            }

            public bool HadError { get; }

            public string ErrorSummary { get; }

            public IReadOnlyList<ScanResultItem> Items { get; }
        }

        public static Task<ScanSummary> RunAsync(Action<int, string> progress)
        {
            return Task.Run(() =>
            {
            // 统一的进度上报（不传递 message 内容，仅数值）
            void Report(int stepIndex) { try { progress?.Invoke((int)Math.Round((double)stepIndex * 100 / StepCount), ""); } catch { } }

            bool hadError = false; // 仅 Error
            var errorSummary = new StringBuilder();
            var items = new List<ScanResultItem>();

            void AddResult(string item, string detail, ScanResultLevel level)
            {
                items.Add(new ScanResultItem
                {
                    Item = item,
                    Detail = detail,
                    Level = level
                });

                if (level == ScanResultLevel.Error)
                {
                    hadError = true;
                    errorSummary.AppendLine(item);
                }
            }

            Report(0);

            // 局部函数：无对外暴露，保持单方法结构
            string GetCpuName()
            {
                if (!OperatingSystem.IsWindows())
                {
                    return "未知处理器";
                }

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
                if (!OperatingSystem.IsWindows())
                {
                    return set.ToList();
                }

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
                    var baseDir = AppPathResolver.ResolveBaseDir();
                    var cfgPath = Path.Combine(baseDir, "config.toml");

                    bool enabledByConfig = false;
                    if (File.Exists(cfgPath))
                    {
                        var cfg = new ConfigHandler(cfgPath);
                        var s = cfg.ReadString("Setting", "compatlayer", "false");
                        bool enabled;
                        enabledByConfig = bool.TryParse(s, out enabled) && enabled;
                    }

                    string modulesDir = Path.Combine(baseDir, "contents", "modules");
                    string[] compatFiles = { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll" };
                    int stubCount = 0;
                    foreach (var file in compatFiles)
                    {
                        if (File.Exists(Path.Combine(modulesDir, file)))
                        {
                            stubCount++;
                        }
                    }

                    bool enabledByFiles = stubCount >= 1;
                    return enabledByConfig || enabledByFiles;
                }
                catch { return false; }
            }

            void LogCpu()
            {
                var cpuName = GetCpuName();
                AddResult("CPU/处理器", cpuName, string.Equals(cpuName, "未知处理器", StringComparison.Ordinal) ? ScanResultLevel.Warning : ScanResultLevel.Success);
            }

            void LogGpu()
            {
                var gpus = GetGpuNames();
                if (gpus.Count == 0)
                {
                    AddResult("GPU/显示适配器", string.Empty, ScanResultLevel.Warning);
                    return;
                }

                var vmKeywords = new[]
                {
                    "VMware",
                    "VirtIO",
                    "VirtualBox",
                    "Hyper-V",
                    "QEMU",
                    "Parallels",
                    "KVM"
                };

                bool isVirtualMachine = gpus.Any(gpu =>
                    vmKeywords.Any(keyword => gpu.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0));

                if (isVirtualMachine)
                {
                    foreach (var gpu in gpus)
                    {
                        AddResult($"GPU/{gpu}", "虚拟机", ScanResultLevel.Warning);
                    }
                }
                else
                {
                    foreach (var gpu in gpus)
                    {
                        AddResult($"GPU/{gpu}", string.Empty, ScanResultLevel.Success);
                    }
                }
            }

            void LogNvidia()
            {
                if (IsCompatLayerEnabled())
                {
                    AddResult("NVIDIA API/系统库检测", "已启用兼容层，自动跳过 NVIDIA API 检测", ScanResultLevel.Success);
                    return;
                }
                try
                {
                    var sys32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
                    foreach (var f in new[] { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll" })
                    {
                        bool ok = File.Exists(Path.Combine(sys32, f));
                        AddResult($"NVIDIA API/{f}", string.Empty, ok ? ScanResultLevel.Success : ScanResultLevel.Error);
                    }
                }
                catch (Exception)
                {
                    AddResult("NVIDIA API/系统库检测", string.Empty, ScanResultLevel.Error);
                }
            }

            void LogDirectX()
            {
                try
                {
                    var sys32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
                    bool hasCore = File.Exists(Path.Combine(sys32, "d3d9.dll"));
                    bool hasJun = File.Exists(Path.Combine(sys32, "d3dx9_43.dll"));
                    AddResult("DirectX9/d3d9.dll", string.Empty, hasCore ? ScanResultLevel.Success : ScanResultLevel.Error);
                    AddResult("DirectX9/d3dx9_43.dll", string.Empty, hasJun ? ScanResultLevel.Success : ScanResultLevel.Error);
                }
                catch (Exception)
                {
                    AddResult("DirectX9/运行时检测", string.Empty, ScanResultLevel.Error);
                }
            }

            // 新增：系统媒体功能包（MF.dll、MFPLAT.dll、WMVCore.dll）
            void LogMediaFeaturePack()
            {
                try
                {
                    var sys32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
                    foreach (var f in new[] { "MF.dll", "MFPLAT.dll", "WMVCore.dll" })
                    {
                        bool ok = File.Exists(Path.Combine(sys32, f));
                        AddResult($"媒体功能包/{f}", string.Empty, ok ? ScanResultLevel.Success : ScanResultLevel.Error);
                    }
                }
                catch (Exception)
                {
                    AddResult("媒体功能包/运行时检测", string.Empty, ScanResultLevel.Error);
                }
            }

            void LogVc2010(bool x64)
            {
                string arch = x64 ? "x64" : "x86";
                try
                {
                    var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                    string dllDir = x64 ? Path.Combine(winDir, "System32") : (Environment.Is64BitOperatingSystem ? Path.Combine(winDir, "SysWOW64") : Path.Combine(winDir, "System32"));
                    var dlls = new[] { "msvcr100.dll", "msvcp100.dll" };
                    foreach (var d in dlls)
                    {
                        bool ok = File.Exists(Path.Combine(dllDir, d));
                        AddResult($"VC++2010 {arch}/{d}", string.Empty, ok ? ScanResultLevel.Success : ScanResultLevel.Error);
                    }
                }
                catch (Exception)
                {
                    AddResult($"VC++2010 {arch}/运行时检测", string.Empty, ScanResultLevel.Error);
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
                catch (Exception) { AddResult($"步骤{i + 1}", string.Empty, ScanResultLevel.Error); }
                Report(i + 1);
            }

            Report(StepCount);
            return new ScanSummary(hadError, errorSummary.ToString(), items.ToArray());
            });
        }
    }
}
