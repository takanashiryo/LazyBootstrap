using System;
using Microsoft.Extensions.Logging;
using LazyBootstrap.Features.Settings;

namespace LazyBootstrap.Shell
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
            ReleaseLaunchControls();
            try
            {
                HandleLaunchClosingAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Final window close cleanup failed.");
            }
        }
    }
}
