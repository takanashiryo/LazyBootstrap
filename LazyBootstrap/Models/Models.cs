using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LazyBootstrap.Services.Environment;

namespace LazyBootstrap.Models
{
    public sealed record LauncherRuntimeContext(
        string BaseDirectoryPath,
        string ApplicationDirectoryPath,
        string ConfigFilePath);

    public sealed class ServerPresetItem
    {
        public string Name { get; set; } = string.Empty;

        public string ServerUrl { get; set; } = string.Empty;

        public string PcbId { get; set; } = string.Empty;

        public override string ToString() => Name;
    }

    public enum WindowsDefenderExclusionStatus
    {
        Added,
        AlreadyExcluded,
        Skipped,
        Failed
    }

    public sealed class WindowsDefenderExclusionResult
    {
        public WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public WindowsDefenderExclusionStatus Status { get; }

        public string Message { get; }
    }

    public enum DisplaySelectionTarget
    {
        None,
        Main,
        Sub
    }

    public sealed class DisplayChoiceOption
    {
        internal DisplayChoiceOption(DisplayInfo info, string displayName)
        {
            Info = info;
            DisplayName = displayName ?? string.Empty;
        }

        internal DisplayInfo Info { get; }

        public string DisplayName { get; }

        public override string ToString() => DisplayName;
    }

    public sealed class RotationOption
    {
        public RotationOption(int angle)
        {
            Angle = angle;
            DisplayName = GetDisplayName(angle);
        }

        public int Angle { get; }

        public string DisplayName { get; }

        public static string GetDisplayName(int angle)
        {
            int normalizedAngle = ((angle % 360) + 360) % 360;
            return normalizedAngle switch
            {
                0 => "横向",
                90 => "纵向",
                180 => "横向（翻转）",
                270 => "纵向（翻转）",
                _ => $"{normalizedAngle}°"
            };
        }

        public override string ToString() => DisplayName;
    }

    public sealed class DisplayConfigurationSnapshot
    {
        private bool _suspendUpdates;

        public ObservableCollection<DisplayChoiceOption> Displays { get; } = new ObservableCollection<DisplayChoiceOption>();

        public ObservableCollection<RotationOption> Rotations { get; } = new ObservableCollection<RotationOption>();

        public ObservableCollection<string> MainResolutions { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> SubResolutions { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> MainRefreshRates { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> SubRefreshRates { get; } = new ObservableCollection<string>();

        public bool IsDisplayConfigurationEnabled { get; set; }

        public bool IsDualDisplay { get; set; }

        public bool ExitRestore { get; set; } = true;

        public DisplayChoiceOption SelectedMainDisplay { get; set; }

        public DisplayChoiceOption SelectedSubDisplay { get; set; }

        public RotationOption SelectedMainRotation { get; set; }

        public RotationOption SelectedSubRotation { get; set; }

        public string SelectedMainResolution { get; set; } = string.Empty;

        public string SelectedSubResolution { get; set; } = string.Empty;

        public string SelectedMainRefreshRate { get; set; } = string.Empty;

        public string SelectedSubRefreshRate { get; set; } = string.Empty;

        public string MainOutputInfo { get; set; } = string.Empty;

        public string SubOutputInfo { get; set; } = string.Empty;

        public string MainStartupInfo { get; set; } = string.Empty;

        public string SubStartupInfo { get; set; } = string.Empty;

        public string MainDiagnosticsTooltip { get; set; } = string.Empty;

        public string SubDiagnosticsTooltip { get; set; } = string.Empty;

        public DisplaySelectionTarget SelectedTarget { get; set; } = DisplaySelectionTarget.None;

        public bool ShowNoScreenSelected { get; set; } = true;

        public bool ShowMainScreenConfig { get; set; }

        public bool ShowSubScreenConfig { get; set; }

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

        public bool IsSuspended => _suspendUpdates;

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
        }

        public void SelectMainDisplay()
        {
            SelectedTarget = DisplaySelectionTarget.Main;
            ShowNoScreenSelected = false;
            ShowMainScreenConfig = true;
            ShowSubScreenConfig = false;
        }

        public void SelectSubDisplay()
        {
            if (!IsDualDisplay)
            {
                SelectedTarget = DisplaySelectionTarget.None;
                ShowNoScreenSelected = true;
                ShowMainScreenConfig = false;
                ShowSubScreenConfig = false;
                return;
            }

            SelectedTarget = DisplaySelectionTarget.Sub;
            ShowNoScreenSelected = false;
            ShowMainScreenConfig = false;
            ShowSubScreenConfig = true;
        }
    }

    public sealed record DisplayUpdateRequest(DisplayConfigurationSnapshot Display, bool RefreshMainOptions, bool RefreshSubOptions);

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
