using System.Collections.Generic;
using System.Linq;
using LazyBootstrap.Features.Diagnostic.Services;

namespace LazyBootstrap.Features.Diagnostic
{
    internal sealed class EnvironmentScanResult
    {
        public EnvironmentScanResultRow CpuPrimaryRow { get; } = new EnvironmentScanResultRow();

        public List<EnvironmentScanResultRow> GpuAdapterRows { get; } = new List<EnvironmentScanResultRow>();

        public EnvironmentScanResultRow NvidiaSkipNoticeRow { get; } = new EnvironmentScanResultRow();

        public EnvironmentScanResultOutcome NvidiaNvcuda { get; } = new EnvironmentScanResultOutcome();

        public EnvironmentScanResultOutcome NvidiaNvcuvid { get; } = new EnvironmentScanResultOutcome();

        public EnvironmentScanResultOutcome NvidiaEncodeApi { get; } = new EnvironmentScanResultOutcome();

        public bool NvidiaDetailVisible { get; set; }

        public EnvironmentScanResultRow DirectXRuntimeFaultRow { get; } = new EnvironmentScanResultRow();

        public EnvironmentScanResultOutcome DirectXD3d9 { get; } = new EnvironmentScanResultOutcome();

        public EnvironmentScanResultOutcome DirectXD3Dx43 { get; } = new EnvironmentScanResultOutcome();

        public EnvironmentScanResultRow MediaPackRuntimeFaultRow { get; } = new EnvironmentScanResultRow();

        public EnvironmentScanResultOutcome MediaPackMf { get; } = new EnvironmentScanResultOutcome();

        public EnvironmentScanResultOutcome MediaPackMfplat { get; } = new EnvironmentScanResultOutcome();

        public EnvironmentScanResultOutcome MediaPackWmvCore { get; } = new EnvironmentScanResultOutcome();

        public EnvironmentScanResultRow Vc2010X86RuntimeFaultRow { get; } = new EnvironmentScanResultRow();

        public EnvironmentScanResultOutcome Vc2010X86Msvcr { get; } = new EnvironmentScanResultOutcome();

        public EnvironmentScanResultOutcome Vc2010X86Msvcp { get; } = new EnvironmentScanResultOutcome();

        public EnvironmentScanResultRow Vc2010X64RuntimeFaultRow { get; } = new EnvironmentScanResultRow();

        public EnvironmentScanResultOutcome Vc2010X64Msvcr { get; } = new EnvironmentScanResultOutcome();

        public EnvironmentScanResultOutcome Vc2010X64Msvcp { get; } = new EnvironmentScanResultOutcome();

        public List<string> ScanRootAlerts { get; } = new List<string>();

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

        public int Revision { get; private set; }

        public void NotifyScanPresentationChanged()
        {
            Revision++;
        }

        public bool HasAnyEnvironmentScanWarning()
        {
            return WarningRows().Any(r => r is { IsShown: true, BadgeLevel: EnvironmentScan.ScanResultLevel.Warning })
                || GpuAdapterRows.Any(r => r is { IsShown: true, BadgeLevel: EnvironmentScan.ScanResultLevel.Warning })
                || WarningOutcomes().Any(o => o is { OutcomeVisible: true, BadgeLevel: EnvironmentScan.ScanResultLevel.Warning });
        }

        private IEnumerable<EnvironmentScanResultRow> WarningRows()
        {
            yield return CpuPrimaryRow;
            yield return NvidiaSkipNoticeRow;
            yield return DirectXRuntimeFaultRow;
            yield return MediaPackRuntimeFaultRow;
            yield return Vc2010X86RuntimeFaultRow;
            yield return Vc2010X64RuntimeFaultRow;
        }

        private IEnumerable<EnvironmentScanResultOutcome> WarningOutcomes()
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

    internal sealed class EnvironmentScanResultRow
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

    internal sealed class EnvironmentScanResultOutcome
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
