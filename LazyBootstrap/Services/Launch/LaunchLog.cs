using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using SukiUI.Controls;
using SukiUI.MessageBox;

namespace LazyBootstrap
{
    public partial class MainWindow
    {
        private async void OnOpenLogClick(object sender, RoutedEventArgs e)
        {
            await OpenLogCoreAsync();
        }

        private async void OnToggleLaunchLogClick(object sender, RoutedEventArgs e)
        {
            await ToggleLaunchLogCoreAsync();
        }

        private void AppendLaunchOutput(string message, NotificationType type = NotificationType.Information)
        {
            if (LogOutputTextBlock == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            void AppendAction()
            {
                var normalized = message.Replace("\r\n", "\n");
                var lines = normalized.Split('\n');
                foreach (var line in lines)
                {
                    string entry;
                    if (string.IsNullOrEmpty(line))
                    {
                        entry = string.Empty;
                    }
                    else
                    {
                        string prefix = type switch
                        {
                            NotificationType.Error => "[错误] ",
                            NotificationType.Warning => "[警告] ",
                            _ => string.Empty
                        };

                        entry = $"[{DateTime.UtcNow:HH:mm:ss}] {prefix}{line}";
                    }

                    _launchLogLineQueue.Enqueue(entry);
                    _launchLogBuffer.AppendLine(entry);

                    while (_launchLogLineQueue.Count > MaxLaunchLogLines)
                    {
                        var removed = _launchLogLineQueue.Dequeue();
                        int removeLength = removed.Length + Environment.NewLine.Length;
                        if (removeLength >= _launchLogBuffer.Length)
                        {
                            _launchLogBuffer.Clear();
                            break;
                        }

                        _launchLogBuffer.Remove(0, removeLength);
                    }
                }

                LogOutputTextBlock.Text = _launchLogBuffer.ToString();

                if (LaunchLogScrollViewer != null)
                {
                    LaunchLogScrollViewer.Offset = new Vector(LaunchLogScrollViewer.Offset.X, double.MaxValue);
                }

                _ = AnimateLaunchLogAppendAsync();
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                AppendAction();
            }
            else
            {
                Dispatcher.UIThread.Post(AppendAction);
            }
        }

        private async Task AnimateLaunchLogAppendAsync()
        {
            if (LogOutputTextBlock == null)
            {
                return;
            }

            if (_isLaunchLogAppendAnimating)
            {
                _isLaunchLogAppendAnimationPending = true;
                return;
            }

            _isLaunchLogAppendAnimating = true;
            try
            {
                do
                {
                    _isLaunchLogAppendAnimationPending = false;
                    LogOutputTextBlock.RenderTransformOrigin = Avalonia.RelativePoint.Center;
                    var scale = LogOutputTextBlock.RenderTransform as ScaleTransform;
                    if (scale == null)
                    {
                        scale = new ScaleTransform(0.985, 0.985);
                        LogOutputTextBlock.RenderTransform = scale;
                    }

                    LogOutputTextBlock.Opacity = 0.55;
                    scale.ScaleX = 0.985;
                    scale.ScaleY = 0.985;

                    const int steps = 6;
                    for (int i = 0; i <= steps; i++)
                    {
                        double t = (double)i / steps;
                        double eased = 1 - Math.Pow(1 - t, 3);
                        LogOutputTextBlock.Opacity = 0.55 + 0.45 * eased;
                        double currentScale = 0.985 + 0.015 * eased;
                        scale.ScaleX = currentScale;
                        scale.ScaleY = currentScale;
                        await Task.Delay(12);
                    }

                    LogOutputTextBlock.Opacity = 1;
                    scale.ScaleX = 1;
                    scale.ScaleY = 1;
                }
                while (_isLaunchLogAppendAnimationPending);
            }
            finally
            {
                _isLaunchLogAppendAnimating = false;
            }
        }

        private async Task ShowLaunchLogAreaWithAnimationAsync()
        {
            if (LaunchLogContainer == null)
            {
                return;
            }

            if (_isLaunchLogVisible && LaunchLogContainer.IsVisible)
            {
                return;
            }

            _isLaunchLogVisible = true;
            LaunchLogContainer.IsVisible = true;
            LaunchLogContainer.Opacity = 0;
            LaunchLogContainer.RenderTransformOrigin = Avalonia.RelativePoint.TopLeft;
            UpdateLaunchLogToggleButtonText();

            var scale = LaunchLogContainer.RenderTransform as ScaleTransform;
            if (scale == null)
            {
                scale = new ScaleTransform(0.12, 0.12);
                LaunchLogContainer.RenderTransform = scale;
            }

            scale.ScaleX = 0.12;
            scale.ScaleY = 0.12;

            const int steps = 14;
            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                double eased = 1 - Math.Pow(1 - t, 3);
                double currentScale = 0.12 + (0.88 * eased);
                LaunchLogContainer.Opacity = eased;
                scale.ScaleX = currentScale;
                scale.ScaleY = currentScale;
                await Task.Delay(16);
            }

            LaunchLogContainer.Opacity = 1;
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }

