using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Threading;
using SukiUI.Controls;

namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private void AttachEnvironmentScanCollections()
        {
            _viewModel.Info.GpuAdapterRows.CollectionChanged += OnEnvironmentScanSurfaceChanged;
            _viewModel.Info.ScanRootAlerts.CollectionChanged += OnEnvironmentScanSurfaceChanged;
        }

        private void DetachEnvironmentScanCollections()
        {
            _viewModel.Info.GpuAdapterRows.CollectionChanged -= OnEnvironmentScanSurfaceChanged;
            _viewModel.Info.ScanRootAlerts.CollectionChanged -= OnEnvironmentScanSurfaceChanged;
        }

        private void HookInfoViewModelState()
        {
            _viewModel.Info.PropertyChanged += OnInfoViewModelPropertyChanged;
            AttachEnvironmentScanCollections();
        }

        private void UnhookInfoViewModelState()
        {
            _viewModel.Info.PropertyChanged -= OnInfoViewModelPropertyChanged;
            DetachEnvironmentScanCollections();
        }

        private void OnEnvironmentScanSurfaceChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(RefreshEnvironmentOverviewChrome, DispatcherPriority.Background);
        }

        private void OnInfoViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var propertyName = e.PropertyName;

            if (string.IsNullOrEmpty(propertyName)
                || propertyName.Equals(nameof(InfoPageViewModel.EnvironmentSummary), StringComparison.Ordinal)
                || propertyName.Equals(nameof(InfoPageViewModel.HasEnvironmentScanErrors), StringComparison.Ordinal)
                || propertyName.Equals(nameof(InfoPageViewModel.EnvironmentScanPresentationRevision), StringComparison.Ordinal))
            {
                Dispatcher.UIThread.Post(RefreshEnvironmentOverviewChrome, DispatcherPriority.Background);
            }
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

        internal void RefreshEnvironmentOverviewChrome()
        {
            if (EnvironmentOverviewInfoBar == null || _viewModel?.Info == null)
            {
                return;
            }

            var info = _viewModel.Info;

            EnvironmentOverviewInfoBar.MessageTextAlignment = TextAlignment.Left;
            EnvironmentOverviewInfoBar.IsClosable = false;
            EnvironmentOverviewInfoBar.IsVisible = true;

            if (info.HasEnvironmentScanErrors)
            {
                EnvironmentOverviewInfoBar.Severity = NotificationType.Error;
                EnvironmentOverviewInfoBar.Title = "存在未通过的检查项";
                EnvironmentOverviewInfoBar.Message = string.IsNullOrWhiteSpace(info.EnvironmentSummary)
                    ? "请对照下方固定检测项查看未通过条目。"
                    : info.EnvironmentSummary.Trim();
            }
            else if (info.HasAnyEnvironmentScanWarning())
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
    }
}
