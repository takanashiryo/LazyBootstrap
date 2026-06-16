using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace LazyBootstrap.Features.Settings.Views
{
    public partial class SettingsView : UserControl
    {
        private readonly SettingsState _settingsState = null!;
        private readonly SettingsOrchestrator _settingsWorkflowService = null!;
        private readonly AppShellState _shellState = null!;
        private readonly ILogger<SettingsView> _logger = null!;

        private bool _isLoadingSettings;
        private bool _isSyncingModel;
        private bool _isUpdatingGpuCompatLayerUi;
        private bool _isUpdatingServerPresetUi;
        private bool _isUpdatingAsioDriverUi;
        private bool _isUpdatingNetworkUi;

        public SettingsView()
        {
            InitializeComponent();
        }

        public SettingsView(
            SettingsState settingsState,
            SettingsOrchestrator settingsOrchestrator,
            AppShellState shellState,
            ILogger<SettingsView> logger)
        {
            InitializeComponent();

            _settingsState = settingsState ?? throw new ArgumentNullException(nameof(settingsState));
            _settingsWorkflowService = settingsOrchestrator ?? throw new ArgumentNullException(nameof(settingsOrchestrator));
            _shellState = shellState ?? throw new ArgumentNullException(nameof(shellState));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _isLoadingSettings = true;
            InitializeCustomComponents();
            _isLoadingSettings = false;

            _shellState.PropertyChanged += OnShellStateChanged;
        }

        /// <summary>Loads the initial (startup) settings and applies them to the UI.</summary>
        public async Task InitializeStartupAsync()
        {
            await _settingsWorkflowService.InitializeStartupAsync(_settingsState);
            ApplyStartupSettingsStateToUi();
        }

        /// <summary>Warms up deferred settings options (ASIO/network/...) and applies them.</summary>
        public async Task WarmDeferredAsync()
        {
            await _settingsWorkflowService.WarmDeferredAsync(_settingsState);
            ApplyDeferredSettingsStateToUi();
        }

        private void InitializeCustomComponents()
        {
            InitializeGpuCompatLayerControls();
            InitializeNetworkBindings();
            InitializeStartupSettingsBindings();
            InitializeSpiceSettingsBindings();
            InitializeServerPresetBindings();
        }

        private void OnShellStateChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnShellStateChanged(sender, e));
                return;
            }

            string propertyName = e?.PropertyName ?? string.Empty;
            if ((string.IsNullOrWhiteSpace(propertyName)
                 || string.Equals(propertyName, nameof(AppShellState.SelectedPage), StringComparison.Ordinal))
                && _shellState.SelectedPage == ShellPage.Settings)
            {
                ApplyServerPresetStateToUi();
            }
        }
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

        private void UpdateAsioControlPanelButtonState()
        {
            if (OpenAsioControlPanelButton == null)
            {
                return;
            }

            var selectedDriverValue = _settingsState.SelectedAsioDriver?.Value
                ?? _settingsState.AsioDriverValue
                ?? string.Empty;

            OpenAsioControlPanelButton.IsEnabled = OperatingSystem.IsWindows()
                && !string.IsNullOrWhiteSpace(selectedDriverValue);
        }

        private Task PersistSpice() => _settingsWorkflowService.PersistSpiceSettingsAsync(_settingsState);

        private void BindToggleSwitch(ToggleSwitch toggle, Action<bool> setValue, Func<Task> persist)
        {
            if (toggle is null) return;
            toggle.IsCheckedChanged += async (_, _) =>
            {
                if (_isLoadingSettings) return;
                setValue(toggle.IsChecked == true);
                await persist();
            };
        }

        private static string BuildCurrentNetworkAdapterDisplayName(string ipAddress, string subnetMask)
        {
            var normalizedIpAddress = ConfigHelper.NormalizeNetworkValue(ipAddress);
            var normalizedSubnetMask = ConfigHelper.NormalizeNetworkValue(subnetMask);
            if (string.IsNullOrEmpty(normalizedIpAddress) && string.IsNullOrEmpty(normalizedSubnetMask))
            {
                return "无";
            }

            if (string.IsNullOrEmpty(normalizedIpAddress))
            {
                return $"{normalizedSubnetMask}（当前配置）";
            }

            if (string.IsNullOrEmpty(normalizedSubnetMask))
            {
                return $"{normalizedIpAddress}（当前配置）";
            }

            return $"{normalizedIpAddress} / {normalizedSubnetMask}（当前配置）";
        }

        private void InitializeGpuCompatLayerControls()
        {
            if (GpuCompatLayerToggleSwitch != null)
            {
                GpuCompatLayerToggleSwitch.IsCheckedChanged -= OnGpuCompatLayerToggleChanged;
                GpuCompatLayerToggleSwitch.IsCheckedChanged += OnGpuCompatLayerToggleChanged;
            }
        }

        private void InitializeNetworkBindings()
        {
            if (ServerAddressTextBox != null)
            {
                ServerAddressTextBox.PlaceholderText = "http://SERVER:PORT";
            }
            if (PcbIdTextBox != null)
            {
                PcbIdTextBox.PlaceholderText = string.Empty;
            }
            if (NetworkAdapterIpTextBox != null)
            {
                NetworkAdapterIpTextBox.PlaceholderText = string.Empty;
                NetworkAdapterIpTextBox.TextChanged += async (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingNetworkUi) return;
                    _settingsState.NetworkAdapterIp = NetworkAdapterIpTextBox.Text ?? string.Empty;
                    _settingsState.NetworkAdapterSubnet = NetworkAdapterSubnetTextBox?.Text ?? string.Empty;
                    await _settingsWorkflowService.PersistNetworkSettingsAsync(_settingsState);
                    ApplyNetworkAdapterStateFromState();
                };
            }
            if (NetworkAdapterSubnetTextBox != null)
            {
                NetworkAdapterSubnetTextBox.PlaceholderText = string.Empty;
                NetworkAdapterSubnetTextBox.TextChanged += async (s, e) =>
                {
                    if (_isLoadingSettings || _isUpdatingNetworkUi) return;
                    _settingsState.NetworkAdapterIp = NetworkAdapterIpTextBox?.Text ?? string.Empty;
                    _settingsState.NetworkAdapterSubnet = NetworkAdapterSubnetTextBox.Text ?? string.Empty;
                    await _settingsWorkflowService.PersistNetworkSettingsAsync(_settingsState);
                    ApplyNetworkAdapterStateFromState();
                };
            }

            if (OpenNetworkAdapterPickerButton != null)
            {
                OpenNetworkAdapterPickerButton.Content = "加载中...";
                OpenNetworkAdapterPickerButton.IsEnabled = false;
                ToolTip.SetTip(OpenNetworkAdapterPickerButton, "正在读取网卡配置...");
            }
        }

        private void InitializeStartupSettingsBindings()
        {
            BindToggleSwitch(WindowedToggleSwitch,
                v => _settingsState.Windowed = v,
                () => _settingsWorkflowService.PersistSpiceSettingsAsync(_settingsState));

            if (NoAsphyxiaToggleSwitch != null)
            {
                NoAsphyxiaToggleSwitch.IsCheckedChanged += async (_, _) =>
                {
                    if (_isLoadingSettings) return;
                    _settingsState.NoAsphyxia = NoAsphyxiaToggleSwitch.IsChecked == true;
                    await _settingsWorkflowService.PersistLauncherSettingsAsync(_settingsState);
                };
            }

            if (UseSystemSpiceConfigToggleSwitch != null)
            {
                UseSystemSpiceConfigToggleSwitch.IsCheckedChanged += async (_, _) =>
                {
                    if (_isLoadingSettings) return;
                    _settingsState.UseSystemSpiceConfig = UseSystemSpiceConfigToggleSwitch.IsChecked == true;
                    await _settingsWorkflowService.PersistUseSystemSpiceConfigAsync(_settingsState);
                    ApplyStartupSettingsStateToUi();
                    ApplyDeferredSettingsStateToUi();
                };
            }
        }

        private void InitializeSpiceSettingsBindings()
        {
            if (DllInjectionTextBox != null)
            {
                DllInjectionTextBox.PlaceholderText = "example.dll";
                DllInjectionTextBox.TextChanged += async (_, _) =>
                {
                    if (_isLoadingSettings) return;
                    _settingsState.DllInjection = DllInjectionTextBox.Text ?? string.Empty;
                    await _settingsWorkflowService.PersistSpiceSettingsAsync(_settingsState);
                };
            }

            BindToggleSwitch(NetDumpToggleSwitch, v => _settingsState.NetDump = v, PersistSpice);
            BindToggleSwitch(DisableSubDisplayToggleSwitch, v => _settingsState.DisableSubDisplay = v, PersistSpice);

            if (WindowModeComboBox != null)
            {
                WindowModeComboBox.SelectionChanged += async (_, _) =>
                {
                    if (_isLoadingSettings) return;
                    _settingsState.WindowModeIndex = WindowModeComboBox.SelectedIndex < 0 ? 0 : WindowModeComboBox.SelectedIndex;
                    await PersistSpice();
                };
            }

            BindToggleSwitch(PCoreOptimizationToggleSwitch, v => _settingsState.PCoreOptimization = v, PersistSpice);
            BindToggleSwitch(SubBorderlessToggleSwitch, v => _settingsState.SubBorderless = v, PersistSpice);
            BindToggleSwitch(ShowCursorTouchSimToggleSwitch, v => _settingsState.ShowCursorTouchSim = v, PersistSpice);
            BindToggleSwitch(WindowTopMostToggleSwitch, v => _settingsState.WindowTopMost = v, PersistSpice);
            BindToggleSwitch(SingleAdapterToggleSwitch, v => _settingsState.SingleAdapter = v, PersistSpice);
            BindToggleSwitch(NvidiaPerformanceProfileToggleSwitch, v => _settingsState.NvidiaPerformanceProfile = v, PersistSpice);
            BindToggleSwitch(SubWindowTopMostToggleSwitch, v => _settingsState.SubWindowTopMost = v, PersistSpice);
            BindToggleSwitch(SubForceRenderToggleSwitch, v => _settingsState.SubForceRender = v, PersistSpice);
            BindToggleSwitch(NativeTouchToggleSwitch, v => _settingsState.NativeTouch = v, PersistSpice);
            BindToggleSwitch(CardIoToggleSwitch, v => _settingsState.CardIo = v, PersistSpice);
            BindToggleSwitch(HidSmartCardToggleSwitch, v => _settingsState.HidSmartCard = v, PersistSpice);

            if (WindowSizeTextBox != null)
            {
                WindowSizeTextBox.TextChanged += async (_, _) =>
                {
                    if (_isLoadingSettings) return;
                    _settingsState.WindowSize = WindowSizeTextBox.Text ?? string.Empty;
                    await PersistSpice();
                };
            }
            if (AsioDriverComboBox != null)
            {
                ApplyAsioDriverChoicesFromState();

                AsioDriverComboBox.DropDownOpened += (s, e) =>
                {
                    ApplyAsioDriverChoicesFromState();
                };

                AsioDriverComboBox.SelectionChanged += async (_, _) =>
                {
                    if (_isLoadingSettings || _isUpdatingAsioDriverUi) return;
                    if (AsioDriverComboBox.SelectedItem is AsioDriverOption choice)
                    {
                        _settingsState.SelectedAsioDriver = choice;
                        _settingsState.AsioDriverValue = choice.Value;
                    }
                    else
                    {
                        _settingsState.SelectedAsioDriver = null;
                        _settingsState.AsioDriverValue = string.Empty;
                    }

                    await PersistSpice();
                    UpdateAsioControlPanelButtonState();
                };
            }
            BindToggleSwitch(Asio2ChToggleSwitch, v => _settingsState.Asio2Ch = v, PersistSpice);
            if (VolumeBoostComboBox != null)
            {
                VolumeBoostComboBox.SelectionChanged += async (_, _) =>
                {
                    if (_isLoadingSettings) return;
                    _settingsState.VolumeBoostIndex = VolumeBoostComboBox.SelectedIndex < 0 ? 0 : VolumeBoostComboBox.SelectedIndex;
                    await PersistSpice();
                };
            }
            if (ResampleComboBox != null)
            {
                ResampleComboBox.SelectionChanged += async (_, _) =>
                {
                    if (_isLoadingSettings) return;
                    _settingsState.ResampleIndex = ResampleComboBox.SelectedIndex < 0 ? 0 : ResampleComboBox.SelectedIndex;
                    await PersistSpice();
                };
            }
            BindToggleSwitch(WasapiSharedToggleSwitch, v => _settingsState.WasapiShared = v, PersistSpice);
            BindToggleSwitch(LowLatencySharedAudioToggleSwitch, v => _settingsState.LowLatencySharedAudio = v, PersistSpice);
        }

        private void InitializeServerPresetBindings()
        {
            if (ServerAddressTextBox != null)
            {
                ServerAddressTextBox.TextChanged += async (s, e) =>
                {
                    if (_isLoadingSettings || _isSyncingModel) return;
                    _settingsState.ServerAddress = ServerAddressTextBox.Text ?? string.Empty;
                    _settingsState.PcbId = PcbIdTextBox?.Text ?? string.Empty;
                    await _settingsWorkflowService.PersistServerEndpointAsync(_settingsState);
                    ApplyServerPresetStateToUi();
                };
            }
            if (PcbIdTextBox != null)
            {
                PcbIdTextBox.TextChanged += async (s, e) =>
                {
                    if (_isLoadingSettings || _isSyncingModel) return;
                    _settingsState.ServerAddress = ServerAddressTextBox?.Text ?? string.Empty;
                    _settingsState.PcbId = PcbIdTextBox.Text ?? string.Empty;
                    await _settingsWorkflowService.PersistServerEndpointAsync(_settingsState);
                    ApplyServerPresetStateToUi();
                };
            }
        }
    }
}
