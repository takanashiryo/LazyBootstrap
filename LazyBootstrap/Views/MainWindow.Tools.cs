using System;
using Avalonia.Interactivity;
using LazyBootstrap.Services;

namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private void SetRuntimeInstallProgress(string statusText, double progressValue)
        {
            double normalizedProgressValue = Math.Clamp(progressValue, 0d, 100d);
            if (RuntimeStatusText != null) RuntimeStatusText.Text = statusText ?? string.Empty;
            if (RuntimeProgressBar != null) RuntimeProgressBar.Value = normalizedProgressValue;
            var pct = $"{Math.Clamp((int)Math.Round(normalizedProgressValue), 0, 100)}%";
            if (RuntimeProgressValueText != null) RuntimeProgressValueText.Text = pct;
        }

        private async void OnClearCacheClick(object sender, RoutedEventArgs e) =>
            await AppServices.ToolsWorkflow.ClearCacheAsync();
        private async void OnAddFirewallRuleClick(object sender, RoutedEventArgs e) =>
            await AppServices.ToolsWorkflow.AddFirewallRuleAsync();
        private async void OnOpenAudioPanelClick(object sender, RoutedEventArgs e) =>
            await AppServices.ToolsWorkflow.OpenAudioPanelAsync();
        private async void OnInstallRuntimeClick(object sender, RoutedEventArgs e)
        {
            await AppServices.ToolsWorkflow.InstallRuntimeAsync();
        }
        private async void OnBackupSavedataClick(object sender, RoutedEventArgs e) =>
            await AppServices.ToolsWorkflow.BackupSavedataAsync();
        private async void OnImportSavedataClick(object sender, RoutedEventArgs e) =>
            await AppServices.ToolsWorkflow.ImportSavedataAsync();
        private async void OnMigrateSavedataClick(object sender, RoutedEventArgs e) =>
            await AppServices.ToolsWorkflow.MigrateSavedataAsync();

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_allowImmediateWindowClose)
            {
                PerformFinalWindowCloseCleanup();
                return;
            }

            e.Cancel = true;

            if (_isWindowCloseAnimationRunning || !IsVisible)
            {
                return;
            }

            _ = PlayWindowFadeOutAndCloseAsync();
        }

        private void PerformFinalWindowCloseCleanup()
        {
            AsioDriverRegistry.DisposeControlPanelDrivers();
            try
            {
                _launchWorkflowService.HandleClosingAsync(_displayState).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Final window close cleanup failed.");
            }
        }
    }
}
