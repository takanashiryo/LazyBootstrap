using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.MessageBox;

namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private void HookDisplayViewModelState()
        {
            if (_viewModel?.Display == null)
            {
                return;
            }

            _viewModel.Display.PropertyChanged -= OnDisplayViewModelPropertyChanged;
            _viewModel.Display.PropertyChanged += OnDisplayViewModelPropertyChanged;

            _viewModel.Display.Displays.CollectionChanged -= OnDisplayCollectionChanged;
            _viewModel.Display.Displays.CollectionChanged += OnDisplayCollectionChanged;
            _viewModel.Display.Rotations.CollectionChanged -= OnDisplayCollectionChanged;
            _viewModel.Display.Rotations.CollectionChanged += OnDisplayCollectionChanged;
            _viewModel.Display.MainResolutions.CollectionChanged -= OnDisplayCollectionChanged;
            _viewModel.Display.MainResolutions.CollectionChanged += OnDisplayCollectionChanged;
            _viewModel.Display.SubResolutions.CollectionChanged -= OnDisplayCollectionChanged;
            _viewModel.Display.SubResolutions.CollectionChanged += OnDisplayCollectionChanged;
            _viewModel.Display.MainRefreshRates.CollectionChanged -= OnDisplayCollectionChanged;
            _viewModel.Display.MainRefreshRates.CollectionChanged += OnDisplayCollectionChanged;
            _viewModel.Display.SubRefreshRates.CollectionChanged -= OnDisplayCollectionChanged;
            _viewModel.Display.SubRefreshRates.CollectionChanged += OnDisplayCollectionChanged;
        }

        private void UnhookDisplayViewModelState()
        {
            if (_viewModel?.Display == null)
            {
                return;
            }

            _viewModel.Display.PropertyChanged -= OnDisplayViewModelPropertyChanged;
            _viewModel.Display.Displays.CollectionChanged -= OnDisplayCollectionChanged;
            _viewModel.Display.Rotations.CollectionChanged -= OnDisplayCollectionChanged;
            _viewModel.Display.MainResolutions.CollectionChanged -= OnDisplayCollectionChanged;
            _viewModel.Display.SubResolutions.CollectionChanged -= OnDisplayCollectionChanged;
            _viewModel.Display.MainRefreshRates.CollectionChanged -= OnDisplayCollectionChanged;
            _viewModel.Display.SubRefreshRates.CollectionChanged -= OnDisplayCollectionChanged;
        }

        private void OnDisplayViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                var propertyName = e?.PropertyName;
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    ApplyDisplayViewModelStateToUi();
                    return;
                }

                switch (propertyName)
                {
                    case nameof(DisplayConfigurationPageViewModel.SelectedTarget):
                        SelectDisplayTarget(MapDisplaySelectionTarget(_viewModel.Display.SelectedTarget));
                        break;

                    case nameof(DisplayConfigurationPageViewModel.ShowNoScreenSelected):
                    case nameof(DisplayConfigurationPageViewModel.ShowMainScreenConfig):
                    case nameof(DisplayConfigurationPageViewModel.ShowSubScreenConfig):
                    case nameof(DisplayConfigurationPageViewModel.IsDisplayConfigurationEnabled):
                    case nameof(DisplayConfigurationPageViewModel.IsDualDisplay):
                    case nameof(DisplayConfigurationPageViewModel.SelectedMainDisplay):
                    case nameof(DisplayConfigurationPageViewModel.SelectedSubDisplay):
                    case nameof(DisplayConfigurationPageViewModel.SelectedMainRotation):
                    case nameof(DisplayConfigurationPageViewModel.SelectedSubRotation):
                    case nameof(DisplayConfigurationPageViewModel.SelectedMainResolution):
                    case nameof(DisplayConfigurationPageViewModel.SelectedSubResolution):
                    case nameof(DisplayConfigurationPageViewModel.SelectedMainRefreshRate):
                    case nameof(DisplayConfigurationPageViewModel.SelectedSubRefreshRate):
                    case nameof(DisplayConfigurationPageViewModel.MainOutputInfo):
                    case nameof(DisplayConfigurationPageViewModel.SubOutputInfo):
                    case nameof(DisplayConfigurationPageViewModel.MainStartupInfo):
                    case nameof(DisplayConfigurationPageViewModel.SubStartupInfo):
                    case nameof(DisplayConfigurationPageViewModel.MainDiagnosticsTooltip):
                    case nameof(DisplayConfigurationPageViewModel.SubDiagnosticsTooltip):
                        ApplyDisplayViewModelStateToUi();
                        break;
                }
            });
        }

        private void OnDisplayCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _ = Dispatcher.UIThread.InvokeAsync(ApplyDisplayViewModelStateToUi);
        }

        private void InitializeDisplayLayoutControls()
        {
            if (!_isDisplayLayoutInitialized)
            {
                if (MainScreenComboBox != null)
                {
                    MainScreenComboBox.SelectionChanged += (s, e) =>
                    {
                        if (_isLoadingSettings) return;
                        RefreshMainOptions();
                        UpdateDisplayInfoTexts();
                        SaveSettings();
                    };
                }

                if (SubScreenComboBox != null)
                {
                    SubScreenComboBox.SelectionChanged += (s, e) =>
                    {
                        if (_isLoadingSettings) return;
                        RefreshSubOptions();
                        UpdateDisplayInfoTexts();
                        SaveSettings();
                    };
                }

                if (RotationComboBox != null)
                {
                    RotationComboBox.SelectionChanged += (s, e) =>
                    {
                        if (_isLoadingSettings) return;
                        RefreshMainOptions(refreshResolutionList: true, refreshRateList: true);
                        UpdateDisplayInfoTexts();
                        SaveSettings();
                    };
                }

                if (SubRotationComboBox != null)
                {
                    SubRotationComboBox.SelectionChanged += (s, e) =>
                    {
                        if (_isLoadingSettings) return;
                        RefreshSubOptions(refreshResolutionList: true, refreshRateList: true);
                        UpdateDisplayInfoTexts();
                        SaveSettings();
                    };
                }

                if (MainResolutionComboBox != null)
                {
                    MainResolutionComboBox.SelectionChanged += (s, e) =>
                    {
                        if (_isLoadingSettings) return;
                        RefreshMainOptions(refreshResolutionList: false, refreshRateList: true);
                        UpdateDisplayInfoTexts();
                        SaveSettings();
                    };
                }

                if (SubResolutionComboBox != null)
                {
                    SubResolutionComboBox.SelectionChanged += (s, e) =>
                    {
                        if (_isLoadingSettings) return;
                        RefreshSubOptions(refreshResolutionList: false, refreshRateList: true);
                        UpdateDisplayInfoTexts();
                        SaveSettings();
                    };
                }

                if (MainRefreshRateComboBox != null)
                {
                    MainRefreshRateComboBox.SelectionChanged += (s, e) =>
                    {
                        if (_isLoadingSettings) return;
                        UpdateDisplayInfoTexts();
                        SaveSettings();
                    };
                }

                if (SubRefreshRateComboBox != null)
                {
                    SubRefreshRateComboBox.SelectionChanged += (s, e) =>
                    {
                        if (_isLoadingSettings) return;
                        UpdateDisplayInfoTexts();
                        SaveSettings();
                    };
                }

                if (DisplayConfigEnabledToggleSwitch != null)
                {
                    DisplayConfigEnabledToggleSwitch.IsCheckedChanged += (s, e) =>
                    {
                        if (_isLoadingSettings) return;
                        _displayConfigEnabled = DisplayConfigEnabledToggleSwitch.IsChecked == true;
                        _viewModel.Display.IsDisplayConfigurationEnabled = _displayConfigEnabled;
                        UpdateDisplayLayoutControlsEnabled();
                        SaveSettings();
                    };
                }

                if (DisplayModeComboBox != null)
                {
                    DisplayModeComboBox.SelectionChanged += (s, e) =>
                    {
                        if (_isLoadingSettings) return;
                        _isDualDisplay = DisplayModeComboBox.SelectedIndex != 0;
                        _viewModel.Display.IsDualDisplay = _isDualDisplay;
                        UpdateDisplayLayoutControlsEnabled();
                        UpdateDisplayInfoTexts();
                        SaveSettings();
                    };
                }

                StartDisplayPulseAnimation();
                _isDisplayLayoutInitialized = true;
            }

            ApplyDisplayViewModelStateToUi();
        }

        private void ApplyDisplayViewModelStateToUi()
        {
            bool previousLoadingState = _isLoadingSettings;
            _isLoadingSettings = true;

            try
            {
                _displayInfos.Clear();
                foreach (var display in _viewModel.Display.Displays)
                {
                    if (display?.Info != null)
                    {
                        _displayInfos.Add(display.Info);
                    }
                }

                ReplaceComboBoxItems(MainScreenComboBox, _viewModel.Display.Displays.Select(option => option.DisplayName));
                ReplaceComboBoxItems(SubScreenComboBox, _viewModel.Display.Displays.Select(option => option.DisplayName));
                ReplaceComboBoxItems(RotationComboBox, _viewModel.Display.Rotations.Select(option => option.DisplayName));
                ReplaceComboBoxItems(SubRotationComboBox, _viewModel.Display.Rotations.Select(option => option.DisplayName));
                ReplaceComboBoxItems(MainResolutionComboBox, _viewModel.Display.MainResolutions);
                ReplaceComboBoxItems(SubResolutionComboBox, _viewModel.Display.SubResolutions);
                ReplaceComboBoxItems(MainRefreshRateComboBox, _viewModel.Display.MainRefreshRates);
                ReplaceComboBoxItems(SubRefreshRateComboBox, _viewModel.Display.SubRefreshRates);

                _displayConfigEnabled = _viewModel.Display.IsDisplayConfigurationEnabled;
                _isDualDisplay = _viewModel.Display.IsDualDisplay;

                if (DisplayConfigEnabledToggleSwitch != null)
                {
                    DisplayConfigEnabledToggleSwitch.IsChecked = _displayConfigEnabled;
                }

                if (DisplayModeComboBox != null)
                {
                    DisplayModeComboBox.SelectedIndex = _isDualDisplay ? 1 : 0;
                }

                if (MainScreenComboBox != null)
                {
                    MainScreenComboBox.SelectedIndex = _viewModel.Display.Displays.Count == 0
                        ? -1
                        : Math.Max(0, _viewModel.Display.Displays.IndexOf(_viewModel.Display.SelectedMainDisplay));
                }

                if (SubScreenComboBox != null)
                {
                    SubScreenComboBox.SelectedIndex = _viewModel.Display.Displays.Count == 0
                        ? -1
                        : Math.Max(0, _viewModel.Display.Displays.IndexOf(_viewModel.Display.SelectedSubDisplay));
                }

                if (RotationComboBox != null)
                {
                    RotationComboBox.SelectedIndex = _viewModel.Display.Rotations.Count == 0
                        ? -1
                        : Math.Max(0, _viewModel.Display.Rotations.IndexOf(_viewModel.Display.SelectedMainRotation));
                }

                if (SubRotationComboBox != null)
                {
                    SubRotationComboBox.SelectedIndex = _viewModel.Display.Rotations.Count == 0
                        ? -1
                        : Math.Max(0, _viewModel.Display.Rotations.IndexOf(_viewModel.Display.SelectedSubRotation));
                }

                SelectComboBoxItem(MainResolutionComboBox, _viewModel.Display.SelectedMainResolution);
                SelectComboBoxItem(SubResolutionComboBox, _viewModel.Display.SelectedSubResolution);
                SelectComboBoxItem(MainRefreshRateComboBox, _viewModel.Display.SelectedMainRefreshRate);
                SelectComboBoxItem(SubRefreshRateComboBox, _viewModel.Display.SelectedSubRefreshRate);

                if (MainOutputInfoTextBlock != null)
                {
                    MainOutputInfoTextBlock.Text = _viewModel.Display.MainOutputInfo;
                }

                if (SubOutputInfoTextBlock != null)
                {
                    SubOutputInfoTextBlock.Text = _viewModel.Display.SubOutputInfo;
                }

                if (MainStartupInfoTextBlock != null)
                {
                    MainStartupInfoTextBlock.Text = _viewModel.Display.MainStartupInfo;
                }

                if (SubStartupInfoTextBlock != null)
                {
                    SubStartupInfoTextBlock.Text = _viewModel.Display.SubStartupInfo;
                }

                ToolTip.SetTip(MainResolutionComboBox, string.IsNullOrWhiteSpace(_viewModel.Display.MainDiagnosticsTooltip) ? null : _viewModel.Display.MainDiagnosticsTooltip);
                ToolTip.SetTip(MainRefreshRateComboBox, string.IsNullOrWhiteSpace(_viewModel.Display.MainDiagnosticsTooltip) ? null : _viewModel.Display.MainDiagnosticsTooltip);
                ToolTip.SetTip(SubResolutionComboBox, string.IsNullOrWhiteSpace(_viewModel.Display.SubDiagnosticsTooltip) ? null : _viewModel.Display.SubDiagnosticsTooltip);
                ToolTip.SetTip(SubRefreshRateComboBox, string.IsNullOrWhiteSpace(_viewModel.Display.SubDiagnosticsTooltip) ? null : _viewModel.Display.SubDiagnosticsTooltip);

                SelectDisplayTarget(MapDisplaySelectionTarget(_viewModel.Display.SelectedTarget));
                UpdateDisplayLayoutControlsEnabled();
            }
            finally
            {
                _isLoadingSettings = previousLoadingState;
            }
        }

        private static void ReplaceComboBoxItems(ComboBox comboBox, IEnumerable<string> items)
        {
            if (comboBox == null)
            {
                return;
            }

            comboBox.Items.Clear();
            foreach (var item in items ?? Enumerable.Empty<string>())
            {
                comboBox.Items.Add(item);
            }
        }

        private static void SelectComboBoxItem(ComboBox comboBox, string value)
        {
            if (comboBox == null || comboBox.Items.Count == 0)
            {
                return;
            }

            var selectedValue = value ?? string.Empty;
            if (comboBox.Items.Cast<object>().Any(item => string.Equals(item?.ToString(), selectedValue, StringComparison.OrdinalIgnoreCase)))
            {
                comboBox.SelectedItem = selectedValue;
                return;
            }

            comboBox.SelectedIndex = 0;
        }

        private static DisplaySelectionTarget MapDisplaySelectionTarget(global::LazyBootstrap.Models.DisplaySelectionTarget target)
        {
            return target switch
            {
                global::LazyBootstrap.Models.DisplaySelectionTarget.Main => DisplaySelectionTarget.Main,
                global::LazyBootstrap.Models.DisplaySelectionTarget.Sub => DisplaySelectionTarget.Sub,
                _ => DisplaySelectionTarget.None
            };
        }

        private static string BuildDisplayLabel(DisplayInfo display)
        {
            if (display == null)
            {
                return "未知显示器";
            }

            var deviceName = display.DeviceName ?? string.Empty;
            var displayId = deviceName.StartsWith(@"\.\", StringComparison.OrdinalIgnoreCase)
                ? deviceName.Substring(4)
                : deviceName;

            if (string.IsNullOrWhiteSpace(displayId))
            {
                return string.IsNullOrWhiteSpace(display.FriendlyName) ? "未知显示器" : display.FriendlyName;
            }

            if (string.IsNullOrWhiteSpace(display.FriendlyName))
            {
                return display.IsPrimary ? $"{displayId} - Primary" : displayId;
            }

            var label = $"{displayId} - {display.FriendlyName}";
            return display.IsPrimary ? $"{label} - Primary" : label;
        }

        private static void InitializeRotationCombo(ComboBox combo)
        {
            if (combo == null || combo.Items.Count > 0)
            {
                return;
            }

            combo.Items.Add("0");
            combo.Items.Add("90");
            combo.Items.Add("180");
            combo.Items.Add("270");
            combo.SelectedIndex = 0;
        }

        private void UpdateDisplayLayoutControlsEnabled()
        {
            bool enabled = _displayConfigEnabled;
            if (DisplayConfigDisabledMask != null)
            {
                DisplayConfigDisabledMask.IsBusy = !enabled;
            }
            if (MainScreenComboBox != null) MainScreenComboBox.IsEnabled = enabled;
            if (RotationComboBox != null) RotationComboBox.IsEnabled = enabled;
            if (MainResolutionComboBox != null) MainResolutionComboBox.IsEnabled = enabled;
            if (MainRefreshRateComboBox != null) MainRefreshRateComboBox.IsEnabled = enabled;
            if (PreviewDisplaySettingsButton != null) PreviewDisplaySettingsButton.IsEnabled = enabled;
            bool subEnabled = enabled && _isDualDisplay;
            if (SelectMainScreenAreaButton != null)
            {
                SelectMainScreenAreaButton.IsVisible = enabled;
                SelectMainScreenAreaButton.IsEnabled = enabled;
            }
            if (SelectSubScreenAreaButton != null)
            {
                SelectSubScreenAreaButton.IsVisible = enabled && _isDualDisplay;
                SelectSubScreenAreaButton.IsEnabled = subEnabled;
            }
            if (DotSubCore != null) DotSubCore.IsVisible = _isDualDisplay;
            if (DotSubGlow != null) DotSubGlow.IsVisible = _isDualDisplay;
            if (DotSubSelectedRing != null) DotSubSelectedRing.IsVisible = _isDualDisplay && _selectedDisplayTarget == DisplaySelectionTarget.Sub;
            if (SubScreenComboBox != null) SubScreenComboBox.IsEnabled = subEnabled;
            if (SubRotationComboBox != null) SubRotationComboBox.IsEnabled = subEnabled;
            if (SubResolutionComboBox != null) SubResolutionComboBox.IsEnabled = subEnabled;
            if (SubRefreshRateComboBox != null) SubRefreshRateComboBox.IsEnabled = subEnabled;

            if (!_isDualDisplay && _selectedDisplayTarget == DisplaySelectionTarget.Sub)
            {
                SelectDisplayTarget(DisplaySelectionTarget.None);
            }
        }

        private void RefreshMainOptions(bool refreshResolutionList = true, bool refreshRateList = true)
        {
            RefreshDisplayOptions(MainScreenComboBox, RotationComboBox, MainResolutionComboBox, MainRefreshRateComboBox, refreshResolutionList, refreshRateList);
        }

        private void RefreshSubOptions(bool refreshResolutionList = true, bool refreshRateList = true)
        {
            RefreshDisplayOptions(SubScreenComboBox, SubRotationComboBox, SubResolutionComboBox, SubRefreshRateComboBox, refreshResolutionList, refreshRateList);
        }

        private void RefreshDisplayOptions(ComboBox displayCombo, ComboBox rotationCombo, ComboBox resolutionCombo, ComboBox refreshCombo, bool refreshResolutionList, bool refreshRateList)
        {
            if (displayCombo == null || resolutionCombo == null || refreshCombo == null)
            {
                return;
            }

            var displayInfo = GetSelectedDisplayInfo(displayCombo);
            if (displayInfo == null)
            {
                if (refreshResolutionList)
                {
                    resolutionCombo.Items.Clear();
                }
                if (refreshRateList)
                {
                    refreshCombo.Items.Clear();
                }
                return;
            }

            var supportedModesResult = _displayConfigurationService.GetSupportedModes(displayInfo.DeviceName);
            var supportedModes = supportedModesResult.Modes;
            var diagnosticsTooltip = BuildDisplayModeDiagnosticsTooltip(supportedModesResult);
            int rotation = ParseRotationValue(rotationCombo);

            string previousResolution = resolutionCombo.SelectedItem?.ToString() ?? string.Empty;
            if (refreshResolutionList)
            {
                var resolutions = supportedModes
                    .Select(m => NormalizeResolutionByRotation(m.Width, m.Height, rotation))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                resolutionCombo.Items.Clear();
                foreach (var resolution in resolutions)
                {
                    resolutionCombo.Items.Add(resolution);
                }

                if (resolutions.Count > 0)
                {
                    if (!string.IsNullOrEmpty(previousResolution) && resolutions.Contains(previousResolution, StringComparer.OrdinalIgnoreCase))
                    {
                        resolutionCombo.SelectedItem = previousResolution;
                    }
                    else
                    {
                        resolutionCombo.SelectedIndex = 0;
                    }
                }
            }

            if (!refreshRateList)
            {
                ApplyDisplayModeDiagnostics(resolutionCombo, refreshCombo, diagnosticsTooltip);
                return;
            }

            string selectedResolution = resolutionCombo.SelectedItem?.ToString() ?? string.Empty;
            string previousRefresh = refreshCombo.SelectedItem?.ToString() ?? string.Empty;

            var rates = supportedModes
                .Where(m => string.Equals(NormalizeResolutionByRotation(m.Width, m.Height, rotation), selectedResolution, StringComparison.OrdinalIgnoreCase))
                .Select(m => m.RefreshRate)
                .Distinct()
                .OrderBy(v => v)
                .Select(v => v.ToString())
                .ToList();

            refreshCombo.Items.Clear();
            foreach (var rate in rates)
            {
                refreshCombo.Items.Add(rate);
            }

            if (rates.Count > 0)
            {
                if (!string.IsNullOrEmpty(previousRefresh) && rates.Contains(previousRefresh, StringComparer.OrdinalIgnoreCase))
                {
                    refreshCombo.SelectedItem = previousRefresh;
                }
                else
                {
                    refreshCombo.SelectedIndex = 0;
                }
            }

            ApplyDisplayModeDiagnostics(resolutionCombo, refreshCombo, diagnosticsTooltip);
        }

        private static string NormalizeResolutionByRotation(int width, int height, int rotation)
        {
            bool vertical = rotation == 90 || rotation == 270;
            int w = width;
            int h = height;

            if (vertical)
            {
                if (w > h)
                {
                    int temp = w;
                    w = h;
                    h = temp;
                }
                return $"{w}x{h}";
            }

            if (w < h)
            {
                int temp = w;
                w = h;
                h = temp;
            }
            return $"{w}x{h}";
        }

        private static void ApplyDisplayModeDiagnostics(ComboBox resolutionCombo, ComboBox refreshCombo, string tooltip)
        {
            var tooltipValue = string.IsNullOrWhiteSpace(tooltip) ? null : tooltip;
            ToolTip.SetTip(resolutionCombo, tooltipValue);
            ToolTip.SetTip(refreshCombo, tooltipValue);
        }

        private static string BuildDisplayModeDiagnosticsTooltip(DisplayModeQueryResult result)
        {
            if (result == null || result.Succeeded || string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                return string.Empty;
            }

            return result.Modes.Count > 0
                ? $"{result.ErrorMessage}{Environment.NewLine}已显示可读取到的显示模式，结果可能不完整。"
                : result.ErrorMessage;
        }

        private void StartDisplayPulseAnimation()
        {
            if (_displayPulseTimer != null)
            {
                return;
            }

            _displayPulseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _displayPulseTimer.Tick += (s, e) =>
            {
                _displayPulsePhase += 0.08;
                if (_displayPulsePhase > Math.PI * 2)
                {
                    _displayPulsePhase = 0;
                }

                double t = (Math.Sin(_displayPulsePhase) + 1d) / 2d;
                ApplyPulseVisual(DotMainGlow, 0.18, 0.58, 1.0, 1.45, t);
                ApplyPulseVisual(DotSubGlow, 0.18, 0.58, 1.0, 1.45, t);
            };
            _displayPulseTimer.Start();
        }

        private static void ApplyPulseVisual(Control control, double minOpacity, double maxOpacity, double minScale, double maxScale, double t)
        {
            if (control == null)
            {
                return;
            }

            control.Opacity = minOpacity + (maxOpacity - minOpacity) * t;
            double scale = minScale + (maxScale - minScale) * t;
            control.RenderTransformOrigin = Avalonia.RelativePoint.Center;
            control.RenderTransform = new ScaleTransform(scale, scale);
        }

        private async Task PreviewDisplaySettingsCoreAsync()
        {
            try
            {
                if (!_displayConfigEnabled)
                {
                    ShowWarningToast("显示器预览", "显示配置未启用，无法预览。");
                    return;
                }

                SaveSettings();
                bool applied = TryApplySelectedDisplayConfiguration(out var backupStates, out var applyMessages);
                if (!applied)
                {
                    if (backupStates.Count > 0)
                    {
                        int recoveredCount = RestoreDisplayStates(backupStates, out var remainingRestoreStates, out var restoreMessages);
                        ReplaceDisplayRestoreStates(remainingRestoreStates);
                        foreach (var restoreMessage in restoreMessages)
                        {
                            applyMessages.Add(restoreMessage);
                        }

                        if (recoveredCount > 0)
                        {
                            applyMessages.Add($"已额外恢复 {recoveredCount} 个显示器设置。");
                        }
                    }

                    ShowWarningToast("显示器预览", BuildDisplayDiagnosticsMessage(applyMessages));
                    UpdateDisplayInfoTexts();
                    return;
                }

                var result = await ShowPreviewDecisionDialogAsync();
                if (result == PreviewDecision.Restore)
                {
                    int restored = RestoreDisplayStates(backupStates, out var remainingRestoreStates, out var restoreMessages);
                    ReplaceDisplayRestoreStates(remainingRestoreStates);
                    if (restoreMessages.Count > 0)
                    {
                        ShowWarningToast("显示器还原", BuildDisplayDiagnosticsMessage(restoreMessages));
                    }
                    ShowInfoToast("显示器预览", restored > 0 ? $"已还原 {restored} 个显示器设置。" : "未还原任何显示器设置。");
                    UpdateDisplayInfoTexts();
                    return;
                }

                ShowInfoToast("显示器预览", "已保留当前预览设置。");
            }
            catch (Exception ex)
            {
                ShowErrorToast("显示器预览失败", ex.Message);
            }
        }

        private enum PreviewDecision
        {
            Keep,
            Restore
        }

        private Task<PreviewDecision> ShowPreviewDecisionDialogAsync()
        {
            return ShowPreviewDecisionMessageBoxAsync();
        }

        private async Task<PreviewDecision> ShowPreviewDecisionMessageBoxAsync()
        {
            var restoreButton = SukiMessageBoxButtonsFactory.CreateButton("还原", SukiMessageBoxResult.No, "Flat Accent");
            var keepButton = SukiMessageBoxButtonsFactory.CreateButton("保持现状", SukiMessageBoxResult.Yes, "Flat");

            var result = await SukiMessageBox.ShowDialog(new SukiMessageBoxHost
            {
                UseAlternativeHeaderStyle = true,
                IconPreset = SukiMessageBoxIcons.Question,
                Header = "显示器预览",
                Content = "已应用当前预览设置。\n\n如果选择还原，将恢复为预览前状态；如果保持现状，修改将继续生效并关闭对话框。",
                ActionButtonsSource = [restoreButton, keepButton]
            });

            if (result is SukiMessageBoxResult messageBoxResult)
            {
                return messageBoxResult switch
                {
                    SukiMessageBoxResult.No => PreviewDecision.Restore,
                    SukiMessageBoxResult.Yes => PreviewDecision.Keep,
                    _ => PreviewDecision.Keep
                };
            }

            return PreviewDecision.Keep;
        }

        private bool TryApplySelectedDisplayConfiguration(out Dictionary<string, DisplayState> restoreStates, out List<string> messages)
        {
            messages = new List<string>();
            restoreStates = new Dictionary<string, DisplayState>(StringComparer.OrdinalIgnoreCase);

            if (!_displayConfigEnabled)
            {
                return true;
            }

            if (!TryBuildDisplaySettingsRequests(out var requests, messages))
            {
                return false;
            }

            var transactionResult = _displaySettingsTransactionCoordinator.Apply(requests);
            restoreStates = new Dictionary<string, DisplayState>(transactionResult.RestoreStates, StringComparer.OrdinalIgnoreCase);
            messages.AddRange(transactionResult.Messages);
            return transactionResult.Succeeded;
        }

        private bool TryBuildDisplaySettingsRequests(out List<DisplaySettingsRequest> requests, List<string> messages)
        {
            requests = new List<DisplaySettingsRequest>();
            bool allValid = true;

            allValid &= TryBuildDisplaySettingsRequest(
                MainScreenComboBox,
                RotationComboBox,
                MainResolutionComboBox,
                MainRefreshRateComboBox,
                "主显示器",
                messages,
                requests);

            if (_isDualDisplay)
            {
                allValid &= TryBuildDisplaySettingsRequest(
                    SubScreenComboBox,
                    SubRotationComboBox,
                    SubResolutionComboBox,
                    SubRefreshRateComboBox,
                    "副显示器",
                    messages,
                    requests);
            }

            return allValid;
        }

        private bool TryBuildDisplaySettingsRequest(
            ComboBox screenCombo,
            ComboBox rotationCombo,
            ComboBox resolutionCombo,
            ComboBox refreshCombo,
            string targetName,
            List<string> messages,
            List<DisplaySettingsRequest> requests)
        {
            var info = GetSelectedDisplayInfo(screenCombo);
            if (info == null)
            {
                messages.Add($"{targetName}未选择有效的显示器。");
                return false;
            }

            int rotation = ParseRotationValue(rotationCombo);
            string resolution = resolutionCombo?.SelectedItem?.ToString() ?? string.Empty;
            string refreshText = refreshCombo?.SelectedItem?.ToString() ?? string.Empty;

            if (!TryParseResolution(resolution, out int width, out int height))
            {
                messages.Add($"{targetName}分辨率无效: {resolution}");
                return false;
            }

            if (!int.TryParse(refreshText, out int refreshRate))
            {
                messages.Add($"{targetName}刷新率无效: {refreshText}");
                return false;
            }

            requests.Add(new DisplaySettingsRequest(targetName, info.DeviceName, rotation, width, height, refreshRate));
            return true;
        }

        private void ReplaceDisplayRestoreStates(IReadOnlyDictionary<string, DisplayState> states)
        {
            _displayRestoreStates.Clear();
            if (states == null)
            {
                return;
            }

            foreach (var pair in states)
            {
                _displayRestoreStates[pair.Key] = pair.Value;
            }
        }

        private int RestoreDisplayStates(
            IReadOnlyDictionary<string, DisplayState> states,
            out Dictionary<string, DisplayState> remainingStates,
            out List<string> messages)
        {
            messages = new List<string>();
            remainingStates = new Dictionary<string, DisplayState>(StringComparer.OrdinalIgnoreCase);
            int restored = 0;
            if (states == null)
            {
                return restored;
            }

            foreach (var state in states.Values)
            {
                var restoreResult = _displayConfigurationService.RestoreDisplaySettings(state);
                if (restoreResult.Succeeded)
                {
                    restored++;
                    continue;
                }

                messages.Add($"还原 {state.DeviceName} 失败: {restoreResult.ErrorMessage}");
                remainingStates[state.DeviceName] = state;
            }

            return restored;
        }

        private static bool TryParseResolution(string resolution, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (string.IsNullOrWhiteSpace(resolution))
            {
                return false;
            }

            var parts = resolution.Split('x', 'X');
            return parts.Length == 2
                && int.TryParse(parts[0], out width)
                && int.TryParse(parts[1], out height);
        }

        private static int ParseRotationValue(ComboBox combo)
        {
            if (combo == null)
            {
                return 0;
            }

            var selected = combo.SelectedItem?.ToString();
            if (int.TryParse(selected, out var value))
            {
                return value;
            }
            return 0;
        }

        private DisplayInfo GetSelectedDisplayInfo(ComboBox combo)
        {
            if (combo == null)
            {
                return null;
            }

            int idx = combo.SelectedIndex;
            if (idx < 0 || idx >= _displayInfos.Count)
            {
                return null;
            }

            return _displayInfos[idx];
        }

        private void SelectDisplayTarget(DisplaySelectionTarget target)
        {
            if (target == DisplaySelectionTarget.Sub && !_isDualDisplay)
            {
                target = DisplaySelectionTarget.None;
            }

            _selectedDisplayTarget = target;

            if (PanelNoScreenSelected != null) PanelNoScreenSelected.IsVisible = target == DisplaySelectionTarget.None;
            if (PanelMainScreenConfig != null) PanelMainScreenConfig.IsVisible = target == DisplaySelectionTarget.Main;
            if (PanelSubScreenConfig != null) PanelSubScreenConfig.IsVisible = target == DisplaySelectionTarget.Sub;

            if (DotMainSelectedRing != null) DotMainSelectedRing.IsVisible = target == DisplaySelectionTarget.Main;
            if (DotSubSelectedRing != null) DotSubSelectedRing.IsVisible = _isDualDisplay && target == DisplaySelectionTarget.Sub;

            UpdateDisplayInfoTexts();
        }

        private void UpdateDisplayInfoTexts()
        {
            UpdateDisplayInfoText(MainScreenComboBox, RotationComboBox, MainResolutionComboBox, MainRefreshRateComboBox, MainOutputInfoTextBlock, MainStartupInfoTextBlock);
            UpdateDisplayInfoText(SubScreenComboBox, SubRotationComboBox, SubResolutionComboBox, SubRefreshRateComboBox, SubOutputInfoTextBlock, SubStartupInfoTextBlock);
            _viewModel.Display.MainDisplayInfo = MainStartupInfoTextBlock?.Text ?? string.Empty;
            _viewModel.Display.SubDisplayInfo = SubStartupInfoTextBlock?.Text ?? string.Empty;
        }

        private void UpdateDisplayInfoText(ComboBox displayCombo, ComboBox rotationCombo, ComboBox resolutionCombo, ComboBox refreshCombo, TextBlock outputText, TextBlock startupText)
        {
            if (outputText == null || startupText == null)
            {
                return;
            }

            var info = GetSelectedDisplayInfo(displayCombo);
            if (info == null)
            {
                outputText.Text = "未知";
                startupText.Text = "未设置";
                return;
            }

            var currentStateResult = _displayConfigurationService.GetCurrentState(info.DeviceName);
            if (currentStateResult.Succeeded)
            {
                int currentAngle = _displayConfigurationService.OrientationToAngle(currentStateResult.State!.Orientation);
                outputText.Text = $"设备: {info.FriendlyName} ({info.DeviceName})\n当前: {currentStateResult.State.Width}x{currentStateResult.State.Height} @ {currentStateResult.State.RefreshRate}Hz, 旋转 {currentAngle}°";
            }
            else
            {
                outputText.Text = $"设备: {info.FriendlyName} ({info.DeviceName})\n当前: 读取失败\n原因: {currentStateResult.ErrorMessage}";
            }

            int startupRotation = ParseRotationValue(rotationCombo);
            string startupResolution = resolutionCombo?.SelectedItem?.ToString() ?? "未设置";
            string startupRefresh = refreshCombo?.SelectedItem?.ToString() ?? "未设置";
            startupText.Text = $"旋转: {startupRotation}°\n分辨率: {startupResolution}\n刷新率: {startupRefresh}Hz";
        }

        private void SelectMainScreenAreaCore()
        {
            SelectDisplayTarget(DisplaySelectionTarget.Main);
        }

        private void SelectSubScreenAreaCore()
        {
            SelectDisplayTarget(DisplaySelectionTarget.Sub);
        }

        private static string BuildDisplayDiagnosticsMessage(IReadOnlyList<string> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                return "未知错误。";
            }

            const int maxMessageCount = 3;
            var visibleMessages = messages
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Take(maxMessageCount)
                .ToList();

            if (visibleMessages.Count == 0)
            {
                return "未知错误。";
            }

            if (messages.Count > maxMessageCount)
            {
                visibleMessages.Add($"其余 {messages.Count - maxMessageCount} 项请查看当前设置。");
            }

            return string.Join(Environment.NewLine, visibleMessages);
        }
    }
}

