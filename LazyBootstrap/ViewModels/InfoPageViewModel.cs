using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LazyBootstrap.Services.Environment;

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

        public EnvironmentScanDisplayRow CpuPrimaryRow { get; } = new();

        public ObservableCollection<EnvironmentScanDisplayRow> GpuAdapterRows { get; } =
            new ObservableCollection<EnvironmentScanDisplayRow>();

        public EnvironmentScanDisplayRow NvidiaSkipNoticeRow { get; } = new();

        public EnvironmentScanLineOutcome NvidiaNvcuda { get; } = new();

        public EnvironmentScanLineOutcome NvidiaNvcuvid { get; } = new();

        public EnvironmentScanLineOutcome NvidiaEncodeApi { get; } = new();

        [ObservableProperty]
        private bool nvidiaDetailVisible;

        public EnvironmentScanDisplayRow DirectXRuntimeFaultRow { get; } = new();

        public EnvironmentScanLineOutcome DirectXD3d9 { get; } = new();

        public EnvironmentScanLineOutcome DirectXD3Dx43 { get; } = new();

        public EnvironmentScanDisplayRow MediaPackRuntimeFaultRow { get; } = new();

        public EnvironmentScanLineOutcome MediaPackMf { get; } = new();

        public EnvironmentScanLineOutcome MediaPackMfplat { get; } = new();

        public EnvironmentScanLineOutcome MediaPackWmvCore { get; } = new();

        public EnvironmentScanDisplayRow Vc2010X86RuntimeFaultRow { get; } = new();

        public EnvironmentScanLineOutcome Vc2010X86Msvcr { get; } = new();

        public EnvironmentScanLineOutcome Vc2010X86Msvcp { get; } = new();

        public EnvironmentScanDisplayRow Vc2010X64RuntimeFaultRow { get; } = new();

        public EnvironmentScanLineOutcome Vc2010X64Msvcr { get; } = new();

        public EnvironmentScanLineOutcome Vc2010X64Msvcp { get; } = new();

        public ObservableCollection<string> ScanRootAlerts { get; } = new ObservableCollection<string>();

        [ObservableProperty]
        private bool hasScanRootAlerts;

        [ObservableProperty]
        private bool scanUiReady;

        public bool ScanUiPendingHintVisible => !ScanUiReady;

        partial void OnScanUiReadyChanged(bool value)
        {
            OnPropertyChanged(nameof(ScanUiPendingHintVisible));
        }

        [ObservableProperty]
        private string machineProperty = string.Empty;

        [ObservableProperty]
        private string gameVersion = string.Empty;

        [ObservableProperty]
        private string launcherVersion = string.Empty;

        [ObservableProperty]
        private string environmentSummary = string.Empty;

        [ObservableProperty]
        private bool hasEnvironmentScanErrors;

        [ObservableProperty]
        private int environmentScanPresentationRevision;

        public Task InitializeInfoAsync()
        {
            return _workflowService?.InitializeInfoAsync(this) ?? Task.CompletedTask;
        }

        public Task RunEnvironmentScanAsync()
        {
            return _workflowService?.RunScanAsync(this) ?? Task.CompletedTask;
        }

        [RelayCommand]
        private async Task RefreshEnvironmentScanAsync()
        {
            await RunEnvironmentScanAsync();
        }

        public void NotifyScanPresentationChanged()
        {
            EnvironmentScanPresentationRevision++;
        }

        public bool HasAnyEnvironmentScanWarning()
        {
            return WarningsFromRows().Any(r => r is { IsShown: true, BadgeLevel: EnvironmentScan.ScanResultLevel.Warning })
                || GpuAdapterRows.Any(r => r is { IsShown: true, BadgeLevel: EnvironmentScan.ScanResultLevel.Warning })
                || WarningsFromOutcomes().Any(o => o is { OutcomeVisible: true, BadgeLevel: EnvironmentScan.ScanResultLevel.Warning });
        }

        private EnvironmentScanDisplayRow[] _cachedWarningRows;
        private EnvironmentScanLineOutcome[] _cachedWarningOutcomes;

        private EnvironmentScanDisplayRow[] WarningsFromRows() =>
            _cachedWarningRows ??= [CpuPrimaryRow, NvidiaSkipNoticeRow, DirectXRuntimeFaultRow, MediaPackRuntimeFaultRow,
                                     Vc2010X86RuntimeFaultRow, Vc2010X64RuntimeFaultRow];

        private EnvironmentScanLineOutcome[] WarningsFromOutcomes() =>
            _cachedWarningOutcomes ??= [NvidiaNvcuda, NvidiaNvcuvid, NvidiaEncodeApi, DirectXD3d9, DirectXD3Dx43,
                                         MediaPackMf, MediaPackMfplat, MediaPackWmvCore, Vc2010X86Msvcr, Vc2010X86Msvcp,
                                         Vc2010X64Msvcr, Vc2010X64Msvcp];
    }
}
