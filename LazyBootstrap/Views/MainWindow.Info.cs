using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using SukiUI.Controls;

namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private async Task ShowEnvironmentScanErrorDialogAsync()
        {
            const string errorContent =
                "(*´ - `*)∩ 啊哇哇。。。Near 检测到你的系统可能缺少必要的运行环境！\n\n" +
                "(∩^-^)∩(∩^-^)∩ Noah 建议的操作步骤：\n" +
                "- 在工具页点击「安装运行库」按钮安装必要运行环境\n" +
                "- 确保已安装最新的显卡驱动程序\n" +
                "- 如为 AMD/Intel 显卡请启用“显卡兼容层”功能\n\n" +
                "如“系统媒体功能包”异常：\n" +
                "- 检查“Windows 设置”中是否已启用“媒体功能包”\n\n" +
                "请注意！由于硬件不同，检查结果可能会误报！\n" +
                "如果所有游戏运行正常没有问题，请忽略以上提示。";

            bool openInfoPage = await _uiInteractionService.ShowDialogAsync(
                "环境检查提示",
                errorContent,
                "查看异常项",
                "关闭",
                NotificationType.Error,
                "Flat");

            if (openInfoPage)
            {
                GoToInfoPageCore();
            }
        }

        private void GoToInfoPageCore()
        {
            try
            {
                if (MainSideMenu == null)
                {
                    return;
                }

                // Must match the "信息" SukiSideMenuItem Header in MainWindow.axaml (order: 启动,设定,显示器,工具,更新,信息,关于).
                var target = MainSideMenu.Items?
                    .OfType<SukiSideMenuItem>()
                    .FirstOrDefault(item => string.Equals(item.Header?.ToString(), "信息", StringComparison.Ordinal));

                if (target == null)
                {
                    target = MainSideMenu.Items?.OfType<SukiSideMenuItem>().ElementAtOrDefault(5);
                }

                if (target != null)
                {
                    MainSideMenu.SelectedItem = target;
                }
            }
            catch
            {
            }
        }

        private void RefreshEnvironmentScanResultCard()
        {
            if (PanelEnvScanResults == null)
            {
                return;
            }

            PanelEnvScanResults.Children.Clear();

            void AddRow(string labelText, string statusText, bool showStatus, EnvironmentScan.ScanResultLevel level, bool isVirtualMachine, double indentLeft)
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
                        Text = statusText,
                        Foreground = ResolveStatusBrush(level, isVirtualMachine),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };
                    Grid.SetColumn(status, 1);
                    row.Children.Add(status);
                }

                PanelEnvScanResults.Children.Add(row);
            }

            foreach (var group in _viewModel.Info.Groups)
            {
                AddRow(group.Title, ResolveStatusText(group.Level, false), group.ShowStatus, group.Level, false, 0);

                foreach (var item in group.Items)
                {
                    AddRow(item.Label, item.StatusText, item.ShowStatus, item.Level, item.IsVirtualMachine, 28);
                }
            }
        }

        private static string ResolveStatusText(EnvironmentScan.ScanResultLevel level, bool isVirtualMachine)
        {
            if (isVirtualMachine)
            {
                return "虚拟机";
            }

            return level switch
            {
                EnvironmentScan.ScanResultLevel.Success => "通过",
                EnvironmentScan.ScanResultLevel.Warning => "警告",
                _ => "失败"
            };
        }

        private static IBrush ResolveStatusBrush(EnvironmentScan.ScanResultLevel level, bool isVirtualMachine)
        {
            if (isVirtualMachine)
            {
                return Brushes.Goldenrod;
            }

            return level switch
            {
                EnvironmentScan.ScanResultLevel.Success => Brushes.LightGreen,
                EnvironmentScan.ScanResultLevel.Warning => Brushes.Orange,
                _ => Brushes.IndianRed
            };
        }
    }
}
