using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;

namespace LazyBootstrap
{
    public static class EnvironmentScan
    {
        private const int StepCount = 6; // CPU¡¢GPU¡¢NVIDIA API¡¢DirectX9.0c¡¢VC2010 x86¡¢VC2010 x64

        public static async Task RunAsync(Action<int, string> progress)
        {
            Action<int, string> reportProgressOnly = (p, m) => { try { progress?.Invoke(p, ""); } catch { } };

            int step = 0;
            int Percent() => (int)Math.Round((double)step * 100 / StepCount);

            // ³õÊ¼»¯
            LogSystem.Log("³õÊ¼»¯»·¾³¼ì²â...");
            reportProgressOnly(Percent(), "");
            await Task.Delay(150);

            // 1. CPU ÐÍºÅ
            try
            {
                step++;
                string cpu = GetCpuNameByRegistry();
                LogSystem.Log($"CPU: {cpu}");
            }
            catch (Exception ex)
            {
                LogSystem.Log($"¶ÁÈ¡ CPU ÐÅÏ¢Ê§°Ü: {ex.Message}", LogSystem.LogLevel.Error);
            }
            reportProgressOnly(Percent(), "");
            await Task.Delay(200);

            // 2. GPU ÐÍºÅ£¨×¢²á±í£©
            try
            {
                step++;
                var gpus = GetGpuNamesByRegistry();
                if (gpus.Count == 0)
                {
                    LogSystem.Log("GPU: Î´¼ì²âµ½", LogSystem.LogLevel.Warning);
                }
                else
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("GPU:");
                    foreach (var gpu in gpus)
                    {
                        sb.AppendLine($"  - {gpu}");
                    }
                    LogSystem.Log(sb.ToString().TrimEnd());
                }
            }
            catch (Exception ex)
            {
                LogSystem.Log($"¶ÁÈ¡ GPU ÐÅÏ¢Ê§°Ü: {ex.Message}", LogSystem.LogLevel.Error);
            }
            reportProgressOnly(Percent(), "");
            await Task.Delay(200);

            // 3. NVIDIA API (System32) ÖðÏî
            try
            {
                step++;
                LogNvidiaApiDetailed();
            }
            catch (Exception ex)
            {
                LogSystem.Log($"¼ì²â NVIDIA API Ê§°Ü: {ex.Message}", LogSystem.LogLevel.Error);
            }
            reportProgressOnly(Percent(), "");
            await Task.Delay(200);

            // 4. DirectX 9.0c ¼ì²â£¨ÖðÏî£©
            try
            {
                step++;
                LogDirectX9cDetailed();
            }
            catch (Exception ex)
            {
                LogSystem.Log($"¼ì²â DirectX 9.0c Ê§°Ü: {ex.Message}", LogSystem.LogLevel.Error);
            }
            reportProgressOnly(Percent(), "");
            await Task.Delay(200);

            // 5. VC++ 2010 x86
            try
            {
                step++;
                bool vcredist86 = CheckVCRedist2010(false);
                if (vcredist86)
                    LogSystem.Log("VC++ 2010 x86: ÒÑ°²×°");
                else
                    LogSystem.Log("VC++ 2010 x86: Î´¼ì²âµ½", LogSystem.LogLevel.Error);
            }
            catch (Exception ex)
            {
                LogSystem.Log($"¼ì²â VC++ 2010 x86 Ê§°Ü: {ex.Message}", LogSystem.LogLevel.Error);
            }
            reportProgressOnly(Percent(), "");
            await Task.Delay(200);

            // 6. VC++ 2010 x64
            try
            {
                step++;
                bool vcredist64 = CheckVCRedist2010(true);
                if (vcredist64)
                    LogSystem.Log("VC++ 2010 x64: ÒÑ°²×°");
                else
                    LogSystem.Log("VC++ 2010 x64: Î´¼ì²âµ½", LogSystem.LogLevel.Error);
            }
            catch (Exception ex)
            {
                LogSystem.Log($"¼ì²â VC++ 2010 x64 Ê§°Ü: {ex.Message}", LogSystem.LogLevel.Error);
            }
            reportProgressOnly(Percent(), "");
            await Task.Delay(200);

