using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using LazyBootstrap.FileSystem;
using LazyBootstrap.Services;
using LazyBootstrap.Models;

namespace LazyBootstrap.UI
{
    public partial class MainWindow
    {
        private async void OnClearCacheClick(object sender, RoutedEventArgs e) =>
                    await _toolsWorkflowService.ClearCacheAsync();
        private async void OnAddFirewallRuleClick(object sender, RoutedEventArgs e) =>
            await _toolsWorkflowService.AddFirewallRuleAsync();
        private async void OnOpenAudioPanelClick(object sender, RoutedEventArgs e) =>
            await _toolsWorkflowService.OpenAudioPanelAsync();
        private async void OnInstallRuntimeClick(object sender, RoutedEventArgs e)
        {
            using var busy = BeginBusy(
                BusyPresentation.RuntimeProgress,
                "正在准备安装运行库...",
                5d);
            await _toolsWorkflowService.InstallRuntimeAsync(busy.UpdateProgress);
        }
        private async void OnBackupSavedataClick(object sender, RoutedEventArgs e) =>
            await _toolsWorkflowService.BackupSavedataAsync();
        private async void OnImportSavedataClick(object sender, RoutedEventArgs e) =>
            await _toolsWorkflowService.ImportSavedataAsync();
        private async void OnMigrateSavedataClick(object sender, RoutedEventArgs e)
        {
            var directories = await PromptForMigrationDirectoriesAsync();
            if (directories == null)
            {
                return;
            }

            var entries = _toolsWorkflowService.GetMigrationEntries(
                directories.Value.GameDirectory,
                directories.Value.AsphyxiaDirectory);
            if (entries.Count == 0)
            {
                _uiInteractionService.ShowWarningToast("存档迁移", "在指定目录中未找到可迁移的数据");
                return;
            }

            var selectedEntries = await PromptForMigrationSelectionAsync(entries);
            if (selectedEntries == null)
            {
                return;
            }

            var overwriteEntries = selectedEntries
                .Where(entry => entry.IsDirectory
                    ? Directory.Exists(entry.DestinationPath)
                    : File.Exists(entry.DestinationPath))
                .ToList();
            if (overwriteEntries.Count > 0
                && !await _uiInteractionService.ShowDialogAsync(
                    "存档迁移覆盖提示",
                    "检测到以下目标文件已存在，是否覆盖？\n" +
                    string.Join("\n", overwriteEntries.Select(entry => $"• {entry.DisplayName}")),
                    "覆盖",
                    "取消",
                    NotificationType.Warning))
            {
                return;
            }

            await _toolsWorkflowService.MigrateSavedataAsync(selectedEntries);
        }

        private async Task<(string GameDirectory, string AsphyxiaDirectory)?> PromptForMigrationDirectoriesAsync()
        {
            while (true)
            {
                var gameDirectoryBox = new TextBox { Watermark = "旧游戏目录" };
                var asphyxiaDirectoryBox = new TextBox { Watermark = "旧氧无目录" };
                var selectGameButton = CreateFolderPickerButton(gameDirectoryBox, "选择旧游戏目录");
                var selectAsphyxiaButton = CreateFolderPickerButton(asphyxiaDirectoryBox, "选择旧氧无目录");
                var content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        BuildFolderPickerRow("游戏目录", gameDirectoryBox, selectGameButton),
                        BuildFolderPickerRow("氧无目录", asphyxiaDirectoryBox, selectAsphyxiaButton),
                        new TextBlock
                        {
                            Text = "点击下一步后将扫描可迁移数据",
                            Opacity = 0.72,
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                };

                if (!await _uiInteractionService.ShowDialogAsync("存档迁移", content, "下一步", "取消"))
                {
                    return null;
                }

                string gameDirectory = PathHelper.NormalizePath(gameDirectoryBox.Text);
                string asphyxiaDirectory = PathHelper.NormalizePath(asphyxiaDirectoryBox.Text);
                if (!Directory.Exists(gameDirectory) || !Directory.Exists(asphyxiaDirectory))
                {
                    _uiInteractionService.ShowWarningToast("存档迁移", "请选择有效的旧游戏目录和旧氧无目录。");
                    continue;
                }

                return (gameDirectory, asphyxiaDirectory);
            }
        }

        private Button CreateFolderPickerButton(TextBox target, string title)
        {
            var button = new Button { Content = "选择", MinWidth = 72 };
            button.Classes.Add("Basic");
            button.Click += async (_, _) =>
            {
                string path = await _uiInteractionService.PickFolderAsync(title);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    target.Text = path;
                }
            };
            return button;
        }

        private async Task<List<SavedataTransferEntry>> PromptForMigrationSelectionAsync(
            IReadOnlyList<SavedataTransferEntry> entries)
        {
            var panel = new StackPanel { Spacing = 10 };
            var selections = new List<(SavedataTransferEntry Entry, CheckBox CheckBox)>();
            foreach (var entry in entries)
            {
                var checkBox = new CheckBox { IsChecked = true, Content = entry.DisplayName };
                panel.Children.Add(new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        checkBox,
                        new TextBlock
                        {
                            Text = entry.SourcePath,
                            Opacity = 0.68,
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                });
                selections.Add((entry, checkBox));
            }

            var content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "默认已勾选全部项目；取消勾选的项目不会迁移。" },
                    new ScrollViewer { MaxHeight = 320, Content = panel }
                }
            };
            if (!await _uiInteractionService.ShowDialogAsync("选择迁移内容", content, "开始迁移", "取消"))
            {
                return null;
            }

            var selected = selections
                .Where(selection => selection.CheckBox.IsChecked == true)
                .Select(selection => selection.Entry)
                .ToList();
            if (selected.Count == 0)
            {
                _uiInteractionService.ShowWarningToast("存档迁移", "请至少选择一个要迁移的项目。");
                return null;
            }

            return selected;
        }

        private static Grid BuildFolderPickerRow(string label, TextBox textBox, Button button)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 8,
                Children =
                {
                    new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center },
                    textBox,
                    button
                }
            };
            Grid.SetColumn(textBox, 1);
            Grid.SetColumn(button, 2);
            return grid;
        }
    }
}
