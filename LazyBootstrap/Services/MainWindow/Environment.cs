using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Threading;
using SukiUI.Dialogs;

namespace LazyBootstrap
{
    public partial class MainWindow
    {
        private async Task RunEnvironmentScanAsync()
        {
            try
            {
                SetControlsEnabled(false);
                if (StatusLabel != null) StatusLabel.Text = "正在进行环境检查...";
                if (StatusProgress != null)
                {
                    StatusProgress.IsVisible = true;
                    StatusProgress.Value = 0;
                    StatusProgress.Minimum = 0;
                    StatusProgress.Maximum = 100;
                }

                await EnvironmentScan.RunAsync((progress, message) =>
                {
                    int value = progress;
                    if (value < 0) value = 0;
                    if (value > 100) value = 100;
                    try
                    {
                        if (StatusProgress != null)
                        {
                            Dispatcher.UIThread.Post(() => { StatusProgress.Value = value; });
                        }
                    }
                    catch { }
                });

                RefreshEnvironmentScanResultCard();

                if (EnvironmentScan.LastHadError)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("(> _<) 啊哇哇。。。Near 检测到你的系统可能缺少必要的运行环境！");
                    sb.AppendLine();
                    sb.AppendLine("以下是检查异常项：");
                    sb.AppendLine(EnvironmentScan.LastErrorSummary);
                    sb.AppendLine("(* ^_^) Noah 建议的操作步骤：");
                    sb.AppendLine("- 在工具页点击「安装运行库」按钮安装必要运行环境");
                    sb.AppendLine("- 确保已安装最新的显卡驱动程序");
                    sb.AppendLine("- 如为 AMD/Intel 显卡请启用\u201c显卡兼容层\u201d功能");
                    sb.AppendLine();
                    sb.AppendLine("如\u201c系统媒体功能包\u201d异常：");
                    sb.AppendLine("- 检查\u201cWindows 设置\u201d中是否已启用\u201c媒体功能包\u201d");
                    sb.AppendLine();
                    sb.AppendLine("请注意！由于硬件不同，检查结果可能会误报！");
                    sb.AppendLine("如果所有游戏运行正常没有问题，请忽略以上提示。");
                    sb.AppendLine();

                    var dialogBuilder = _dialogManager.CreateDialog()
                        .OfType(NotificationType.Error)
                        .WithTitle("环境检查提示")
                        .WithContent(sb.ToString())
                        .WithActionButton("关闭", _ => { }, true, "Flat")
                        .Dismiss().ByClickingBackground();
                    ApplyDialogNotificationIcon(dialogBuilder, NotificationType.Error);
                    dialogBuilder.TryShow();
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast("环境检查失败", ex.Message);
            }
            finally
            {
                if (StatusLabel != null) StatusLabel.Text = "就绪";
                if (StatusProgress != null)
                {
                    try { StatusProgress.Value = 0; } catch { }
                    StatusProgress.IsVisible = false;
                }
                SetControlsEnabled(true);
            }
        }

        private void RefreshEnvironmentScanResultCard()
        {
            if (PanelEnvScanResults == null)
            {
                return;
            }

            PanelEnvScanResults.Children.Clear();

            var rootItems = new List<EnvironmentScan.ScanResultItem>();
            var groupedItems = new Dictionary<string, List<EnvironmentScan.ScanResultItem>>(StringComparer.Ordinal);

            foreach (var item in EnvironmentScan.LastItems)
            {
                var slashIndex = item.Item.IndexOf('/');
                if (slashIndex <= 0 || slashIndex >= item.Item.Length - 1)
                {
                    rootItems.Add(item);
                    continue;
                }

                var groupName = item.Item.Substring(0, slashIndex).Trim();
                if (!groupedItems.TryGetValue(groupName, out var list))
                {
                    list = new List<EnvironmentScan.ScanResultItem>();
                    groupedItems[groupName] = list;
                }

                list.Add(item);
            }

            static string ResolveStatusText(EnvironmentScan.ScanResultItem item)
            {
                if (!string.IsNullOrWhiteSpace(item.Detail)
                    && item.Detail.IndexOf("虚拟机", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "虚拟机";
                }

                return item.Level switch
                {
                    EnvironmentScan.ScanResultLevel.Success => "通过",
                    EnvironmentScan.ScanResultLevel.Warning => "警告",
                    _ => "失败"
                };
            }

            static IBrush ResolveStatusBrush(EnvironmentScan.ScanResultItem item)
            {
                if (!string.IsNullOrWhiteSpace(item.Detail)
                    && item.Detail.IndexOf("虚拟机", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return Brushes.Goldenrod;
                }

                return item.Level switch
                {
                    EnvironmentScan.ScanResultLevel.Success => Brushes.LightGreen,
                    EnvironmentScan.ScanResultLevel.Warning => Brushes.Orange,
                    _ => Brushes.IndianRed
                };
            }

            void AddRow(string labelText, EnvironmentScan.ScanResultItem sourceItem, bool showStatus, double indentLeft)
            {
                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("300,80,*"),
                    ColumnSpacing = 8,
                    Margin = new Thickness(indentLeft, 0, 0, 0)
                };

                var label = new TextBlock
                {
                    Text = labelText,
                    TextWrapping = TextWrapping.Wrap
                };
                row.Children.Add(label);

                if (showStatus)
                {
                    var status = new TextBlock
                    {
                        Text = ResolveStatusText(sourceItem),
                        Foreground = ResolveStatusBrush(sourceItem),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };
                    Grid.SetColumn(status, 1);
                    row.Children.Add(status);
                }

                PanelEnvScanResults.Children.Add(row);
            }

            foreach (var item in rootItems)
            {
                AddRow(item.Item, item, true, 0);
            }

            foreach (var group in groupedItems)
            {
                var groupLevel = group.Value.Any(x => x.Level == EnvironmentScan.ScanResultLevel.Error)
                    ? EnvironmentScan.ScanResultLevel.Error
                    : (group.Value.Any(x => x.Level == EnvironmentScan.ScanResultLevel.Warning)
                        ? EnvironmentScan.ScanResultLevel.Warning
                        : EnvironmentScan.ScanResultLevel.Success);

                var groupItem = new EnvironmentScan.ScanResultItem
                {
                    Item = group.Key,
                    Level = groupLevel
                };

                AddRow(group.Key, groupItem, false, 0);

                bool noStatusGroup = string.Equals(group.Key, "CPU", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(group.Key, "GPU", StringComparison.OrdinalIgnoreCase);

                foreach (var child in group.Value)
                {
                    var slashIndex = child.Item.IndexOf('/');
                    var childSuffix = slashIndex >= 0 && slashIndex < child.Item.Length - 1
                        ? child.Item.Substring(slashIndex + 1)
                        : child.Item;
                    bool isVm = !string.IsNullOrWhiteSpace(child.Detail)
                        && child.Detail.IndexOf("\u865a\u62df\u673a", StringComparison.OrdinalIgnoreCase) >= 0;

                    string childLabel;
                    if (noStatusGroup)
                    {
                        if (string.Equals(group.Key, "CPU", StringComparison.OrdinalIgnoreCase))
                        {
                            childLabel = string.IsNullOrWhiteSpace(child.Detail) ? childSuffix : child.Detail;
                        }
                        else
                        {
                            childLabel = childSuffix;
                        }
                    }
                    else
                    {
                        childLabel = string.IsNullOrWhiteSpace(child.Detail) ? childSuffix : $"{childSuffix} - {child.Detail}";
                    }

                    AddRow(childLabel, child, noStatusGroup ? isVm : true, 28);
                }
            }
        }
    }
}
