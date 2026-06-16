using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using SukiUI.Controls;

namespace LazyBootstrap.Shell
{
    public partial class MainWindow
    {
        private async void OnRefreshEnvironmentScanClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await _environmentScanService.RunScanAsync(_infoState);
            ApplyInfoStateToUi();
        }

        private void ApplyInfoStateToUi()
        {
            SetTextBoxTextIfNeeded(MachinePropertyTextBox, _infoState.MachineProperty);
            SetTextBoxTextIfNeeded(GameVersionTextBox, _infoState.GameVersion);

            if (LauncherVersionTextBlock != null)
            {
                LauncherVersionTextBlock.Text = _infoState.LauncherVersion;
            }

            if (EnvironmentScanPendingHintTextBlock != null)
            {
                EnvironmentScanPendingHintTextBlock.IsVisible = _infoState.ScanUiPendingHintVisible;
            }

            SetContent(CpuPrimaryRowHost, CreateCpuGpuTextRow(_infoState.CpuPrimaryRow));
            ReplacePanelChildren(GpuAdapterRowsHost, _infoState.GpuAdapterRows.Select(CreateCpuGpuTextRow));
            SetContent(NvidiaSkipNoticeRowHost, CreateEnvironmentScanDisplayRow(_infoState.NvidiaSkipNoticeRow));
            SetContent(NvidiaNvcudaOutcomeHost, CreateEnvironmentScanOutcomeBadge(_infoState.NvidiaNvcuda));
            SetContent(NvidiaNvcuvidOutcomeHost, CreateEnvironmentScanOutcomeBadge(_infoState.NvidiaNvcuvid));
            SetContent(NvidiaEncodeApiOutcomeHost, CreateEnvironmentScanOutcomeBadge(_infoState.NvidiaEncodeApi));

            if (NvidiaDetailPanel != null)
            {
                NvidiaDetailPanel.IsVisible = _infoState.NvidiaDetailVisible;
            }

            SetContent(DirectXRuntimeFaultRowHost, CreateEnvironmentScanDisplayRow(_infoState.DirectXRuntimeFaultRow));
            SetContent(DirectXD3d9OutcomeHost, CreateEnvironmentScanOutcomeBadge(_infoState.DirectXD3d9));
            SetContent(DirectXD3Dx43OutcomeHost, CreateEnvironmentScanOutcomeBadge(_infoState.DirectXD3Dx43));

            SetContent(MediaPackRuntimeFaultRowHost, CreateEnvironmentScanDisplayRow(_infoState.MediaPackRuntimeFaultRow));
            SetContent(MediaPackMfOutcomeHost, CreateEnvironmentScanOutcomeBadge(_infoState.MediaPackMf));
            SetContent(MediaPackMfplatOutcomeHost, CreateEnvironmentScanOutcomeBadge(_infoState.MediaPackMfplat));
            SetContent(MediaPackWmvCoreOutcomeHost, CreateEnvironmentScanOutcomeBadge(_infoState.MediaPackWmvCore));

            SetContent(Vc2010X86RuntimeFaultRowHost, CreateEnvironmentScanDisplayRow(_infoState.Vc2010X86RuntimeFaultRow));
            SetContent(Vc2010X86MsvcrOutcomeHost, CreateEnvironmentScanOutcomeBadge(_infoState.Vc2010X86Msvcr));
            SetContent(Vc2010X86MsvcpOutcomeHost, CreateEnvironmentScanOutcomeBadge(_infoState.Vc2010X86Msvcp));

            SetContent(Vc2010X64RuntimeFaultRowHost, CreateEnvironmentScanDisplayRow(_infoState.Vc2010X64RuntimeFaultRow));
            SetContent(Vc2010X64MsvcrOutcomeHost, CreateEnvironmentScanOutcomeBadge(_infoState.Vc2010X64Msvcr));
            SetContent(Vc2010X64MsvcpOutcomeHost, CreateEnvironmentScanOutcomeBadge(_infoState.Vc2010X64Msvcp));

            if (ScanRootAlertsCard != null)
            {
                ScanRootAlertsCard.IsVisible = _infoState.HasScanRootAlerts;
            }

            ReplacePanelChildren(ScanRootAlertsPanel, _infoState.ScanRootAlerts.Select(CreateRootAlertRow));
            RefreshEnvironmentOverviewChrome();
        }

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

                var target = MainSideMenu.Items?
                    .OfType<SukiSideMenuItem>()
                    .FirstOrDefault(item => string.Equals(item.Header?.ToString(), "信息", StringComparison.Ordinal));

                target ??= MainSideMenu.Items?.OfType<SukiSideMenuItem>().ElementAtOrDefault(5);

