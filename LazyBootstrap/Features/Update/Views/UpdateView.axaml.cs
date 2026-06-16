using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LazyBootstrap.Features.Update.Views
{
    public partial class UpdateView : UserControl
    {
        private readonly UpdateOrchestrator _updateWorkflowService = null!;

        public UpdateView()
        {
            InitializeComponent();
        }

        public UpdateView(UpdateOrchestrator updateOrchestrator)
        {
            InitializeComponent();
            _updateWorkflowService = updateOrchestrator ?? throw new ArgumentNullException(nameof(updateOrchestrator));
        }

        private async void OnApplyUpdateClick(object sender, RoutedEventArgs e)
        {
            await _updateWorkflowService.ApplyUpdateFromUserSelectedArchiveAsync();
        }
    }
}
