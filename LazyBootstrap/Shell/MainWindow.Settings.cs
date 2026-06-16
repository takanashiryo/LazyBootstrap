using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LazyBootstrap.Shell
{
    public partial class MainWindow
    {
        private async void OnEditConfigClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await _settingsWorkflowService.EditConfigAsync(_settingsState);
                ApplyStartupSettingsStateToUi();
                ApplyDeferredSettingsStateToUi();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Edit spicecfg workflow failed.");
            }
        }

        private async void OnSelectGpuCompatLayerRenderModeClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn)
            {
                return;
            }

            var mode = btn.Tag?.ToString() ?? btn.CommandParameter?.ToString() ?? string.Empty;
            string normalizedMode = GpuCompatLayerConfigurator.NormalizeRenderMode(mode);
            if (string.Equals(
                    GpuCompatLayerConfigurator.NormalizeRenderMode(_settingsState.GpuCompatLayerRenderMode),
                    normalizedMode,
                    StringComparison.OrdinalIgnoreCase))
            {
                _settingsState.GpuCompatLayerRenderMode = normalizedMode;
                UpdateGpuCompatLayerStatus();
                return;
            }

            _settingsState.GpuCompatLayerRenderMode = normalizedMode;
            await _settingsWorkflowService.PersistGpuCompatLayerRenderModeAsync(_settingsState);
            UpdateGpuCompatLayerStatus();
        }

        private async void OnAddServerPresetClick(object sender, RoutedEventArgs e)
        {
            await _settingsWorkflowService.AddServerPresetAsync(_settingsState);
            ApplyServerPresetStateToUi();
        }

        private async void OnDeleteServerPresetClick(object sender, RoutedEventArgs e)
        {
            await _settingsWorkflowService.DeleteServerPresetAsync(_settingsState);
            ApplyServerPresetStateToUi();
        }

        private async void OnOpenNetworkAdapterPickerClick(object sender, RoutedEventArgs e)
        {
            await _settingsWorkflowService.OpenNetworkAdapterPickerAsync(_settingsState);
            ApplyNetworkAdapterStateFromState();
        }

        private async void OnOpenAsioControlPanelClick(object sender, RoutedEventArgs e)
        {
            await _settingsWorkflowService.OpenAsioControlPanelAsync(_settingsState);
        }

        private void ApplyStartupSettingsStateToUi()
        {
            bool previousLoadingState = _isLoadingSettings;
            _isLoadingSettings = true;

            try
            {
                if (NoAsphyxiaToggleSwitch != null)
                {
                    NoAsphyxiaToggleSwitch.IsChecked = _settingsState.NoAsphyxia;
                }

                if (UseSystemSpiceConfigToggleSwitch != null)
                {
                    UseSystemSpiceConfigToggleSwitch.IsChecked = _settingsState.UseSystemSpiceConfig;
                }

                UpdateGpuCompatLayerStatus();
                ApplyServerPresetStateToUi();
                ApplySettingsAvailabilityStateToUi();
            }
            finally
            {
                _isLoadingSettings = previousLoadingState;
            }
        }

        private void ApplyDeferredSettingsStateToUi()
        {
            bool previousLoadingState = _isLoadingSettings;
            _isLoadingSettings = true;

            try
            {
                ApplySettingsAvailabilityStateToUi();
                ApplyInfoStateToUi();
                if (!_settingsState.IsSpiceConfigAvailable)
                {
                    return;
                }

                ApplySpiceSettingsFromState();
                ApplySpiceTextInputsFromState();
                ApplyAsioDriverChoicesFromState();
                ApplyNetworkAdapterStateFromState();
                ApplyServerPresetStateToUi();
                UpdateGpuCompatLayerStatus();
            }
            finally
            {
                _isLoadingSettings = previousLoadingState;
            }
        }

        private void ApplySettingsAvailabilityStateToUi()
        {
            bool isSpiceConfigAvailable = _settingsState.IsSpiceConfigAvailable;

            if (GameSettingsLayout != null)
            {
                GameSettingsLayout.IsVisible = isSpiceConfigAvailable;
            }

            if (SettingsEmptyStatePanel != null)
            {
                SettingsEmptyStatePanel.IsVisible = !isSpiceConfigAvailable;
            }

            if (SettingsEmptyStateTextBlock != null)
            {
                SettingsEmptyStateTextBlock.Text = _settingsState.SpiceConfigEmptyStateMessage ?? string.Empty;
            }

            if (SettingsMoreFeaturesHintTextBlock != null)
            {
                SettingsMoreFeaturesHintTextBlock.IsVisible = isSpiceConfigAvailable;
            }

            UpdateUseSystemSpiceConfigSwitchVisibility();
        }

        private void ApplySpiceSettingsFromState()
        {
            if (WindowedToggleSwitch != null)
            {
                WindowedToggleSwitch.IsChecked = _settingsState.Windowed;
            }

            if (NetDumpToggleSwitch != null)
            {
                NetDumpToggleSwitch.IsChecked = _settingsState.NetDump;
            }

            if (DisableSubDisplayToggleSwitch != null)
            {
                DisableSubDisplayToggleSwitch.IsChecked = _settingsState.DisableSubDisplay;
            }

            if (WindowModeComboBox != null)
            {
                WindowModeComboBox.SelectedIndex = Math.Clamp(_settingsState.WindowModeIndex, 0, Math.Max(0, WindowModeComboBox.ItemCount - 1));
            }

            if (PCoreOptimizationToggleSwitch != null)
            {
                PCoreOptimizationToggleSwitch.IsChecked = _settingsState.PCoreOptimization;
            }

            if (SubBorderlessToggleSwitch != null)
            {
                SubBorderlessToggleSwitch.IsChecked = _settingsState.SubBorderless;
            }

            if (ShowCursorTouchSimToggleSwitch != null)
            {
                ShowCursorTouchSimToggleSwitch.IsChecked = _settingsState.ShowCursorTouchSim;
            }

            if (WindowTopMostToggleSwitch != null)
            {
                WindowTopMostToggleSwitch.IsChecked = _settingsState.WindowTopMost;
            }

            if (SingleAdapterToggleSwitch != null)
            {
                SingleAdapterToggleSwitch.IsChecked = _settingsState.SingleAdapter;
            }

            if (NvidiaPerformanceProfileToggleSwitch != null)
            {
                NvidiaPerformanceProfileToggleSwitch.IsChecked = _settingsState.NvidiaPerformanceProfile;
            }

            if (SubWindowTopMostToggleSwitch != null)
            {
                SubWindowTopMostToggleSwitch.IsChecked = _settingsState.SubWindowTopMost;
            }

            if (SubForceRenderToggleSwitch != null)
            {
                SubForceRenderToggleSwitch.IsChecked = _settingsState.SubForceRender;
            }

            if (NativeTouchToggleSwitch != null)
            {
                NativeTouchToggleSwitch.IsChecked = _settingsState.NativeTouch;
            }

            if (Asio2ChToggleSwitch != null)
            {
                Asio2ChToggleSwitch.IsChecked = _settingsState.Asio2Ch;
            }

            if (VolumeBoostComboBox != null)
            {
                VolumeBoostComboBox.SelectedIndex = Math.Clamp(_settingsState.VolumeBoostIndex, 0, Math.Max(0, VolumeBoostComboBox.ItemCount - 1));
            }

            if (ResampleComboBox != null)
            {
                ResampleComboBox.SelectedIndex = Math.Clamp(_settingsState.ResampleIndex, 0, Math.Max(0, ResampleComboBox.ItemCount - 1));
            }

            if (WasapiSharedToggleSwitch != null)
            {
                WasapiSharedToggleSwitch.IsChecked = _settingsState.WasapiShared;
            }

            if (LowLatencySharedAudioToggleSwitch != null)
            {
                LowLatencySharedAudioToggleSwitch.IsChecked = _settingsState.LowLatencySharedAudio;
            }

            if (CardIoToggleSwitch != null)
            {
                CardIoToggleSwitch.IsChecked = _settingsState.CardIo;
            }

            if (HidSmartCardToggleSwitch != null)
            {
                HidSmartCardToggleSwitch.IsChecked = _settingsState.HidSmartCard;
            }
        }

        private void ApplySpiceTextInputsFromState()
        {
            SetTextBoxTextIfNeeded(DllInjectionTextBox, _settingsState.DllInjection);
            SetTextBoxTextIfNeeded(WindowSizeTextBox, _settingsState.WindowSize);
        }

        private void ApplyAsioDriverChoicesFromState()
        {
            if (AsioDriverComboBox == null)
            {
                return;
            }

            var choices = _settingsState.AsioDrivers.Count > 0
                ? _settingsState.AsioDrivers.ToList()
                : new List<AsioDriverOption> { new("无", string.Empty) };

            var selectedValue = _settingsState.SelectedAsioDriver?.Value
                ?? _settingsState.AsioDriverValue
                ?? string.Empty;

            var targetChoice = choices.FirstOrDefault(choice =>
                string.Equals(choice.Value, selectedValue, StringComparison.OrdinalIgnoreCase))
                ?? choices.FirstOrDefault()
                ?? new AsioDriverOption("无", string.Empty);

            _isUpdatingAsioDriverUi = true;
            try
            {
                AsioDriverComboBox.Items.Clear();
                foreach (var choice in choices)
                {
                    AsioDriverComboBox.Items.Add(choice);
                }

                AsioDriverComboBox.SelectedItem = targetChoice;
            }
            finally
            {
                _isUpdatingAsioDriverUi = false;
            }

            UpdateAsioControlPanelButtonState();
        }

        private void ApplyNetworkAdapterStateFromState()
        {
            var networkIp = ConfigHelper.NormalizeNetworkValue(_settingsState.NetworkAdapterIp);
            var subnetMask = ConfigHelper.NormalizeNetworkValue(_settingsState.NetworkAdapterSubnet);

            _isUpdatingNetworkUi = true;
            try
            {
                SetTextBoxTextIfNeeded(NetworkAdapterIpTextBox, networkIp);
                SetTextBoxTextIfNeeded(NetworkAdapterSubnetTextBox, subnetMask);
            }
            finally
            {
                _isUpdatingNetworkUi = false;
            }

            if (OpenNetworkAdapterPickerButton != null)
            {
                bool hasSelectableChoice = _settingsState.NetworkAdapters.Count > 1;
                var selectedAdapter = _settingsState.SelectedNetworkAdapter;

                OpenNetworkAdapterPickerButton.Content = hasSelectableChoice ? "选择" : "无可用网卡";
                OpenNetworkAdapterPickerButton.IsEnabled = true;
                ToolTip.SetTip(
                    OpenNetworkAdapterPickerButton,
                    hasSelectableChoice
                        ? $"当前配置：{selectedAdapter?.DisplayName ?? BuildCurrentNetworkAdapterDisplayName(networkIp, subnetMask)}"
                        : "未检测到可用网卡");
            }
        }

        private async void OnGpuCompatLayerToggleChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isLoadingSettings || _isUpdatingGpuCompatLayerUi)
                {
                    return;
                }

                _settingsState.GpuCompatLayerEnabled = GpuCompatLayerToggleSwitch?.IsChecked == true;
                await _settingsWorkflowService.PersistGpuCompatLayerToggleAsync(_settingsState);
                UpdateGpuCompatLayerStatus();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Persist compatibility layer toggle failed.");
            }
        }

        private void UpdateGpuCompatLayerStatus()
        {
            bool modulesDirectoryExists = _settingsWorkflowService.HasGpuCompatLayerModulesDirectory();
            bool gpuCompatLayerEnabled = _settingsState.GpuCompatLayerEnabled;

            if (GpuCompatLayerRenderModeBusyArea != null)
            {
                GpuCompatLayerRenderModeBusyArea.IsBusy = gpuCompatLayerEnabled;
            }

            if (GpuCompatLayerStatusTextBlock != null)
            {
                if (!modulesDirectoryExists && !gpuCompatLayerEnabled)
                {
                    GpuCompatLayerStatusTextBlock.Text = "未找到modules目录，无法启用显卡兼容层。";
                    GpuCompatLayerStatusTextBlock.IsVisible = true;
                }
                else
                {
                    GpuCompatLayerStatusTextBlock.Text = string.Empty;
                    GpuCompatLayerStatusTextBlock.IsVisible = false;
                }
            }

            _isUpdatingGpuCompatLayerUi = true;
            try
            {
                if (GpuCompatLayerToggleSwitch != null)
                {
                    GpuCompatLayerToggleSwitch.IsChecked = gpuCompatLayerEnabled;
                    GpuCompatLayerToggleSwitch.IsEnabled = gpuCompatLayerEnabled || modulesDirectoryExists;
                }

                bool chipsEnabled = !gpuCompatLayerEnabled && modulesDirectoryExists;
                string renderMode = GpuCompatLayerConfigurator.NormalizeRenderMode(_settingsState.GpuCompatLayerRenderMode);
                if (GpuCompatLayerDx9on12RadioButton != null)
                {
                    GpuCompatLayerDx9on12RadioButton.IsChecked = string.Equals(renderMode, "dx9on12", StringComparison.OrdinalIgnoreCase);
                }

                if (GpuCompatLayerDx9on12ExternalRadioButton != null)
                {
                    GpuCompatLayerDx9on12ExternalRadioButton.IsChecked = string.Equals(renderMode, "dx9on12_external", StringComparison.OrdinalIgnoreCase);
                }

                if (GpuCompatLayerDxvkRadioButton != null)
                {
                    GpuCompatLayerDxvkRadioButton.IsChecked = string.Equals(renderMode, "dxvk", StringComparison.OrdinalIgnoreCase);
                }

                if (GpuCompatLayerDx9on12RadioButton != null) GpuCompatLayerDx9on12RadioButton.IsEnabled = chipsEnabled;
                if (GpuCompatLayerDx9on12ExternalRadioButton != null) GpuCompatLayerDx9on12ExternalRadioButton.IsEnabled = chipsEnabled;
                if (GpuCompatLayerDxvkRadioButton != null) GpuCompatLayerDxvkRadioButton.IsEnabled = chipsEnabled;
            }
            finally
            {
                _isUpdatingGpuCompatLayerUi = false;
            }
        }

        private async void OnServerPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isLoadingSettings || _isSyncingModel || _isUpdatingServerPresetUi)
                {
                    return;
                }

                if (ServerPresetComboBox?.SelectedItem is not ServerPresetItem preset)
                {
                    return;
                }

                _settingsState.SelectedServerPreset = preset;
                await _settingsWorkflowService.PersistSelectedServerPresetAsync(_settingsState);
                ApplyServerPresetStateToUi();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Persist server preset selection failed.");
            }
        }

        private void ApplyServerPresetStateToUi()
        {
            _isUpdatingServerPresetUi = true;
            _isSyncingModel = true;
            try
            {
                if (ServerPresetComboBox != null)
                {
                    ReplaceComboBoxItems(ServerPresetComboBox, _settingsState.ServerPresets);
                    var selectedPreset = FindServerPreset(_settingsState.SelectedServerPreset);
                    if (selectedPreset != null && !ReferenceEquals(ServerPresetComboBox.SelectedItem, selectedPreset))
                    {
                        ServerPresetComboBox.SelectedItem = selectedPreset;
                    }
                }

                SetTextBoxTextIfNeeded(ServerAddressTextBox, _settingsState.ServerAddress);
                SetTextBoxTextIfNeeded(PcbIdTextBox, _settingsState.PcbId);
            }
            finally
            {
                _isSyncingModel = false;
                _isUpdatingServerPresetUi = false;
            }
        }

        private ServerPresetItem FindServerPreset(ServerPresetItem selected)
        {
            if (selected == null)
            {
                return null;
            }

            var presets = _settingsState.ServerPresets;
            for (int i = 0; i < presets.Count; i++)
            {
                if (ReferenceEquals(presets[i], selected))
                {
                    return presets[i];
                }
            }

            for (int i = 0; i < presets.Count; i++)
            {
                if (string.Equals(presets[i].Name, selected.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return presets[i];
                }
            }

            return null;
        }

        private void UpdateUseSystemSpiceConfigSwitchVisibility()
        {
            bool show = _settingsState.IsSpiceConfigAvailable;
            if (UseSystemSpiceConfigRow != null)
            {
                UseSystemSpiceConfigRow.IsVisible = show;
            }
            else if (UseSystemSpiceConfigToggleSwitch != null)
            {
                UseSystemSpiceConfigToggleSwitch.IsVisible = show;
            }
        }
    }
}
