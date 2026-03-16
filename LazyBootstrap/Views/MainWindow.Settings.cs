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
                    case nameof(SettingsPageViewModel.PortableMode):
                    case nameof(SettingsPageViewModel.GameDirectoryOverride):
                    case nameof(SettingsPageViewModel.AsphyxiaDirectoryOverride):
                    case nameof(SettingsPageViewModel.NoAsphyxia):
                    case nameof(SettingsPageViewModel.CanImportRecommendedConfig):
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

        private async void OnPortableModeToggleChanged(object sender, RoutedEventArgs e)
        {
            await TogglePortableModeCoreAsync();
        }

        private async Task TogglePortableModeCoreAsync()
        {
            if (_isLoadingSettings || _isUpdatingPortableModeUi)
            {
                return;
            }

            bool newPortableMode = PortableModeToggleSwitch.IsChecked == true;

            if (newPortableMode)
            {
                // Revert toggle immediately — only enable after user confirms
                ApplyPortableModeToggleState(false);

                var dialogBuilder = _dialogManager.CreateDialog()
                    .OfType(NotificationType.Warning)
                    .WithTitle("切换至便携模式")
                    .WithContent("切换至便携模式后，spice2x将不再调用系统内的配置，转而使用游戏文件夹里的配置文件，可以对游戏进行随意的拷贝\n（按键绑定根据你所选的模式不同，可能无法迁移）\n你确定要切换吗？")
                    .WithActionButton("确定", dialog =>
                    {
                        _ = ApplyPortableModeAsync(true);
                    }, true, "Flat")
                    .WithActionButton("取消", _ => { }, true, "Basic")
                    .Dismiss().ByClickingBackground();
                ApplyDialogNotificationIcon(dialogBuilder, NotificationType.Warning);
                dialogBuilder.TryShow();
                return;
            }

            await ApplyPortableModeAsync(false);
        }

        private async Task ApplyPortableModeAsync(bool enabled)
        {
            bool previousMode = _portableMode;
            _viewModel.Settings.PortableMode = enabled;
            await _settingsWorkflowService.PersistPortableModeAsync(_viewModel.Settings);

            _portableMode = _viewModel.Settings.PortableMode;
            _paths.PortableMode = _portableMode;
            ApplyPortableModeToggleState(_portableMode);

            if (_portableMode == previousMode)
            {
                return;
            }

            var targetXmlPath = GetSpiceXmlPathForMode(_portableMode);
            ShowInfoToast("便携模式切换", _portableMode
                ? $"已切换至便携模式，XML: {targetXmlPath}"
                : $"已切换至系统模式，XML: {targetXmlPath}");

            RefreshSettingsPanelAfterPortableModeSwitch();
        }

        private string GetSpiceXmlPathForMode(bool portableMode)
        {
            return _paths.GetSpiceXmlPath(portableMode);
        }

        private void RefreshSettingsPanelAfterPortableModeSwitch()
        {
            LoadSpiceConfig();
            LoadServerPresetsFromConfig();
            SelectPresetByCurrentFields();
            UpdateCompatLayerStatus();
            SyncCompatModeButtonsFromCombo();
            UpdateRecommendedSpiceConfigButtonVisibility();
        }

        private void RefreshPathOverrideDependentUi()
        {
            RefreshSettingsVersionTexts();
            LoadSpiceConfig();
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
                _portableMode = _viewModel.Settings.PortableMode;
                _paths.PortableMode = _portableMode;
                ApplyPortableModeToggleState(_portableMode);
                ApplyPathOverrideTextBoxesFromViewModel();

                if (NoAsphyxiaToggleSwitch != null)
                {
                    NoAsphyxiaToggleSwitch.IsChecked = _viewModel.Settings.NoAsphyxia;
                }

                _lastKnownCompatRenderMode = CompatibilitySettingsService.NormalizeRenderMode(_viewModel.Settings.CompatibilityRenderMode);
                ApplyCompatibilityStateFromViewModel();
                ApplyServerPresetViewModelStateToUi();
                UpdateRecommendedSpiceConfigButtonVisibility();
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
                ApplySpiceSettingsFromViewModel();
                ApplyAsioDriverChoicesFromViewModel();
                ApplyNetworkAdapterStateFromViewModel();
                ApplyServerPresetViewModelStateToUi();
                ApplyInfoViewModelStateToUi();
                ApplyCompatibilityStateFromViewModel();
                UpdateRecommendedSpiceConfigButtonVisibility();
            }
            finally
            {
                _isLoadingSettings = previousLoadingState;
            }
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
            _disableSubDisplay = _viewModel.Settings.DisableSubDisplay;
            _windowModeIndex = _viewModel.Settings.WindowModeIndex;
            _subBorderless = _viewModel.Settings.SubBorderless;
            _showCursorTouchSim = _viewModel.Settings.ShowCursorTouchSim;
            _pCoreOptimization = _viewModel.Settings.PCoreOptimization;
            _windowTopMost = _viewModel.Settings.WindowTopMost;
            _windowSize = _viewModel.Settings.WindowSize ?? string.Empty;
            _singleAdapter = _viewModel.Settings.SingleAdapter;
            _subWindowTopMost = _viewModel.Settings.SubWindowTopMost;
            _subForceRender = _viewModel.Settings.SubForceRender;
            _nativeTouch = _viewModel.Settings.NativeTouch;
            _asioDriver = _viewModel.Settings.SelectedAsioDriver?.Value ?? _viewModel.Settings.AsioDriverValue ?? string.Empty;
            _lowLatencySharedAudio = _viewModel.Settings.LowLatencySharedAudio;
            _cardIo = _viewModel.Settings.CardIo;
            _hidSmartCard = _viewModel.Settings.HidSmartCard;
            _dbgNetDump = _viewModel.Settings.NetDump;

            if (WindowedToggleSwitch != null)
            {
                WindowedToggleSwitch.IsChecked = _viewModel.Settings.Windowed;
            }

            if (NetDumpToggleSwitch != null)
            {
                NetDumpToggleSwitch.IsChecked = _dbgNetDump;
            }

            if (DisableSubDisplayToggleSwitch != null)
            {
                DisableSubDisplayToggleSwitch.IsChecked = _disableSubDisplay;
            }

            if (WindowModeComboBox != null)
            {
                WindowModeComboBox.SelectedIndex = Math.Clamp(_windowModeIndex, 0, Math.Max(0, WindowModeComboBox.ItemCount - 1));
            }

            if (PCoreOptimizationToggleSwitch != null)
            {
                PCoreOptimizationToggleSwitch.IsChecked = _pCoreOptimization;
            }

            if (SubBorderlessToggleSwitch != null)
            {
                SubBorderlessToggleSwitch.IsChecked = _subBorderless;
            }

            if (ShowCursorTouchSimToggleSwitch != null)
            {
                ShowCursorTouchSimToggleSwitch.IsChecked = _showCursorTouchSim;
            }

            if (WindowTopMostToggleSwitch != null)
            {
                WindowTopMostToggleSwitch.IsChecked = _windowTopMost;
            }

            if (WindowSizeTextBox != null)
            {
                WindowSizeTextBox.Text = _windowSize;
            }

            if (SingleAdapterToggleSwitch != null)
            {
                SingleAdapterToggleSwitch.IsChecked = _singleAdapter;
            }

            if (SubWindowTopMostToggleSwitch != null)
            {
                SubWindowTopMostToggleSwitch.IsChecked = _subWindowTopMost;
            }

            if (SubForceRenderToggleSwitch != null)
            {
                SubForceRenderToggleSwitch.IsChecked = _subForceRender;
            }

            if (NativeTouchToggleSwitch != null)
            {
                NativeTouchToggleSwitch.IsChecked = _nativeTouch;
            }

            if (LowLatencySharedAudioToggleSwitch != null)
            {
                LowLatencySharedAudioToggleSwitch.IsChecked = _lowLatencySharedAudio;
            }

            if (CardIoToggleSwitch != null)
            {
                CardIoToggleSwitch.IsChecked = _cardIo;
            }

            if (HidSmartCardToggleSwitch != null)
            {
                HidSmartCardToggleSwitch.IsChecked = _hidSmartCard;
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

            if (ServerAddressTextBox != null)
            {
                ServerAddressTextBox.Text = _viewModel.Settings.ServerAddress ?? string.Empty;
            }

            if (PcbIdTextBox != null)
            {
                PcbIdTextBox.Text = _viewModel.Settings.PcbId ?? string.Empty;
            }
        }

        private void ApplyPortableModeToggleState(bool enabled)
        {
            _viewModel.Settings.PortableMode = enabled;
            _isUpdatingPortableModeUi = true;
            try
            {
                if (PortableModeToggleSwitch != null)
                {
                    PortableModeToggleSwitch.IsChecked = enabled;
                }
            }
            finally
            {
                _isUpdatingPortableModeUi = false;
            }

            Dispatcher.UIThread.Post(() =>
            {
                _isUpdatingPortableModeUi = true;
                try
                {
                    if (PortableModeToggleSwitch != null)
                    {
                        PortableModeToggleSwitch.IsChecked = enabled;
                    }
                }
                finally
                {
                    _isUpdatingPortableModeUi = false;
                }
            }, DispatcherPriority.Render);
        }

        private bool EnsureSpiceXmlExistsForToggleOrRevert(ToggleSwitch toggle, Action onReverted)
        {
            var xmlPath = GetSpiceXmlPath();
            if (File.Exists(xmlPath))
            {
                return true;
            }

            ShowErrorToast("配置设置失败", "未找到 spicetools.xml，已自动关闭该选项。");

            _isUpdatingSpiceToggleUi = true;
            try
            {
                if (onReverted != null)
                {
                    onReverted();
                }

                if (toggle != null)
                {
                    toggle.IsChecked = false;
                }
            }
            finally
            {
                _isUpdatingSpiceToggleUi = false;
            }

            Dispatcher.UIThread.Post(() =>
            {
                _isUpdatingSpiceToggleUi = true;
                try
                {
                    if (toggle != null)
                    {
                        toggle.IsChecked = false;
                    }
                }
                finally
                {
                    _isUpdatingSpiceToggleUi = false;
                }
            }, DispatcherPriority.Render);

            return false;
        }

        private void SaveSettings()
        {
            if (_isLoadingSettings)
            {
                return;
            }

            try
            {
                _configFile.WriteString(SettingSectionName, "portablemode", _portableMode.ToString().ToLowerInvariant());
                _configFile.WriteString(SettingSectionName, "contentsoverride", _paths.ContentsDirectoryOverride);
                _configFile.WriteString(SettingSectionName, "asphyxiaoverride", _paths.AsphyxiaDirectoryOverride);

                if (NoAsphyxiaToggleSwitch != null)
                    _configFile.WriteString(SettingSectionName, "noasphyxia", (NoAsphyxiaToggleSwitch.IsChecked == true).ToString().ToLowerInvariant());
                if (ExitRestoreToggleSwitch != null)
                    _configFile.WriteString(DisplaySectionName, "exitrestore", (ExitRestoreToggleSwitch.IsChecked == true).ToString().ToLowerInvariant());

                if (CompatTypeComboBox != null && CompatTypeComboBox.SelectedItem != null)
                {
                    string renderMode = CompatTypeComboBox.SelectedItem.ToString();
                    _configFile.WriteString(SettingSectionName, "cl-rendermode", renderMode);
                }

                _configFile.WriteString(DisplaySectionName, "displayconfigure", _displayConfigEnabled.ToString().ToLowerInvariant());
                _configFile.WriteString(DisplaySectionName, "mode", _isDualDisplay ? "dual" : "single");

                if (MainScreenComboBox != null) _configFile.WriteString(DisplaySectionName, "mainscreen", MainScreenComboBox.SelectedIndex.ToString());
                if (SubScreenComboBox != null) _configFile.WriteString(DisplaySectionName, "subscreen", SubScreenComboBox.SelectedIndex.ToString());
                if (SubRotationComboBox != null) _configFile.WriteString(DisplaySectionName, "subrotation", SubRotationComboBox.SelectedIndex.ToString());
                if (RotationComboBox != null) _configFile.WriteString(DisplaySectionName, "mainrotation", RotationComboBox.SelectedIndex.ToString());
                if (MainResolutionComboBox != null && MainResolutionComboBox.SelectedItem != null) _configFile.WriteString(DisplaySectionName, "mainresolution", MainResolutionComboBox.SelectedItem.ToString());
                if (SubResolutionComboBox != null && SubResolutionComboBox.SelectedItem != null) _configFile.WriteString(DisplaySectionName, "subresolution", SubResolutionComboBox.SelectedItem.ToString());
                if (MainRefreshRateComboBox != null && MainRefreshRateComboBox.SelectedItem != null) _configFile.WriteString(DisplaySectionName, "mainrefresh", MainRefreshRateComboBox.SelectedItem.ToString());
                if (SubRefreshRateComboBox != null && SubRefreshRateComboBox.SelectedItem != null) _configFile.WriteString(DisplaySectionName, "subrefresh", SubRefreshRateComboBox.SelectedItem.ToString());
            }
            catch (Exception ex)
            {
                ShowErrorToast("保存设置失败", ex.Message);
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
            _viewModel.Settings.CompatibilityRenderMode = GetSelectedCompatRenderMode();
            await _settingsWorkflowService.PersistCompatibilityToggleAsync(_viewModel.Settings);
            ApplyCompatibilityStateFromViewModel();
        }

        private async void OnCompatModeChecked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton { IsChecked: true })
            {
                return;
            }

            await ChangeCompatModeCoreAsync(GetRequestedCompatRenderMode());
        }

        private async void OnCompatTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await ChangeCompatModeCoreAsync(GetSelectedCompatRenderMode());
        }

        private int GetCompatLayerFileCount()
        {
            string modulesDir = GetCompatModulesDirectoryPath();
            string[] compatFiles = { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll" };

            int foundCount = 0;
            foreach (var fileName in compatFiles)
            {
                string filePath = Path.Combine(modulesDir, fileName);
                if (File.Exists(filePath))
                {
                    foundCount++;
                }
            }

            return foundCount;
        }

        private bool IsCompatLayerEnabledConfigured()
        {
            try
            {
                var s = _configFile.ReadString(SettingSectionName, "compatlayer", "false");
                bool enabled;
                return bool.TryParse(s, out enabled) && enabled;
            }
            catch { return false; }
        }

        private void UpdateCompatLayerStatus()
        {
            int fileCount = GetCompatLayerFileCount();
            bool modulesDirectoryExists = HasCompatModulesDirectory();

            bool effectiveEnabled = fileCount >= 1 || IsCompatLayerEnabledConfigured();
            UpdateCompatRenderModeBusyState(effectiveEnabled);

            if (CompatStatusTextBlock != null)
            {
                if (!modulesDirectoryExists && !effectiveEnabled)
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

            if (CompatTypeComboBox != null)
            {
                CompatTypeComboBox.IsEnabled = !effectiveEnabled && modulesDirectoryExists;
                if (effectiveEnabled)
                {
                    ToolTip.SetTip(CompatTypeComboBox, null);
                }
                else if (!modulesDirectoryExists)
                {
                    ToolTip.SetTip(CompatTypeComboBox, "未找到 contents/modules，无法启用显卡兼容层。");
                }
                else if (!string.IsNullOrEmpty(_compatTypeTooltipCache))
                {
                    ToolTip.SetTip(CompatTypeComboBox, _compatTypeTooltipCache);
                }
            }

            if (LoadCompatButton != null)
            {
                LoadCompatButton.IsEnabled = !effectiveEnabled && modulesDirectoryExists;
            }

            if (UnloadCompatButton != null)
            {
                UnloadCompatButton.IsEnabled = effectiveEnabled;
            }

            _isUpdatingCompatUi = true;
            try
            {
                _viewModel.Settings.CompatibilityLayerEnabled = effectiveEnabled;
                if (CompatLayerToggleSwitch != null)
                {
                    CompatLayerToggleSwitch.IsChecked = effectiveEnabled;
                    CompatLayerToggleSwitch.IsEnabled = effectiveEnabled || modulesDirectoryExists;
                }

                bool chipsEnabled = !effectiveEnabled && modulesDirectoryExists;
                if (CompatDx9on12RadioButton != null) CompatDx9on12RadioButton.IsEnabled = chipsEnabled;
                if (CompatDx9on12ExternalRadioButton != null) CompatDx9on12ExternalRadioButton.IsEnabled = chipsEnabled;
                if (CompatDxvkRadioButton != null) CompatDxvkRadioButton.IsEnabled = chipsEnabled;
                SyncCompatModeButtonsFromCombo();
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

        private void SyncCompatModeButtonsFromCombo()
        {
            ApplyCompatRenderModeButtons(GetSelectedCompatRenderMode());
        }

        private async Task ChangeCompatModeCoreAsync(string selected)
        {
            if (_isLoadingSettings || _isUpdatingCompatUi || _isSyncingModel)
            {
                return;
            }

            if (CompatTypeComboBox == null)
            {
                return;
            }

            selected = CompatibilitySettingsService.NormalizeRenderMode(selected);
            if (string.Equals(selected, _lastKnownCompatRenderMode, StringComparison.OrdinalIgnoreCase))
            {
                ApplyCompatRenderModeSelection(selected);
                return;
            }

            _viewModel.Settings.CompatibilityLayerEnabled = IsCompatLayerEffectivelyEnabled();
            _viewModel.Settings.CompatibilityRenderMode = selected;
            await _settingsWorkflowService.PersistCompatibilityRenderModeAsync(_viewModel.Settings);
            ApplyCompatibilityStateFromViewModel();
        }

        private string ResolveDxModeValue()
        {
            return CompatibilitySettingsService.ResolveDxModeValue(IsCompatLayerEffectivelyEnabled(), GetSelectedCompatRenderMode());
        }

        private void ApplyCompatibilityStateFromViewModel()
        {
            _lastKnownCompatRenderMode = CompatibilitySettingsService.NormalizeRenderMode(_viewModel.Settings.CompatibilityRenderMode);
            ApplyCompatRenderModeSelection(_lastKnownCompatRenderMode);
            UpdateCompatLayerStatus();
        }

        private bool IsCompatLayerEffectivelyEnabled()
        {
            try
            {
                int fileCount = GetCompatLayerFileCount();
                return fileCount >= 1 || IsCompatLayerEnabledConfigured();
            }
            catch { return IsCompatLayerEnabledConfigured(); }
        }

        private string GetCompatModulesDirectoryPath()
        {
            return Path.Combine(GetContentsDirectoryPath(), "modules");
        }

        private bool HasCompatModulesDirectory()
        {
            return Directory.Exists(GetCompatModulesDirectoryPath());
        }

        private void EnsureCompatRenderModesInitialized()
        {
            if (CompatTypeComboBox == null || CompatTypeComboBox.Items.Count > 0)
            {
                return;
            }

            CompatTypeComboBox.Items.Add("dx9on12");
            CompatTypeComboBox.Items.Add("dx9on12_external");
            CompatTypeComboBox.Items.Add("dxvk");
        }

        private string GetSelectedCompatRenderMode()
        {
            return CompatibilitySettingsService.NormalizeRenderMode(CompatTypeComboBox?.SelectedItem?.ToString());
        }

        private string GetRequestedCompatRenderMode()
        {
            if (CompatDxvkRadioButton?.IsChecked == true)
            {
                return "dxvk";
            }

            if (CompatDx9on12ExternalRadioButton?.IsChecked == true)
            {
                return "dx9on12_external";
            }

            return GetSelectedCompatRenderMode();
        }

        private void ApplyCompatRenderModeSelection(string renderMode)
        {
            renderMode = CompatibilitySettingsService.NormalizeRenderMode(renderMode);
            EnsureCompatRenderModesInitialized();

            _isSyncingModel = true;
            try
            {
                if (CompatTypeComboBox != null)
                {
                    CompatTypeComboBox.SelectedItem = renderMode;
                }

                ApplyCompatRenderModeButtons(renderMode);
            }
            finally
            {
                _isSyncingModel = false;
            }
        }

        private void ApplyCompatRenderModeButtons(string renderMode)
        {
            renderMode = CompatibilitySettingsService.NormalizeRenderMode(renderMode);
            if (CompatDxvkRadioButton != null)
            {
                CompatDxvkRadioButton.IsChecked = string.Equals(renderMode, "dxvk", StringComparison.OrdinalIgnoreCase);
            }

            if (CompatDx9on12ExternalRadioButton != null)
            {
                CompatDx9on12ExternalRadioButton.IsChecked = string.Equals(renderMode, "dx9on12_external", StringComparison.OrdinalIgnoreCase);
            }

            if (CompatDx9on12RadioButton != null)
            {
                CompatDx9on12RadioButton.IsChecked = string.Equals(renderMode, "dx9on12", StringComparison.OrdinalIgnoreCase);
            }
        }

    }
}


namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private void LoadServerPresetsFromConfig()
        {
            try
            {
                var result = _configFile.LoadServerPresets(NonePresetName, AsphyxiaPresetName, AsphyxiaDefaultUrl);
                _serverPresets.Clear();
                _serverPresets.AddRange(result.Presets);
                _activeServerPreset = string.IsNullOrWhiteSpace(result.ActivePreset) ? NonePresetName : result.ActivePreset;

                if (result.Mutated)
                {
                    _configFile.SaveServerPresets(_serverPresets, _activeServerPreset, NonePresetName);
                }
            }
            catch (Exception ex)
            {
                ShowWarningToast("服务器预设读取异常", ex.Message);
                _serverPresets.Clear();
                _serverPresets.Add(new ServerPresetItem { Name = NonePresetName });
                _serverPresets.Add(new ServerPresetItem { Name = AsphyxiaPresetName, ServerUrl = AsphyxiaDefaultUrl, PcbId = string.Empty });
                _activeServerPreset = NonePresetName;
            }

            _viewModel.Settings.ServerPresets.Clear();
            foreach (var preset in _serverPresets)
            {
                _viewModel.Settings.ServerPresets.Add(preset);
            }

            var activePreset = _serverPresets.FirstOrDefault(p => string.Equals(p.Name, _activeServerPreset, StringComparison.OrdinalIgnoreCase))
                ?? _serverPresets.FirstOrDefault();
            _viewModel.Settings.RunSilently(() => _viewModel.Settings.SelectedServerPreset = activePreset);
            _viewModel.Settings.ActiveServerPreset = _activeServerPreset;
            _viewModel.Settings.ServerAddress = activePreset?.ServerUrl ?? string.Empty;
            _viewModel.Settings.PcbId = activePreset?.PcbId ?? string.Empty;
            RefreshServerPresetCombo();
        }

        private void SaveServerPresetsToConfig()
        {
            _configFile.SaveServerPresets(_serverPresets, _activeServerPreset, NonePresetName);
        }

        private void RefreshServerPresetCombo()
        {
            if (ServerPresetComboBox == null)
            {
                return;
            }

            ServerPresetComboBox.Items.Clear();
            foreach (var preset in _serverPresets)
            {
                ServerPresetComboBox.Items.Add(preset);
            }

            if (ServerPresetComboBox.Items.Count > 0)
            {
                var active = _serverPresets.FirstOrDefault(p => string.Equals(p.Name, _activeServerPreset, StringComparison.OrdinalIgnoreCase));
                ServerPresetComboBox.SelectedItem = active ?? _serverPresets[0];
            }
        }

        private void SelectPresetByCurrentFields()
        {
            if (ServerPresetComboBox == null)
            {
                return;
            }

            var serverUrl = ServerAddressTextBox?.Text ?? string.Empty;
            var pcbId = PcbIdTextBox?.Text ?? string.Empty;

            var matched = _serverPresets.FirstOrDefault(p =>
                !string.Equals(p.Name, NonePresetName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.ServerUrl ?? string.Empty, serverUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.PcbId ?? string.Empty, pcbId, StringComparison.OrdinalIgnoreCase));

            _isSyncingModel = true;
            try
            {
                if (matched != null)
                {
                    ServerPresetComboBox.SelectedItem = matched;
                    _activeServerPreset = matched.Name;
                }
                else
                {
                    ServerPresetComboBox.SelectedIndex = 0;
                    _activeServerPreset = NonePresetName;
                }
            }
            finally
            {
                _isSyncingModel = false;
            }

            _viewModel.Settings.ActiveServerPreset = _activeServerPreset;
        }

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
            _serverPresets.Clear();
            _serverPresets.AddRange(_viewModel.Settings.ServerPresets);
            _activeServerPreset = _viewModel.Settings.ActiveServerPreset;

            _isSyncingModel = true;
            try
            {
                RefreshServerPresetCombo();

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
        private bool UpdateSpiceConfig(params SpiceOptionUpdate[] updates)
        {
            try
            {
                if (updates == null || updates.Length == 0)
                {
                    updates = BuildDefaultOptionUpdates().ToArray();
                }

                var appliedUpdates = updates
                    .Where(update => update != null && !string.IsNullOrEmpty(update.Name))
                    .ToArray();

                if (appliedUpdates.Length == 0)
                {
                    return true;
                }

                string spiceXmlPath = GetSpiceXmlPath();
                if (!File.Exists(spiceXmlPath))
                {
                    ShowErrorToast("保存设定失败", "未找到 spicetools.xml。");
                    RestoreUiFromLastKnownSpiceValues();
                    return false;
                }

                if (!TryGetSpiceOptionsContext(spiceXmlPath, LoadOptions.PreserveWhitespace, true, out var context))
                {
                    ShowErrorToast("保存设定失败", "配置写入失败。");
                    RestoreUiFromLastKnownSpiceValues();
                    return false;
                }

                string normalizationWarning = _spiceConfigFileService.ApplyUpdates(context, appliedUpdates);
                if (!string.IsNullOrWhiteSpace(normalizationWarning))
                {
                    ShowWarningToast("配置格式修复失败", normalizationWarning);
                }

                CacheLastKnownSpiceUpdates(appliedUpdates);
                return true;
            }
            catch (Exception ex)
            {
                ShowErrorToast("保存设定失败", ex.Message);
                RestoreUiFromLastKnownSpiceValues();
                return false;
            }
        }

        private void LoadSpiceConfig()
        {
            bool previousLoadingState = _isLoadingSettings;
            _isLoadingSettings = true;
            try
            {
                if (!TryGetSpiceOptionsContext(LoadOptions.PreserveWhitespace, false, out var context))
                {
                    return;
                }

                string GetValue(string name) => context.GetOptionValue(name);

                CacheLastKnownSpiceValue("w", GetValue("w"));
                CacheLastKnownSpiceValue("sp2x-processefficiency", GetValue("sp2x-processefficiency"));
                CacheLastKnownSpiceValue("sp2x-dx9on12", GetValue("sp2x-dx9on12"));
                CacheLastKnownSpiceValue("sp2x-sdvxnosub", GetValue("sp2x-sdvxnosub"));
                CacheLastKnownSpiceValue("sp2x-windowborder", GetValue("sp2x-windowborder"));
                CacheLastKnownSpiceValue("sdvxwsubborderless", GetValue("sdvxwsubborderless"));
                CacheLastKnownSpiceValue("s", GetValue("s"));
                CacheLastKnownSpiceValue("sp2x-windowalwaysontop", GetValue("sp2x-windowalwaysontop"));
                CacheLastKnownSpiceValue("sp2x-windowsize", GetValue("sp2x-windowsize"));
                CacheLastKnownSpiceValue("graphics-force-single-adapter", GetValue("graphics-force-single-adapter"));
                CacheLastKnownSpiceValue("sdvxwsubtop", GetValue("sdvxwsubtop"));
                CacheLastKnownSpiceValue("sp2x-sdvxsubredraw", GetValue("sp2x-sdvxsubredraw"));
                CacheLastKnownSpiceValue("sdvxnativetouch", GetValue("sdvxnativetouch"));
                CacheLastKnownSpiceValue("sp2x-sdvxasio", GetValue("sp2x-sdvxasio"));
                CacheLastKnownSpiceValue("sp2x-lowlatencysharedaudio", GetValue("sp2x-lowlatencysharedaudio"));
                CacheLastKnownSpiceValue("cardio", GetValue("cardio"));
                CacheLastKnownSpiceValue("scard", GetValue("scard"));
                CacheLastKnownSpiceValue("netdump", GetValue("netdump"));
                CacheLastKnownSpiceValue("network", NormalizeNetworkValue(GetValue("network")));
                CacheLastKnownSpiceValue("subnet", NormalizeNetworkValue(GetValue("subnet")));
                CacheLastKnownSpiceValue("url", GetValue("url"));
                CacheLastKnownSpiceValue("p", GetValue("p"));

                var wVal = GetValue("w");
                bool windowed = string.Equals(wVal, "/ENABLED", StringComparison.OrdinalIgnoreCase);
                if (WindowedToggleSwitch != null)
                {
                    WindowedToggleSwitch.IsChecked = windowed;
                }

                var peVal = GetValue("sp2x-processefficiency");
                _pCoreOptimization = string.Equals(peVal, "pcores", StringComparison.OrdinalIgnoreCase);

                _disableSubDisplay = string.Equals(GetValue("sp2x-sdvxnosub"), "/ENABLED", StringComparison.Ordinal);
                var wborder = GetValue("sp2x-windowborder");
                if (string.Equals(wborder, "1", StringComparison.Ordinal))
                {
                    _windowModeIndex = 1;
                }
                else if (string.Equals(wborder, "2", StringComparison.Ordinal))
                {
                    _windowModeIndex = 2;
                }
                else
                {
                    _windowModeIndex = 0;
                }

                _subBorderless = string.Equals(GetValue("sdvxwsubborderless"), "/ENABLED", StringComparison.Ordinal);
                _showCursorTouchSim = string.Equals(GetValue("s"), "/ENABLED", StringComparison.Ordinal);
                _windowTopMost = string.Equals(GetValue("sp2x-windowalwaysontop"), "/ENABLED", StringComparison.Ordinal);
                _windowSize = GetValue("sp2x-windowsize") ?? string.Empty;
                _singleAdapter = string.Equals(GetValue("graphics-force-single-adapter"), "/ENABLED", StringComparison.Ordinal);
                _subWindowTopMost = string.Equals(GetValue("sdvxwsubtop"), "/ENABLED", StringComparison.Ordinal);
                _subForceRender = string.Equals(GetValue("sp2x-sdvxsubredraw"), "/ENABLED", StringComparison.Ordinal);
                _nativeTouch = string.Equals(GetValue("sdvxnativetouch"), "/ENABLED", StringComparison.Ordinal);
                _asioDriver = GetValue("sp2x-sdvxasio") ?? string.Empty;
                _lowLatencySharedAudio = string.Equals(GetValue("sp2x-lowlatencysharedaudio"), "/ENABLED", StringComparison.Ordinal);
                _cardIo = string.Equals(GetValue("cardio"), "/ENABLED", StringComparison.Ordinal);
                _hidSmartCard = string.Equals(GetValue("scard"), "/ENABLED", StringComparison.Ordinal);
                _dbgNetDump = string.Equals(GetValue("netdump"), "/ENABLED", StringComparison.Ordinal);
                if (NetworkAdapterIpTextBox != null)
                {
                    NetworkAdapterIpTextBox.Text = NormalizeNetworkValue(GetValue("network"));
                }

                if (NetworkAdapterSubnetTextBox != null)
                {
                    NetworkAdapterSubnetTextBox.Text = NormalizeNetworkValue(GetValue("subnet"));
                }

                RefreshNetworkAdapterChoices(GetNetworkAdapterIpAddress(), GetNetworkAdapterSubnetMask());
                if (ServerAddressTextBox != null)
                {
                    ServerAddressTextBox.Text = GetValue("url");
                }

                if (PcbIdTextBox != null)
                {
                    PcbIdTextBox.Text = GetValue("p");
                }

                if (NetDumpToggleSwitch != null)
                {
                    NetDumpToggleSwitch.IsChecked = _dbgNetDump;
                }

                if (DisableSubDisplayToggleSwitch != null)
                {
                    DisableSubDisplayToggleSwitch.IsChecked = _disableSubDisplay;
                }

                if (WindowModeComboBox != null)
                {
                    WindowModeComboBox.SelectedIndex = _windowModeIndex;
                }

                if (PCoreOptimizationToggleSwitch != null)
                {
                    PCoreOptimizationToggleSwitch.IsChecked = _pCoreOptimization;
                }

                if (SubBorderlessToggleSwitch != null)
                {
                    SubBorderlessToggleSwitch.IsChecked = _subBorderless;
                }

                if (ShowCursorTouchSimToggleSwitch != null)
                {
                    ShowCursorTouchSimToggleSwitch.IsChecked = _showCursorTouchSim;
                }

                if (WindowTopMostToggleSwitch != null)
                {
                    WindowTopMostToggleSwitch.IsChecked = _windowTopMost;
                }

                if (WindowSizeTextBox != null)
                {
                    WindowSizeTextBox.Text = _windowSize;
                }

                if (SingleAdapterToggleSwitch != null)
                {
                    SingleAdapterToggleSwitch.IsChecked = _singleAdapter;
                }

                if (SubWindowTopMostToggleSwitch != null)
                {
                    SubWindowTopMostToggleSwitch.IsChecked = _subWindowTopMost;
                }

                if (SubForceRenderToggleSwitch != null)
                {
                    SubForceRenderToggleSwitch.IsChecked = _subForceRender;
                }

                if (NativeTouchToggleSwitch != null)
                {
                    NativeTouchToggleSwitch.IsChecked = _nativeTouch;
                }

                RefreshAsioDriverChoices(_asioDriver);
                if (LowLatencySharedAudioToggleSwitch != null)
                {
                    LowLatencySharedAudioToggleSwitch.IsChecked = _lowLatencySharedAudio;
                }

                if (CardIoToggleSwitch != null)
                {
                    CardIoToggleSwitch.IsChecked = _cardIo;
                }

                if (HidSmartCardToggleSwitch != null)
                {
                    HidSmartCardToggleSwitch.IsChecked = _hidSmartCard;
                }

                SelectPresetByCurrentFields();
            }
            catch (Exception ex)
            {
                ShowErrorToast("读取配置失败", ex.Message);
            }
            finally
            {
                _isLoadingSettings = previousLoadingState;
            }
        }

        private void CacheLastKnownSpiceValue(string key, string value)
        {
            _lastKnownSpiceValues[key] = value ?? string.Empty;
        }

        private void CacheLastKnownSpiceUpdates(IEnumerable<SpiceOptionUpdate> updates)
        {
            foreach (var update in updates)
            {
                if (update == null || string.IsNullOrEmpty(update.Name))
                {
                    continue;
                }

                var value = update.ShouldRemove
                    ? string.Empty
                    : NormalizeCachedSpiceValue(update.Name, update.Value);
                CacheLastKnownSpiceValue(update.Name, value);
            }
        }

        private static string NormalizeCachedSpiceValue(string optionName, string value)
        {
            if (string.Equals(optionName, "network", StringComparison.Ordinal)
                || string.Equals(optionName, "subnet", StringComparison.Ordinal))
            {
                return NormalizeNetworkValue(value);
            }

            return value ?? string.Empty;
        }

        private string GetLastKnownSpiceValue(string key)
        {
            return _lastKnownSpiceValues.TryGetValue(key, out var value) ? value : string.Empty;
        }

        private void RestoreUiFromLastKnownSpiceValues()
        {
            if (_lastKnownSpiceValues.Count == 0)
            {
                return;
            }

            bool previousLoadingState = _isLoadingSettings;
            _isLoadingSettings = true;
            try
            {
                if (WindowedToggleSwitch != null)
                {
                    WindowedToggleSwitch.IsChecked = string.Equals(GetLastKnownSpiceValue("w"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                }

                _pCoreOptimization = string.Equals(GetLastKnownSpiceValue("sp2x-processefficiency"), "pcores", StringComparison.OrdinalIgnoreCase);
                _disableSubDisplay = string.Equals(GetLastKnownSpiceValue("sp2x-sdvxnosub"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _subBorderless = string.Equals(GetLastKnownSpiceValue("sdvxwsubborderless"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _showCursorTouchSim = string.Equals(GetLastKnownSpiceValue("s"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _windowTopMost = string.Equals(GetLastKnownSpiceValue("sp2x-windowalwaysontop"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _windowSize = GetLastKnownSpiceValue("sp2x-windowsize");
                _singleAdapter = string.Equals(GetLastKnownSpiceValue("graphics-force-single-adapter"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _subWindowTopMost = string.Equals(GetLastKnownSpiceValue("sdvxwsubtop"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _subForceRender = string.Equals(GetLastKnownSpiceValue("sp2x-sdvxsubredraw"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _nativeTouch = string.Equals(GetLastKnownSpiceValue("sdvxnativetouch"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _asioDriver = GetLastKnownSpiceValue("sp2x-sdvxasio");
                _lowLatencySharedAudio = string.Equals(GetLastKnownSpiceValue("sp2x-lowlatencysharedaudio"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _cardIo = string.Equals(GetLastKnownSpiceValue("cardio"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _hidSmartCard = string.Equals(GetLastKnownSpiceValue("scard"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _dbgNetDump = string.Equals(GetLastKnownSpiceValue("netdump"), "/ENABLED", StringComparison.OrdinalIgnoreCase);

                var wborder = GetLastKnownSpiceValue("sp2x-windowborder");
                if (string.Equals(wborder, "1", StringComparison.Ordinal))
                {
                    _windowModeIndex = 1;
                }
                else if (string.Equals(wborder, "2", StringComparison.Ordinal))
                {
                    _windowModeIndex = 2;
                }
                else
                {
                    _windowModeIndex = 0;
                }

                if (DisableSubDisplayToggleSwitch != null)
                {
                    DisableSubDisplayToggleSwitch.IsChecked = _disableSubDisplay;
                }

                if (NetDumpToggleSwitch != null)
                {
                    NetDumpToggleSwitch.IsChecked = _dbgNetDump;
                }

                if (PCoreOptimizationToggleSwitch != null)
                {
                    PCoreOptimizationToggleSwitch.IsChecked = _pCoreOptimization;
                }

                if (SubBorderlessToggleSwitch != null)
                {
                    SubBorderlessToggleSwitch.IsChecked = _subBorderless;
                }

                if (ShowCursorTouchSimToggleSwitch != null)
                {
                    ShowCursorTouchSimToggleSwitch.IsChecked = _showCursorTouchSim;
                }

                if (WindowTopMostToggleSwitch != null)
                {
                    WindowTopMostToggleSwitch.IsChecked = _windowTopMost;
                }

                if (WindowSizeTextBox != null)
                {
                    WindowSizeTextBox.Text = _windowSize;
                }

                if (SingleAdapterToggleSwitch != null)
                {
                    SingleAdapterToggleSwitch.IsChecked = _singleAdapter;
                }

                if (SubWindowTopMostToggleSwitch != null)
                {
                    SubWindowTopMostToggleSwitch.IsChecked = _subWindowTopMost;
                }

                if (SubForceRenderToggleSwitch != null)
                {
                    SubForceRenderToggleSwitch.IsChecked = _subForceRender;
                }

                if (NativeTouchToggleSwitch != null)
                {
                    NativeTouchToggleSwitch.IsChecked = _nativeTouch;
                }

                RefreshAsioDriverChoices(_asioDriver);
                if (LowLatencySharedAudioToggleSwitch != null)
                {
                    LowLatencySharedAudioToggleSwitch.IsChecked = _lowLatencySharedAudio;
                }

                if (CardIoToggleSwitch != null)
                {
                    CardIoToggleSwitch.IsChecked = _cardIo;
                }

                if (HidSmartCardToggleSwitch != null)
                {
                    HidSmartCardToggleSwitch.IsChecked = _hidSmartCard;
                }

                if (WindowModeComboBox != null)
                {
                    WindowModeComboBox.SelectedIndex = _windowModeIndex;
                }

                RestoreNetworkUiFromLastKnownValues();

                if (ServerAddressTextBox != null)
                {
                    ServerAddressTextBox.Text = GetLastKnownSpiceValue("url");
                }

                if (PcbIdTextBox != null)
                {
                    PcbIdTextBox.Text = GetLastKnownSpiceValue("p");
                }

                SelectPresetByCurrentFields();
            }
            finally
            {
                _isLoadingSettings = previousLoadingState;
            }
        }

        private IEnumerable<SpiceOptionUpdate> BuildDefaultOptionUpdates()
        {
            yield return new SpiceOptionUpdate("w", WindowedToggleSwitch != null && WindowedToggleSwitch.IsChecked == true ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sp2x-processefficiency", _pCoreOptimization ? "pcores" : string.Empty);
            yield return new SpiceOptionUpdate("sp2x-dx9on12", ResolveDxModeValue(), false);
            yield return new SpiceOptionUpdate("sp2x-sdvxnosub", _disableSubDisplay ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sp2x-windowborder", ResolveWindowBorderValue());
            yield return new SpiceOptionUpdate("sdvxwsubborderless", _subBorderless ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("s", _showCursorTouchSim ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sp2x-windowalwaysontop", _windowTopMost ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sp2x-windowsize", _windowSize ?? string.Empty);
            yield return new SpiceOptionUpdate("graphics-force-single-adapter", _singleAdapter ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sdvxwsubtop", _subWindowTopMost ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sp2x-sdvxsubredraw", _subForceRender ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sdvxnativetouch", _nativeTouch ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sp2x-sdvxasio", _asioDriver ?? string.Empty);
            yield return new SpiceOptionUpdate("sp2x-lowlatencysharedaudio", _lowLatencySharedAudio ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("cardio", _cardIo ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("scard", _hidSmartCard ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("netdump", _dbgNetDump ? "/ENABLED" : string.Empty);

            if (NetworkAdapterIpTextBox != null)
            {
                yield return new SpiceOptionUpdate("network", GetNetworkAdapterIpAddress(), false);
            }

            if (NetworkAdapterSubnetTextBox != null)
            {
                yield return new SpiceOptionUpdate("subnet", GetNetworkAdapterSubnetMask(), false);
            }

            if (ServerAddressTextBox != null)
            {
                yield return new SpiceOptionUpdate("url", ServerAddressTextBox.Text ?? string.Empty, false);
            }

            if (PcbIdTextBox != null)
            {
                yield return new SpiceOptionUpdate("p", PcbIdTextBox.Text ?? string.Empty, false);
            }
        }

        private bool EnsureSpiceXmlExistsForTextOrRevert(TextBox textBox, string optionName)
        {
            var xmlPath = GetSpiceXmlPath();
            if (File.Exists(xmlPath))
            {
                return true;
            }

            ShowErrorToast("保存设定失败", "未找到 spicetools.xml。");

            _isUpdatingSpiceToggleUi = true;
            try
            {
                if (textBox != null)
                {
                    textBox.Text = GetLastKnownSpiceValue(optionName);
                }
            }
            finally
            {
                _isUpdatingSpiceToggleUi = false;
            }

            return false;
        }

        private string ResolveWindowBorderValue()
        {
            switch (_windowModeIndex)
            {
                case 1:
                    return "1";
                case 2:
                    return "2";
                default:
                    return string.Empty;
            }
        }

        private bool TryGetSpiceOptionsContext(LoadOptions loadOptions, bool createOptionsWhenMissing, out SpiceOptionsContext context)
        {
            string spiceXmlPath = GetSpiceXmlPath();
            return TryGetSpiceOptionsContext(spiceXmlPath, loadOptions, createOptionsWhenMissing, out context);
        }

        private bool TryGetSpiceOptionsContext(string spiceXmlPath, LoadOptions loadOptions, bool createOptionsWhenMissing, out SpiceOptionsContext context)
        {
            if (!_spiceConfigFileService.TryLoadOptionsContext(
                    spiceXmlPath,
                    loadOptions,
                    createOptionsWhenMissing,
                    out context,
                    out var message,
                    out var warning))
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return false;
                }

                if (warning)
                {
                    ShowWarningToast("读取配置异常", message);
                }
                else
                {
                    ShowErrorToast("读取配置失败", message);
                }

                return false;
            }

            return true;
        }
    }
}


namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private static readonly SpiceOptionUpdate[] RecommendedSpiceOptionUpdates =
        {
            new SpiceOptionUpdate("k", "ifs_hook.dll", false),
            new SpiceOptionUpdate("sp2x-nvprofile", "/ENABLED", false),
            new SpiceOptionUpdate("sp2x-lowlatencysharedaudio", "/ENABLED", false),
            new SpiceOptionUpdate("sp2x-dx9on12", "0", false),
            new SpiceOptionUpdate("url", "http://localhost:8083", false),
            new SpiceOptionUpdate("sp2x-sdvxsubredraw", "/ENABLED", false)
        };

        private void UpdateRecommendedSpiceConfigButtonVisibility()
        {
            if (ImportRecommendedSpiceConfigButton != null)
            {
                ImportRecommendedSpiceConfigButton.IsVisible = !_portableMode;
            }
        }
    }
}

