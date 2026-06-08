using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private void OnSelectMainDisplayClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _displayState.SelectMainDisplay();
            ApplyDisplayStateToUi();
        }

        private void OnSelectSubDisplayClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _displayState.SelectSubDisplay();
            ApplyDisplayStateToUi();
        }

        private async void OnOpenTouchPanelClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await _displayWorkflowService.OpenTouchPanelAsync();
        }

        private async void OnPreviewDisplaySettingsClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await _displayWorkflowService.PreviewDisplaySettingsAsync(_displayState);
            ApplyDisplayStateToUi();
        }

        private async Task HandleDisplayConfigurationChangedAsync(bool refreshMainOptions, bool refreshSubOptions)
        {
            await _displayWorkflowService.HandleConfigurationChangedAsync(_displayState, refreshMainOptions, refreshSubOptions);
            ApplyDisplayStateToUi();
        }

        private void InitializeDisplayLayoutControls()
        {
            if (!_isDisplayLayoutInitialized)
            {
                if (MainScreenComboBox != null)
                {
                    MainScreenComboBox.SelectionChanged += async (s, e) =>
                    {
                        if (ShouldSkipDisplayLayoutInteraction())
                        {
                            return;
                        }

                        SyncMainSelectionFromUi();
                        _displayState.SelectedMainResolution = string.Empty;
                        _displayState.SelectedMainRefreshRate = string.Empty;
                        await HandleDisplayConfigurationChangedAsync(refreshMainOptions: true, refreshSubOptions: false);
                    };
                }

                if (SubScreenComboBox != null)
                {
                    SubScreenComboBox.SelectionChanged += async (s, e) =>
                    {
                        if (ShouldSkipDisplayLayoutInteraction())
                        {
                            return;
                        }

                        SyncSubSelectionFromUi();
                        _displayState.SelectedSubResolution = string.Empty;
                        _displayState.SelectedSubRefreshRate = string.Empty;
                        await HandleDisplayConfigurationChangedAsync(refreshMainOptions: false, refreshSubOptions: true);
                    };
                }

                if (RotationComboBox != null)
                {
                    RotationComboBox.SelectionChanged += async (s, e) =>
                    {
                        if (ShouldSkipDisplayLayoutInteraction())
                        {
                            return;
                        }

                        SyncMainSelectionFromUi();
                        await HandleDisplayConfigurationChangedAsync(refreshMainOptions: true, refreshSubOptions: false);
                    };
                }

                if (SubRotationComboBox != null)
                {
                    SubRotationComboBox.SelectionChanged += async (s, e) =>
                    {
                        if (ShouldSkipDisplayLayoutInteraction())
                        {
                            return;
                        }

                        SyncSubSelectionFromUi();
                        await HandleDisplayConfigurationChangedAsync(refreshMainOptions: false, refreshSubOptions: true);
                    };
                }

                if (MainResolutionComboBox != null)
                {
                    MainResolutionComboBox.SelectionChanged += async (s, e) =>
                    {
                        if (ShouldSkipDisplayLayoutInteraction())
                        {
                            return;
                        }

                        SyncMainSelectionFromUi();
                        await HandleDisplayConfigurationChangedAsync(refreshMainOptions: true, refreshSubOptions: false);
                    };
                }

                if (SubResolutionComboBox != null)
                {
                    SubResolutionComboBox.SelectionChanged += async (s, e) =>
                    {
                        if (ShouldSkipDisplayLayoutInteraction())
                        {
                            return;
                        }

                        SyncSubSelectionFromUi();
                        await HandleDisplayConfigurationChangedAsync(refreshMainOptions: false, refreshSubOptions: true);
                    };
                }

                if (MainRefreshRateComboBox != null)
                {
                    MainRefreshRateComboBox.SelectionChanged += async (s, e) =>
                    {
                        if (ShouldSkipDisplayLayoutInteraction())
                        {
                            return;
                        }

                        SyncMainSelectionFromUi();
                        await HandleDisplayConfigurationChangedAsync(refreshMainOptions: false, refreshSubOptions: false);
                    };
                }

                if (SubRefreshRateComboBox != null)
                {
                    SubRefreshRateComboBox.SelectionChanged += async (s, e) =>
                    {
                        if (ShouldSkipDisplayLayoutInteraction())
                        {
                            return;
                        }

                        SyncSubSelectionFromUi();
                        await HandleDisplayConfigurationChangedAsync(refreshMainOptions: false, refreshSubOptions: false);
                    };
                }

                if (DisplayConfigEnabledToggleSwitch != null)
                {
                    DisplayConfigEnabledToggleSwitch.IsCheckedChanged += async (s, e) =>
                    {
                        if (ShouldSkipDisplayLayoutInteraction())
                        {
                            return;
                        }

                        _displayState.IsDisplayConfigurationEnabled = DisplayConfigEnabledToggleSwitch.IsChecked == true;
                        await _displayWorkflowService.PersistGeneralSettingsAsync(_displayState);
                        ApplyDisplayStateToUi();
                    };
                }

                if (DisplayModeComboBox != null)
                {
                    DisplayModeComboBox.SelectionChanged += async (s, e) =>
                    {
                        if (ShouldSkipDisplayLayoutInteraction())
                        {
                            return;
                        }

                        bool isDualDisplay = DisplayModeComboBox.SelectedIndex == 1;
                        _displayState.IsDualDisplay = isDualDisplay;

                        if (!isDualDisplay && _displayState.SelectedTarget == global::LazyBootstrap.Models.DisplaySelectionTarget.Sub)
                        {
                            _displayState.SelectedTarget = global::LazyBootstrap.Models.DisplaySelectionTarget.None;
                            _displayState.ShowNoScreenSelected = true;
                            _displayState.ShowMainScreenConfig = false;
                            _displayState.ShowSubScreenConfig = false;
                        }

                        await _displayWorkflowService.PersistGeneralSettingsAsync(_displayState);
                        ApplyDisplayStateToUi();
                    };
                }

                StartDisplayPulseAnimation();
                _isDisplayLayoutInitialized = true;
            }

            ApplyDisplayStateToUi();
        }

        private bool ShouldSkipDisplayLayoutInteraction()
        {
            return _isLoadingSettings;
        }

        private void SyncMainSelectionFromUi()
        {
            _displayState.SelectedMainDisplay = GetSelectedDisplayOption(MainScreenComboBox);
            _displayState.SelectedMainRotation = GetSelectedRotationOption(RotationComboBox);
            _displayState.SelectedMainResolution = GetSelectedComboBoxText(MainResolutionComboBox);
            _displayState.SelectedMainRefreshRate = GetSelectedComboBoxText(MainRefreshRateComboBox);
        }

        private void SyncSubSelectionFromUi()
        {
            _displayState.SelectedSubDisplay = GetSelectedDisplayOption(SubScreenComboBox);
            _displayState.SelectedSubRotation = GetSelectedRotationOption(SubRotationComboBox);
            _displayState.SelectedSubResolution = GetSelectedComboBoxText(SubResolutionComboBox);
            _displayState.SelectedSubRefreshRate = GetSelectedComboBoxText(SubRefreshRateComboBox);
        }

        private DisplayChoiceOption GetSelectedDisplayOption(ComboBox comboBox)
        {
            if (comboBox == null)
            {
                return null;
            }

            int selectedIndex = comboBox.SelectedIndex;
            return selectedIndex >= 0 && selectedIndex < _displayState.Displays.Count
                ? _displayState.Displays[selectedIndex]
                : null;
        }

        private RotationOption GetSelectedRotationOption(ComboBox comboBox)
        {
            if (comboBox == null)
            {
                return null;
            }

            int selectedIndex = comboBox.SelectedIndex;
            return selectedIndex >= 0 && selectedIndex < _displayState.Rotations.Count
                ? _displayState.Rotations[selectedIndex]
                : null;
        }

        private static string GetSelectedComboBoxText(ComboBox comboBox)
        {
            return comboBox?.SelectedItem?.ToString() ?? string.Empty;
        }

        private void ApplyDisplayStateToUi()
        {
            bool previousLoadingState = _isLoadingSettings;
            _isLoadingSettings = true;

            try
            {
                ReplaceComboBoxItems(MainScreenComboBox, _displayState.Displays.Select(option => option.DisplayName));
                ReplaceComboBoxItems(SubScreenComboBox, _displayState.Displays.Select(option => option.DisplayName));
                ReplaceComboBoxItems(RotationComboBox, _displayState.Rotations.Select(option => option.DisplayName));
                ReplaceComboBoxItems(SubRotationComboBox, _displayState.Rotations.Select(option => option.DisplayName));
                ReplaceComboBoxItems(MainResolutionComboBox, _displayState.MainResolutions);
                ReplaceComboBoxItems(SubResolutionComboBox, _displayState.SubResolutions);
                ReplaceComboBoxItems(MainRefreshRateComboBox, _displayState.MainRefreshRates);
                ReplaceComboBoxItems(SubRefreshRateComboBox, _displayState.SubRefreshRates);

                if (DisplayConfigEnabledToggleSwitch != null)
                {
                    DisplayConfigEnabledToggleSwitch.IsChecked = _displayState.IsDisplayConfigurationEnabled;
                }

                if (DisplayModeComboBox != null)
                {
                    DisplayModeComboBox.SelectedIndex = _displayState.IsDualDisplay ? 1 : 0;
                }

                if (ExitRestoreToggleSwitch != null)
                {
                    ExitRestoreToggleSwitch.IsChecked = _displayState.ExitRestore;
                }

                SelectComboBoxIndex(MainScreenComboBox, GetOptionIndex(_displayState.Displays, _displayState.SelectedMainDisplay));
                SelectComboBoxIndex(SubScreenComboBox, GetOptionIndex(_displayState.Displays, _displayState.SelectedSubDisplay));
                SelectComboBoxIndex(RotationComboBox, GetOptionIndex(_displayState.Rotations, _displayState.SelectedMainRotation));
                SelectComboBoxIndex(SubRotationComboBox, GetOptionIndex(_displayState.Rotations, _displayState.SelectedSubRotation));

                SelectComboBoxItem(MainResolutionComboBox, _displayState.SelectedMainResolution);
                SelectComboBoxItem(SubResolutionComboBox, _displayState.SelectedSubResolution);
                SelectComboBoxItem(MainRefreshRateComboBox, _displayState.SelectedMainRefreshRate);
                SelectComboBoxItem(SubRefreshRateComboBox, _displayState.SelectedSubRefreshRate);

                if (MainOutputInfoTextBlock != null)
                {
                    MainOutputInfoTextBlock.Text = _displayState.MainOutputInfo;
                }

                if (SubOutputInfoTextBlock != null)
                {
                    SubOutputInfoTextBlock.Text = _displayState.SubOutputInfo;
                }

                if (MainStartupInfoTextBlock != null)
                {
                    MainStartupInfoTextBlock.Text = _displayState.MainStartupInfo;
                }

                if (SubStartupInfoTextBlock != null)
                {
                    SubStartupInfoTextBlock.Text = _displayState.SubStartupInfo;
                }

                ToolTip.SetTip(MainResolutionComboBox, string.IsNullOrWhiteSpace(_displayState.MainDiagnosticsTooltip) ? null : _displayState.MainDiagnosticsTooltip);
                ToolTip.SetTip(MainRefreshRateComboBox, string.IsNullOrWhiteSpace(_displayState.MainDiagnosticsTooltip) ? null : _displayState.MainDiagnosticsTooltip);
                ToolTip.SetTip(SubResolutionComboBox, string.IsNullOrWhiteSpace(_displayState.SubDiagnosticsTooltip) ? null : _displayState.SubDiagnosticsTooltip);
                ToolTip.SetTip(SubRefreshRateComboBox, string.IsNullOrWhiteSpace(_displayState.SubDiagnosticsTooltip) ? null : _displayState.SubDiagnosticsTooltip);

                SelectDisplayTarget(_displayState.SelectedTarget);
                UpdateDisplayLayoutControlsEnabled();
            }
            finally
            {
                _isLoadingSettings = previousLoadingState;
            }
        }

        private static void ReplaceComboBoxItems(ComboBox comboBox, System.Collections.Generic.IEnumerable<string> items)
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

        private static void SelectComboBoxIndex(ComboBox comboBox, int index)
        {
            if (comboBox == null)
            {
                return;
            }

            comboBox.SelectedIndex = comboBox.Items.Count == 0
                ? -1
                : Math.Clamp(index, -1, comboBox.Items.Count - 1);
        }

        private static void SelectComboBoxItem(ComboBox comboBox, string value)
        {
            if (comboBox == null)
            {
                return;
            }

            if (comboBox.Items.Count == 0)
            {
                comboBox.SelectedIndex = -1;
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

        private static int GetOptionIndex<T>(System.Collections.ObjectModel.ObservableCollection<T> options, T selected)
        {
            if (options == null || options.Count == 0 || selected == null)
            {
                return -1;
            }

            return options.IndexOf(selected);
        }

        private void UpdateDisplayLayoutControlsEnabled()
        {
            bool enabled = _displayState.IsDisplayConfigurationEnabled;
            bool isDualDisplay = _displayState.IsDualDisplay;
            bool subEnabled = enabled && isDualDisplay;
            var selectedTarget = _displayState.SelectedTarget;

            if (DisplayConfigDisabledMask != null)
            {
                DisplayConfigDisabledMask.IsBusy = !enabled;
            }

            if (MainScreenComboBox != null) MainScreenComboBox.IsEnabled = enabled;
            if (RotationComboBox != null) RotationComboBox.IsEnabled = enabled;
            if (MainResolutionComboBox != null) MainResolutionComboBox.IsEnabled = enabled;
            if (MainRefreshRateComboBox != null) MainRefreshRateComboBox.IsEnabled = enabled;
            if (PreviewDisplaySettingsButton != null) PreviewDisplaySettingsButton.IsEnabled = enabled;

            if (SelectMainScreenAreaButton != null)
            {
                SelectMainScreenAreaButton.IsVisible = enabled;
                SelectMainScreenAreaButton.IsEnabled = enabled;
            }

            if (SelectSubScreenAreaButton != null)
            {
                SelectSubScreenAreaButton.IsVisible = enabled && isDualDisplay;
                SelectSubScreenAreaButton.IsEnabled = subEnabled;
            }

            if (DotSubCore != null) DotSubCore.IsVisible = isDualDisplay;
            if (DotSubGlow != null) DotSubGlow.IsVisible = isDualDisplay;
            if (DotSubSelectedRing != null) DotSubSelectedRing.IsVisible = isDualDisplay && selectedTarget == DisplaySelectionTarget.Sub;

            if (SubScreenComboBox != null) SubScreenComboBox.IsEnabled = subEnabled;
            if (SubRotationComboBox != null) SubRotationComboBox.IsEnabled = subEnabled;
            if (SubResolutionComboBox != null) SubResolutionComboBox.IsEnabled = subEnabled;
            if (SubRefreshRateComboBox != null) SubRefreshRateComboBox.IsEnabled = subEnabled;

            if (!isDualDisplay && selectedTarget == DisplaySelectionTarget.Sub)
            {
                SelectDisplayTarget(DisplaySelectionTarget.None);
            }
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

        private void SelectDisplayTarget(DisplaySelectionTarget target)
        {
            bool isDualDisplay = _displayState.IsDualDisplay;
            if (target == DisplaySelectionTarget.Sub && !isDualDisplay)
            {
                target = DisplaySelectionTarget.None;
            }

            if (PanelNoScreenSelected != null) PanelNoScreenSelected.IsVisible = target == DisplaySelectionTarget.None;
            if (PanelMainScreenConfig != null) PanelMainScreenConfig.IsVisible = target == DisplaySelectionTarget.Main;
            if (PanelSubScreenConfig != null) PanelSubScreenConfig.IsVisible = isDualDisplay && target == DisplaySelectionTarget.Sub;

            if (DotMainSelectedRing != null) DotMainSelectedRing.IsVisible = target == DisplaySelectionTarget.Main;
            if (DotSubSelectedRing != null) DotSubSelectedRing.IsVisible = isDualDisplay && target == DisplaySelectionTarget.Sub;
        }
    }
}
