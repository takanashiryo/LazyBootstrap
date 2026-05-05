using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LazyBootstrap.ViewModels
{
    public partial class LaunchPageViewModel : ObservableObject
    {
        private ILaunchWorkflowService _workflowService;
        private SettingsPageViewModel _settingsViewModel;
        private DisplayConfigurationPageViewModel _displayViewModel;

        public LaunchPageViewModel()
        {
            if (!Design.IsDesignMode)
            {
                throw new InvalidOperationException("LaunchPageViewModel requires dependency injection outside of the Avalonia designer.");
            }
        }

        public LaunchPageViewModel(ILaunchWorkflowService workflowService)
        {
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
        }

        [ObservableProperty]
        private string launchLogText = string.Empty;

        [ObservableProperty]
        private bool isLaunchLogVisible;

        [ObservableProperty]
        private string toggleLaunchLogText = "显示启动日志";

        [ObservableProperty]
        private string stateText = "就绪";

        [ObservableProperty]
        private bool isLaunching;

        [ObservableProperty]
        private bool isGameRunning;

        [ObservableProperty]
        private bool isMessageVisible;

        [ObservableProperty]
        private NotificationType messageType = NotificationType.Error;

        [ObservableProperty]
        private string messageTitle = string.Empty;

        [ObservableProperty]
        private string messageAccentText = string.Empty;

        [ObservableProperty]
        private string messageBodyText = string.Empty;

        public bool CanStartLaunch => !IsLaunching && !IsGameRunning;

        public void AttachContext(SettingsPageViewModel settingsViewModel, DisplayConfigurationPageViewModel displayViewModel)
        {
            _settingsViewModel = settingsViewModel;
            _displayViewModel = displayViewModel;
        }

        public Task InitializeStartupAsync()
        {
            if (_workflowService is null)
            {
                return Task.CompletedTask;
            }

            return _workflowService.InitializeStartupAsync(this, _settingsViewModel, _displayViewModel);
        }

        public Task HandleClosingAsync()
        {
            if (_workflowService is null)
            {
                return Task.CompletedTask;
            }

            return _workflowService.HandleClosingAsync(_displayViewModel);
        }

        partial void OnIsLaunchingChanged(bool value)
        {
            OnPropertyChanged(nameof(CanStartLaunch));
        }

        partial void OnIsGameRunningChanged(bool value)
        {
            OnPropertyChanged(nameof(CanStartLaunch));
        }

        [RelayCommand]
        private Task ToggleLaunchLogAsync() => _workflowService is null ? Task.CompletedTask : _workflowService.ToggleLaunchLogAsync(this);

        [RelayCommand]
        private Task GoToSettingsAsync() => _workflowService is null ? Task.CompletedTask : _workflowService.NavigateToSettingsAsync();

        [RelayCommand]
        private Task OpenLogAsync() => _workflowService is null ? Task.CompletedTask : _workflowService.OpenLogAsync();

        [RelayCommand]
        private Task KillProcessesAsync() => _workflowService is null ? Task.CompletedTask : _workflowService.KillProcessesAsync();

        [RelayCommand]
        private Task StartAsync()
        {
            if (!CanStartLaunch || _workflowService is null)
            {
                return Task.CompletedTask;
            }

            return _workflowService.StartAsync(this, _settingsViewModel, _displayViewModel, false);
        }

        [RelayCommand]
        private Task StartAsphyxiaDevAsync()
        {
            if (!CanStartLaunch || _workflowService is null)
            {
                return Task.CompletedTask;
            }

            return _workflowService.StartAsync(this, _settingsViewModel, _displayViewModel, true);
        }
    }
}
