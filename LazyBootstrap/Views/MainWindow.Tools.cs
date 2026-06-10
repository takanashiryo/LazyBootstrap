using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LazyBootstrap.Services;

namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private bool _isRuntimeInstallVisible;
        private string _runtimeStatusText = "正在准备安装运行库...";
        private double _runtimeProgressValue;

        private void SetRuntimeInstallProgress(string statusText, double progressValue)
        {
            _runtimeStatusText = statusText ?? string.Empty;
            _runtimeProgressValue = Math.Clamp(progressValue, 0d, 100d);
            if (RuntimeStatusText != null) RuntimeStatusText.Text = _runtimeStatusText;
            if (RuntimeProgressBar != null) RuntimeProgressBar.Value = _runtimeProgressValue;
            var pct = $"{Math.Clamp((int)Math.Round(_runtimeProgressValue), 0, 100)}%";
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
            await AppServices.ToolsWorkflow.InstallRuntimeAsync(
                visible =>
                {
                    _isRuntimeInstallVisible = visible;
                    if (RuntimeInstallOverlay != null)
                    {
                        RuntimeInstallOverlay.IsVisible = visible;
                        RuntimeInstallOverlay.Opacity = visible ? 1 : 0;
                    }
                },
                SetRuntimeInstallProgress);
        }
        private async void OnBackupSavedataClick(object sender, RoutedEventArgs e) =>
            await AppServices.ToolsWorkflow.BackupSavedataAsync();
        private async void OnImportSavedataClick(object sender, RoutedEventArgs e) =>
            await AppServices.ToolsWorkflow.ImportSavedataAsync();
        private async void OnMigrateSavedataClick(object sender, RoutedEventArgs e) =>
            await AppServices.ToolsWorkflow.MigrateSavedataAsync();
        private void SetControlsEnabled(bool enabled)
        {
            foreach (var c in GetToggleableControls())
            {
                if (c is not null) c.IsEnabled = enabled;
            }

            if (SelectSubScreenAreaButton != null)
                SelectSubScreenAreaButton.IsEnabled = enabled && _displayState.IsDualDisplay;

            if (KillProcessesButton != null) KillProcessesButton.IsEnabled = true;

            if (enabled)
            {
                UpdateGpuCompatLayerStatus();
                UpdateDisplayLayoutControlsEnabled();
            }
        }

        private Control[] GetToggleableControls() =>
        [
            StartButton!,
            WindowedToggleSwitch!,
            NoAsphyxiaToggleSwitch!,
            ExitRestoreToggleSwitch!,
            EditConfigButton!,
            UseSystemSpiceConfigRow!,
            UseSystemSpiceConfigToggleSwitch!,
            EmptyStateEditConfigButton!,
            ServerPresetComboBox!,
            AddServerPresetButton!,
            DeleteServerPresetButton!,
            GpuCompatLayerToggleSwitch!,
            GpuCompatLayerDx9on12RadioButton!,
            GpuCompatLayerDx9on12ExternalRadioButton!,
            GpuCompatLayerDxvkRadioButton!,
            OpenLogButton!,
            TouchPanelButton!,
            NetDumpToggleSwitch!,
            DisableSubDisplayToggleSwitch!,
            WindowModeComboBox!,
            PCoreOptimizationToggleSwitch!,
            SubBorderlessToggleSwitch!,
            ShowCursorTouchSimToggleSwitch!,
            WindowTopMostToggleSwitch!,
            WindowSizeTextBox!,
            SingleAdapterToggleSwitch!,
            SubWindowTopMostToggleSwitch!,
            SubForceRenderToggleSwitch!,
            NativeTouchToggleSwitch!,
            AsioDriverComboBox!,
            Asio2ChToggleSwitch!,
            VolumeBoostComboBox!,
            ResampleComboBox!,
            LowLatencySharedAudioToggleSwitch!,
            CardIoToggleSwitch!,
            HidSmartCardToggleSwitch!,
            ServerAddressTextBox!,
            PcbIdTextBox!,
            DisplayConfigEnabledToggleSwitch!,
            DisplayModeComboBox!,
            MainScreenComboBox!,
            MainResolutionComboBox!,
            MainRefreshRateComboBox!,
            SubScreenComboBox!,
            SubRotationComboBox!,
            SubResolutionComboBox!,
            SubRefreshRateComboBox!,
            RotationComboBox!,
            PreviewDisplaySettingsButton!,
            SelectMainScreenAreaButton!,
            ClearCacheButton!,
            InstallRuntimeButton!,
            AddFirewallRuleButton!,
            AudioPanelButton!,
            SavedataBackupButton!,
            SavedataImportButton!,
            SavedataMigrateButton!,
        ];

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
