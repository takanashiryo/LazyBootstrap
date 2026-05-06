using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private void HookToolsViewModelState()
        {
            if (_viewModel?.Tools == null)
            {
                return;
            }

            _viewModel.Tools.PropertyChanged -= OnToolsViewModelPropertyChanged;
            _viewModel.Tools.PropertyChanged += OnToolsViewModelPropertyChanged;
            ApplyRuntimeInstallOverlayVisibilityAsync().ForgetWithLogging(_logger, "Failed to apply runtime install overlay visibility.");
        }

        private void UnhookToolsViewModelState()
        {
            if (_viewModel?.Tools == null)
            {
                return;
            }

            _viewModel.Tools.PropertyChanged -= OnToolsViewModelPropertyChanged;
        }

        private void OnToolsViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e?.PropertyName)
                || string.Equals(e.PropertyName, nameof(ToolsPageViewModel.IsRuntimeInstallVisible), StringComparison.Ordinal))
            {
                ApplyRuntimeInstallOverlayVisibilityAsync().ForgetWithLogging(_logger, "Failed to apply runtime install overlay visibility.");
            }
        }

        private async Task ApplyRuntimeInstallOverlayVisibilityAsync()
        {
            if (RuntimeInstallOverlay == null)
            {
                return;
            }

            if (_viewModel.Tools.IsRuntimeInstallVisible)
            {
                RuntimeInstallOverlay.IsVisible = true;
                RuntimeInstallOverlay.Opacity = 1;
                return;
            }

            RuntimeInstallOverlay.Opacity = 0;
            await Task.Delay(300);

            if (!_viewModel.Tools.IsRuntimeInstallVisible)
            {
                RuntimeInstallOverlay.IsVisible = false;
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            foreach (var c in GetToggleableControls())
            {
                if (c is not null) c.IsEnabled = enabled;
            }

            if (SelectSubScreenAreaButton != null)
                SelectSubScreenAreaButton.IsEnabled = enabled && _viewModel.Display.IsDualDisplay;

            if (KillProcessesButton != null) KillProcessesButton.IsEnabled = true;

            if (enabled)
            {
                UpdateCompatLayerStatus();
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
            CompatLayerToggleSwitch!,
            CompatDx9on12RadioButton!,
            CompatDx9on12ExternalRadioButton!,
            CompatDxvkRadioButton!,
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
                // HandleClosingAsync is currently synchronous (returns completed tasks); keep a sync wait for the final close path.
                _viewModel.HandleClosingAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Final window close cleanup failed.");
            }
        }
    }
}
