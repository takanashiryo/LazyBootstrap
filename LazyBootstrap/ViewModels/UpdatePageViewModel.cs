using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LazyBootstrap.Services.Update;

namespace LazyBootstrap.ViewModels
{
    public partial class UpdatePageViewModel : ObservableObject
    {
        private readonly IUpdateWorkflowService _workflowService;

        public UpdatePageViewModel()
        {
        }

        public UpdatePageViewModel(IUpdateWorkflowService workflowService)
        {
            _workflowService = workflowService;
        }

        [ObservableProperty]
        private bool isUpdateBusy;

        public bool CanApplyUpdate => !IsUpdateBusy;

        partial void OnIsUpdateBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(CanApplyUpdate));
            ApplyUpdateCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanApplyUpdate))]
        private Task ApplyUpdateAsync() =>
            _workflowService?.ApplyUpdateFromUserSelectedArchiveAsync(this) ?? Task.CompletedTask;
    }
}
