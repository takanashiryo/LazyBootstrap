using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.MessageBox;

namespace LazyBootstrap
{
    public partial class MainWindow
    {
        private async void OnPreviewDisplaySettingsClick(object sender, RoutedEventArgs e)
        {
            await PreviewDisplaySettingsCoreAsync();
        }

        private void OnSelectMainScreenAreaClick(object sender, RoutedEventArgs e)
        {
            SelectMainScreenAreaCore();
        }

        private void OnSelectSubScreenAreaClick(object sender, RoutedEventArgs e)
        {
            SelectSubScreenAreaCore();
        }

        private void OnPortableModeToggleChanged(object sender, RoutedEventArgs e)
        {
            TogglePortableModeCore();
        }

        private void InitializeDisplayLayoutControls()
        {
            _displayInfos.Clear();
            _displayInfos.AddRange(DisplayConfigure.GetDisplays());

            if (MainScreenComboBox != null && MainScreenComboBox.Items.Count == 0)
            {
                if (_displayInfos.Count > 0)
                {
                    foreach (var display in _displayInfos)
                    {
                        MainScreenComboBox.Items.Add(display.FriendlyName);
                        if (SubScreenComboBox != null) SubScreenComboBox.Items.Add(display.FriendlyName);
                    }
                }
                else
                {
                    MainScreenComboBox.Items.Add("无显示器");
                    if (SubScreenComboBox != null) SubScreenComboBox.Items.Add("无显示器");
                }

                MainScreenComboBox.SelectedIndex = 0;
                if (SubScreenComboBox != null && SubScreenComboBox.Items.Count > 0)
                    SubScreenComboBox.SelectedIndex = Math.Min(1, SubScreenComboBox.Items.Count - 1);
            }

            InitializeRotationCombo(RotationComboBox);
            InitializeRotationCombo(SubRotationComboBox);

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
                    UpdateDisplayLayoutControlsEnabled();
                    UpdateDisplayInfoTexts();
                    SaveSettings();
                };
            }

            RefreshMainOptions();
            RefreshSubOptions();
            SelectDisplayTarget(DisplaySelectionTarget.None);
            UpdateDisplayLayoutControlsEnabled();
            StartDisplayPulseAnimation();
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

            var supportedModes = DisplayConfigure.GetSupportedModes(displayInfo.DeviceName);
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
                var backupStates = CaptureCurrentSelectedDisplayStates();

                bool applied = ApplyDisplaySettingsForLaunch();
                if (!applied)
                {
                    ShowWarningToast("显示器预览", "预览应用存在失败项，请检查当前显示配置。");
                }

                var result = await ShowPreviewDecisionDialogAsync();
                if (result == PreviewDecision.Restore)
                {
                    int restored = RestoreDisplayStates(backupStates);
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

        private Dictionary<string, DisplayConfigure.DisplayState> CaptureCurrentSelectedDisplayStates()
        {
            var result = new Dictionary<string, DisplayConfigure.DisplayState>(StringComparer.OrdinalIgnoreCase);

            void Capture(ComboBox combo)
            {
                var info = GetSelectedDisplayInfo(combo);
                if (info == null || result.ContainsKey(info.DeviceName))
                {
                    return;
                }

                if (DisplayConfigure.TryGetCurrentState(info.DeviceName, out var state))
                {
                    result[info.DeviceName] = state;
                }
            }

            Capture(MainScreenComboBox);
            if (_isDualDisplay)
            {
                Capture(SubScreenComboBox);
            }

            return result;
        }

        private static int RestoreDisplayStates(Dictionary<string, DisplayConfigure.DisplayState> states)
        {
            int restored = 0;
            if (states == null)
            {
                return restored;
            }

            foreach (var state in states.Values)
            {
                if (DisplayConfigure.RestoreDisplaySettings(state))
                {
                    restored++;
                }
            }

            return restored;
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

        private DisplayConfigure.DisplayInfo GetSelectedDisplayInfo(ComboBox combo)
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

            if (DisplayConfigure.TryGetCurrentState(info.DeviceName, out var current))
            {
                int currentAngle = DisplayConfigure.OrientationToAngle(current.Orientation);
                outputText.Text = $"设备: {info.FriendlyName} ({info.DeviceName})\n当前: {current.Width}x{current.Height} @ {current.RefreshRate}Hz, 旋转 {currentAngle}°";
            }
            else
            {
                outputText.Text = $"设备: {info.FriendlyName} ({info.DeviceName})\n当前: 未知";
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

        private string ResolveMachineProperty()
        {
            var identPath = Path.Combine(_contentsDir, "prop", "ea3-ident.xml");
            var result = TryReadMachinePropertyFromEa3(identPath);
            if (!string.IsNullOrWhiteSpace(result))
            {
                return result;
            }

            var configPath = Path.Combine(_contentsDir, "prop", "ea3-config.xml");
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
                var bootstrapPath = Path.Combine(_contentsDir, "prop", "bootstrap.xml");
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
                var launcherExe = Path.Combine(_baseDir, "launcher", "LazyBootstrap.exe");
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

        private void TogglePortableModeCore()
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
                    .WithActionButton("确定", _ =>
                    {
                        // User confirmed — apply portable mode
                        ApplyPortableMode(true);
                    }, true, "Flat")
                    .WithActionButton("取消", _ => { }, true, "Basic")
                    .Dismiss().ByClickingBackground();
                ApplyDialogNotificationIcon(dialogBuilder, NotificationType.Warning);
                dialogBuilder.TryShow();
                return;
            }

            // Disabling portable mode — apply immediately
            ApplyPortableMode(false);
        }

        private void ApplyPortableMode(bool enabled)
        {
            _portableMode = enabled;
            ApplyPortableModeToggleState(enabled);

            var activeXmlPath = GetSpiceXmlPath();
            if (!File.Exists(activeXmlPath))
            {
                ShowErrorToast("切换失败", "未找到 spicetools.xml，已自动关闭该选项。");

                _portableMode = false;
                ApplyPortableModeToggleState(false);
                RefreshSettingsPanelAfterPortableModeSwitch();

                SaveSettings();
                return;
            }

            SaveSettings();
            ShowInfoToast("便携模式切换", _portableMode
                ? $"已切换至便携模式，XML: {activeXmlPath}"
                : $"已切换至系统模式，XML: {activeXmlPath}");

            RefreshSettingsPanelAfterPortableModeSwitch();
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

        private void ApplyPortableModeToggleState(bool enabled)
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
    }
}
