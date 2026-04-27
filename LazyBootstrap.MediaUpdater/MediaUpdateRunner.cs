using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LazyBootstrap.MediaUpdate
{
    public static class MediaUpdateRunner
    {
        public static async Task RunAsync(
            string gamePath,
            string stagingPath,
            Action<string> log,
            CancellationToken cancellationToken = default,
            Action onUpdateComplete = null)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            try
            {
                gamePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gamePath));
                stagingPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagingPath));

                if (!IsValidGameLayout(gamePath))
                {
                    log("错误: 游戏目录中未找到 contents 或 asphyxia。");
                    return;
                }

                string syncBat = FindShallowestFile(stagingPath, MediaUpdateConstants.SyncBatchFileName);
                if (string.IsNullOrEmpty(syncBat) || !File.Exists(syncBat))
                {
                    log($"错误: 在 staging 中未找到 {MediaUpdateConstants.SyncBatchFileName}。");
                    return;
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

                    return;
                }

                string cache = Path.Combine(gamePath, "contents", "data_mods", "_cache");
                if (Directory.Exists(Path.GetDirectoryName(cache))
                    && Directory.Exists(cache))
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

                log($"正在清理 {MediaUpdateConstants.UpdateStagingFolderName}…");
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

                string starter = Path.Combine(gamePath, MediaUpdateConstants.GameLauncherExeName);
                if (File.Exists(starter))
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = starter,
                            WorkingDirectory = gamePath,
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    }
                    catch (Exception ex)
                    {
                        log("无法启动 " + MediaUpdateConstants.GameLauncherExeName + ": " + ex.Message);
                    }
                }
                else
                {
                    log("未找到 " + MediaUpdateConstants.GameLauncherExeName + "，请从游戏根目录手动运行启动器。");
                }
            }
            catch (OperationCanceledException)
            {
                log("已取消。");
            }
            catch (Exception ex)
            {
                log("错误: " + ex);
            }
        }

        private static bool IsValidGameLayout(string baseDir)
        {
            return Directory.Exists(Path.Combine(baseDir, "contents"))
                   && Directory.Exists(Path.Combine(baseDir, "asphyxia"));
        }

        private static string FindShallowestFile(string root, string fileName)
        {
            if (!Directory.Exists(root))
            {
                return string.Empty;
            }

            return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories)
                .OrderBy(p => p.Length)
                .FirstOrDefault() ?? string.Empty;
        }

        private static void TryKillLauncher()
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(MediaUpdateConstants.LauncherProcessImageFileName);
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
            startInfo.Environment[MediaUpdateConstants.GamePathVariableName] = gamePath;
            startInfo.Environment[MediaUpdateConstants.SyncFromLauncherVariableName] = "1";

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
