using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SukiUI.Controls;

namespace LazyBootstrap.UI.Dialogs
{
    public partial class SavedataBackupImportWindow : SukiWindow
    {
        private readonly Func<Task> _onBackupRequested;
        private readonly Func<Task> _onImportRequested;
        private Button _backupButton;
        private Button _importButton;
        private StackPanel _backupProgressPanel;
        private TextBlock _backupProgressText;

        public SavedataBackupImportWindow()
        {
            InitializeComponent();
        }

        public SavedataBackupImportWindow(Func<Task> onBackupRequested, Func<Task> onImportRequested)
            : this()
        {
            _onBackupRequested = onBackupRequested;
            _onImportRequested = onImportRequested;

            _backupButton = this.FindControl<Button>("BackupButton");
            _importButton = this.FindControl<Button>("ImportButton");
            _backupProgressPanel = this.FindControl<StackPanel>("BackupProgressPanel");
            _backupProgressText = this.FindControl<TextBlock>("BackupProgressText");

            if (_backupButton != null)
            {
                _backupButton.Click += async (_, _) =>
                {
                    if (_onBackupRequested != null)
                    {
                        SetBackupInProgress(true, "正在备份...");
                        try
                        {
                            await _onBackupRequested();
                        }
                        finally
                        {
                            SetBackupInProgress(false, string.Empty);
                        }
                    }
                };
            }

            if (_importButton != null)
            {
                _importButton.Click += async (_, _) =>
                {
                    if (_onImportRequested != null)
                    {
                        await _onImportRequested();
                    }
                };
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void SetBackupInProgress(bool inProgress, string statusText)
        {
            if (_backupButton != null)
            {
                _backupButton.IsEnabled = !inProgress;
            }

            if (_importButton != null)
            {
                _importButton.IsEnabled = !inProgress;
            }

            if (_backupProgressText != null)
            {
                _backupProgressText.Text = statusText;
            }

            if (_backupProgressPanel != null)
            {
                _backupProgressPanel.IsVisible = inProgress;
            }
        }

    }
}
