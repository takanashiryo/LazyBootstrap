using Avalonia.Interactivity;

namespace LazyBootstrap.Shell
{
    public partial class MainWindow
    {
        private async void OnApplyUpdateClick(object sender, RoutedEventArgs e)
        {
            using var busy = BeginBusy(BusyPresentation.GlobalOverlay, "正在选择更新压缩包...");
            await _updateWorkflowService.ApplyUpdateFromUserSelectedArchiveAsync(busy.UpdateText);
        }
    }
}
