using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using LazyBootstrap.MediaUpdate;
using LazyBootstrap.Services.Paths;
using LazyBootstrap.Services.Shell;
using LazyBootstrap.Services.UI;
using LazyBootstrap.ViewModels;
using Microsoft.Extensions.Logging;

namespace LazyBootstrap.Services.Update
{
    internal sealed class UpdateWorkflowService : IUpdateWorkflowService
    {
        private static readonly string[] UpdateArchiveFilePatterns = ["*.7z", "*.zip", "*.rar", "*.001"];

        private readonly ILauncherPaths _paths;
        private readonly IUiInteractionService _uiInteractionService;
        private readonly IShellStateService _shellStateService;
        private readonly ILogger<UpdateWorkflowService> _logger;

        public UpdateWorkflowService(
            ILauncherPaths paths,
            IUiInteractionService uiInteractionService,
            IShellStateService shellStateService,
            ILogger<UpdateWorkflowService> logger)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
            _shellStateService = shellStateService ?? throw new ArgumentNullException(nameof(shellStateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ApplyUpdateFromUserSelectedArchiveAsync(UpdatePageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            string sevenZip = _paths.ResolveSevenZipExecutablePath();
            if (!File.Exists(sevenZip))
            {
                _uiInteractionService.ShowErrorToast("更新失败", $"未找到 7za：{sevenZip}");
                return;
            }

            if (!MediaUpdatePaths.IsValidGameRoot(_paths.BaseDir))
            {
                _uiInteractionService.ShowErrorToast(
                    "无法更新",
                    "当前游戏目录下未找到 contents 或 asphyxia，请从正确的游戏根目录启动启动器。");
                return;
            }

            string mediaUpdater = Path.Combine(_paths.ApplicationDirectoryPath, KfcUpdateEnvironment.MediaUpdaterExecutableFileName);
            if (!File.Exists(mediaUpdater))
            {
                _uiInteractionService.ShowErrorToast(
                    "更新失败",
                    $"未找到 {KfcUpdateEnvironment.MediaUpdaterExecutableFileName}。请与 LazyBootstrap 一并部署。");
                return;
            }

            string archivePath = await _uiInteractionService.PickFileAsync("选择更新压缩包", UpdateArchiveFilePatterns).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            {
                return;
            }

            string staging = _paths.GetUpdateStagingDirectoryPath();
            _shellStateService.IsInteractionEnabled = false;
            viewModel.IsUpdateBusy = true;
            bool updateDetached = false;
            try
            {
                ClearStagingDirectory(staging);

                if (!await RunSevenZipExtractAsync(sevenZip, archivePath, staging).ConfigureAwait(true))
                {
                    return;
                }

                if (string.IsNullOrEmpty(MediaUpdatePaths.FindShallowestFile(staging, MediaUpdateConstants.SyncBatchFileName)))
                {
                    _uiInteractionService.ShowErrorToast("更新失败", $"压缩包中未找到 {MediaUpdateConstants.SyncBatchFileName}。");
                    return;
                }

                if (!TryStartMediaUpdater(mediaUpdater, _paths.BaseDir, staging, _paths.ApplicationDirectoryPath))
                {
                    _uiInteractionService.ShowErrorToast("更新失败", "无法启动 MediaUpdater。");
                    return;
                }

                updateDetached = true;
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

                    viewModel.IsUpdateBusy = false;
                    _shellStateService.IsInteractionEnabled = true;
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

        private async Task<bool> RunSevenZipExtractAsync(string sevenZipPath, string archivePath, string outputDir)
        {
            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                _uiInteractionService.ShowErrorToast("更新失败", $"无法创建临时目录: {ex.Message}");
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(sevenZipPath) ?? _paths.ApplicationDirectoryPath
            };
            startInfo.ArgumentList.Add("x");
            startInfo.ArgumentList.Add(archivePath);
            startInfo.ArgumentList.Add("-o" + outputDir + Path.DirectorySeparatorChar);
            startInfo.ArgumentList.Add("-y");

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            try
            {
                if (!process.Start())
                {
                    _uiInteractionService.ShowErrorToast("更新失败", "无法启动 7za。");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _uiInteractionService.ShowErrorToast("更新失败", $"无法启动 7za: {ex.Message}");
                return false;
            }

            await process.WaitForExitAsync().ConfigureAwait(true);
            if (process.ExitCode != 0)
            {
                _logger.LogWarning("7za exited with code {Code}.", process.ExitCode);
                _uiInteractionService.ShowErrorToast("解压失败", $"7za 退出代码: {process.ExitCode}");
                return false;
            }

            return true;
        }

        private static bool TryStartMediaUpdater(
            string mediaUpdaterPath,
            string gamePath,
            string stagingPath,
            string applicationDirectoryPath)
        {
            try
            {
                if (string.IsNullOrEmpty(gamePath) || string.IsNullOrEmpty(stagingPath)
                    || gamePath.Contains('"', StringComparison.Ordinal)
                    || stagingPath.Contains('"', StringComparison.Ordinal))
                {
                    return false;
                }

                string g = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gamePath));
                string s = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagingPath));

                var startInfo = new ProcessStartInfo
                {
                    FileName = mediaUpdaterPath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(mediaUpdaterPath) ?? applicationDirectoryPath
                };
                startInfo.ArgumentList.Add("--game");
                startInfo.ArgumentList.Add(g);
                startInfo.ArgumentList.Add("--staging");
                startInfo.ArgumentList.Add(s);

                using var process = Process.Start(startInfo);
                return process != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
