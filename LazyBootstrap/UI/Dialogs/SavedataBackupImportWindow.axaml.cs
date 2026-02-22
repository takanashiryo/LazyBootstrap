using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
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

            ConfigureWindowBackdrop();
            Opened += async (_, _) =>
            {
                EnsureBackdropEffectIsApplied();
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                EnsureBackdropEffectIsApplied();
            };
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

        private void ConfigureWindowBackdrop()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            Background = Brushes.Transparent;
            TransparencyBackgroundFallback = new SolidColorBrush(Color.FromArgb(0xE6, 0x08, 0x08, 0x08));
            TransparencyLevelHint =
            [
                WindowTransparencyLevel.Blur
            ];
        }

        private void EnsureBackdropEffectIsApplied()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            if (ActualTransparencyLevel == WindowTransparencyLevel.Blur)
            {
                return;
            }

            TransparencyLevelHint =
            [
                WindowTransparencyLevel.AcrylicBlur,
                WindowTransparencyLevel.Blur
            ];

            if (ActualTransparencyLevel == WindowTransparencyLevel.AcrylicBlur
                || ActualTransparencyLevel == WindowTransparencyLevel.Blur)
            {
                return;
            }

            TransparencyLevelHint =
            [
                WindowTransparencyLevel.None
            ];
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x10, 0x10, 0x10));
        }
    }
}