                if (target != null)
                {
                    MainSideMenu.SelectedItem = target;
                }
            }
            catch
            {
            }
        }

        internal void RefreshEnvironmentOverviewChrome()
        {
            if (EnvironmentOverviewInfoBar == null)
            {
                return;
            }

            EnvironmentOverviewInfoBar.MessageTextAlignment = TextAlignment.Left;
            EnvironmentOverviewInfoBar.IsClosable = false;
            EnvironmentOverviewInfoBar.IsVisible = true;

            if (_infoState.HasEnvironmentScanErrors)
            {
                EnvironmentOverviewInfoBar.Severity = NotificationType.Error;
                EnvironmentOverviewInfoBar.Title = "存在未通过的检查项";
                EnvironmentOverviewInfoBar.Message = string.IsNullOrWhiteSpace(_infoState.EnvironmentSummary)
                    ? "请对照下方固定检测项查看未通过条目。"
                    : _infoState.EnvironmentSummary.Trim();
            }
            else if (_infoState.HasAnyEnvironmentScanWarning())
            {
                EnvironmentOverviewInfoBar.Severity = NotificationType.Warning;
                EnvironmentOverviewInfoBar.Title = "存在警告";
                EnvironmentOverviewInfoBar.Message = string.Empty;
            }
            else
            {
                EnvironmentOverviewInfoBar.Severity = NotificationType.Success;
                EnvironmentOverviewInfoBar.Title = "检测通过";
                EnvironmentOverviewInfoBar.Message = string.Empty;
            }
        }

        private static void SetContent(ContentControl host, Control content)
        {
            if (host != null)
            {
                host.Content = content;
            }
        }

        private static void ReplacePanelChildren(Panel panel, IEnumerable<Control> controls)
        {
            if (panel == null)
            {
                return;
            }

            panel.Children.Clear();
            foreach (var control in controls ?? Enumerable.Empty<Control>())
            {
                panel.Children.Add(control);
            }
        }

        private static Control CreateCpuGpuTextRow(EnvironmentScanDisplayRow row)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 8,
                Margin = new Thickness(0, 1),
                IsVisible = row?.IsShown == true
            };

            grid.Children.Add(new Ellipse
            {
                Width = 6,
                Height = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = new SolidColorBrush(Color.FromRgb(150, 150, 150))
            });

            var text = new TextBlock
            {
                FontSize = 13,
                LineHeight = 18,
                Text = row?.PrimaryText ?? string.Empty,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            return grid;
        }

        private static Control CreateEnvironmentScanDisplayRow(EnvironmentScanDisplayRow row)
        {
            if (row == null || !row.IsShown)
            {
                return new Border { IsVisible = false };
            }

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 10
            };

            grid.Children.Add(new Ellipse
            {
                Width = 8,
                Height = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = GetEnvironmentScanBrush(row.BadgeLevel, EnvironmentScanBrushRole.Foreground)
            });

            var textStack = new StackPanel
            {
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(textStack, 1);
            textStack.Children.Add(new TextBlock
            {
                FontSize = 13,
                LineHeight = 18,
                Text = row.PrimaryText,
                TextWrapping = TextWrapping.Wrap
            });
            textStack.Children.Add(new TextBlock
            {
                FontSize = 12,
                IsVisible = row.SecondaryVisible,
                LineHeight = 16,
                Opacity = 0.65,
                Text = row.SecondaryText,
                TextWrapping = TextWrapping.Wrap
            });
            grid.Children.Add(textStack);

            var badge = CreateBadge(row.BadgeLevel, row.StatusText, row.ShowStatusBadge);
            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);

            return new Border
            {
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(12, 10),
                BorderThickness = new Thickness(1),
                Background = GetEnvironmentScanBrush(row.BadgeLevel, EnvironmentScanBrushRole.RowFill),
                BorderBrush = GetEnvironmentScanBrush(row.BadgeLevel, EnvironmentScanBrushRole.RowStroke),
                Child = grid
            };
        }

        private static Control CreateEnvironmentScanOutcomeBadge(EnvironmentScanLineOutcome outcome)
        {
            if (outcome == null || !outcome.OutcomeVisible)
            {
                return new Border { IsVisible = false };
            }

            return CreateBadge(outcome.BadgeLevel, outcome.StatusText, true);
        }

        private static Control CreateRootAlertRow(string text)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 8,
                Margin = new Thickness(0, 1)
            };

            grid.Children.Add(new Ellipse
            {
                Width = 6,
                Height = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.5,
                Fill = new SolidColorBrush(Color.FromRgb(150, 150, 150))
            });

            var textBlock = new TextBlock
            {
                FontSize = 13,
                LineHeight = 18,
                Opacity = 0.8,
                Text = text ?? string.Empty,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(textBlock, 1);
            grid.Children.Add(textBlock);

            return grid;
        }

        private static Border CreateBadge(EnvironmentScan.ScanResultLevel level, string text, bool isVisible)
        {
            return new Border
            {
                Padding = new Thickness(10, 3),
                CornerRadius = new CornerRadius(999),
                BorderThickness = new Thickness(1),
                MinWidth = 56,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsVisible = isVisible,
                Background = GetEnvironmentScanBrush(level, EnvironmentScanBrushRole.BadgeBackground),
                BorderBrush = GetEnvironmentScanBrush(level, EnvironmentScanBrushRole.Border),
                Child = new TextBlock
                {
                    FontSize = 11.5,
                    FontWeight = FontWeight.DemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = GetEnvironmentScanBrush(level, EnvironmentScanBrushRole.Foreground),
                    Text = text ?? string.Empty
                }
            };
        }

        private static IBrush GetEnvironmentScanBrush(EnvironmentScan.ScanResultLevel level, EnvironmentScanBrushRole role)
        {
            Color accent = level switch
            {
                EnvironmentScan.ScanResultLevel.Success => Color.FromRgb(82, 196, 26),
                EnvironmentScan.ScanResultLevel.Warning => Color.FromRgb(250, 140, 22),
                _ => Color.FromRgb(245, 34, 45)
            };

            byte alpha = role switch
            {
                EnvironmentScanBrushRole.Foreground => 255,
                EnvironmentScanBrushRole.Border => 200,
                EnvironmentScanBrushRole.RowFill => 24,
                EnvironmentScanBrushRole.RowStroke => 96,
                EnvironmentScanBrushRole.BadgeBackground => level == EnvironmentScan.ScanResultLevel.Success ? (byte)28 : level == EnvironmentScan.ScanResultLevel.Warning ? (byte)26 : (byte)30,
                _ => 0
            };

            return new SolidColorBrush(Color.FromArgb(alpha, accent.R, accent.G, accent.B));
        }

        private enum EnvironmentScanBrushRole
        {
            Foreground,
            Border,
            RowFill,
            RowStroke,
            BadgeBackground
        }
    }
}
