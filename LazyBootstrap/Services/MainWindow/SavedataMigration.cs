using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SukiUI.Dialogs;

namespace LazyBootstrap
{
    public partial class MainWindow
    {
        private sealed class SavedataTransferEntry
        {
            public SavedataTransferEntry(string id, string displayName, string sourcePath, string destinationPath, string archiveRelativePath, bool isDirectory)
            {
                Id = id ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                SourcePath = sourcePath ?? string.Empty;
                DestinationPath = destinationPath ?? string.Empty;
                ArchiveRelativePath = archiveRelativePath ?? string.Empty;
                IsDirectory = isDirectory;
            }

            public string Id { get; }

            public string DisplayName { get; }

            public string SourcePath { get; }

            public string DestinationPath { get; }

            public string ArchiveRelativePath { get; }

            public bool IsDirectory { get; }
        }

        private async Task BackupSavedataWithCurrentPathsAsync()
        {
            string sevenZipPath = ResolveSevenZipExecutablePath();
            if (!File.Exists(sevenZipPath))
            {
                ShowErrorToast("存档备份失败", $"未找到 7za.exe：{sevenZipPath}");
                return;
            }

            var entries = GetCurrentSavedataEntries();
            if (entries.Count == 0)
            {
                ShowWarningToast("存档备份", "未找到可备份的数据");
                return;
            }

            string backupDirectory = Path.Combine(_baseDir, "savedata_backup");
            Directory.CreateDirectory(backupDirectory);
            string backupFilePath = Path.Combine(backupDirectory, $"savedata_{DateTime.Now:yyyyMMdd_HHmmss}.7z");
            string stagingDirectory = CreateTemporarySavedataDirectory("backup");

            try
            {
                StageSavedataEntries(entries, stagingDirectory);

                var stagedTopLevelEntries = Directory.GetFileSystemEntries(stagingDirectory)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();
                if (stagedTopLevelEntries.Count == 0)
                {
                    ShowWarningToast("存档备份", "未生成可压缩的备份内容。");
                    return;
                }

                var arguments = $"a -t7z \"{backupFilePath}\" {string.Join(" ", stagedTopLevelEntries.Select(name => $"\"{name}\""))} -mx=9";
                var result = await RunProcessCaptureAsync(sevenZipPath, arguments, stagingDirectory);
                if (result.ExitCode == 0)
                {
                    ShowInfoToast("存档备份完成", $"已备份到：{backupFilePath}");
                    return;
                }

