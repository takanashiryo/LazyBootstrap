using System;

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
                UpdateRecommendedSpiceConfigButtonVisibility();

                LoadServerPresetsFromConfig();

                NoAsphyxiaToggleSwitch.IsChecked = bool.TryParse(_configFile.ReadString(SettingSectionName, "noasphyxia", "false"), out var noAsphyxia) && noAsphyxia;
                bool exitRestore = bool.TryParse(_configFile.ReadString(DisplaySectionName, "exitrestore", "false"), out var restoreOnExit) && restoreOnExit;
                ExitRestoreToggleSwitch.IsChecked = exitRestore;

                _displayConfigEnabled = bool.TryParse(_configFile.ReadString(DisplaySectionName, "displayconfigure", "false"), out var displayCfg) && displayCfg;
                if (DisplayConfigEnabledToggleSwitch != null) DisplayConfigEnabledToggleSwitch.IsChecked = _displayConfigEnabled;
                _isDualDisplay = !string.Equals(_configFile.ReadString(DisplaySectionName, "mode", "dual"), "single", StringComparison.OrdinalIgnoreCase);
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

                string machineProperty = ResolveMachineProperty();
                if (CurrentVersionTextBox != null)
                {
                    CurrentVersionTextBox.Text = machineProperty;
                }

                string currentGameVersion = ResolveCurrentGameVersion();
                if (RevisionTextBox != null)
                {
                    RevisionTextBox.Text = currentGameVersion;
                }

                string launcherVersion = ResolveLauncherVersion();
                if (LauncherVersionTextBox != null)
                {
                    LauncherVersionTextBox.Text = launcherVersion;
                }

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

        private void SaveSettings()
        {
            if (_isLoadingSettings)
            {
                return;
            }

            try
            {
                _configFile.WriteString(SettingSectionName, "portablemode", _portableMode.ToString().ToLowerInvariant());

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
