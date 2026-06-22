using System;
using System.Collections.ObjectModel;

namespace LazyBootstrap.Features.Display
{
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
}
