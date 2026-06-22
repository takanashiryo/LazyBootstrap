using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LazyBootstrap.Shell
{
    public partial class MainWindow
    {
        private async void OnApplyUpdateClick(object sender, RoutedEventArgs e)
        {
            await _updateWorkflowService.ApplyUpdateFromUserSelectedArchiveAsync();
        }
    }
}
