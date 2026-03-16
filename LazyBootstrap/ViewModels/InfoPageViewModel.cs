using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LazyBootstrap.ViewModels
{
    public partial class InfoPageViewModel : ObservableObject
    {
        private readonly IEnvironmentScanService _workflowService;

        public InfoPageViewModel()
        {
        }

        public InfoPageViewModel(IEnvironmentScanService workflowService)
        {
            _workflowService = workflowService;
        }

        public ObservableCollection<EnvironmentScanGroup> Groups { get; } = new ObservableCollection<EnvironmentScanGroup>();

        [ObservableProperty]
        private string machineProperty = string.Empty;

        [ObservableProperty]
        private string gameVersion = string.Empty;

        [ObservableProperty]
        private string launcherVersion = string.Empty;

        [ObservableProperty]
        private string environmentSummary = string.Empty;

        public Task InitializeInfoAsync()
        {
            return _workflowService?.InitializeInfoAsync(this) ?? Task.CompletedTask;
        }

        public Task RunEnvironmentScanAsync()
        {
            return _workflowService?.RunScanAsync(this) ?? Task.CompletedTask;
        }

        [RelayCommand]
        private Task RunEnvironmentScanCommandAsync() => RunEnvironmentScanAsync();
    }
}
