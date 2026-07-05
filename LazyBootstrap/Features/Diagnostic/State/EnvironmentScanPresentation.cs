using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LazyBootstrap.Features.Diagnostic
{
    public sealed class EnvironmentScanPresentation
    {
        public EnvironmentScanDisplayRow CpuPrimaryRow { get; } = new EnvironmentScanDisplayRow();

        public ObservableCollection<EnvironmentScanDisplayRow> GpuAdapterRows { get; } =
            new ObservableCollection<EnvironmentScanDisplayRow>();

        public EnvironmentScanDisplayRow NvidiaSkipNoticeRow { get; } = new EnvironmentScanDisplayRow();

        public EnvironmentScanLineOutcome NvidiaNvcuda { get; } = new EnvironmentScanLineOutcome();

        public EnvironmentScanLineOutcome NvidiaNvcuvid { get; } = new EnvironmentScanLineOutcome();

        public EnvironmentScanLineOutcome NvidiaEncodeApi { get; } = new EnvironmentScanLineOutcome();

        public bool NvidiaDetailVisible { get; set; }

        public EnvironmentScanDisplayRow DirectXRuntimeFaultRow { get; } = new EnvironmentScanDisplayRow();

        public EnvironmentScanLineOutcome DirectXD3d9 { get; } = new EnvironmentScanLineOutcome();

        public EnvironmentScanLineOutcome DirectXD3Dx43 { get; } = new EnvironmentScanLineOutcome();

        public EnvironmentScanDisplayRow MediaPackRuntimeFaultRow { get; } = new EnvironmentScanDisplayRow();

        public EnvironmentScanLineOutcome MediaPackMf { get; } = new EnvironmentScanLineOutcome();

        public EnvironmentScanLineOutcome MediaPackMfplat { get; } = new EnvironmentScanLineOutcome();

        public EnvironmentScanLineOutcome MediaPackWmvCore { get; } = new EnvironmentScanLineOutcome();

        public EnvironmentScanDisplayRow Vc2010X86RuntimeFaultRow { get; } = new EnvironmentScanDisplayRow();

        public EnvironmentScanLineOutcome Vc2010X86Msvcr { get; } = new EnvironmentScanLineOutcome();

        public EnvironmentScanLineOutcome Vc2010X86Msvcp { get; } = new EnvironmentScanLineOutcome();

        public EnvironmentScanDisplayRow Vc2010X64RuntimeFaultRow { get; } = new EnvironmentScanDisplayRow();

        public EnvironmentScanLineOutcome Vc2010X64Msvcr { get; } = new EnvironmentScanLineOutcome();

        public EnvironmentScanLineOutcome Vc2010X64Msvcp { get; } = new EnvironmentScanLineOutcome();

        public ObservableCollection<string> ScanRootAlerts { get; } = new ObservableCollection<string>();

        public bool HasScanRootAlerts { get; set; }

        public bool ScanUiReady { get; set; }

        public bool ScanUiPendingHintVisible => !ScanUiReady;

        public string MachineProperty { get; set; } = string.Empty;

        public string GameVersion { get; set; } = string.Empty;

        public string OperatingSystemVersionName { get; set; } = string.Empty;

        public string OperatingSystemBuildNumber { get; set; } = string.Empty;

        public string LauncherVersion { get; set; } = string.Empty;

        public string EnvironmentSummary { get; set; } = string.Empty;

        public bool HasEnvironmentScanErrors { get; set; }

        public int EnvironmentScanPresentationRevision { get; private set; }

        public void NotifyScanPresentationChanged()
        {
            EnvironmentScanPresentationRevision++;
        }

        public bool HasAnyEnvironmentScanWarning()
        {
            return WarningRows().Any(r => r is { IsShown: true, BadgeLevel: EnvironmentScan.ScanResultLevel.Warning })
                || GpuAdapterRows.Any(r => r is { IsShown: true, BadgeLevel: EnvironmentScan.ScanResultLevel.Warning })
                || WarningOutcomes().Any(o => o is { OutcomeVisible: true, BadgeLevel: EnvironmentScan.ScanResultLevel.Warning });
        }

        private IEnumerable<EnvironmentScanDisplayRow> WarningRows()
        {
            yield return CpuPrimaryRow;
            yield return NvidiaSkipNoticeRow;
            yield return DirectXRuntimeFaultRow;
            yield return MediaPackRuntimeFaultRow;
            yield return Vc2010X86RuntimeFaultRow;
            yield return Vc2010X64RuntimeFaultRow;
        }

        private IEnumerable<EnvironmentScanLineOutcome> WarningOutcomes()
        {
            yield return NvidiaNvcuda;
            yield return NvidiaNvcuvid;
            yield return NvidiaEncodeApi;
            yield return DirectXD3d9;
            yield return DirectXD3Dx43;
            yield return MediaPackMf;
            yield return MediaPackMfplat;
            yield return MediaPackWmvCore;
            yield return Vc2010X86Msvcr;
            yield return Vc2010X86Msvcp;
            yield return Vc2010X64Msvcr;
            yield return Vc2010X64Msvcp;
        }
    }

    public sealed class EnvironmentScanDisplayRow
    {
        public string PrimaryText { get; set; } = string.Empty;

        public string SecondaryText { get; set; } = string.Empty;

        public bool SecondaryVisible { get; set; }

        public bool ShowStatusBadge { get; set; } = true;

        public string StatusText { get; set; } = string.Empty;

        public EnvironmentScan.ScanResultLevel BadgeLevel { get; set; } = EnvironmentScan.ScanResultLevel.Success;

        public bool IsShown { get; set; }

        internal void ApplyResult(
            string primary,
            string secondary,
            bool secondaryShown,
            bool showBadge,
            EnvironmentScan.ScanResultLevel level,
            string badgeText)
        {
            PrimaryText = primary ?? string.Empty;
            SecondaryText = secondary ?? string.Empty;
            SecondaryVisible = secondaryShown;
            ShowStatusBadge = showBadge;
            BadgeLevel = level;
            StatusText = badgeText ?? string.Empty;
            IsShown = true;
        }

        internal void Hide()
        {
            IsShown = false;
            SecondaryVisible = false;
            ShowStatusBadge = false;
            PrimaryText = string.Empty;
            SecondaryText = string.Empty;
            StatusText = string.Empty;
            BadgeLevel = EnvironmentScan.ScanResultLevel.Success;
        }
    }

    public sealed class EnvironmentScanLineOutcome
    {
        public EnvironmentScan.ScanResultLevel BadgeLevel { get; set; } = EnvironmentScan.ScanResultLevel.Success;

        public string StatusText { get; set; } = string.Empty;

        public bool OutcomeVisible { get; set; }

        internal void Apply(EnvironmentScan.ScanResultLevel level, string badgeText)
        {
            BadgeLevel = level;
            StatusText = badgeText ?? string.Empty;
            OutcomeVisible = true;
        }

        internal void Hide()
        {
            OutcomeVisible = false;
            StatusText = string.Empty;
            BadgeLevel = EnvironmentScan.ScanResultLevel.Success;
        }
    }
}
