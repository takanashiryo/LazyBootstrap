using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using SukiUI.Dialogs;

namespace LazyBootstrap
{
    public partial class MainWindow
    {
        private async void OnAddServerPresetClick(object sender, RoutedEventArgs e)
        {
            await AddServerPresetCoreAsync();
        }

        private async void OnDeleteServerPresetClick(object sender, RoutedEventArgs e)
        {
            await DeleteServerPresetCoreAsync();
        }

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
        }

        private void OnServerPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings || _isSyncingModel)
            {
                return;
            }

            if (ServerPresetComboBox?.SelectedItem is not ServerPresetItem preset)
            {
                return;
            }

            _isSyncingModel = true;
            try
            {
                _activeServerPreset = preset.Name;
            }
            finally
            {
                _isSyncingModel = false;
            }

            if (string.Equals(preset.Name, NonePresetName, StringComparison.OrdinalIgnoreCase))
            {
                if (ServerAddressTextBox != null) ServerAddressTextBox.Text = string.Empty;
                if (PcbIdTextBox != null) PcbIdTextBox.Text = string.Empty;
            }
            else
            {
                if (ServerAddressTextBox != null) ServerAddressTextBox.Text = preset.ServerUrl ?? string.Empty;
                if (PcbIdTextBox != null) PcbIdTextBox.Text = preset.PcbId ?? string.Empty;
            }

            UpdateSpiceConfig(
                new OptionUpdate("url", ServerAddressTextBox?.Text ?? string.Empty, false),
                new OptionUpdate("p", PcbIdTextBox?.Text ?? string.Empty, false));
            SaveServerPresetsToConfig();
        }

        private async Task AddServerPresetCoreAsync()
        {
            await CreateServerPresetInteractiveAsync();
        }

        private async Task DeleteServerPresetCoreAsync()
        {
            try
            {
                if (ServerPresetComboBox?.SelectedItem is not ServerPresetItem preset)
                {
                    ShowWarningToast("删除预设", "请先选择要删除的预设。");
                    return;
                }

                if (string.Equals(preset.Name, NonePresetName, StringComparison.OrdinalIgnoreCase))
                {
                    ShowWarningToast("删除预设", "「无」是默认项，不可删除。");
                    return;
                }

                if (string.Equals(preset.Name, AsphyxiaPresetName, StringComparison.OrdinalIgnoreCase))
                {
                    ShowWarningToast("删除预设", "Asphyxia 是内置预设，不可删除。");
                    return;
                }

                var dialogBuilder = _dialogManager
                    .CreateDialog()
                    .OfType(NotificationType.Warning)
                    .WithTitle("删除服务器预设")
                    .WithContent($"确定删除预设「{preset.Name}」？")
                    .WithYesNoResult("删除", "取消", "Flat")
                    .Dismiss().ByClickingBackground();
                ApplyDialogNotificationIcon(dialogBuilder, NotificationType.Warning);
                bool confirmed = await dialogBuilder.TryShowAsync();
                if (!confirmed)
                {
                    return;
                }

                _serverPresets.RemoveAll(p => string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));

                if (string.Equals(_activeServerPreset, preset.Name, StringComparison.OrdinalIgnoreCase))
                {
                    _activeServerPreset = NonePresetName;
                }

                RefreshServerPresetCombo();
                if (ServerPresetComboBox != null)
                {
                    var fallback = _serverPresets.FirstOrDefault(p => string.Equals(p.Name, NonePresetName, StringComparison.OrdinalIgnoreCase));
                    if (fallback != null)
                    {
                        ServerPresetComboBox.SelectedItem = fallback;
                    }
                    else if (_serverPresets.Count > 0)
                    {
                        ServerPresetComboBox.SelectedItem = _serverPresets[0];
                    }
                }

                SaveServerPresetsToConfig();
                ShowInfoToast("删除预设", $"已删除预设：{preset.Name}");
            }
            catch (Exception ex)
            {
                ShowErrorToast("删除预设失败", ex.Message);
            }
        }

        private async Task<bool> CreateServerPresetInteractiveAsync()
        {
            try
            {
                var nameBox = new TextBox { Watermark = "预设名" };
                var urlBox = new TextBox { Watermark = "http://SERVERURL:PORT" };
                var pcbBox = new TextBox { Watermark = "PCBID" };

                var content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "请填写预设信息" },
                        nameBox,
                        urlBox,
                        pcbBox
                    }
                };

                var confirmed = await _dialogManager
                    .CreateDialog()
                    .WithTitle("新建服务器预设")
                    .WithContent(content)
                    .WithYesNoResult("创建", "取消", "Flat")
                    .TryShowAsync();

                if (!confirmed)
                {
                    return false;
                }

                var presetName = (nameBox.Text ?? string.Empty).Trim();
                var serverUrl = (urlBox.Text ?? string.Empty).Trim();
                var pcbId = (pcbBox.Text ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(presetName))
                {
                    ShowErrorToast("新建预设失败", "预设名不能为空。");
                    return false;
                }

                if (_serverPresets.Any(p => string.Equals(p.Name, presetName, StringComparison.OrdinalIgnoreCase)))
                {
                    ShowErrorToast("新建预设失败", "已存在同名预设。");
                    return false;
                }

                var preset = new ServerPresetItem
                {
                    Name = presetName,
                    ServerUrl = serverUrl,
                    PcbId = pcbId
                };

                _serverPresets.Add(preset);
                SaveServerPresetsToConfig();
                RefreshServerPresetCombo();
                if (ServerPresetComboBox != null)
                {
                    ServerPresetComboBox.SelectedItem = preset;
                }
                ShowInfoToast("预设已保存", $"已创建预设：{presetName}");
                return true;
            }
            catch (Exception ex)
            {
                ShowErrorToast("新建预设失败", ex.Message);
                return false;
            }
        }
    }
}
