using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace LazyBootstrap.Views
{
    public partial class MainWindow
    {
        private static readonly Color LaunchMessageErrorStartColor = Color.Parse("#FFFF0000");
        private static readonly Color LaunchMessageWarningStartColor = Color.Parse("#FFFFD200");
        private static readonly Color LaunchMessageBorderEndColor = Color.Parse("#FFFFFFFF");
        private static readonly TimeSpan LaunchMessageOverlayAnimationDuration = TimeSpan.FromSeconds(1.4);

        private readonly SolidColorBrush _launchMessageOverlayBorderBrush = new SolidColorBrush(LaunchMessageErrorStartColor);
        private DispatcherTimer _launchMessageOverlayAnimationTimer;
        private Stopwatch _launchMessageOverlayAnimationStopwatch;
        private Color _launchMessageOverlayStartColor = LaunchMessageErrorStartColor;

        private void HookLaunchViewModelState()
        {
            if (_viewModel?.Launch == null)
            {
                return;
            }

            RemoveHandler(InputElement.PointerPressedEvent, OnLaunchMessageOverlayPointerPressed);
            AddHandler(InputElement.PointerPressedEvent, OnLaunchMessageOverlayPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _viewModel.Launch.PropertyChanged -= OnLaunchViewModelPropertyChanged;
            _viewModel.Launch.PropertyChanged += OnLaunchViewModelPropertyChanged;
            _ = ApplyLaunchLogVisibilityAsync();
            _ = ApplyLaunchLogTextAsync();
            _ = ApplyLaunchMessageOverlayAsync();
        }

        private void UnhookLaunchViewModelState()
        {
            if (_viewModel?.Launch == null)
            {
                return;
            }

            RemoveHandler(InputElement.PointerPressedEvent, OnLaunchMessageOverlayPointerPressed);
            _viewModel.Launch.PropertyChanged -= OnLaunchViewModelPropertyChanged;
            StopLaunchMessageOverlayAnimation();
        }

        private void OnLaunchViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e?.PropertyName)
                || string.Equals(e.PropertyName, nameof(LaunchPageViewModel.IsLaunchLogVisible), StringComparison.Ordinal))
            {
                _ = ApplyLaunchLogVisibilityAsync();
            }

            if (string.IsNullOrWhiteSpace(e?.PropertyName)
                || string.Equals(e.PropertyName, nameof(LaunchPageViewModel.LaunchLogText), StringComparison.Ordinal))
            {
                _ = ApplyLaunchLogTextAsync();
            }

            if (string.IsNullOrWhiteSpace(e?.PropertyName)
                || string.Equals(e.PropertyName, nameof(LaunchPageViewModel.IsMessageVisible), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(LaunchPageViewModel.MessageType), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(LaunchPageViewModel.MessageTitle), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(LaunchPageViewModel.MessageAccentText), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(LaunchPageViewModel.MessageBodyText), StringComparison.Ordinal))
            {
                _ = ApplyLaunchMessageOverlayAsync();
            }
        }

        private async Task ApplyLaunchLogVisibilityAsync()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                await Dispatcher.UIThread.InvokeAsync(async () => await ApplyLaunchLogVisibilityAsync());
                return;
            }

            if (_viewModel.Launch.IsLaunchLogVisible)
            {
                await ShowLaunchLogAreaWithAnimationAsync(syncViewModel: false);
                return;
            }

            HideLaunchLogArea(syncViewModel: false);
        }

        private async Task ApplyLaunchLogTextAsync()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                await Dispatcher.UIThread.InvokeAsync(async () => await ApplyLaunchLogTextAsync());
                return;
            }

            if (LogOutputTextBlock == null)
            {
                return;
            }

            LogOutputTextBlock.Text = _viewModel.Launch.LaunchLogText ?? string.Empty;

            if (LaunchLogScrollViewer != null)
            {
                LaunchLogScrollViewer.Offset = new Vector(LaunchLogScrollViewer.Offset.X, double.MaxValue);
            }

            if (!string.IsNullOrWhiteSpace(LogOutputTextBlock.Text))
            {
                await AnimateLaunchLogAppendAsync();
            }
        }

        private async Task AnimateLaunchLogAppendAsync()
        {
            if (LogOutputTextBlock == null)
            {
                return;
            }

            if (_isLaunchLogAppendAnimating)
            {
                _isLaunchLogAppendAnimationPending = true;
                return;
            }

            _isLaunchLogAppendAnimating = true;
            try
            {
                do
                {
                    _isLaunchLogAppendAnimationPending = false;
                    LogOutputTextBlock.RenderTransformOrigin = Avalonia.RelativePoint.Center;
                    var scale = LogOutputTextBlock.RenderTransform as ScaleTransform;
                    if (scale == null)
                    {
                        scale = new ScaleTransform(0.985, 0.985);
                        LogOutputTextBlock.RenderTransform = scale;
                    }

                    LogOutputTextBlock.Opacity = 0.55;
                    scale.ScaleX = 0.985;
                    scale.ScaleY = 0.985;

                    const int steps = 6;
                    for (int i = 0; i <= steps; i++)
                    {
                        double t = (double)i / steps;
                        double eased = 1 - Math.Pow(1 - t, 3);
                        LogOutputTextBlock.Opacity = 0.55 + (0.45 * eased);
                        double currentScale = 0.985 + (0.015 * eased);
                        scale.ScaleX = currentScale;
                        scale.ScaleY = currentScale;
                        await Task.Delay(12);
                    }

                    LogOutputTextBlock.Opacity = 1;
                    scale.ScaleX = 1;
                    scale.ScaleY = 1;
                }
                while (_isLaunchLogAppendAnimationPending);
            }
            finally
            {
                _isLaunchLogAppendAnimating = false;
            }
        }

        private async Task ShowLaunchLogAreaWithAnimationAsync(bool syncViewModel = true)
        {
            if (LaunchLogContainer == null)
            {
                return;
            }

            if (_isLaunchLogVisible && LaunchLogContainer.IsVisible)
            {
                return;
            }

            _isLaunchLogVisible = true;
            if (syncViewModel)
            {
                _viewModel.Launch.IsLaunchLogVisible = true;
            }

            LaunchLogContainer.IsVisible = true;
            LaunchLogContainer.Opacity = 0;
            LaunchLogContainer.RenderTransformOrigin = Avalonia.RelativePoint.TopLeft;

            var scale = LaunchLogContainer.RenderTransform as ScaleTransform;
            if (scale == null)
            {
                scale = new ScaleTransform(0.12, 0.12);
                LaunchLogContainer.RenderTransform = scale;
            }

            scale.ScaleX = 0.12;
            scale.ScaleY = 0.12;

            const int steps = 14;
            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                double eased = 1 - Math.Pow(1 - t, 3);
                double currentScale = 0.12 + (0.88 * eased);
                LaunchLogContainer.Opacity = eased;
                scale.ScaleX = currentScale;
                scale.ScaleY = currentScale;
                await Task.Delay(16);
            }

            LaunchLogContainer.Opacity = 1;
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }

        private void HideLaunchLogArea(bool clearOutput = false, bool syncViewModel = true)
        {
            _isLaunchLogVisible = false;
            if (syncViewModel)
            {
                _viewModel.Launch.IsLaunchLogVisible = false;
            }

            if (LaunchLogContainer == null)
            {
                return;
            }

            LaunchLogContainer.IsVisible = false;
            LaunchLogContainer.Opacity = 0;
            LaunchLogContainer.RenderTransformOrigin = Avalonia.RelativePoint.TopLeft;
            LaunchLogContainer.RenderTransform = new ScaleTransform(0.12, 0.12);

            if (clearOutput)
            {
                ClearLaunchOutput(syncViewModel);
            }
        }

        private void ClearLaunchOutput(bool syncViewModel = true)
        {
            if (syncViewModel)
            {
                _viewModel.Launch.LaunchLogText = string.Empty;
            }

            if (LogOutputTextBlock != null)
            {
                LogOutputTextBlock.Text = _viewModel.Launch.LaunchLogText ?? string.Empty;
            }
        }

        private async Task ApplyLaunchMessageOverlayAsync()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                await Dispatcher.UIThread.InvokeAsync(async () => await ApplyLaunchMessageOverlayAsync());
                return;
            }

            UpdateLaunchMessageContent();

            if (_viewModel?.Launch == null)
            {
                HideLaunchMessageOverlay();
                return;
            }

            if (_viewModel.Launch.IsMessageVisible)
            {
                ShowLaunchMessageOverlay();
                return;
            }

            HideLaunchMessageOverlay();
        }

        private void UpdateLaunchMessageContent()
        {
            if (_viewModel?.Launch == null)
            {
                return;
            }

            if (LaunchMessageTitleTextBlock != null)
            {
                LaunchMessageTitleTextBlock.Text = _viewModel.Launch.MessageTitle ?? string.Empty;
            }

            if (LaunchMessageAccentTextBlock != null)
            {
                string accentText = _viewModel.Launch.MessageAccentText ?? string.Empty;
                LaunchMessageAccentTextBlock.Text = accentText;
                LaunchMessageAccentTextBlock.IsVisible = !string.IsNullOrWhiteSpace(accentText);
            }

            if (LaunchMessageBodyTextBlock != null)
            {
                LaunchMessageBodyTextBlock.Text = _viewModel.Launch.MessageBodyText ?? string.Empty;
            }
        }

        private void ShowLaunchMessageOverlay()
        {
            if (LaunchMessageOverlay == null)
            {
                return;
            }

            _launchMessageOverlayStartColor = ResolveLaunchMessageStartColor(_viewModel?.Launch?.MessageType ?? NotificationType.Error);

            if (LaunchMessageBorder != null && !ReferenceEquals(LaunchMessageBorder.BorderBrush, _launchMessageOverlayBorderBrush))
            {
                LaunchMessageBorder.BorderBrush = _launchMessageOverlayBorderBrush;
            }

            _launchMessageOverlayBorderBrush.Color = _launchMessageOverlayStartColor;
            UpdateLaunchMessageContent();
            LaunchMessageOverlay.IsVisible = true;
            StartLaunchMessageOverlayAnimation();
        }

        private void DismissLaunchMessageOverlay()
        {
            if (_viewModel?.Launch == null || !_viewModel.Launch.IsMessageVisible)
            {
                return;
            }

            _viewModel.Launch.IsMessageVisible = false;
            HideLaunchMessageOverlay();
        }

        private void HideLaunchMessageOverlay()
        {
            StopLaunchMessageOverlayAnimation();

            if (LaunchMessageOverlay == null)
            {
                return;
            }

            LaunchMessageOverlay.IsVisible = false;
        }

        private void StartLaunchMessageOverlayAnimation()
        {
            if (_launchMessageOverlayAnimationTimer == null)
            {
                _launchMessageOverlayAnimationTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16)
                };
                _launchMessageOverlayAnimationTimer.Tick += OnLaunchMessageOverlayAnimationTick;
            }

            if (_launchMessageOverlayAnimationStopwatch == null)
            {
                _launchMessageOverlayAnimationStopwatch = new Stopwatch();
            }

            _launchMessageOverlayBorderBrush.Color = _launchMessageOverlayStartColor;
            _launchMessageOverlayAnimationStopwatch.Restart();

            if (!_launchMessageOverlayAnimationTimer.IsEnabled)
            {
                _launchMessageOverlayAnimationTimer.Start();
            }
        }

        private void StopLaunchMessageOverlayAnimation()
        {
            if (_launchMessageOverlayAnimationTimer != null)
            {
                _launchMessageOverlayAnimationTimer.Stop();
            }

            if (_launchMessageOverlayAnimationStopwatch != null && _launchMessageOverlayAnimationStopwatch.IsRunning)
            {
                _launchMessageOverlayAnimationStopwatch.Stop();
            }

            _launchMessageOverlayBorderBrush.Color = _launchMessageOverlayStartColor;

            if (LaunchMessageBorder != null && !ReferenceEquals(LaunchMessageBorder.BorderBrush, _launchMessageOverlayBorderBrush))
            {
                LaunchMessageBorder.BorderBrush = _launchMessageOverlayBorderBrush;
            }
        }

        private void OnLaunchMessageOverlayPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (_viewModel?.Launch == null || !_viewModel.Launch.IsMessageVisible)
            {
                return;
            }

            DismissLaunchMessageOverlay();
            e.Handled = true;
        }

        private void OnLaunchMessageOverlayAnimationTick(object sender, EventArgs e)
        {
            if (LaunchMessageOverlay == null
                || !LaunchMessageOverlay.IsVisible
                || _launchMessageOverlayAnimationStopwatch == null)
            {
                return;
            }

            double durationMilliseconds = LaunchMessageOverlayAnimationDuration.TotalMilliseconds;
            if (durationMilliseconds <= 0)
            {
                _launchMessageOverlayBorderBrush.Color = _launchMessageOverlayStartColor;
                return;
            }

            double cycleProgress = (_launchMessageOverlayAnimationStopwatch.Elapsed.TotalMilliseconds / durationMilliseconds) % 2d;
            double pingPongProgress = cycleProgress <= 1d ? cycleProgress : 2d - cycleProgress;
            double easedProgress = EaseInOutCubic(Math.Clamp(pingPongProgress, 0d, 1d));
            _launchMessageOverlayBorderBrush.Color = InterpolateColor(_launchMessageOverlayStartColor, LaunchMessageBorderEndColor, easedProgress);
        }

        private static Color ResolveLaunchMessageStartColor(NotificationType messageType)
        {
            return messageType switch
            {
                NotificationType.Warning => LaunchMessageWarningStartColor,
                _ => LaunchMessageErrorStartColor
            };
        }

        private static Color InterpolateColor(Color from, Color to, double progress)
        {
            progress = Math.Clamp(progress, 0d, 1d);

            return Color.FromArgb(
                (byte)Math.Round(from.A + ((to.A - from.A) * progress)),
                (byte)Math.Round(from.R + ((to.R - from.R) * progress)),
                (byte)Math.Round(from.G + ((to.G - from.G) * progress)),
                (byte)Math.Round(from.B + ((to.B - from.B) * progress)));
        }
    }
}
