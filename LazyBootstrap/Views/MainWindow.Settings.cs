using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SukiUI.Dialogs;

namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private void HookSettingsViewModelState()
        {
            if (_viewModel?.Settings == null)
            {
                return;
            }

            _viewModel.Settings.PropertyChanged -= OnSettingsViewModelPropertyChanged;
            _viewModel.Settings.PropertyChanged += OnSettingsViewModelPropertyChanged;

            _viewModel.Settings.ServerPresets.CollectionChanged -= OnSettingsCollectionChanged;
            _viewModel.Settings.ServerPresets.CollectionChanged += OnSettingsCollectionChanged;
            _viewModel.Settings.AsioDrivers.CollectionChanged -= OnSettingsCollectionChanged;
            _viewModel.Settings.AsioDrivers.CollectionChanged += OnSettingsCollectionChanged;
            _viewModel.Settings.NetworkAdapters.CollectionChanged -= OnSettingsCollectionChanged;
            _viewModel.Settings.NetworkAdapters.CollectionChanged += OnSettingsCollectionChanged;

            _viewModel.PropertyChanged -= OnMainWindowViewModelPropertyChanged;
            _viewModel.PropertyChanged += OnMainWindowViewModelPropertyChanged;
        }

        private void OnMainWindowViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!string.Equals(e?.PropertyName, nameof(MainWindowViewModel.SelectedPage), StringComparison.Ordinal))
            {
                return;
            }

            if (_viewModel.SelectedPage != ShellPage.Settings)
            {
                return;
            }

            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_viewModel?.Settings == null)
                {
                    return;
                }

                ApplyServerPresetViewModelStateToUi();
            });
        }

        private void UnhookSettingsViewModelState()
        {
            if (_viewModel?.Settings == null)
            {
                return;
            }

            _viewModel.PropertyChanged -= OnMainWindowViewModelPropertyChanged;
            _viewModel.Settings.PropertyChanged -= OnSettingsViewModelPropertyChanged;
            _viewModel.Settings.ServerPresets.CollectionChanged -= OnSettingsCollectionChanged;
            _viewModel.Settings.AsioDrivers.CollectionChanged -= OnSettingsCollectionChanged;
            _viewModel.Settings.NetworkAdapters.CollectionChanged -= OnSettingsCollectionChanged;
        }

        private void OnSettingsViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                var propertyName = e?.PropertyName;
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    ApplyStartupSettingsViewModelStateToUi();
                    ApplyDeferredSettingsViewModelStateToUi();
                    SetSettingsBusy(_viewModel.Settings.IsSettingsBusy);
                    return;
                }

                switch (propertyName)
                {
                    case nameof(SettingsPageViewModel.IsSpiceConfigAvailable):
                    case nameof(SettingsPageViewModel.SpiceConfigEmptyStateMessage):
                        ApplySettingsAvailabilityStateToUi();
                        break;

                    case nameof(SettingsPageViewModel.NoAsphyxia):
                    case nameof(SettingsPageViewModel.UseSystemSpiceConfig):
                    case nameof(SettingsPageViewModel.GpuCompatLayerEnabled):
                    case nameof(SettingsPageViewModel.GpuCompatLayerRenderMode):
                        ApplyStartupSettingsViewModelStateToUi();
                        break;

                    case nameof(SettingsPageViewModel.ServerAddress):
                    case nameof(SettingsPageViewModel.PcbId):
                    case nameof(SettingsPageViewModel.ActiveServerPreset):
                    case nameof(SettingsPageViewModel.SelectedServerPreset):
                        ApplyServerPresetViewModelStateToUi();
                        break;

                    case nameof(SettingsPageViewModel.DllInjection):
                    case nameof(SettingsPageViewModel.WindowSize):
                        ApplySpiceTextInputsFromViewModel();
                        break;

                    case nameof(SettingsPageViewModel.Windowed):
                    case nameof(SettingsPageViewModel.NetDump):
                    case nameof(SettingsPageViewModel.DisableSubDisplay):
                    case nameof(SettingsPageViewModel.WindowModeIndex):
                    case nameof(SettingsPageViewModel.PCoreOptimization):
                    case nameof(SettingsPageViewModel.SubBorderless):
                    case nameof(SettingsPageViewModel.ShowCursorTouchSim):
                    case nameof(SettingsPageViewModel.WindowTopMost):
                    case nameof(SettingsPageViewModel.SingleAdapter):
                    case nameof(SettingsPageViewModel.NvidiaPerformanceProfile):
                    case nameof(SettingsPageViewModel.SubWindowTopMost):
                    case nameof(SettingsPageViewModel.SubForceRender):
                    case nameof(SettingsPageViewModel.NativeTouch):
                    case nameof(SettingsPageViewModel.LowLatencySharedAudio):
                    case nameof(SettingsPageViewModel.CardIo):
                    case nameof(SettingsPageViewModel.HidSmartCard):
                        ApplySpiceSettingsFromViewModel();
                        break;

                    case nameof(SettingsPageViewModel.AsioDriverValue):
                    case nameof(SettingsPageViewModel.SelectedAsioDriver):
                        ApplyAsioDriverChoicesFromViewModel();
                        break;

                    case nameof(SettingsPageViewModel.NetworkAdapterIp):
                    case nameof(SettingsPageViewModel.NetworkAdapterSubnet):
                    case nameof(SettingsPageViewModel.SelectedNetworkAdapter):
                        ApplyNetworkAdapterStateFromViewModel();
                        break;

                    case nameof(SettingsPageViewModel.IsSettingsBusy):
                        SetSettingsBusy(_viewModel.Settings.IsSettingsBusy);
                        break;
                }
            });
        }

        private void OnSettingsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ReferenceEquals(sender, _viewModel.Settings.ServerPresets))
                {
                    ApplyServerPresetViewModelStateToUi();
                    return;
                }

                if (ReferenceEquals(sender, _viewModel.Settings.AsioDrivers))
                {
                    ApplyAsioDriverChoicesFromViewModel();
                    return;
                }

                if (ReferenceEquals(sender, _viewModel.Settings.NetworkAdapters))
                {
                    ApplyNetworkAdapterStateFromViewModel();
                }
            });
        }

        private void ApplyStartupSettingsViewModelStateToUi()
        {
            bool previousLoadingState = _isLoadingSettings;
            _isLoadingSettings = true;

            try
            {
                if (NoAsphyxiaToggleSwitch != null)
                {
                    NoAsphyxiaToggleSwitch.IsChecked = _viewModel.Settings.NoAsphyxia;
                }

                if (UseSystemSpiceConfigToggleSwitch != null)
                {
                    UseSystemSpiceConfigToggleSwitch.IsChecked = _viewModel.Settings.UseSystemSpiceConfig;
                }

                UpdateGpuCompatLayerStatus();
                ApplyServerPresetViewModelStateToUi();
                ApplySettingsAvailabilityStateToUi();
            }
            finally
            {
                _isLoadingSettings = previousLoadingState;
            }
        }

        private void ApplyDeferredSettingsViewModelStateToUi()
        {
            bool previousLoadingState = _isLoadingSettings;
            _isLoadingSettings = true;

            try
            {
                ApplySettingsAvailabilityStateToUi();
                ApplyInfoViewModelStateToUi();
                if (!_viewModel.Settings.IsSpiceConfigAvailable)
                {
                    return;
                }

                ApplySpiceSettingsFromViewModel();
                ApplySpiceTextInputsFromViewModel();
                ApplyAsioDriverChoicesFromViewModel();
                ApplyNetworkAdapterStateFromViewModel();
                ApplyServerPresetViewModelStateToUi();
                UpdateGpuCompatLayerStatus();
            }
            finally
            {
                _isLoadingSettings = previousLoadingState;
            }
        }

        private void ApplySettingsAvailabilityStateToUi()
        {
            bool isSpiceConfigAvailable = _viewModel.Settings.IsSpiceConfigAvailable;

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
                SettingsEmptyStateTextBlock.Text = _viewModel.Settings.SpiceConfigEmptyStateMessage ?? string.Empty;
            }

            UpdateUseSystemSpiceConfigSwitchVisibility();
        }

        private void ApplyInfoViewModelStateToUi()
        {
            RefreshEnvironmentOverviewChrome();
        }

        private void ApplySpiceSettingsFromViewModel()
        {
            if (WindowedToggleSwitch != null)
            {
                WindowedToggleSwitch.IsChecked = _viewModel.Settings.Windowed;
            }

            if (NetDumpToggleSwitch != null)
            {
                NetDumpToggleSwitch.IsChecked = _viewModel.Settings.NetDump;
            }

            if (DisableSubDisplayToggleSwitch != null)
            {
                DisableSubDisplayToggleSwitch.IsChecked = _viewModel.Settings.DisableSubDisplay;
            }

            if (WindowModeComboBox != null)
            {
                WindowModeComboBox.SelectedIndex = Math.Clamp(_viewModel.Settings.WindowModeIndex, 0, Math.Max(0, WindowModeComboBox.ItemCount - 1));
            }

            if (PCoreOptimizationToggleSwitch != null)
            {
                PCoreOptimizationToggleSwitch.IsChecked = _viewModel.Settings.PCoreOptimization;
            }

            if (SubBorderlessToggleSwitch != null)
            {
                SubBorderlessToggleSwitch.IsChecked = _viewModel.Settings.SubBorderless;
            }

            if (ShowCursorTouchSimToggleSwitch != null)
            {
                ShowCursorTouchSimToggleSwitch.IsChecked = _viewModel.Settings.ShowCursorTouchSim;
            }

            if (WindowTopMostToggleSwitch != null)
            {
                WindowTopMostToggleSwitch.IsChecked = _viewModel.Settings.WindowTopMost;
            }

            if (SingleAdapterToggleSwitch != null)
            {
                SingleAdapterToggleSwitch.IsChecked = _viewModel.Settings.SingleAdapter;
            }

            if (NvidiaPerformanceProfileToggleSwitch != null)
            {
                NvidiaPerformanceProfileToggleSwitch.IsChecked = _viewModel.Settings.NvidiaPerformanceProfile;
            }

            if (SubWindowTopMostToggleSwitch != null)
            {
                SubWindowTopMostToggleSwitch.IsChecked = _viewModel.Settings.SubWindowTopMost;
            }

            if (SubForceRenderToggleSwitch != null)
            {
                SubForceRenderToggleSwitch.IsChecked = _viewModel.Settings.SubForceRender;
            }

            if (NativeTouchToggleSwitch != null)
            {
                NativeTouchToggleSwitch.IsChecked = _viewModel.Settings.NativeTouch;
            }

            if (LowLatencySharedAudioToggleSwitch != null)
            {
                LowLatencySharedAudioToggleSwitch.IsChecked = _viewModel.Settings.LowLatencySharedAudio;
            }

            if (CardIoToggleSwitch != null)
            {
                CardIoToggleSwitch.IsChecked = _viewModel.Settings.CardIo;
            }

            if (HidSmartCardToggleSwitch != null)
            {
                HidSmartCardToggleSwitch.IsChecked = _viewModel.Settings.HidSmartCard;
            }
        }

        private void ApplySpiceTextInputsFromViewModel()
        {
            SetTextBoxTextIfNeeded(DllInjectionTextBox, _viewModel.Settings.DllInjection);
            SetTextBoxTextIfNeeded(WindowSizeTextBox, _viewModel.Settings.WindowSize);
        }

        private void ApplyAsioDriverChoicesFromViewModel()
        {
            if (AsioDriverComboBox == null)
            {
                return;
            }

            var choices = _viewModel.Settings.AsioDrivers.Count > 0
                ? _viewModel.Settings.AsioDrivers.ToList()
                : new List<AsioDriverOption> { new("无", string.Empty) };

            var selectedValue = _viewModel.Settings.SelectedAsioDriver?.Value
                ?? _viewModel.Settings.AsioDriverValue
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

        private void ApplyNetworkAdapterStateFromViewModel()
        {
            var networkIp = ConfigHelper.NormalizeNetworkValue(_viewModel.Settings.NetworkAdapterIp);
            var subnetMask = ConfigHelper.NormalizeNetworkValue(_viewModel.Settings.NetworkAdapterSubnet);

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
                bool hasSelectableChoice = _viewModel.Settings.NetworkAdapters.Count > 1;
                var selectedAdapter = _viewModel.Settings.SelectedNetworkAdapter;

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

                _viewModel.Settings.GpuCompatLayerEnabled = GpuCompatLayerToggleSwitch?.IsChecked == true;
                await _settingsWorkflowService.PersistGpuCompatLayerToggleAsync(_viewModel.Settings);
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
            bool gpuCompatLayerEnabled = _viewModel.Settings.GpuCompatLayerEnabled;

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

                _viewModel.Settings.SelectedServerPreset = preset;
                await _settingsWorkflowService.PersistSelectedServerPresetAsync(_viewModel.Settings);
                ApplyServerPresetViewModelStateToUi();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Persist server preset selection failed.");
            }
        }

        private void ApplyServerPresetViewModelStateToUi()
        {
            _isUpdatingServerPresetUi = true;
            _isSyncingModel = true;
            try
            {
                if (ServerPresetComboBox != null)
                {
                    int index = FindServerPresetIndex(_viewModel.Settings.SelectedServerPreset);
                    if (index >= 0
                        && (ServerPresetComboBox.SelectedIndex != index
                            || !ReferenceEquals(
                                ServerPresetComboBox.SelectedItem,
                                _viewModel.Settings.ServerPresets[index])))
                    {
                        ServerPresetComboBox.SelectedIndex = index;
                    }
                }

                SetTextBoxTextIfNeeded(ServerAddressTextBox, _viewModel.Settings.ServerAddress);
                SetTextBoxTextIfNeeded(PcbIdTextBox, _viewModel.Settings.PcbId);
            }
            finally
            {
                _isSyncingModel = false;
                _isUpdatingServerPresetUi = false;
            }
        }

        private int FindServerPresetIndex(ServerPresetItem selected)
        {
            if (selected == null)
            {
                return -1;
            }

            var presets = _viewModel.Settings.ServerPresets;
            for (int i = 0; i < presets.Count; i++)
            {
                if (ReferenceEquals(presets[i], selected))
                {
                    return i;
                }
            }

            for (int i = 0; i < presets.Count; i++)
            {
                if (string.Equals(presets[i].Name, selected.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void SetTextBoxTextIfNeeded(TextBox textBox, string value)
        {
            if (textBox == null)
            {
                return;
            }

            string normalizedValue = value ?? string.Empty;
            if (string.Equals(textBox.Text ?? string.Empty, normalizedValue, StringComparison.Ordinal))
            {
                return;
            }

            textBox.Text = normalizedValue;
        }

        private void UpdateUseSystemSpiceConfigSwitchVisibility()
        {
            bool show = _viewModel.Settings.IsSpiceConfigAvailable;
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
