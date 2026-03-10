using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LazyBootstrap
{
    public partial class MainWindow
    {
        private void OnLoadCompatLayerClick(object sender, RoutedEventArgs e)
        {
            LoadCompatLayerCore();
        }

        private void OnUnloadCompatLayerClick(object sender, RoutedEventArgs e)
        {
            UnloadCompatLayerCore();
        }

        private void OnCompatLayerToggleChanged(object sender, RoutedEventArgs e)
        {
            ToggleCompatLayerCore();
        }

        private void OnCompatModeChecked(object sender, RoutedEventArgs e)
        {
            ChangeCompatModeCore();
        }

        private int GetCompatLayerFileCount()
        {
            string modulesDir = Path.Combine(_contentsDir, "modules");
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

            bool effectiveEnabled = fileCount >= 1 || IsCompatLayerEnabledConfigured();
            UpdateCompatRenderModeBusyState(effectiveEnabled);

            if (CompatStatusTextBlock != null)
            {
                CompatStatusTextBlock.Text = string.Empty;
                CompatStatusTextBlock.IsVisible = false;
            }

            if (CompatTypeComboBox != null)
            {
                CompatTypeComboBox.IsEnabled = !effectiveEnabled;
                if (effectiveEnabled)
                {
                    ToolTip.SetTip(CompatTypeComboBox, null);
                }
                else if (!string.IsNullOrEmpty(_compatTypeTooltipCache))
                {
                    ToolTip.SetTip(CompatTypeComboBox, _compatTypeTooltipCache);
                }
            }

            if (LoadCompatButton != null)
            {
                LoadCompatButton.IsEnabled = !effectiveEnabled;
            }

            if (UnloadCompatButton != null)
            {
                UnloadCompatButton.IsEnabled = effectiveEnabled;
            }

            _isUpdatingCompatUi = true;
            try
            {
                if (CompatLayerToggleSwitch != null)
                {
                    CompatLayerToggleSwitch.IsChecked = effectiveEnabled;
                }

                bool chipsEnabled = !effectiveEnabled;
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
            var mode = CompatTypeComboBox?.SelectedItem?.ToString() ?? "dx9on12";
            if (CompatDxvkRadioButton != null) CompatDxvkRadioButton.IsChecked = string.Equals(mode, "dxvk", StringComparison.OrdinalIgnoreCase);
            if (CompatDx9on12ExternalRadioButton != null) CompatDx9on12ExternalRadioButton.IsChecked = string.Equals(mode, "dx9on12_external", StringComparison.OrdinalIgnoreCase);
            if (CompatDx9on12RadioButton != null)
            {
                CompatDx9on12RadioButton.IsChecked = !string.Equals(mode, "dxvk", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(mode, "dx9on12_external", StringComparison.OrdinalIgnoreCase);
            }
        }

        private void LoadCompatLayerCore()
        {
            if (!ToggleCompatLayer(true, out var error))
            {
                ShowErrorToast("兼容层切换失败", string.IsNullOrWhiteSpace(error) ? "未知错误" : error);
            }
        }

        private void UnloadCompatLayerCore()
        {
            if (!ToggleCompatLayer(false, out var error))
            {
                ShowErrorToast("兼容层切换失败", string.IsNullOrWhiteSpace(error) ? "未知错误" : error);
            }
        }

        private bool ToggleCompatLayer(bool enable, out string error)
        {
            error = string.Empty;
            if (enable)
            {
                if (!ApplyCompatLayerFilesByMode(out error))
                {
                    UpdateCompatLayerStatus();
                    return false;
                }
            }
            else
            {
                if (!RemoveCompatLayerFilesFromModules(out error))
                {
                    UpdateCompatLayerStatus();
                    return false;
                }
            }

            try
            {
                _configFile.WriteString(SettingSectionName, "compatlayer", enable ? "true" : "false");
            }
            catch (Exception ex)
            {
                error = ex.Message;
                UpdateCompatLayerStatus();
                return false;
            }

            UpdateCompatLayerStatus();
            try { UpdateSpiceConfig(new OptionUpdate("sp2x-dx9on12", ResolveDxModeValue(), false)); } catch { }
            return true;
        }

        private bool ApplyCompatLayerFilesByMode(out string error)
        {
            error = string.Empty;
            string stubsDir = Path.Combine(_contentsDir, "lazy", "stubs");
            string modulesDir = Path.Combine(_contentsDir, "modules");
            if (!Directory.Exists(stubsDir))
            {
                error = "未找到 contents/lazy/stubs";
                return false;
            }

            Directory.CreateDirectory(modulesDir);

            string mode = "dx9on12";
            try
            {
                mode = CompatTypeComboBox != null && CompatTypeComboBox.SelectedItem != null
                    ? CompatTypeComboBox.SelectedItem.ToString()
                    : "dx9on12";
            }
            catch { }

            try
            {
                var baseFiles = new[] { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll" };
                foreach (var file in baseFiles)
                {
                    string src = Path.Combine(stubsDir, file);
                    string dst = Path.Combine(modulesDir, file);
                    if (!File.Exists(src))
                    {
                        error = $"缺少文件: {file}";
                        return false;
                    }
                    File.Copy(src, dst, true);
                }

                string d3d9Path = Path.Combine(modulesDir, "d3d9.dll");
                if (File.Exists(d3d9Path))
                {
                    File.Delete(d3d9Path);
                }

                if (string.Equals(mode, "dxvk", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mode, "dx9on12_external", StringComparison.OrdinalIgnoreCase))
                {
                    string stubName = string.Equals(mode, "dxvk", StringComparison.OrdinalIgnoreCase)
                        ? "d3d9.dll.dxvk"
                        : "d3d9.dll.dx9on12";
                    string src = Path.Combine(stubsDir, stubName);
                    if (!File.Exists(src))
                    {
                        error = $"缺少文件: {stubName}";
                        return false;
                    }

                    File.Copy(src, d3d9Path, true);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool RemoveCompatLayerFilesFromModules(out string error)
        {
            error = string.Empty;
            string modulesDir = Path.Combine(_contentsDir, "modules");
            try
            {
                var files = new[] { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll", "d3d9.dll" };
                foreach (var file in files)
                {
                    string path = Path.Combine(modulesDir, file);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private async void ToggleCompatLayerCore()
        {
            if (_isLoadingSettings || _isUpdatingCompatUi)
            {
                return;
            }

            bool enable = CompatLayerToggleSwitch?.IsChecked == true;
            if (!ToggleCompatLayer(enable, out var error))
            {
                ShowErrorToast("兼容层切换失败", string.IsNullOrWhiteSpace(error) ? "未知错误" : error);
                return;
            }

            ShowInfoToast("兼容层状态已更新", enable ? "已启用 AMD/Intel 显卡兼容层。" : "已关闭 AMD/Intel 显卡兼容层。");
            await Task.CompletedTask;
        }

        private void ChangeCompatModeCore()
        {
            if (_isLoadingSettings || _isUpdatingCompatUi)
            {
                return;
            }

            if (CompatTypeComboBox == null)
            {
                return;
            }

            string selected = "dx9on12";
            if (CompatDxvkRadioButton?.IsChecked == true)
            {
                selected = "dxvk";
            }
            else if (CompatDx9on12ExternalRadioButton?.IsChecked == true)
            {
                selected = "dx9on12_external";
            }
            _isSyncingModel = true;
            try
            {
                CompatTypeComboBox.SelectedItem = selected;
            }
            finally
            {
                _isSyncingModel = false;
            }

            UpdateSpiceConfig(new OptionUpdate("sp2x-dx9on12", ResolveDxModeValue(), false));
            SaveSettings();
        }

        private string ResolveDxModeValue()
        {
            try
            {
                bool compatEnabled = IsCompatLayerEffectivelyEnabled();
                if (!compatEnabled) return "0";

                var compat = CompatTypeComboBox != null && CompatTypeComboBox.SelectedItem != null
                    ? CompatTypeComboBox.SelectedItem.ToString()
                    : "dx9on12";
                return string.Equals(compat, "dx9on12", StringComparison.OrdinalIgnoreCase) ? "1" : "0";
            }
            catch { return "0"; }
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
    }
}
