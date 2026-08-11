using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LazyBootstrap.MediaUpdate
{
    internal static class MediaUpdateRunner
    {
        public const int ExitSecurityBlocked = 4;

        public static async Task<int> RunAsync(
            string gamePath,
            string stagingPath,
            Action<string> log,
            CancellationToken cancellationToken = default,
            Action onUpdateComplete = null,
            Action<string> onSecurityBlockUi = null)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            try
            {
                gamePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gamePath));
                stagingPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagingPath));

                if (!MediaUpdateProtocol.IsValidGameRoot(gamePath))
                {
                    log("错误: 游戏目录中未找到 contents 或 asphyxia。");
                    return 0;
                }

                string syncBat = MediaUpdateProtocol.FindShallowestFile(stagingPath, MediaUpdateProtocol.SyncBatchFileName);
                if (string.IsNullOrEmpty(syncBat) || !File.Exists(syncBat))
                {
                    log($"错误: 在 staging 中未找到 {MediaUpdateProtocol.SyncBatchFileName}。");
                    return 0;
                }

                if (!MediaUpdateSecurity.TryValidateStagingBatches(stagingPath, gamePath, out string securityError))
                {
                    string msg = securityError ?? MediaUpdateSecurity.BlockedNonGamePathMessage;
                    if (onSecurityBlockUi != null)
                    {
                        onSecurityBlockUi(msg);
                    }
                    else
                    {
                        log(msg);
                    }

                    return ExitSecurityBlocked;
                }

                string syncDir = Path.GetDirectoryName(syncBat) ?? stagingPath;
                string updaterLog = Path.Combine(gamePath, "updater_log.txt");

                log("正在结束启动器…");
                TryKillLauncher();
                await Task.Delay(2000, cancellationToken).ConfigureAwait(true);

                log("开始同步资源…");
                int syncCode = await RunSyncBatchAsync(syncBat, syncDir, gamePath, log, cancellationToken).ConfigureAwait(true);
                if (syncCode != 0)
                {
                    log("同步失败。退出代码: " + syncCode);
                    if (File.Exists(updaterLog))
                    {
                        log("详见: " + updaterLog);
                    }

                    return 0;
                }

                string dataMods = Path.Combine(gamePath, "contents", "data_mods");
                string cache = Path.Combine(dataMods, "_cache");
                if (Directory.Exists(dataMods) && Directory.Exists(cache))
                {
                    log("正在清除 data_mods 缓存…");
                    try
                    {
                        Directory.Delete(cache, true);
                    }
                    catch (Exception ex)
                    {
                        log("清理缓存时出现问题: " + ex.Message);
                    }
                }

                log($"正在清理 {MediaUpdateProtocol.UpdateStagingFolderName}…");
                try
                {
                    if (Directory.Exists(stagingPath))
                    {
                        Directory.Delete(stagingPath, true);
                    }
                }
                catch (Exception ex)
                {
                    log("清理临时目录时出现问题: " + ex.Message);
                }

                onUpdateComplete?.Invoke();

                await Task.Delay(5000, cancellationToken).ConfigureAwait(true);

                string gameLauncherPath = Path.Combine(gamePath, MediaUpdateProtocol.GameLauncherExeName);
                string outerShellPath = Path.Combine(gamePath, MediaUpdateProtocol.LauncherProcessImageFileName);
                if (!TryStartShellExe(gameLauncherPath, gamePath, log, MediaUpdateProtocol.GameLauncherExeName))
                {
                    if (!TryStartShellExe(outerShellPath, gamePath, log, MediaUpdateProtocol.LauncherProcessImageFileName))
                    {
                        log(
                            $"未找到 {MediaUpdateProtocol.GameLauncherExeName} 与 {MediaUpdateProtocol.LauncherProcessImageFileName}，请从游戏根目录手动运行启动器。");
                    }
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                log("已取消。");
            }
            catch (Exception ex)
            {
                log("错误: " + ex);
            }

            return 0;
        }

        // Outer shell path is game root LazyBootstrap.exe, not launcher/LazyBootstrap.exe.
        private static bool TryStartShellExe(string exePath, string workingDirectory, Action<string> log, string displayName)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = true
                };
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                log("无法启动 " + displayName + ": " + ex.Message);
                return false;
            }
        }

        private static void TryKillLauncher()
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(MediaUpdateProtocol.LauncherProcessImageFileName);
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try
                    {
                        p.Kill();
                    }
                    catch
                    {
                    }
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
            catch
            {
            }
        }

        private static async Task<int> RunSyncBatchAsync(
            string syncBatPath,
            string workingDirectory,
            string gamePath,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            string cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = cmd,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                StandardOutputEncoding = Encoding.UTF8
            };
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add("call");
            startInfo.ArgumentList.Add(syncBatPath);
            startInfo.Environment[MediaUpdateProtocol.GamePathVariableName] = gamePath;
            startInfo.Environment[MediaUpdateProtocol.SyncFromLauncherVariableName] = "1";

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            var sb = new StringBuilder(65536);
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null)
                {
                    return;
                }

                sb.AppendLine(e.Data);
            };

            if (!process.Start())
            {
                return -1;
            }

            process.BeginOutputReadLine();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(true);
            string combined = sb.ToString();
            if (combined.Length > 0)
            {
                const int max = 20000;
                if (combined.Length > max)
                {
                    log(combined.Substring(0, max) + "…(truncated)");
                }
                else
                {
                    log(combined.TrimEnd());
                }
            }

            return process.ExitCode;
        }
    }
}
