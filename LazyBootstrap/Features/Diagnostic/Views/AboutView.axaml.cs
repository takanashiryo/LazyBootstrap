using System;
using Avalonia.Controls;

namespace LazyBootstrap.Features.Diagnostic.Views
{
    public partial class AboutView : UserControl
    {
        private readonly EnvironmentScanPresentation _infoState = null!;

        public AboutView()
        {
            InitializeComponent();
        }

        public AboutView(EnvironmentScanPresentation infoState)
        {
            InitializeComponent();
            _infoState = infoState ?? throw new ArgumentNullException(nameof(infoState));
        }

        /// <summary>Applies the resolved launcher version (populated by the diagnostic scan) to the about page.</summary>
        public void ApplyVersion()
        {
            if (LauncherVersionTextBlock != null)
            {
                LauncherVersionTextBlock.Text = _infoState.LauncherVersion;
            }
        }
    }
}
