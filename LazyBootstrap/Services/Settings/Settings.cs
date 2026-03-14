using System;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using SukiUI.Dialogs;

namespace LazyBootstrap
{
    public partial class MainWindow
    {
        private void LoadSettings()
        {
            try
            {
                _isLoadingSettings = true;
                string portableModeStr = _configFile.ReadString(SettingSectionName, "portablemode", "false");
                if (!bool.TryParse(portableModeStr, out _portableMode))
                {
                    _portableMode = false;
                }
                if (PortableModeToggleSwitch != null)
                {
                    PortableModeToggleSwitch.IsChecked = _portableMode;
                }
                _contentsDirOverride = NormalizeDirectoryOverride(_configFile.ReadString(SettingSectionName, "contentsoverride", string.Empty));
                _asphyxiaDirOverride = NormalizeDirectoryOverride(_configFile.ReadString(SettingSectionName, "asphyxiaoverride", string.Empty));
                if (GameDirectoryOverrideTextBox != null) GameDirectoryOverrideTextBox.Text = _contentsDirOverride;
                if (AsphyxiaDirectoryOverrideTextBox != null) AsphyxiaDirectoryOverrideTextBox.Text = _asphyxiaDirOverride;

                LoadServerPresetsFromConfig();

                NoAsphyxiaToggleSwitch.IsChecked = bool.TryParse(_configFile.ReadString(SettingSectionName, "noasphyxia", "false"), out var noAsphyxia) && noAsphyxia;
                bool exitRestore = bool.TryParse(_configFile.ReadString(DisplaySectionName, "exitrestore", "true"), out var restoreOnExit) && restoreOnExit;
                ExitRestoreToggleSwitch.IsChecked = exitRestore;

                _displayConfigEnabled = bool.TryParse(_configFile.ReadString(DisplaySectionName, "displayconfigure", "false"), out var displayCfg) && displayCfg;
                if (DisplayConfigEnabledToggleSwitch != null) DisplayConfigEnabledToggleSwitch.IsChecked = _displayConfigEnabled;
                _isDualDisplay = !string.Equals(_configFile.ReadString(DisplaySectionName, "mode", "single"), "single", StringComparison.OrdinalIgnoreCase);
                if (DisplayModeComboBox != null) DisplayModeComboBox.SelectedIndex = _isDualDisplay ? 1 : 0;

                try
                {
                    string renderMode = _configFile.ReadString(SettingSectionName, "cl-rendermode", "dx9on12");
                    if (CompatTypeComboBox != null)
                    {
                        if (CompatTypeComboBox.Items.Count == 0)
                        {
                            CompatTypeComboBox.Items.Add("dx9on12");
                            CompatTypeComboBox.Items.Add("dx9on12_external");
                            CompatTypeComboBox.Items.Add("dxvk");
                        }
                        int idx = 0;
                        if (string.Equals(renderMode, "dx9on12_external", StringComparison.OrdinalIgnoreCase)) idx = 1;
                        else if (string.Equals(renderMode, "dxvk", StringComparison.OrdinalIgnoreCase)) idx = 2;
                        CompatTypeComboBox.SelectedIndex = idx;
                    }
                }
                catch
                {
                }

                RefreshPathOverrideDependentUi();

                if (MainScreenComboBox != null)
                {
                    int.TryParse(_configFile.ReadString(DisplaySectionName, "mainscreen", "0"), out var mainScreenIndex);
                    if (mainScreenIndex >= 0 && mainScreenIndex < MainScreenComboBox.Items.Count) MainScreenComboBox.SelectedIndex = mainScreenIndex;
                }
                if (SubScreenComboBox != null)
                {
                    int.TryParse(_configFile.ReadString(DisplaySectionName, "subscreen", "0"), out var subScreenIndex);
                    if (subScreenIndex >= 0 && subScreenIndex < SubScreenComboBox.Items.Count) SubScreenComboBox.SelectedIndex = subScreenIndex;
                }
                if (SubRotationComboBox != null)
                {
                    int.TryParse(_configFile.ReadString(DisplaySectionName, "subrotation", "0"), out var subRotationIndex);
                    if (subRotationIndex >= 0 && subRotationIndex < SubRotationComboBox.Items.Count) SubRotationComboBox.SelectedIndex = subRotationIndex;
                }
                if (RotationComboBox != null)
                {
                    int.TryParse(_configFile.ReadString(DisplaySectionName, "mainrotation", "0"), out var mainRotationIndex);
                    if (mainRotationIndex >= 0 && mainRotationIndex < RotationComboBox.Items.Count) RotationComboBox.SelectedIndex = mainRotationIndex;
                }

                RefreshMainOptions();
                RefreshSubOptions();

                if (MainResolutionComboBox != null)
                {
                    var res = _configFile.ReadString(DisplaySectionName, "mainresolution", "");
                    if (!string.IsNullOrWhiteSpace(res)) MainResolutionComboBox.SelectedItem = res;
                }
                if (SubResolutionComboBox != null)
                {
                    var res = _configFile.ReadString(DisplaySectionName, "subresolution", "");
                    if (!string.IsNullOrWhiteSpace(res)) SubResolutionComboBox.SelectedItem = res;
                }
                RefreshMainOptions(refreshResolutionList: false, refreshRateList: true);
                RefreshSubOptions(refreshResolutionList: false, refreshRateList: true);

                if (MainRefreshRateComboBox != null)
                {
                    var refresh = _configFile.ReadString(DisplaySectionName, "mainrefresh", "");
                    if (!string.IsNullOrWhiteSpace(refresh)) MainRefreshRateComboBox.SelectedItem = refresh;
                }
                if (SubRefreshRateComboBox != null)
                {
                    var refresh = _configFile.ReadString(DisplaySectionName, "subrefresh", "");
                    if (!string.IsNullOrWhiteSpace(refresh)) SubRefreshRateComboBox.SelectedItem = refresh;
                }

                UpdateDisplayLayoutControlsEnabled();
                UpdateDisplayInfoTexts();
                SyncCompatModeButtonsFromCombo();
            }
            catch (Exception ex)
            {
                ShowErrorToast("加载设置失败", ex.Message);
                if (CurrentVersionTextBox != null) CurrentVersionTextBox.Text = "读取失败";
                if (RevisionTextBox != null) RevisionTextBox.Text = "读取失败";
                if (LauncherVersionTextBox != null) LauncherVersionTextBox.Text = "读取失败";
            }
            finally
            {
                _isLoadingSettings = false;
            }
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
            bool previousMode = _portableMode;
            var targetXmlPath = GetSpiceXmlPathForMode(enabled);
            if (!File.Exists(targetXmlPath))
            {
                ShowErrorToast("切换失败", $"未找到 spicetools.xml：{targetXmlPath}");

                _portableMode = previousMode;
                ApplyPortableModeToggleState(previousMode);
                return;
            }

            _portableMode = enabled;
            ApplyPortableModeToggleState(enabled);

            SaveSettings();
            ShowInfoToast("便携模式切换", _portableMode
                ? $"已切换至便携模式，XML: {targetXmlPath}"
                : $"已切换至系统模式，XML: {targetXmlPath}");

            RefreshSettingsPanelAfterPortableModeSwitch();
        }

        private string GetSpiceXmlPathForMode(bool portableMode)
        {
            if (portableMode)
            {
                return Path.Combine(GetContentsDirectoryPath(), "lazy", "spicetools.xml");
            }

            string appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataDir, "spicetools.xml");
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

        private void RefreshSettingsVersionTexts()
        {
            if (CurrentVersionTextBox != null)
            {
                CurrentVersionTextBox.Text = ResolveMachineProperty();
            }

            if (RevisionTextBox != null)
            {
                RevisionTextBox.Text = ResolveCurrentGameVersion();
            }

            if (LauncherVersionTextBox != null)
            {
                LauncherVersionTextBox.Text = ResolveLauncherVersion();
            }
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

        private void SaveSettings()
        {
            if (_isLoadingSettings)
            {
                return;
            }

            try
            {
                _configFile.WriteString(SettingSectionName, "portablemode", _portableMode.ToString().ToLowerInvariant());
                _configFile.WriteString(SettingSectionName, "contentsoverride", NormalizeDirectoryOverride(_contentsDirOverride));
                _configFile.WriteString(SettingSectionName, "asphyxiaoverride", NormalizeDirectoryOverride(_asphyxiaDirOverride));

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
