using System;
using System.Collections.Specialized;
using System.ComponentModel;
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
            if (_viewModel?.Display == null || _viewModel.Display.IsSuspended)
            {
                return;
            }

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
                    case nameof(DisplayConfigurationPageViewModel.ShowNoScreenSelected):
                    case nameof(DisplayConfigurationPageViewModel.ShowMainScreenConfig):
                    case nameof(DisplayConfigurationPageViewModel.ShowSubScreenConfig):
                    case nameof(DisplayConfigurationPageViewModel.IsDisplayConfigurationEnabled):
                    case nameof(DisplayConfigurationPageViewModel.IsDualDisplay):
                    case nameof(DisplayConfigurationPageViewModel.ExitRestore):
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
            if (_viewModel?.Display == null || _viewModel.Display.IsSuspended)
            {
                return;
            }

            _ = Dispatcher.UIThread.InvokeAsync(ApplyDisplayViewModelStateToUi);
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
                        await _viewModel.Display.HandleConfigurationChangedAsync(refreshMainOptions: true, refreshSubOptions: false);
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
                        await _viewModel.Display.HandleConfigurationChangedAsync(refreshMainOptions: false, refreshSubOptions: true);
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
                        await _viewModel.Display.HandleConfigurationChangedAsync(refreshMainOptions: true, refreshSubOptions: false);
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
                        await _viewModel.Display.HandleConfigurationChangedAsync(refreshMainOptions: false, refreshSubOptions: true);
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
                        await _viewModel.Display.HandleConfigurationChangedAsync(refreshMainOptions: true, refreshSubOptions: false);
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
                        await _viewModel.Display.HandleConfigurationChangedAsync(refreshMainOptions: false, refreshSubOptions: true);
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
                        await _viewModel.Display.HandleConfigurationChangedAsync(refreshMainOptions: false, refreshSubOptions: false);
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
                        await _viewModel.Display.HandleConfigurationChangedAsync(refreshMainOptions: false, refreshSubOptions: false);
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

                        _viewModel.Display.IsDisplayConfigurationEnabled = DisplayConfigEnabledToggleSwitch.IsChecked == true;
                        await _viewModel.Display.PersistGeneralSettingsAsync();
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
                        _viewModel.Display.IsDualDisplay = isDualDisplay;

                        if (!isDualDisplay && _viewModel.Display.SelectedTarget == global::LazyBootstrap.Models.DisplaySelectionTarget.Sub)
                        {
                            _viewModel.Display.SelectedTarget = global::LazyBootstrap.Models.DisplaySelectionTarget.None;
                            _viewModel.Display.ShowNoScreenSelected = true;
                            _viewModel.Display.ShowMainScreenConfig = false;
                            _viewModel.Display.ShowSubScreenConfig = false;
                        }

                        await _viewModel.Display.PersistGeneralSettingsAsync();
                    };
                }

                StartDisplayPulseAnimation();
                _isDisplayLayoutInitialized = true;
            }

            ApplyDisplayViewModelStateToUi();
        }

        private bool ShouldSkipDisplayLayoutInteraction()
        {
            return _isLoadingSettings || _viewModel?.Display == null;
        }

        private void SyncMainSelectionFromUi()
        {
            if (_viewModel?.Display == null)
            {
                return;
            }

            _viewModel.Display.SelectedMainDisplay = GetSelectedDisplayOption(MainScreenComboBox);
            _viewModel.Display.SelectedMainRotation = GetSelectedRotationOption(RotationComboBox);
            _viewModel.Display.SelectedMainResolution = GetSelectedComboBoxText(MainResolutionComboBox);
            _viewModel.Display.SelectedMainRefreshRate = GetSelectedComboBoxText(MainRefreshRateComboBox);
        }

        private void SyncSubSelectionFromUi()
        {
            if (_viewModel?.Display == null)
            {
                return;
            }

            _viewModel.Display.SelectedSubDisplay = GetSelectedDisplayOption(SubScreenComboBox);
            _viewModel.Display.SelectedSubRotation = GetSelectedRotationOption(SubRotationComboBox);
            _viewModel.Display.SelectedSubResolution = GetSelectedComboBoxText(SubResolutionComboBox);
            _viewModel.Display.SelectedSubRefreshRate = GetSelectedComboBoxText(SubRefreshRateComboBox);
        }

        private DisplayChoiceOption GetSelectedDisplayOption(ComboBox comboBox)
        {
            if (_viewModel?.Display == null || comboBox == null)
            {
                return null;
            }

            int selectedIndex = comboBox.SelectedIndex;
            return selectedIndex >= 0 && selectedIndex < _viewModel.Display.Displays.Count
                ? _viewModel.Display.Displays[selectedIndex]
                : null;
        }

        private RotationOption GetSelectedRotationOption(ComboBox comboBox)
        {
            if (_viewModel?.Display == null || comboBox == null)
            {
                return null;
            }

            int selectedIndex = comboBox.SelectedIndex;
            return selectedIndex >= 0 && selectedIndex < _viewModel.Display.Rotations.Count
                ? _viewModel.Display.Rotations[selectedIndex]
                : null;
        }

        private static string GetSelectedComboBoxText(ComboBox comboBox)
        {
            return comboBox?.SelectedItem?.ToString() ?? string.Empty;
        }

        private void ApplyDisplayViewModelStateToUi()
        {
            if (_viewModel?.Display == null)
            {
                return;
            }

            bool previousLoadingState = _isLoadingSettings;
            _isLoadingSettings = true;

            try
            {
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

                if (ExitRestoreToggleSwitch != null)
                {
                    ExitRestoreToggleSwitch.IsChecked = _viewModel.Display.ExitRestore;
                }

                SelectComboBoxIndex(MainScreenComboBox, GetOptionIndex(_viewModel.Display.Displays, _viewModel.Display.SelectedMainDisplay));
                SelectComboBoxIndex(SubScreenComboBox, GetOptionIndex(_viewModel.Display.Displays, _viewModel.Display.SelectedSubDisplay));
                SelectComboBoxIndex(RotationComboBox, GetOptionIndex(_viewModel.Display.Rotations, _viewModel.Display.SelectedMainRotation));
                SelectComboBoxIndex(SubRotationComboBox, GetOptionIndex(_viewModel.Display.Rotations, _viewModel.Display.SelectedSubRotation));

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

        private static DisplaySelectionTarget MapDisplaySelectionTarget(global::LazyBootstrap.Models.DisplaySelectionTarget target)
        {
            return target switch
            {
                global::LazyBootstrap.Models.DisplaySelectionTarget.Main => DisplaySelectionTarget.Main,
                global::LazyBootstrap.Models.DisplaySelectionTarget.Sub => DisplaySelectionTarget.Sub,
                _ => DisplaySelectionTarget.None
            };
        }

        private void UpdateDisplayLayoutControlsEnabled()
        {
            bool enabled = _displayConfigEnabled;
            bool subEnabled = enabled && _isDualDisplay;

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
            if (target == DisplaySelectionTarget.Sub && !_isDualDisplay)
            {
                target = DisplaySelectionTarget.None;
            }

            _selectedDisplayTarget = target;

            if (PanelNoScreenSelected != null) PanelNoScreenSelected.IsVisible = target == DisplaySelectionTarget.None;
            if (PanelMainScreenConfig != null) PanelMainScreenConfig.IsVisible = target == DisplaySelectionTarget.Main;
            if (PanelSubScreenConfig != null) PanelSubScreenConfig.IsVisible = _isDualDisplay && target == DisplaySelectionTarget.Sub;

            if (DotMainSelectedRing != null) DotMainSelectedRing.IsVisible = target == DisplaySelectionTarget.Main;
            if (DotSubSelectedRing != null) DotSubSelectedRing.IsVisible = _isDualDisplay && target == DisplaySelectionTarget.Sub;
        }
    }
}