                ShowErrorToast("存档备份失败", GetProcessErrorDetail(result));
            }
            finally
            {
                DeleteDirectoryIfExists(stagingDirectory);
            }
        }

        private async Task ImportSavedataWithCurrentPathsAsync()
        {
            string sevenZipPath = ResolveSevenZipExecutablePath();
            if (!File.Exists(sevenZipPath))
            {
                ShowErrorToast("存档导入失败", $"未找到 7za.exe：{sevenZipPath}");
                return;
            }

            if (StorageProvider == null)
            {
                ShowErrorToast("存档导入失败", "当前环境无法打开文件选择器。");
                return;
            }

            var selectedFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择存档备份文件",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("7z 备份文件")
                    {
                        Patterns = new[] { "*.7z" }
                    }
                }
            });
            if (selectedFiles == null || selectedFiles.Count == 0)
            {
                return;
            }

            string archivePath = selectedFiles[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                ShowErrorToast("存档导入失败", "当前选择的文件不可直接访问，请选择本地磁盘文件。");
                return;
            }

            var targetEntries = GetCurrentSavedataTargets();
            if (HasExistingTargets(targetEntries))
            {
                bool confirmed = await ConfirmOverwriteAsync(
                    "存档导入覆盖提示",
                    "检测到当前游戏目录或氧无目录中已有存档文件，是否覆盖？",
                    "覆盖");
                if (!confirmed)
                {
                    return;
                }
            }

            string extractionDirectory = CreateTemporarySavedataDirectory("import");
            try
            {
                string arguments = $"x \"{archivePath}\" -o\"{extractionDirectory}\" -y";
                var extractionResult = await RunProcessCaptureAsync(sevenZipPath, arguments, extractionDirectory);
                if (extractionResult.ExitCode != 0)
                {
                    ShowErrorToast("存档导入失败", GetProcessErrorDetail(extractionResult));
                    return;
                }

                var extractedEntries = BuildArchiveEntriesFromDirectory(extractionDirectory);
                if (extractedEntries.Count == 0)
                {
                    ShowWarningToast("存档导入", "备份文件中未找到可导入的数据");
                    return;
                }

                await Task.Run(() => CopyEntriesToCurrentTargets(extractedEntries));
                ShowInfoToast("存档导入完成", "已导入到当前设置的游戏目录和氧无目录。");
            }
            catch (Exception ex)
            {
                ShowErrorToast("存档导入失败", ex.Message);
            }
            finally
            {
                DeleteDirectoryIfExists(extractionDirectory);
            }
        }

        private async Task MigrateSavedataAsync()
        {
            var directories = await PromptForSavedataMigrationDirectoriesAsync();
            if (directories == null)
            {
                return;
            }

            var migrationEntries = BuildMigrationEntries(directories.Value.GameDirectory, directories.Value.AsphyxiaDirectory);
            if (migrationEntries.Count == 0)
            {
                ShowWarningToast("存档迁移", "在指定目录中未找到可迁移的数据");
                return;
            }

            var selectedEntries = await PromptForMigrationSelectionAsync(migrationEntries);
            if (selectedEntries == null || selectedEntries.Count == 0)
            {
                return;
            }

            var overwriteEntries = selectedEntries
                .Where(entry => entry.IsDirectory ? Directory.Exists(entry.DestinationPath) : File.Exists(entry.DestinationPath))
                .ToList();
            if (overwriteEntries.Count > 0)
            {
                bool confirmed = await ConfirmOverwriteAsync(
                    "存档迁移覆盖提示",
                    "检测到以下目标文件已存在，是否覆盖？" + Environment.NewLine + string.Join(Environment.NewLine, overwriteEntries.Select(entry => $"• {entry.DisplayName}")),
                    "覆盖");
                if (!confirmed)
                {
                    return;
                }
            }

            try
            {
                await Task.Run(() => CopyEntriesToCurrentTargets(selectedEntries));
                ShowInfoToast("存档迁移完成", "已迁移到当前设置的游戏目录和氧无目录。");
            }
            catch (Exception ex)
            {
                ShowErrorToast("存档迁移失败", ex.Message);
            }
        }

        private List<SavedataTransferEntry> GetCurrentSavedataEntries()
        {
            var entries = GetCurrentSavedataTargets();
            return entries
                .Where(entry => entry.IsDirectory ? Directory.Exists(entry.SourcePath) : File.Exists(entry.SourcePath))
                .ToList();
        }

        private List<SavedataTransferEntry> GetCurrentSavedataTargets()
        {
            string contentsDirectory = GetContentsDirectoryPath();
            string asphyxiaDirectory = GetAsphyxiaDirectoryPath();

            return new List<SavedataTransferEntry>
            {
                new(
                    "card0",
                    "card0.txt",
                    Path.Combine(contentsDirectory, "card0.txt"),
                    Path.Combine(contentsDirectory, "card0.txt"),
                    Path.Combine("contents", "card0.txt"),
                    isDirectory: false),
                new(
                    "card1",
                    "card1.txt",
                    Path.Combine(contentsDirectory, "card1.txt"),
                    Path.Combine(contentsDirectory, "card1.txt"),
                    Path.Combine("contents", "card1.txt"),
                    isDirectory: false),
                new(
                    "savedata",
                    "savedata",
                    Path.Combine(asphyxiaDirectory, "savedata"),
                    Path.Combine(asphyxiaDirectory, "savedata"),
                    Path.Combine("asphyxia", "savedata"),
                    isDirectory: true),
                new(
                    "config",
                    "config.ini",
                    Path.Combine(asphyxiaDirectory, "config.ini"),
                    Path.Combine(asphyxiaDirectory, "config.ini"),
                    Path.Combine("asphyxia", "config.ini"),
                    isDirectory: false)
            };
        }

        private static void StageSavedataEntries(IEnumerable<SavedataTransferEntry> entries, string stagingDirectory)
        {
            foreach (var entry in entries)
            {
                string stagedPath = Path.Combine(stagingDirectory, entry.ArchiveRelativePath);
                if (entry.IsDirectory)
                {
                    CopyDirectoryRecursive(entry.SourcePath, stagedPath);
                    continue;
                }

                string stagedParent = Path.GetDirectoryName(stagedPath);
                if (!string.IsNullOrWhiteSpace(stagedParent))
                {
                    Directory.CreateDirectory(stagedParent);
                }

                File.Copy(entry.SourcePath, stagedPath, overwrite: true);
            }
        }

        private List<SavedataTransferEntry> BuildArchiveEntriesFromDirectory(string extractionDirectory)
        {
            var targets = GetCurrentSavedataTargets();
            var extractedEntries = new List<SavedataTransferEntry>();

            foreach (var target in targets)
            {
                string extractedPath = Path.Combine(extractionDirectory, target.ArchiveRelativePath);
                if (target.IsDirectory)
                {
                    if (!Directory.Exists(extractedPath))
                    {
                        continue;
                    }
                }
                else if (!File.Exists(extractedPath))
                {
                    continue;
                }

                extractedEntries.Add(new SavedataTransferEntry(
                    target.Id,
                    target.DisplayName,
                    extractedPath,
                    target.DestinationPath,
                    target.ArchiveRelativePath,
                    target.IsDirectory));
            }

            return extractedEntries;
        }

        private List<SavedataTransferEntry> BuildMigrationEntries(string sourceGameDirectory, string sourceAsphyxiaDirectory)
        {
            string sourceContentsDirectory = ResolveMigrationGameDirectory(sourceGameDirectory);
            string targetContentsDirectory = GetContentsDirectoryPath();
            string targetAsphyxiaDirectory = GetAsphyxiaDirectoryPath();

            var entries = new List<SavedataTransferEntry>();
            AddFileEntryIfExists(entries, "card0", "card0.txt", Path.Combine(sourceContentsDirectory, "card0.txt"), Path.Combine(targetContentsDirectory, "card0.txt"));
            AddFileEntryIfExists(entries, "card1", "card1.txt", Path.Combine(sourceContentsDirectory, "card1.txt"), Path.Combine(targetContentsDirectory, "card1.txt"));
            AddFileEntryIfExists(entries, "config", "config.ini", Path.Combine(sourceAsphyxiaDirectory, "config.ini"), Path.Combine(targetAsphyxiaDirectory, "config.ini"));

            string sourceSavedataDirectory = Path.Combine(sourceAsphyxiaDirectory, "savedata");
            if (Directory.Exists(sourceSavedataDirectory))
            {
                entries.Add(new SavedataTransferEntry(
                    "savedata",
                    "savedata",
                    sourceSavedataDirectory,
                    Path.Combine(targetAsphyxiaDirectory, "savedata"),
                    Path.Combine("asphyxia", "savedata"),
                    isDirectory: true));
            }

            return entries;
        }

        private static string ResolveMigrationGameDirectory(string sourceGameDirectory)
        {
            if (string.IsNullOrWhiteSpace(sourceGameDirectory))
            {
                return string.Empty;
            }

            string directCard0Path = Path.Combine(sourceGameDirectory, "card0.txt");
            string directCard1Path = Path.Combine(sourceGameDirectory, "card1.txt");
            if (File.Exists(directCard0Path) || File.Exists(directCard1Path))
            {
                return sourceGameDirectory;
            }

            string nestedContentsDirectory = Path.Combine(sourceGameDirectory, "contents");
            string nestedCard0Path = Path.Combine(nestedContentsDirectory, "card0.txt");
            string nestedCard1Path = Path.Combine(nestedContentsDirectory, "card1.txt");
            if (File.Exists(nestedCard0Path) || File.Exists(nestedCard1Path))
            {
                return nestedContentsDirectory;
            }

            return sourceGameDirectory;
        }

        private static void AddFileEntryIfExists(List<SavedataTransferEntry> entries, string id, string displayName, string sourcePath, string destinationPath)
        {
            if (!File.Exists(sourcePath))
            {
                return;
            }

            entries.Add(new SavedataTransferEntry(id, displayName, sourcePath, destinationPath, string.Empty, isDirectory: false));
        }

        private async Task<(string GameDirectory, string AsphyxiaDirectory)?> PromptForSavedataMigrationDirectoriesAsync()
        {
            while (true)
            {
                var gameDirectoryTextBox = new TextBox { Watermark = "旧游戏目录" };
                var asphyxiaDirectoryTextBox = new TextBox { Watermark = "旧氧无目录" };

                var selectGameDirectoryButton = new Button { Content = "选择", MinWidth = 72 };
                selectGameDirectoryButton.Classes.Add("Basic");
                selectGameDirectoryButton.Click += async (_, _) =>
                {
                    var selectedPath = await PickFolderPathAsync("选择旧游戏目录");
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        gameDirectoryTextBox.Text = selectedPath;
                    }
                };

                var selectAsphyxiaDirectoryButton = new Button { Content = "选择", MinWidth = 72 };
                selectAsphyxiaDirectoryButton.Classes.Add("Basic");
                selectAsphyxiaDirectoryButton.Click += async (_, _) =>
                {
                    var selectedPath = await PickFolderPathAsync("选择旧氧无目录");
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        asphyxiaDirectoryTextBox.Text = selectedPath;
                    }
                };

                var content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        BuildFolderPickerRow("游戏目录", gameDirectoryTextBox, selectGameDirectoryButton),
                        BuildFolderPickerRow("氧无目录", asphyxiaDirectoryTextBox, selectAsphyxiaDirectoryButton),
                        new TextBlock
                        {
                            Text = "点击下一步后将扫描可迁移数据",
                            Opacity = 0.72,
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                };

                bool confirmed = await _dialogManager
                    .CreateDialog()
                    .WithTitle("存档迁移")
                    .WithContent(content)
                    .WithYesNoResult("下一步", "取消", "Flat")
                    .TryShowAsync();
                if (!confirmed)
                {
                    return null;
                }

                string gameDirectory = NormalizeDirectoryPath(gameDirectoryTextBox.Text);
                string asphyxiaDirectory = NormalizeDirectoryPath(asphyxiaDirectoryTextBox.Text);

                if (!Directory.Exists(gameDirectory))
                {
                    ShowWarningToast("存档迁移", "请选择有效的旧游戏目录。");
                    continue;
                }

                if (!Directory.Exists(asphyxiaDirectory))
                {
                    ShowWarningToast("存档迁移", "请选择有效的旧氧无目录。");
                    continue;
                }

                return (gameDirectory, asphyxiaDirectory);
            }
        }

        private async Task<List<SavedataTransferEntry>> PromptForMigrationSelectionAsync(IReadOnlyList<SavedataTransferEntry> entries)
        {
            while (true)
            {
                var selectionPanel = new StackPanel { Spacing = 10 };
                var selections = new List<(SavedataTransferEntry Entry, CheckBox CheckBox)>();

                foreach (var entry in entries)
                {
                    var checkBox = new CheckBox
                    {
                        IsChecked = true,
                        Content = entry.DisplayName
                    };

                    selectionPanel.Children.Add(new StackPanel
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
                        new TextBlock
                        {
                            Text = "默认已勾选全部项目；取消勾选的项目不会迁移。",
                            TextWrapping = TextWrapping.Wrap
                        },
                        new ScrollViewer
                        {
                            MaxHeight = 320,
                            Content = selectionPanel
                        }
                    }
                };

                bool confirmed = await _dialogManager
                    .CreateDialog()
                    .WithTitle("选择迁移内容")
                    .WithContent(content)
                    .WithYesNoResult("开始迁移", "取消", "Flat")
                    .TryShowAsync();
                if (!confirmed)
                {
                    return null;
                }

                var selectedEntries = selections
                    .Where(selection => selection.CheckBox.IsChecked == true)
                    .Select(selection => selection.Entry)
                    .ToList();
                if (selectedEntries.Count > 0)
                {
                    return selectedEntries;
                }

                ShowWarningToast("存档迁移", "请至少选择一个要迁移的项目。");
            }
        }

        private static Grid BuildFolderPickerRow(string label, TextBox textBox, Button button)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    textBox,
                    button
                }
            };
            Grid.SetColumn(textBox, 1);
            Grid.SetColumn(button, 2);
            return grid;
        }

        private async Task<bool> ConfirmOverwriteAsync(string title, string content, string confirmText)
        {
            var dialogBuilder = _dialogManager
                .CreateDialog()
                .OfType(NotificationType.Warning)
                .WithTitle(title)
                .WithContent(content)
                .WithYesNoResult(confirmText, "取消", "Flat")
                .Dismiss().ByClickingBackground();
            ApplyDialogNotificationIcon(dialogBuilder, NotificationType.Warning);
            return await dialogBuilder.TryShowAsync();
        }

        private static bool HasExistingTargets(IEnumerable<SavedataTransferEntry> entries)
        {
            return entries.Any(entry => entry.IsDirectory ? Directory.Exists(entry.DestinationPath) : File.Exists(entry.DestinationPath));
        }

        private async Task<string> PickFolderPathAsync(string title)
        {
            if (StorageProvider == null)
            {
                return string.Empty;
            }

            var selectedFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });
            if (selectedFolders == null || selectedFolders.Count == 0)
            {
                return string.Empty;
            }

            return NormalizeDirectoryPath(selectedFolders[0].TryGetLocalPath());
        }

        private static string NormalizeDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path.Trim();
            }
        }

        private static void CopyEntriesToCurrentTargets(IEnumerable<SavedataTransferEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry.IsDirectory)
                {
                    DeleteDirectoryIfExists(entry.DestinationPath);
                    CopyDirectoryRecursive(entry.SourcePath, entry.DestinationPath);
                    continue;
                }

                string destinationDirectory = Path.GetDirectoryName(entry.DestinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(entry.SourcePath, entry.DestinationPath, overwrite: true);
            }
        }

        private static void CopyDirectoryRecursive(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                string destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(file));
                File.Copy(file, destinationFile, overwrite: true);
            }

            foreach (var directory in Directory.GetDirectories(sourceDirectory))
            {
                string destinationSubDirectory = Path.Combine(destinationDirectory, Path.GetFileName(directory));
                CopyDirectoryRecursive(directory, destinationSubDirectory);
            }
        }

        private static string CreateTemporarySavedataDirectory(string purpose)
        {
            string path = Path.Combine(Path.GetTempPath(), "LazyBootstrap", purpose, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
