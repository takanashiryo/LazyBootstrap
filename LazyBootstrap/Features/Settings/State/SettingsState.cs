using System;
using System.Collections.ObjectModel;

namespace LazyBootstrap.Features.Settings
{
    public sealed class SettingsState
    {
        private bool _suspendPersistence;

        public ObservableCollection<ServerPresetItem> ServerPresets { get; } = new ObservableCollection<ServerPresetItem>();

        public ObservableCollection<NetworkAdapterOption> NetworkAdapters { get; } = new ObservableCollection<NetworkAdapterOption>();

        public ObservableCollection<AsioDriverOption> AsioDrivers { get; } = new ObservableCollection<AsioDriverOption>();

        public bool NoAsphyxia { get; set; }

        public bool DisableSpiceFso { get; set; }

        public bool UseSystemSpiceConfig { get; set; }

        public bool GpuCompatLayerEnabled { get; set; }

        public bool ExitRestore { get; set; } = true;

        public bool IsSpiceConfigAvailable { get; set; } = true;

        public string SpiceConfigEmptyStateMessage { get; set; } = "未找到任何spice2x配置文件";

        public string ActiveServerPreset { get; set; } = string.Empty;

        public string ServerAddress { get; set; } = string.Empty;

        public string PcbId { get; set; } = string.Empty;

        public ServerPresetItem SelectedServerPreset { get; set; }

        public string NetworkAdapterIp { get; set; } = string.Empty;

        public string NetworkAdapterSubnet { get; set; } = string.Empty;

        public NetworkAdapterOption SelectedNetworkAdapter { get; set; }

        public string GpuCompatLayerRenderMode { get; set; } = "dx9on12";

        public bool Windowed { get; set; }

        public string DllInjection { get; set; } = string.Empty;

        public bool NetDump { get; set; }

        public bool DisableSubDisplay { get; set; }

        public int WindowModeIndex { get; set; }

        public bool PCoreOptimization { get; set; }

        public bool SubBorderless { get; set; }

        public bool ShowCursorTouchSim { get; set; }

        public bool WindowTopMost { get; set; }

        public string WindowSize { get; set; } = string.Empty;

        public bool SingleAdapter { get; set; }

        public bool NvidiaPerformanceProfile { get; set; }

        public bool SubWindowTopMost { get; set; }

        public bool SubForceRender { get; set; }

        public bool NativeTouch { get; set; }

        public string AsioDriverValue { get; set; } = string.Empty;

        public AsioDriverOption SelectedAsioDriver { get; set; }

        public bool Asio2Ch { get; set; }

        public int VolumeBoostIndex { get; set; }

        public int ResampleIndex { get; set; }

        public bool WasapiShared { get; set; }

        public bool LowLatencySharedAudio { get; set; }

        public bool CardIo { get; set; }

        public bool HidSmartCard { get; set; }

        public bool IsSuspended => _suspendPersistence;

        public string AsioDriver
        {
            get => AsioDriverValue;
            set => AsioDriverValue = value ?? string.Empty;
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
    }

    public sealed record SettingsPersistRequest(SettingsState Settings);
}
