using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LazyBootstrap.ViewModels
{
    public partial class DisplayConfigurationPageViewModel : ObservableObject
    {
        private readonly IDisplayWorkflowService _workflowService;
        private bool _suspendUpdates;

        public DisplayConfigurationPageViewModel()
        {
        }

        public DisplayConfigurationPageViewModel(IDisplayWorkflowService workflowService)
        {
            _workflowService = workflowService;
        }

        public ObservableCollection<DisplayChoiceOption> Displays { get; } = new ObservableCollection<DisplayChoiceOption>();

        public ObservableCollection<RotationOption> Rotations { get; } = new ObservableCollection<RotationOption>();

        public ObservableCollection<string> MainResolutions { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> SubResolutions { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> MainRefreshRates { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> SubRefreshRates { get; } = new ObservableCollection<string>();

        [ObservableProperty]
        private bool isDisplayConfigurationEnabled;

        [ObservableProperty]
        private bool isDualDisplay;

        [ObservableProperty]
        private bool exitRestore = true;

        [ObservableProperty]
        private DisplayChoiceOption selectedMainDisplay;

        [ObservableProperty]
        private DisplayChoiceOption selectedSubDisplay;

        [ObservableProperty]
        private RotationOption selectedMainRotation;

        [ObservableProperty]
        private RotationOption selectedSubRotation;

        [ObservableProperty]
        private string selectedMainResolution = string.Empty;

        [ObservableProperty]
        private string selectedSubResolution = string.Empty;

        [ObservableProperty]
        private string selectedMainRefreshRate = string.Empty;

        [ObservableProperty]
        private string selectedSubRefreshRate = string.Empty;

        [ObservableProperty]
        private string mainOutputInfo = string.Empty;

        [ObservableProperty]
        private string subOutputInfo = string.Empty;

        [ObservableProperty]
        private string mainStartupInfo = string.Empty;

        [ObservableProperty]
        private string subStartupInfo = string.Empty;

        public string MainDisplayInfo
        {
            get => MainStartupInfo;
            set => MainStartupInfo = value ?? string.Empty;
        }

        public string SubDisplayInfo
        {
            get => SubStartupInfo;
            set => SubStartupInfo = value ?? string.Empty;
        }

        [ObservableProperty]
        private string mainDiagnosticsTooltip = string.Empty;

        [ObservableProperty]
        private string subDiagnosticsTooltip = string.Empty;

        [ObservableProperty]
        private DisplaySelectionTarget selectedTarget = DisplaySelectionTarget.None;

        [ObservableProperty]
        private bool showNoScreenSelected = true;

        [ObservableProperty]
        private bool showMainScreenConfig;

        [ObservableProperty]
        private bool showSubScreenConfig;

        public bool IsSuspended => _suspendUpdates;

        public Task WarmDeferredAsync()
        {
            return _workflowService?.WarmDeferredAsync(this) ?? Task.CompletedTask;
        }

        public Task PersistGeneralSettingsAsync()
        {
            return _workflowService?.PersistGeneralSettingsAsync(this) ?? Task.CompletedTask;
        }

        public Task HandleConfigurationChangedAsync(bool refreshMainOptions = true, bool refreshSubOptions = true)
        {
            return _workflowService?.HandleConfigurationChangedAsync(this, refreshMainOptions, refreshSubOptions) ?? Task.CompletedTask;
        }

        public void RunSilently(Action action)
        {
            _suspendUpdates = true;
            try
            {
                action?.Invoke();
            }
            finally
            {
                _suspendUpdates = false;
            }

            OnPropertyChanged(string.Empty);
        }

        [RelayCommand]
        private Task PreviewDisplaySettingsAsync() => _workflowService?.PreviewDisplaySettingsAsync(this) ?? Task.CompletedTask;

        [RelayCommand]
        private Task SelectMainDisplayAsync()
        {
            SelectedTarget = DisplaySelectionTarget.Main;
            ShowNoScreenSelected = false;
            ShowMainScreenConfig = true;
            ShowSubScreenConfig = false;
            return Task.CompletedTask;
        }

        [RelayCommand]
        private Task SelectSubDisplayAsync()
        {
            if (!IsDualDisplay)
            {
                SelectedTarget = DisplaySelectionTarget.None;
                ShowNoScreenSelected = true;
                ShowMainScreenConfig = false;
                ShowSubScreenConfig = false;
                return Task.CompletedTask;
            }

            SelectedTarget = DisplaySelectionTarget.Sub;
            ShowNoScreenSelected = false;
            ShowMainScreenConfig = false;
            ShowSubScreenConfig = true;
            return Task.CompletedTask;
        }

        [RelayCommand]
        private Task OpenTouchPanelAsync() => _workflowService?.OpenTouchPanelAsync() ?? Task.CompletedTask;
    }
}