        private void HideLaunchLogArea(bool clearOutput = false)
        {
            _isLaunchLogVisible = false;
            if (LaunchLogContainer == null)
            {
                return;
            }

            LaunchLogContainer.IsVisible = false;
            LaunchLogContainer.Opacity = 0;
            LaunchLogContainer.RenderTransformOrigin = Avalonia.RelativePoint.TopLeft;
            LaunchLogContainer.RenderTransform = new ScaleTransform(0.12, 0.12);
            UpdateLaunchLogToggleButtonText();

            if (clearOutput)
            {
                ClearLaunchOutput();
            }
        }

        private void ClearLaunchOutput()
        {
            _launchLogLineQueue.Clear();
            _launchLogBuffer.Clear();

            if (LogOutputTextBlock != null)
            {
                LogOutputTextBlock.Text = string.Empty;
            }
        }

        private void UpdateLaunchLogToggleButtonText()
        {
            if (ToggleLaunchLogButton == null)
            {
                return;
            }

            ToggleLaunchLogButton.Content = _isLaunchLogVisible ? "隐藏启动日志" : "显示启动日志";
        }

        private async Task OpenLogCoreAsync()
        {
            await ShowLogDialogAsync();
        }

        private async Task ToggleLaunchLogCoreAsync()
        {
            if (_isLaunchLogVisible)
            {
                HideLaunchLogArea();
                return;
            }

            await ShowLaunchLogAreaWithAnimationAsync();
        }

        private async Task ShowLogDialogAsync()
        {
            try
            {
                string logPath = Path.Combine(_contentsDir, "log.txt");
                if (!File.Exists(logPath))
                {
                    ShowErrorToast("查看日志失败", $"未找到日志文件: {logPath}");
                    return;
                }

                string content = await File.ReadAllTextAsync(logPath, Encoding.UTF8);
                if (string.IsNullOrEmpty(content))
                {
                    content = "(log.txt 为空)";
                }

                var logViewer = new TextBox
                {
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.NoWrap,
                    Text = content,
                    MinWidth = 960,
                    MinHeight = 520,
                    MaxWidth = 1200,
                    MaxHeight = 680,
                    [ScrollViewer.HorizontalScrollBarVisibilityProperty] = ScrollBarVisibility.Auto,
                    [ScrollViewer.VerticalScrollBarVisibilityProperty] = ScrollBarVisibility.Auto
                };
                logViewer.CaretIndex = logViewer.Text?.Length ?? 0;

                var openFolderButton = SukiMessageBoxButtonsFactory.CreateButton("打开日志文件夹", SukiMessageBoxResult.Yes, "Flat");

                var result = await SukiMessageBox.ShowDialog(new SukiMessageBoxHost
                {
                    UseAlternativeHeaderStyle = true,
                    IconPreset = SukiMessageBoxIcons.Information,
                    Header = "log.txt",
                    Content = logViewer,
                    FooterLeftItemsSource = [new SelectableTextBlock { Text = $"路径: {logPath}" }],
                    ActionButtonsSource = [openFolderButton]
                });

                if (result is SukiMessageBoxResult.Yes)
                {
                    OpenLogFolderAndSelectFile(logPath);
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast("查看 log 失败", ex.Message);
            }
        }

        private static void OpenLogFolderAndSelectFile(string logPath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{logPath}\"",
                UseShellExecute = true
            });
        }
    }
}
