using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using LazyBootstrap.Features.Diagnostic;
using LazyBootstrap.Features.Diagnostic.Services;
using static LazyBootstrap.Shared.Controls.ControlHelpers;

namespace LazyBootstrap.Shell
{
    public partial class MainWindow
    {
        /// <summary>True when the most recent scan found environment errors (queried by the shell startup flow).</summary>
        private bool HasEnvironmentScanErrors => _environmentScanResult.HasEnvironmentScanErrors;

        /// <summary>Runs the initial environment scan and renders it (invoked during the startup sequence).</summary>
        private async Task InitializeDiagnosticStartupAsync()
        {
            await _diagnosticOrchestrator.InitializeInfoAsync(_environmentScanResult);
            await _diagnosticOrchestrator.RunScanAsync(_environmentScanResult);
            ApplyInfoStateToUi();
        }

        private async void OnRefreshEnvironmentScanClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await _diagnosticOrchestrator.RunScanAsync(_environmentScanResult);
            ApplyInfoStateToUi();
        }

        private void ApplyInfoStateToUi()
        {
            SetTextBoxTextIfNeeded(MachinePropertyTextBox, _environmentScanResult.MachineProperty);
            SetTextBoxTextIfNeeded(GameVersionTextBox, _environmentScanResult.GameVersion);
            SetTextBoxTextIfNeeded(OperatingSystemVersionNameTextBox, _environmentScanResult.OperatingSystemVersionName);
            SetTextBoxTextIfNeeded(OperatingSystemBuildNumberTextBox, _environmentScanResult.OperatingSystemBuildNumber);

            if (EnvironmentScanPendingHintTextBlock != null)
            {
                EnvironmentScanPendingHintTextBlock.IsVisible = _environmentScanResult.ScanUiPendingHintVisible;
            }

            SetContent(CpuPrimaryRowHost, CreateCpuGpuTextRow(_environmentScanResult.CpuPrimaryRow));
            ReplacePanelChildren(GpuAdapterRowsHost, _environmentScanResult.GpuAdapterRows.Select(CreateCpuGpuTextRow));
            SetContent(NvidiaSkipNoticeRowHost, CreateEnvironmentScanResultRow(_environmentScanResult.NvidiaSkipNoticeRow));
            SetContent(NvidiaNvcudaOutcomeHost, CreateEnvironmentScanOutcomeBadge(_environmentScanResult.NvidiaNvcuda));
            SetContent(NvidiaNvcuvidOutcomeHost, CreateEnvironmentScanOutcomeBadge(_environmentScanResult.NvidiaNvcuvid));
            SetContent(NvidiaEncodeApiOutcomeHost, CreateEnvironmentScanOutcomeBadge(_environmentScanResult.NvidiaEncodeApi));

            if (NvidiaDetailPanel != null)
            {
                NvidiaDetailPanel.IsVisible = _environmentScanResult.NvidiaDetailVisible;
            }

            SetContent(DirectXRuntimeFaultRowHost, CreateEnvironmentScanResultRow(_environmentScanResult.DirectXRuntimeFaultRow));
            SetContent(DirectXD3d9OutcomeHost, CreateEnvironmentScanOutcomeBadge(_environmentScanResult.DirectXD3d9));
            SetContent(DirectXD3Dx43OutcomeHost, CreateEnvironmentScanOutcomeBadge(_environmentScanResult.DirectXD3Dx43));

            SetContent(MediaPackRuntimeFaultRowHost, CreateEnvironmentScanResultRow(_environmentScanResult.MediaPackRuntimeFaultRow));
            SetContent(MediaPackMfOutcomeHost, CreateEnvironmentScanOutcomeBadge(_environmentScanResult.MediaPackMf));
            SetContent(MediaPackMfplatOutcomeHost, CreateEnvironmentScanOutcomeBadge(_environmentScanResult.MediaPackMfplat));
            SetContent(MediaPackWmvCoreOutcomeHost, CreateEnvironmentScanOutcomeBadge(_environmentScanResult.MediaPackWmvCore));

            SetContent(Vc2010X86RuntimeFaultRowHost, CreateEnvironmentScanResultRow(_environmentScanResult.Vc2010X86RuntimeFaultRow));
            SetContent(Vc2010X86MsvcrOutcomeHost, CreateEnvironmentScanOutcomeBadge(_environmentScanResult.Vc2010X86Msvcr));
            SetContent(Vc2010X86MsvcpOutcomeHost, CreateEnvironmentScanOutcomeBadge(_environmentScanResult.Vc2010X86Msvcp));

            SetContent(Vc2010X64RuntimeFaultRowHost, CreateEnvironmentScanResultRow(_environmentScanResult.Vc2010X64RuntimeFaultRow));
            SetContent(Vc2010X64MsvcrOutcomeHost, CreateEnvironmentScanOutcomeBadge(_environmentScanResult.Vc2010X64Msvcr));
            SetContent(Vc2010X64MsvcpOutcomeHost, CreateEnvironmentScanOutcomeBadge(_environmentScanResult.Vc2010X64Msvcp));

            if (ScanRootAlertsCard != null)
            {
                ScanRootAlertsCard.IsVisible = _environmentScanResult.HasScanRootAlerts;
            }

            ReplacePanelChildren(ScanRootAlertsPanel, _environmentScanResult.ScanRootAlerts.Select(CreateRootAlertRow));
            RefreshEnvironmentOverviewChrome();
        }

        private void RefreshEnvironmentOverviewChrome()
        {
            if (EnvironmentOverviewInfoBar == null)
            {
                return;
            }

            EnvironmentOverviewInfoBar.MessageTextAlignment = TextAlignment.Left;
            EnvironmentOverviewInfoBar.IsClosable = false;
            EnvironmentOverviewInfoBar.IsVisible = true;

            if (_environmentScanResult.HasEnvironmentScanErrors)
            {
                EnvironmentOverviewInfoBar.Severity = NotificationType.Error;
                EnvironmentOverviewInfoBar.Title = "存在未通过的检查项";
                EnvironmentOverviewInfoBar.Message = string.Empty;
            }
            else if (_environmentScanResult.HasAnyEnvironmentScanWarning())
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

        private static Control CreateCpuGpuTextRow(EnvironmentScanResultRow row)
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

        private static Control CreateEnvironmentScanResultRow(EnvironmentScanResultRow row)
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

        private static Control CreateEnvironmentScanOutcomeBadge(EnvironmentScanResultOutcome outcome)
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
