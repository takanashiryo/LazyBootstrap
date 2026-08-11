using System;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using LazyBootstrap.Platform;

namespace LazyBootstrap.UI
{
    public partial class MainWindow
    {
        private const string ProjectRepositoryUrl = "https://github.com/takanashiryo/LazyBootstrap";

        /// <summary>Applies the resolved launcher version (populated by the diagnostic scan) to the about page.</summary>
        private void ApplyAboutVersion()
        {
            if (LauncherVersionTextBlock != null)
            {
                LauncherVersionTextBlock.Text = _environmentScanResult.LauncherVersion;
            }
        }

        private void OnOpenGitHubRepositoryClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ProcessExecutionHelper.StartShellProcess(
                    ProjectRepositoryUrl,
                    _paths.ApplicationDirectoryPath,
                    false)?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open the project repository in the default browser.");
                _uiInteractionService.ShowErrorToast("无法打开 GitHub", "请检查系统默认浏览器设置后重试。");
            }
        }
    }
}
