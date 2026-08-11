using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.Logging;
using LazyBootstrap.Infrastructure.Paths;
using LazyBootstrap.Infrastructure.Processes;
using LazyBootstrap.MediaUpdate;
using LazyBootstrap.Shell;

namespace LazyBootstrap.Features.Update
{
    public sealed class UpdateOrchestrator
    {
        private const string RequiredUpdateArchiveFileNamePrefix = "UPDATE_LAZY_KFC";
        private static readonly string[] UpdateArchiveFilePatterns = ["*.7z", "*.zip", "*.rar", "*.001"];
        private static readonly string[] UpdateArchiveExtensions = [".7z", ".zip", ".rar", ".001"];

        private enum MediaUpdaterStartResult
        {
            Started,
            CancelledByUser,
            Failed
        }

        private readonly LauncherPaths _paths;
        private readonly UiInteractionService _uiInteractionService;
        private readonly ILogger<UpdateOrchestrator> _logger;

        public UpdateOrchestrator(
            LauncherPaths paths,
            UiInteractionService uiInteractionService,
            ILogger<UpdateOrchestrator> logger)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ApplyUpdateFromUserSelectedArchiveAsync(Action<string> reportProgress)
        {
            _logger.LogInformation("KFC update workflow requested.");

            string sevenZip = _paths.ResolveSevenZipExecutablePath();
            if (!File.Exists(sevenZip))
            {
                _logger.LogWarning("KFC update aborted because 7za was not found: {SevenZipPath}", sevenZip);
                _uiInteractionService.ShowErrorToast("更新失败", $"未找到 7za：{sevenZip}");
                return;
            }

            if (!MediaUpdatePaths.IsValidGameRoot(_paths.BaseDir))
            {
                _logger.LogWarning("KFC update aborted because the base directory is not a valid game root: {BaseDir}", _paths.BaseDir);
                _uiInteractionService.ShowErrorToast(
                    "无法更新",
                    "当前游戏目录下未找到 contents 或 asphyxia，请从正确的游戏根目录启动启动器。");
                return;
            }

            string mediaUpdater = Path.Combine(_paths.ApplicationDirectoryPath, KfcUpdateEnvironment.MediaUpdaterExecutableFileName);
            if (!File.Exists(mediaUpdater))
            {
                _logger.LogWarning("KFC update aborted because MediaUpdater was not found: {MediaUpdaterPath}", mediaUpdater);
                _uiInteractionService.ShowErrorToast(
                    "更新失败",
                    $"未找到 {KfcUpdateEnvironment.MediaUpdaterExecutableFileName}。请与 LazyBootstrap 一并部署。");
                return;
            }

            string archivePath = await _uiInteractionService.PickFileAsync("选择更新压缩包", UpdateArchiveFilePatterns).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            {
                _logger.LogInformation("KFC update cancelled before archive selection.");
                return;
            }

            if (!IsSupportedUpdateArchive(archivePath))
            {
                _logger.LogWarning("KFC update rejected because the selected file is not a supported archive: {ArchivePath}", archivePath);
                _uiInteractionService.ShowErrorToast("更新失败", "仅支持 7z、zip、rar 或 001 更新压缩包。");
                return;
            }

            if (!HasRequiredUpdateArchivePrefix(archivePath))
            {
                _logger.LogWarning("KFC update rejected because the selected archive file name has an invalid prefix: {ArchivePath}", archivePath);
                _uiInteractionService.ShowErrorToast("更新失败", $"更新压缩包文件名必须以 {RequiredUpdateArchiveFileNamePrefix} 开头。");
                return;
            }

            _logger.LogInformation("KFC update archive selected: {ArchivePath}", archivePath);

            string staging = _paths.GetUpdateStagingDirectoryPath();
            reportProgress?.Invoke("正在准备更新...");
            bool updateDetached = false;
            try
            {
                reportProgress?.Invoke("正在清理更新临时目录...");
                _logger.LogInformation("Clearing update staging directory: {StagingDirectory}", staging);
                ClearStagingDirectory(staging);

                reportProgress?.Invoke("正在解压更新压缩包...");
                if (!await RunSevenZipExtractAsync(sevenZip, archivePath, staging).ConfigureAwait(true))
                {
                    _logger.LogWarning("KFC update aborted because archive extraction failed.");
                    return;
                }

                if (string.IsNullOrEmpty(MediaUpdatePaths.FindShallowestFile(staging, MediaUpdateConstants.SyncBatchFileName)))
                {
                    _logger.LogWarning("KFC update aborted because sync batch was not found in staging.");
                    _uiInteractionService.ShowErrorToast("更新失败", $"压缩包中未找到 {MediaUpdateConstants.SyncBatchFileName}。");
                    return;
                }

                reportProgress?.Invoke("正在启动更新程序...");
                var updaterStartResult = TryStartMediaUpdater(
                    mediaUpdater,
                    _paths.BaseDir,
                    staging,
                    _paths.ApplicationDirectoryPath,
                    out string updaterStartError);

                if (updaterStartResult != MediaUpdaterStartResult.Started)
                {
                    if (updaterStartResult == MediaUpdaterStartResult.CancelledByUser)
                    {
                        _logger.LogWarning("KFC update cancelled because MediaUpdater elevation was cancelled.");
                        _uiInteractionService.ShowWarningToast("更新已取消", updaterStartError);
                    }
                    else
                    {
                        _logger.LogWarning("KFC update failed because MediaUpdater could not be started. Error={Error}", updaterStartError);
                        _uiInteractionService.ShowErrorToast(
                            "更新失败",
                            string.IsNullOrWhiteSpace(updaterStartError) ? "无法启动 MediaUpdater。" : updaterStartError);
                    }

                    return;
                }

                updateDetached = true;
                _logger.LogInformation("MediaUpdater started successfully. Main application will exit.");
                _uiInteractionService.ShowInfoToast(
                    "开始更新",
                    "本程序将立即退出。请在弹出的更新窗口中完成操作；结束后可通过 启动.exe 继续。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KFC update workflow failed.");
                _uiInteractionService.ShowErrorToast("更新失败", ex.Message);
            }
            finally
            {
                if (!updateDetached)
                {
                    try
                    {
                        ClearStagingDirectory(staging);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to clear update staging directory.");
                    }
                }
            }

            if (updateDetached)
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime life)
                {
                    life.Shutdown(0);
                }
                else
                {
                    global::System.Environment.Exit(0);
                }
            }
        }

        private static void ClearStagingDirectory(string staging)
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
            }
        }

        private static bool IsSupportedUpdateArchive(string archivePath)
        {
            string extension = Path.GetExtension(archivePath);
            foreach (string supportedExtension in UpdateArchiveExtensions)
            {
                if (string.Equals(extension, supportedExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasRequiredUpdateArchivePrefix(string archivePath)
        {
            string fileName = Path.GetFileName(archivePath);
            return fileName.StartsWith(RequiredUpdateArchiveFileNamePrefix, StringComparison.Ordinal);
        }

        private async Task<bool> RunSevenZipExtractAsync(string sevenZipPath, string archivePath, string outputDir)
        {
            try
            {
                Directory.CreateDirectory(outputDir);
                _logger.LogInformation("Update extraction directory prepared: {OutputDirectory}", outputDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create update extraction directory.");
                _uiInteractionService.ShowErrorToast("更新失败", $"无法创建临时目录: {ex.Message}");
                return false;
            }

            string workingDirectory = Path.GetDirectoryName(sevenZipPath) ?? _paths.ApplicationDirectoryPath;

            int exitCode;
            try
            {
                _logger.LogInformation(
                    "Starting update extraction. Elevated={IsElevated}",
                    ProcessExecutionHelper.IsCurrentProcessElevated());
                exitCode = await ProcessExecutionHelper.RunShellProcessAsync(
                    sevenZipPath,
                    workingDirectory,
                    true,
                    startInfo =>
                    {
                        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        startInfo.ArgumentList.Add("x");
                        startInfo.ArgumentList.Add(archivePath);
                        startInfo.ArgumentList.Add("-o" + outputDir + Path.DirectorySeparatorChar);
                        startInfo.ArgumentList.Add("-y");
                    }).ConfigureAwait(true);

                if (exitCode == -1)
                {
                    _logger.LogWarning("Update extraction failed because 7za process start returned false.");
                    _uiInteractionService.ShowErrorToast("更新失败", "无法启动 7za。");
                    return false;
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                _logger.LogWarning("Update extraction cancelled at UAC prompt.");
                _uiInteractionService.ShowWarningToast("解压已取消", "用户取消了 7za 管理员授权。");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start update extraction process.");
                _uiInteractionService.ShowErrorToast("更新失败", $"无法启动 7za: {ex.Message}");
                return false;
            }

            _logger.LogInformation("Update extraction process exited. ExitCode={ExitCode}", exitCode);
            if (exitCode != 0)
            {
                _logger.LogWarning("7za exited with code {Code}.", exitCode);
                _uiInteractionService.ShowErrorToast("解压失败", $"7za 退出代码: {exitCode}");
                return false;
            }

            return true;
        }

        private static MediaUpdaterStartResult TryStartMediaUpdater(
            string mediaUpdaterPath,
            string gamePath,
            string stagingPath,
            string applicationDirectoryPath,
            out string error)
        {
            error = null;

            try
            {
                if (string.IsNullOrEmpty(gamePath) || string.IsNullOrEmpty(stagingPath)
                    || gamePath.Contains('"', StringComparison.Ordinal)
                    || stagingPath.Contains('"', StringComparison.Ordinal))
                {
                    error = "更新路径无效。";
                    return MediaUpdaterStartResult.Failed;
                }

                string g = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gamePath));
                string s = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagingPath));

                using var process = ProcessExecutionHelper.StartShellProcess(
                    mediaUpdaterPath,
                    Path.GetDirectoryName(mediaUpdaterPath) ?? applicationDirectoryPath,
                    true,
                    startInfo =>
                    {
                        startInfo.ArgumentList.Add("--game");
                        startInfo.ArgumentList.Add(g);
                        startInfo.ArgumentList.Add("--staging");
                        startInfo.ArgumentList.Add(s);
                    });

                if (process == null)
                {
                    error = "未能创建 MediaUpdater 进程。";
                    return MediaUpdaterStartResult.Failed;
                }

                return MediaUpdaterStartResult.Started;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                error = "用户取消了 MediaUpdater 管理员授权。";
                return MediaUpdaterStartResult.CancelledByUser;
            }
            catch (Exception ex)
            {
                error = $"无法启动 MediaUpdater: {ex.Message}";
                return MediaUpdaterStartResult.Failed;
            }
        }
    }
}
