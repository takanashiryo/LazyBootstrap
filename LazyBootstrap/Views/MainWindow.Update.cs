using Avalonia.Interactivity;
using LazyBootstrap.Services;

namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private bool _isUpdateBusy;

        private async void OnApplyUpdateClick(object sender, RoutedEventArgs e)
        {
            if (_isUpdateBusy) return;
            _isUpdateBusy = true;
            if (SelectUpdatePackageButton != null) SelectUpdatePackageButton.IsEnabled = false;

            await AppServices.UpdateWorkflow.ApplyUpdateFromUserSelectedArchiveAsync(
                busy => _isUpdateBusy = busy);

            if (SelectUpdatePackageButton != null) SelectUpdatePackageButton.IsEnabled = !_isUpdateBusy;
        }
    }
}
