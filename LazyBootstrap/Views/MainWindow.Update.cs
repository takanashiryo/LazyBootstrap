using Avalonia.Interactivity;
using LazyBootstrap.Services;

namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private async void OnApplyUpdateClick(object sender, RoutedEventArgs e)
        {
            await _updateWorkflowService.ApplyUpdateFromUserSelectedArchiveAsync();
        }
    }
}
