using System;
using System.ComponentModel;
using System.Threading.Tasks;

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
            _ = ApplyRuntimeInstallOverlayVisibilityAsync();
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
                _ = ApplyRuntimeInstallOverlayVisibilityAsync();
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
            if (StartButton != null) StartButton.IsEnabled = enabled;

            if (WindowedToggleSwitch != null) WindowedToggleSwitch.IsEnabled = enabled;
            if (NoAsphyxiaToggleSwitch != null) NoAsphyxiaToggleSwitch.IsEnabled = enabled;
            if (ExitRestoreToggleSwitch != null) ExitRestoreToggleSwitch.IsEnabled = enabled;
            if (EditConfigButton != null) EditConfigButton.IsEnabled = enabled;
            if (UseSystemSpiceConfigRow != null) UseSystemSpiceConfigRow.IsEnabled = enabled;
            else if (UseSystemSpiceConfigToggleSwitch != null) UseSystemSpiceConfigToggleSwitch.IsEnabled = enabled;
            if (EmptyStateEditConfigButton != null) EmptyStateEditConfigButton.IsEnabled = enabled;
            if (ServerPresetComboBox != null) ServerPresetComboBox.IsEnabled = enabled;
            if (AddServerPresetButton != null) AddServerPresetButton.IsEnabled = enabled;
            if (DeleteServerPresetButton != null) DeleteServerPresetButton.IsEnabled = enabled;
            if (CompatLayerToggleSwitch != null) CompatLayerToggleSwitch.IsEnabled = enabled;
            if (CompatDx9on12RadioButton != null) CompatDx9on12RadioButton.IsEnabled = enabled;
            if (CompatDx9on12ExternalRadioButton != null) CompatDx9on12ExternalRadioButton.IsEnabled = enabled;
            if (CompatDxvkRadioButton != null) CompatDxvkRadioButton.IsEnabled = enabled;
            if (OpenLogButton != null) OpenLogButton.IsEnabled = enabled;
            if (TouchPanelButton != null) TouchPanelButton.IsEnabled = enabled;
            if (NetDumpToggleSwitch != null) NetDumpToggleSwitch.IsEnabled = enabled;
            if (DisableSubDisplayToggleSwitch != null) DisableSubDisplayToggleSwitch.IsEnabled = enabled;
            if (WindowModeComboBox != null) WindowModeComboBox.IsEnabled = enabled;
            if (PCoreOptimizationToggleSwitch != null) PCoreOptimizationToggleSwitch.IsEnabled = enabled;
            if (SubBorderlessToggleSwitch != null) SubBorderlessToggleSwitch.IsEnabled = enabled;
            if (ShowCursorTouchSimToggleSwitch != null) ShowCursorTouchSimToggleSwitch.IsEnabled = enabled;
            if (WindowTopMostToggleSwitch != null) WindowTopMostToggleSwitch.IsEnabled = enabled;
            if (WindowSizeTextBox != null) WindowSizeTextBox.IsEnabled = enabled;
            if (SingleAdapterToggleSwitch != null) SingleAdapterToggleSwitch.IsEnabled = enabled;
            if (SubWindowTopMostToggleSwitch != null) SubWindowTopMostToggleSwitch.IsEnabled = enabled;
            if (SubForceRenderToggleSwitch != null) SubForceRenderToggleSwitch.IsEnabled = enabled;
            if (NativeTouchToggleSwitch != null) NativeTouchToggleSwitch.IsEnabled = enabled;
            if (AsioDriverComboBox != null) AsioDriverComboBox.IsEnabled = enabled;
            if (LowLatencySharedAudioToggleSwitch != null) LowLatencySharedAudioToggleSwitch.IsEnabled = enabled;
            if (CardIoToggleSwitch != null) CardIoToggleSwitch.IsEnabled = enabled;
            if (HidSmartCardToggleSwitch != null) HidSmartCardToggleSwitch.IsEnabled = enabled;
            if (ServerAddressTextBox != null) ServerAddressTextBox.IsEnabled = enabled;
            if (PcbIdTextBox != null) PcbIdTextBox.IsEnabled = enabled;
            if (DisplayConfigEnabledToggleSwitch != null) DisplayConfigEnabledToggleSwitch.IsEnabled = enabled;
            if (DisplayModeComboBox != null) DisplayModeComboBox.IsEnabled = enabled;
            if (MainScreenComboBox != null) MainScreenComboBox.IsEnabled = enabled;
            if (MainResolutionComboBox != null) MainResolutionComboBox.IsEnabled = enabled;
            if (MainRefreshRateComboBox != null) MainRefreshRateComboBox.IsEnabled = enabled;
            if (SubScreenComboBox != null) SubScreenComboBox.IsEnabled = enabled;
            if (SubRotationComboBox != null) SubRotationComboBox.IsEnabled = enabled;
            if (SubResolutionComboBox != null) SubResolutionComboBox.IsEnabled = enabled;
            if (SubRefreshRateComboBox != null) SubRefreshRateComboBox.IsEnabled = enabled;
            if (RotationComboBox != null) RotationComboBox.IsEnabled = enabled;
            if (PreviewDisplaySettingsButton != null) PreviewDisplaySettingsButton.IsEnabled = enabled;
            if (SelectMainScreenAreaButton != null) SelectMainScreenAreaButton.IsEnabled = enabled;
            if (SelectSubScreenAreaButton != null) SelectSubScreenAreaButton.IsEnabled = enabled && _viewModel.Display.IsDualDisplay;

            if (ClearCacheButton != null) ClearCacheButton.IsEnabled = enabled;
            if (InstallRuntimeButton != null) InstallRuntimeButton.IsEnabled = enabled;
            if (AddFirewallRuleButton != null) AddFirewallRuleButton.IsEnabled = enabled;
            if (AudioPanelButton != null) AudioPanelButton.IsEnabled = enabled;
            if (SavedataBackupButton != null) SavedataBackupButton.IsEnabled = enabled;
            if (SavedataImportButton != null) SavedataImportButton.IsEnabled = enabled;
            if (SavedataMigrateButton != null) SavedataMigrateButton.IsEnabled = enabled;
            if (KillProcessesButton != null) KillProcessesButton.IsEnabled = true;

            if (enabled)
            {
                UpdateCompatLayerStatus();
                UpdateDisplayLayoutControlsEnabled();
            }
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
            try
            {
                _viewModel.HandleClosingAsync().GetAwaiter().GetResult();
            }
            catch (Exception) { }
        }
    }
}