            LogSystem.Log("»·¾³¼ì²âÍê³É¡£");
            reportProgressOnly(100, "Done");
        }

        private static string GetCpuNameByRegistry()
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
            return "Î´Öª´¦ÀíÆ÷";
        }

        private static List<string> GetGpuNamesByRegistry()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var root = Registry.LocalMachine.OpenSubKey(@"SYSTEM\\CurrentControlSet\\Control\\Video"))
                {
                    if (root != null)
                    {
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
            }
            catch { }

            return set.ToList();
        }

        // °´ÐÐÊä³ö NVIDIA API ¼ì²â½á¹û£º´æÔÚ -> Info£»È±Ê§ -> Warning
        private static void LogNvidiaApiDetailed()
        {
            LogSystem.Log("NVIDIA API (System32):");
            try
            {
                var sys32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
                string[] nvFiles = { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll" };
                foreach (var f in nvFiles)
                {
                    bool ok = File.Exists(Path.Combine(sys32, f));
                    var line = $"  {f}: {(ok ? "ÒÑ¼ì²âµ½" : "Î´¼ì²âµ½")}";
                    LogSystem.Log(line, ok ? LogSystem.LogLevel.Info : LogSystem.LogLevel.Error);
                }
            }
            catch (Exception)
            {
                LogSystem.Log("  ¼ì²âÊ§°Ü", LogSystem.LogLevel.Error);
            }
        }

        // °´ÐÐÊä³ö DirectX 9.0c ¼ì²â½á¹û£º´æÔÚ -> Info£»È±Ê§ -> Error£¨°üº¬ d3dx9_43.dll£©
        private static void LogDirectX9cDetailed()
        {
            LogSystem.Log("DirectX 9.0c:");
            try
            {
                var sys32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
                bool hasCore = File.Exists(Path.Combine(sys32, "d3d9.dll"));
                bool hasJun2010 = File.Exists(Path.Combine(sys32, "d3dx9_43.dll"));

                LogSystem.Log($"  d3d9.dll: {(hasCore ? "¼ì²âµ½ºËÐÄ" : "Î´¼ì²âµ½ºËÐÄ")}", hasCore ? LogSystem.LogLevel.Info : LogSystem.LogLevel.Error);
                LogSystem.Log($"  d3dx9_43.dll: {(hasJun2010 ? "¼ì²âµ½¸¨Öú¿â" : "Î´¼ì²âµ½¸¨Öú¿â")}", hasJun2010 ? LogSystem.LogLevel.Info : LogSystem.LogLevel.Error);
            }
            catch (Exception)
            {
                LogSystem.Log("  ¼ì²âÊ§°Ü", LogSystem.LogLevel.Error);
            }
        }

        private static bool CheckVCRedist2010(bool x64)
        {
            try
            {
                string[] uninstallRoots = new[]
                {
                    @"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall",
                    @"SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall"
                };

                foreach (var root in uninstallRoots)
                {
                    using (var baseKey = Registry.LocalMachine.OpenSubKey(root))
                    {
                        if (baseKey == null) continue;
                        foreach (var sub in baseKey.GetSubKeyNames())
                        {
                            using (var k = baseKey.OpenSubKey(sub))
                            {
                                var name = k?.GetValue("DisplayName") as string;
                                if (string.IsNullOrEmpty(name)) continue;

                                bool is2010 = name.IndexOf("Visual C++", StringComparison.OrdinalIgnoreCase) >= 0
                                              && name.IndexOf("2010", StringComparison.OrdinalIgnoreCase) >= 0
                                              && name.IndexOf("Redistributable", StringComparison.OrdinalIgnoreCase) >= 0;
                                if (!is2010) continue;

                                bool hasX64 = name.IndexOf("x64", StringComparison.OrdinalIgnoreCase) >= 0
                                              || name.IndexOf("amd64", StringComparison.OrdinalIgnoreCase) >= 0;
                                bool hasX86 = name.IndexOf("x86", StringComparison.OrdinalIgnoreCase) >= 0
                                              || name.IndexOf("x32", StringComparison.OrdinalIgnoreCase) >= 0
                                              || (!hasX64);

                                if (x64)
                                {
                                    if (hasX64) return true;
                                }
                                else
                                {
                                    if (hasX86) return true;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
