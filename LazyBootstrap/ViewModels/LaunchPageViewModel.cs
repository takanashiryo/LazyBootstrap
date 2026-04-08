using System;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LazyBootstrap.ViewModels
{
    public partial class LaunchPageViewModel : ObservableObject
    {
        private readonly ILaunchWorkflowService _workflowService;
        private SettingsPageViewModel _settingsViewModel;
        private DisplayConfigurationPageViewModel _displayViewModel;

        public LaunchPageViewModel()
        {
        }

        public LaunchPageViewModel(ILaunchWorkflowService workflowService)
        {
            _workflowService = workflowService;
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
            return _workflowService?.InitializeStartupAsync(this, _settingsViewModel, _displayViewModel) ?? Task.CompletedTask;
        }

        public Task HandleClosingAsync()
        {
            return _workflowService?.HandleClosingAsync(_displayViewModel) ?? Task.CompletedTask;
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
        private Task ToggleLaunchLogAsync() => _workflowService?.ToggleLaunchLogAsync(this) ?? Task.CompletedTask;

        [RelayCommand]
        private Task GoToSettingsAsync() => _workflowService?.NavigateToSettingsAsync() ?? Task.CompletedTask;

        [RelayCommand]
        private Task OpenLogAsync() => _workflowService?.OpenLogAsync() ?? Task.CompletedTask;

        [RelayCommand]
        private Task KillProcessesAsync() => _workflowService?.KillProcessesAsync() ?? Task.CompletedTask;

        [RelayCommand]
        private Task StartAsync()
        {
            if (!CanStartLaunch)
            {
                return Task.CompletedTask;
            }

            return _workflowService?.StartAsync(this, _settingsViewModel, _displayViewModel, false) ?? Task.CompletedTask;
        }

        [RelayCommand]
        private Task StartAsphyxiaDevAsync()
        {
            if (!CanStartLaunch)
            {
                return Task.CompletedTask;
            }

            return _workflowService?.StartAsync(this, _settingsViewModel, _displayViewModel, true) ?? Task.CompletedTask;
        }
    }
}
