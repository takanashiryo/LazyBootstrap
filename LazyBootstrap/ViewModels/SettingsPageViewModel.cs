using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LazyBootstrap.ViewModels
{
    public partial class SettingsPageViewModel : ObservableObject
    {
        private readonly ISettingsWorkflowService _workflowService;
        private bool _suspendPersistence;

        public SettingsPageViewModel()
        {
        }

        public SettingsPageViewModel(ISettingsWorkflowService workflowService)
        {
            _workflowService = workflowService;
        }

        public ObservableCollection<ServerPresetItem> ServerPresets { get; } = new ObservableCollection<ServerPresetItem>();

        public ObservableCollection<NetworkAdapterOption> NetworkAdapters { get; } = new ObservableCollection<NetworkAdapterOption>();

        public ObservableCollection<AsioDriverOption> AsioDrivers { get; } = new ObservableCollection<AsioDriverOption>();

        [ObservableProperty]
        private bool noAsphyxia;

        [ObservableProperty]
        private bool compatibilityLayerEnabled;

        [ObservableProperty]
        private bool exitRestore = true;

        [ObservableProperty]
        private bool isSpiceConfigAvailable = true;

        [ObservableProperty]
        private string spiceConfigEmptyStateMessage = "未找到任何spice2x配置文件";

        [ObservableProperty]
        private bool isSettingsBusy;

        [ObservableProperty]
        private string activeServerPreset = string.Empty;

        [ObservableProperty]
        private string serverAddress = string.Empty;

        [ObservableProperty]
        private string pcbId = string.Empty;

        [ObservableProperty]
        private ServerPresetItem selectedServerPreset;

        [ObservableProperty]
        private string networkAdapterIp = string.Empty;

        [ObservableProperty]
        private string networkAdapterSubnet = string.Empty;

        [ObservableProperty]
        private NetworkAdapterOption selectedNetworkAdapter;

        [ObservableProperty]
        private string compatibilityRenderMode = "dx9on12";

        [ObservableProperty]
        private bool windowed;

        [ObservableProperty]
        private string dllInjection = string.Empty;

        [ObservableProperty]
        private bool netDump;

        [ObservableProperty]
        private bool disableSubDisplay;

        [ObservableProperty]
        private int windowModeIndex;

        [ObservableProperty]
        private bool pCoreOptimization;

        [ObservableProperty]
        private bool subBorderless;

        [ObservableProperty]
        private bool showCursorTouchSim;

        [ObservableProperty]
        private bool windowTopMost;

        [ObservableProperty]
        private string windowSize = string.Empty;

        [ObservableProperty]
        private bool singleAdapter;

        [ObservableProperty]
        private bool nvidiaPerformanceProfile;

        [ObservableProperty]
        private bool subWindowTopMost;

        [ObservableProperty]
        private bool subForceRender;

        [ObservableProperty]
        private bool nativeTouch;

        [ObservableProperty]
        private string asioDriverValue = string.Empty;

        [ObservableProperty]
        private AsioDriverOption selectedAsioDriver;

        [ObservableProperty]
        private bool lowLatencySharedAudio;

        [ObservableProperty]
        private bool cardIo;

        [ObservableProperty]
        private bool hidSmartCard;

        public bool IsSuspended => _suspendPersistence;

        public string AsioDriver
        {
            get => AsioDriverValue;
            set => AsioDriverValue = value ?? string.Empty;
        }

        public bool IsCompatibilityDx9on12Selected
        {
            get => string.Equals(
                CompatibilitySettingsService.NormalizeRenderMode(CompatibilityRenderMode),
                "dx9on12",
                StringComparison.OrdinalIgnoreCase);
            set
            {
                if (value)
                {
                    CompatibilityRenderMode = "dx9on12";
                }
            }
        }

        public bool IsCompatibilityDx9on12ExternalSelected
        {
            get => string.Equals(
                CompatibilitySettingsService.NormalizeRenderMode(CompatibilityRenderMode),
                "dx9on12_external",
                StringComparison.OrdinalIgnoreCase);
            set
            {
                if (value)
                {
                    CompatibilityRenderMode = "dx9on12_external";
                }
            }
        }

        public bool IsCompatibilityDxvkSelected
        {
            get => string.Equals(
                CompatibilitySettingsService.NormalizeRenderMode(CompatibilityRenderMode),
                "dxvk",
                StringComparison.OrdinalIgnoreCase);
            set
            {
                if (value)
                {
                    CompatibilityRenderMode = "dxvk";
                }
            }
        }

        public Task InitializeStartupAsync()
        {
            return _workflowService?.InitializeStartupAsync(this) ?? Task.CompletedTask;
        }

        public Task WarmDeferredAsync()
        {
            return _workflowService?.WarmDeferredAsync(this) ?? Task.CompletedTask;
        }

        public void RunSilently(Action action)
        {
            _suspendPersistence = true;
            try
            {
                action?.Invoke();
            }
            finally
            {
                _suspendPersistence = false;
            }
        }

        public Task PersistLauncherSettingsAsync()
        {
            return _workflowService?.PersistLauncherSettingsAsync(this) ?? Task.CompletedTask;
        }

        public Task PersistSelectedServerPresetAsync()
        {
            return _workflowService?.PersistSelectedServerPresetAsync(this) ?? Task.CompletedTask;
        }

        public Task PersistServerEndpointAsync()
        {
            return _workflowService?.PersistServerEndpointAsync(this) ?? Task.CompletedTask;
        }

        public Task ApplySelectedNetworkAdapterAsync()
        {
            return _workflowService?.ApplySelectedNetworkAdapterAsync(this) ?? Task.CompletedTask;
        }

        public Task PersistNetworkSettingsAsync()
        {
            return _workflowService?.PersistNetworkSettingsAsync(this) ?? Task.CompletedTask;
        }

        public Task PersistSpiceSettingsAsync()
        {
            return _workflowService?.PersistSpiceSettingsAsync(this) ?? Task.CompletedTask;
        }

        public Task PersistCompatibilityToggleAsync()
        {
            return _workflowService?.PersistCompatibilityToggleAsync(this) ?? Task.CompletedTask;
        }

        public Task PersistCompatibilityRenderModeAsync()
        {
            return _workflowService?.PersistCompatibilityRenderModeAsync(this) ?? Task.CompletedTask;
        }

        partial void OnCompatibilityRenderModeChanged(string value)
        {
            OnPropertyChanged(nameof(IsCompatibilityDx9on12Selected));
            OnPropertyChanged(nameof(IsCompatibilityDx9on12ExternalSelected));
            OnPropertyChanged(nameof(IsCompatibilityDxvkSelected));
        }

        [RelayCommand]
        private Task EditConfigAsync() => _workflowService?.EditConfigAsync(this) ?? Task.CompletedTask;

        [RelayCommand]
        private Task ImportRecommendedConfigAsync() => _workflowService?.ImportRecommendedConfigAsync(this) ?? Task.CompletedTask;

        [RelayCommand]
        private async Task EnableCompatibilityLayerAsync()
        {
            CompatibilityLayerEnabled = true;
            if (_workflowService != null)
            {
                await PersistCompatibilityToggleAsync();
            }
        }

        [RelayCommand]
        private async Task DisableCompatibilityLayerAsync()
        {
            CompatibilityLayerEnabled = false;
            if (_workflowService != null)
            {
                await PersistCompatibilityToggleAsync();
            }
        }

        [RelayCommand]
        private async Task SelectCompatibilityRenderModeAsync(string renderMode)
        {
            var normalizedRenderMode = CompatibilitySettingsService.NormalizeRenderMode(renderMode);
            if (string.Equals(
                    CompatibilitySettingsService.NormalizeRenderMode(CompatibilityRenderMode),
                    normalizedRenderMode,
                    StringComparison.OrdinalIgnoreCase))
            {
                CompatibilityRenderMode = normalizedRenderMode;
                return;
            }

            CompatibilityRenderMode = normalizedRenderMode;
            if (_workflowService != null)
            {
                await PersistCompatibilityRenderModeAsync();
            }
        }

        [RelayCommand]
        private Task OpenNetworkAdapterPickerAsync() => _workflowService?.OpenNetworkAdapterPickerAsync(this) ?? Task.CompletedTask;

        [RelayCommand]
        private Task OpenAsioControlPanelAsync() => _workflowService?.OpenAsioControlPanelAsync(this) ?? Task.CompletedTask;

        [RelayCommand]
        private Task AddServerPresetAsync() => _workflowService?.AddServerPresetAsync(this) ?? Task.CompletedTask;

        [RelayCommand]
        private Task DeleteServerPresetAsync() => _workflowService?.DeleteServerPresetAsync(this) ?? Task.CompletedTask;
    }
}
