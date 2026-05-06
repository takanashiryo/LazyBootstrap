using CommunityToolkit.Mvvm.ComponentModel;
using LazyBootstrap.Services.Environment;

namespace LazyBootstrap.ViewModels
{
    public partial class EnvironmentScanDisplayRow : ObservableObject
    {
        [ObservableProperty]
        private string primaryText = string.Empty;

        [ObservableProperty]
        private string secondaryText = string.Empty;

        [ObservableProperty]
        private bool secondaryVisible;

        [ObservableProperty]
        private bool showStatusBadge = true;

        [ObservableProperty]
        private string statusText = string.Empty;

        [ObservableProperty]
        private EnvironmentScan.ScanResultLevel badgeLevel = EnvironmentScan.ScanResultLevel.Success;

        /// <summary>When false the row hides (unused branch or before assignment).</summary>
        [ObservableProperty]
        private bool isShown;

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

    /// <summary>Fixed detection row (label in AXAML): binds outcome badge only.</summary>
    public partial class EnvironmentScanLineOutcome : ObservableObject
    {
        [ObservableProperty]
        private EnvironmentScan.ScanResultLevel badgeLevel = EnvironmentScan.ScanResultLevel.Success;

        [ObservableProperty]
        private string statusText = string.Empty;

        [ObservableProperty]
        private bool outcomeVisible;

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
