using System;
using System.Diagnostics;
using System.IO;

namespace LazyBootstrap
{
    public static class FirewallHelper
    {
        public static void EnsureFirewallRule(string ruleName, string programPath, Action<string> log)
        {
            string spicePath = Path.GetFullPath(programPath);
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
                    log?.Invoke("防火墙规则添加完成。");
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                {
                    log?.Invoke("用户取消了 UAC 提示，防火墙规则未添加。");
                }
                else
                {
                    log?.Invoke($"防火墙规则处理失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"防火墙规则处理失败: {ex.Message}");
            }
        }
    }
}
