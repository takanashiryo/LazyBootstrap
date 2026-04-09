using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
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
        }

        private void UnhookSettingsViewModelState()
        {
            if (_viewModel?.Settings == null)
            {
                return;
            }

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

                    case nameof(SettingsPageViewModel.GameDirectoryOverride):
                    case nameof(SettingsPageViewModel.AsphyxiaDirectoryOverride):
                    case nameof(SettingsPageViewModel.NoAsphyxia):
                    case nameof(SettingsPageViewModel.CompatibilityLayerEnabled):
                    case nameof(SettingsPageViewModel.CompatibilityRenderMode):
                        ApplyStartupSettingsViewModelStateToUi();
                        break;

                    case nameof(SettingsPageViewModel.ServerAddress):
                    case nameof(SettingsPageViewModel.PcbId):
                    case nameof(SettingsPageViewModel.ActiveServerPreset):
                    case nameof(SettingsPageViewModel.SelectedServerPreset):
                        ApplyServerPresetViewModelStateToUi();
                        break;

                    case nameof(SettingsPageViewModel.Windowed):
                    case nameof(SettingsPageViewModel.DllInjection):
                    case nameof(SettingsPageViewModel.NetDump):
                    case nameof(SettingsPageViewModel.DisableSubDisplay):
                    case nameof(SettingsPageViewModel.WindowModeIndex):
                    case nameof(SettingsPageViewModel.PCoreOptimization):
                    case nameof(SettingsPageViewModel.SubBorderless):
                    case nameof(SettingsPageViewModel.ShowCursorTouchSim):
                    case nameof(SettingsPageViewModel.WindowTopMost):
                    case nameof(SettingsPageViewModel.WindowSize):
                    case nameof(SettingsPageViewModel.SingleAdapter):
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

        private string ResolveMachineProperty()
        {
            var identPath = Path.Combine(GetContentsDirectoryPath(), "prop", "ea3-ident.xml");
            var result = TryReadMachinePropertyFromEa3(identPath);
            if (!string.IsNullOrWhiteSpace(result))
            {
                return result;
            }

            var configPath = Path.Combine(GetContentsDirectoryPath(), "prop", "ea3-config.xml");
            result = TryReadMachinePropertyFromEa3(configPath);
            if (!string.IsNullOrWhiteSpace(result))
            {
                return result;
            }

            return "未知";
        }

        private static string TryReadMachinePropertyFromEa3(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var doc = XDocument.Load(filePath);
                var softNode = doc.Root?.Element("soft");
                if (softNode == null)
                {
                    return null;
                }

                var model = softNode.Element("model")?.Value?.Trim();
                var dest = softNode.Element("dest")?.Value?.Trim();
                var spec = softNode.Element("spec")?.Value?.Trim();
                var rev = softNode.Element("rev")?.Value?.Trim();

                if (string.IsNullOrWhiteSpace(model) ||
                    string.IsNullOrWhiteSpace(dest) ||
                    string.IsNullOrWhiteSpace(spec) ||
                    string.IsNullOrWhiteSpace(rev))
                {
                    return null;
                }

                return $"{model}:{dest}:{spec}:{rev}";
            }
            catch
            {
                return null;
            }
        }

        private string ResolveCurrentGameVersion()
        {
            try
            {
                var bootstrapPath = Path.Combine(GetContentsDirectoryPath(), "prop", "bootstrap.xml");
                if (!File.Exists(bootstrapPath))
                {
                    return "未知";
                }

                var doc = XDocument.Load(bootstrapPath);
                var releaseCode = doc.Root?.Element("release_code")?.Value?.Trim();
                return string.IsNullOrWhiteSpace(releaseCode) ? "未知" : releaseCode;
            }
            catch
            {
                return "未知";
            }
        }

        private string ResolveLauncherVersion()
        {
            try
            {
                var launcherExe = _paths.GetLauncherExecutablePath();
                if (File.Exists(launcherExe))
                {
                    var fileVersion = FileVersionInfo.GetVersionInfo(launcherExe);
                    if (!string.IsNullOrWhiteSpace(fileVersion.FileVersion))
                    {
                        return fileVersion.FileVersion;
                    }

                    if (!string.IsNullOrWhiteSpace(fileVersion.ProductVersion))
                    {
                        return fileVersion.ProductVersion;
                    }
                }
            }
            catch
            {
            }

            return "未知";
        }

        private void RefreshPathOverrideDependentUi()
        {
            RefreshSettingsVersionTexts();
            UpdateCompatLayerStatus();
            UpdateRecommendedSpiceConfigButtonVisibility();
        }

        private void ApplyPathOverrideTextBoxesFromViewModel()
        {
            _paths.SetContentsDirectoryOverride(_viewModel.Settings.GameDirectoryOverride);
            _paths.SetAsphyxiaDirectoryOverride(_viewModel.Settings.AsphyxiaDirectoryOverride);

            if (GameDirectoryOverrideTextBox != null)
            {
                GameDirectoryOverrideTextBox.Text = _viewModel.Settings.GameDirectoryOverride;
            }

            if (AsphyxiaDirectoryOverrideTextBox != null)
            {
                AsphyxiaDirectoryOverrideTextBox.Text = _viewModel.Settings.AsphyxiaDirectoryOverride;
            }
        }

        private void RefreshSettingsVersionTexts()
        {
            _viewModel.Info.MachineProperty = ResolveMachineProperty();
            _viewModel.Info.GameVersion = ResolveCurrentGameVersion();
            _viewModel.Info.LauncherVersion = ResolveLauncherVersion();

            ApplyInfoViewModelStateToUi();
        }

        private void ApplyStartupSettingsViewModelStateToUi()
        {
            bool previousLoadingState = _isLoadingSettings;
            _isLoadingSettings = true;

            try
            {
                ApplyPathOverrideTextBoxesFromViewModel();

                if (NoAsphyxiaToggleSwitch != null)
                {
                    NoAsphyxiaToggleSwitch.IsChecked = _viewModel.Settings.NoAsphyxia;
                }

                ApplyCompatibilityStateFromViewModel();
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
                ApplyAsioDriverChoicesFromViewModel();
                ApplyNetworkAdapterStateFromViewModel();
                ApplyServerPresetViewModelStateToUi();
                ApplyCompatibilityStateFromViewModel();
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

            UpdateRecommendedSpiceConfigButtonVisibility();
        }

        private void ApplyInfoViewModelStateToUi()
        {
            if (CurrentVersionTextBox != null)
            {
                CurrentVersionTextBox.Text = _viewModel.Info.MachineProperty;
            }

            if (RevisionTextBox != null)
            {
                RevisionTextBox.Text = _viewModel.Info.GameVersion;
            }

            if (LauncherVersionTextBox != null)
            {
                LauncherVersionTextBox.Text = _viewModel.Info.LauncherVersion;
            }
        }

        private void ApplySpiceSettingsFromViewModel()
        {
            if (WindowedToggleSwitch != null)
            {
                WindowedToggleSwitch.IsChecked = _viewModel.Settings.Windowed;
            }

            if (DllInjectionTextBox != null)
            {
                DllInjectionTextBox.Text = _viewModel.Settings.DllInjection ?? string.Empty;
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

            if (WindowSizeTextBox != null)
            {
                WindowSizeTextBox.Text = _viewModel.Settings.WindowSize ?? string.Empty;
            }

            if (SingleAdapterToggleSwitch != null)
            {
                SingleAdapterToggleSwitch.IsChecked = _viewModel.Settings.SingleAdapter;
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
            var networkIp = NormalizeNetworkValue(_viewModel.Settings.NetworkAdapterIp);
            var subnetMask = NormalizeNetworkValue(_viewModel.Settings.NetworkAdapterSubnet);

            _isUpdatingNetworkUi = true;
            try
            {
                if (NetworkAdapterIpTextBox != null)
                {
                    NetworkAdapterIpTextBox.Text = networkIp;
                }

                if (NetworkAdapterSubnetTextBox != null)
                {
                    NetworkAdapterSubnetTextBox.Text = subnetMask;
                }
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
    }
}


namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private async void OnCompatLayerToggleChanged(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings || _isUpdatingCompatUi)
            {
                return;
            }

            _viewModel.Settings.CompatibilityLayerEnabled = CompatLayerToggleSwitch?.IsChecked == true;
            await _settingsWorkflowService.PersistCompatibilityToggleAsync(_viewModel.Settings);
            ApplyCompatibilityStateFromViewModel();
        }

        private void UpdateCompatLayerStatus()
        {
            bool modulesDirectoryExists = HasCompatModulesDirectory();
            bool compatibilityEnabled = _viewModel.Settings.CompatibilityLayerEnabled;
            UpdateCompatRenderModeBusyState(compatibilityEnabled);

            if (CompatStatusTextBlock != null)
            {
                if (!modulesDirectoryExists && !compatibilityEnabled)
                {
                    CompatStatusTextBlock.Text = "未找到modules目录，无法启用显卡兼容层。";
                    CompatStatusTextBlock.IsVisible = true;
                }
                else
                {
                    CompatStatusTextBlock.Text = string.Empty;
                    CompatStatusTextBlock.IsVisible = false;
                }
            }

            _isUpdatingCompatUi = true;
            try
            {
                if (CompatLayerToggleSwitch != null)
                {
                    CompatLayerToggleSwitch.IsChecked = compatibilityEnabled;
                    CompatLayerToggleSwitch.IsEnabled = compatibilityEnabled || modulesDirectoryExists;
                }

                bool chipsEnabled = !compatibilityEnabled && modulesDirectoryExists;
                if (CompatDx9on12RadioButton != null) CompatDx9on12RadioButton.IsEnabled = chipsEnabled;
                if (CompatDx9on12ExternalRadioButton != null) CompatDx9on12ExternalRadioButton.IsEnabled = chipsEnabled;
                if (CompatDxvkRadioButton != null) CompatDxvkRadioButton.IsEnabled = chipsEnabled;
            }
            finally
            {
                _isUpdatingCompatUi = false;
            }
        }

        private void UpdateCompatRenderModeBusyState(bool isBusy)
        {
            if (CompatRenderModeBusyArea != null)
            {
                CompatRenderModeBusyArea.IsBusy = isBusy;
            }
        }

        private void ApplyCompatibilityStateFromViewModel()
        {
            UpdateCompatLayerStatus();
        }

        private string GetCompatModulesDirectoryPath()
        {
            return Path.Combine(GetContentsDirectoryPath(), "modules");
        }

        private bool HasCompatModulesDirectory()
        {
            return Directory.Exists(GetCompatModulesDirectoryPath());
        }
    }
}


namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private async void OnServerPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings || _isSyncingModel)
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

        private void ApplyServerPresetViewModelStateToUi()
        {
            _isSyncingModel = true;
            try
            {
                if (ServerAddressTextBox != null)
                {
                    ServerAddressTextBox.Text = _viewModel.Settings.ServerAddress ?? string.Empty;
                }

                if (PcbIdTextBox != null)
                {
                    PcbIdTextBox.Text = _viewModel.Settings.PcbId ?? string.Empty;
                }
            }
            finally
            {
                _isSyncingModel = false;
            }
        }

    }
}


namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
    }
}


namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private void UpdateRecommendedSpiceConfigButtonVisibility()
        {
            if (ImportRecommendedSpiceConfigButton != null)
            {
                ImportRecommendedSpiceConfigButton.IsVisible = _viewModel.Settings.IsSpiceConfigAvailable;
            }
        }
    }
}
