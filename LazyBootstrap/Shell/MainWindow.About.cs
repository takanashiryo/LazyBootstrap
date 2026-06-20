using System;
using Avalonia.Controls;

namespace LazyBootstrap.Shell
{
    public partial class MainWindow
    {
        /// <summary>Applies the resolved launcher version (populated by the diagnostic scan) to the about page.</summary>
        private void ApplyAboutVersion()
        {
            if (LauncherVersionTextBlock != null)
            {
                LauncherVersionTextBlock.Text = _environmentScanState.LauncherVersion;
            }
        }
    }
}
