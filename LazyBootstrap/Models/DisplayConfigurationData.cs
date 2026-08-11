using System;
using System.Collections.Generic;

namespace LazyBootstrap.Models
{
    internal sealed class DisplayConfigurationData
    {
        private bool _suspendUpdates;

        public List<DisplayChoiceOption> Displays { get; } = new List<DisplayChoiceOption>();

        public List<RotationOption> Rotations { get; } = new List<RotationOption>();

        public List<string> MainResolutions { get; } = new List<string>();

        public List<string> SubResolutions { get; } = new List<string>();

        public List<string> MainRefreshRates { get; } = new List<string>();

        public List<string> SubRefreshRates { get; } = new List<string>();

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

    internal sealed record DisplayConfigurationRequest(
        bool IsDisplayConfigurationEnabled,
        bool IsDualDisplay,
        bool ExitRestore,
        DisplayChoiceOption SelectedMainDisplay,
        DisplayChoiceOption SelectedSubDisplay,
        RotationOption SelectedMainRotation,
        RotationOption SelectedSubRotation,
        string SelectedMainResolution,
        string SelectedSubResolution,
        string SelectedMainRefreshRate,
        string SelectedSubRefreshRate);
}
