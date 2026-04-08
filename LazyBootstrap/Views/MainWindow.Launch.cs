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
        private static readonly Color LaunchFailureOverlayStartColor = Color.Parse("#FF0000");
        private static readonly Color LaunchFailureOverlayEndColor = Color.Parse("#FFFFFF");
        private static readonly TimeSpan LaunchFailureOverlayAnimationDuration = TimeSpan.FromSeconds(1.4);

        private readonly SolidColorBrush _launchFailureOverlayBorderBrush = new SolidColorBrush(LaunchFailureOverlayStartColor);
        private DispatcherTimer _launchFailureOverlayAnimationTimer;
        private Stopwatch _launchFailureOverlayAnimationStopwatch;

        private void HookLaunchViewModelState()
        {
            if (_viewModel?.Launch == null)
            {
                return;
            }

            RemoveHandler(InputElement.PointerPressedEvent, OnLaunchFailureOverlayPointerPressed);
            AddHandler(InputElement.PointerPressedEvent, OnLaunchFailureOverlayPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _viewModel.Launch.PropertyChanged -= OnLaunchViewModelPropertyChanged;
            _viewModel.Launch.PropertyChanged += OnLaunchViewModelPropertyChanged;
            _ = ApplyLaunchLogVisibilityAsync();
            _ = ApplyLaunchLogTextAsync();
            _ = ApplyLaunchFailureOverlayVisibilityAsync();
        }

        private void UnhookLaunchViewModelState()
        {
            if (_viewModel?.Launch == null)
            {
                return;
            }

            RemoveHandler(InputElement.PointerPressedEvent, OnLaunchFailureOverlayPointerPressed);
            _viewModel.Launch.PropertyChanged -= OnLaunchViewModelPropertyChanged;
            StopLaunchFailureOverlayAnimation();
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
                || string.Equals(e.PropertyName, nameof(LaunchPageViewModel.IsLaunchFailureOverlayVisible), StringComparison.Ordinal))
            {
                _ = ApplyLaunchFailureOverlayVisibilityAsync();
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

        private async Task ApplyLaunchFailureOverlayVisibilityAsync()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                await Dispatcher.UIThread.InvokeAsync(async () => await ApplyLaunchFailureOverlayVisibilityAsync());
                return;
            }

            if (_viewModel.Launch.IsLaunchFailureOverlayVisible)
            {
                ShowLaunchFailureOverlay();
                return;
            }

            HideLaunchFailureOverlay();
        }

        private void ShowLaunchFailureOverlay()
        {
            if (LaunchFailureOverlay == null)
            {
                return;
            }

            if (LaunchFailureBorder != null && !ReferenceEquals(LaunchFailureBorder.BorderBrush, _launchFailureOverlayBorderBrush))
            {
                LaunchFailureBorder.BorderBrush = _launchFailureOverlayBorderBrush;
            }

            LaunchFailureOverlay.IsVisible = true;
            StartLaunchFailureOverlayAnimation();
        }

        private void DismissLaunchFailureOverlay()
        {
            if (_viewModel?.Launch == null || !_viewModel.Launch.IsLaunchFailureOverlayVisible)
            {
                return;
            }

            _viewModel.Launch.IsLaunchFailureOverlayVisible = false;
            HideLaunchFailureOverlay();
        }

        private void HideLaunchFailureOverlay()
        {
            StopLaunchFailureOverlayAnimation();

            if (LaunchFailureOverlay == null)
            {
                return;
            }

            LaunchFailureOverlay.IsVisible = false;
        }

        private void StartLaunchFailureOverlayAnimation()
        {
            if (_launchFailureOverlayAnimationTimer == null)
            {
                _launchFailureOverlayAnimationTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16)
                };
                _launchFailureOverlayAnimationTimer.Tick += OnLaunchFailureOverlayAnimationTick;
            }

            if (_launchFailureOverlayAnimationStopwatch == null)
            {
                _launchFailureOverlayAnimationStopwatch = new Stopwatch();
            }

            _launchFailureOverlayBorderBrush.Color = LaunchFailureOverlayStartColor;
            _launchFailureOverlayAnimationStopwatch.Restart();

            if (!_launchFailureOverlayAnimationTimer.IsEnabled)
            {
                _launchFailureOverlayAnimationTimer.Start();
            }
        }

        private void StopLaunchFailureOverlayAnimation()
        {
            if (_launchFailureOverlayAnimationTimer != null)
            {
                _launchFailureOverlayAnimationTimer.Stop();
            }

            if (_launchFailureOverlayAnimationStopwatch != null && _launchFailureOverlayAnimationStopwatch.IsRunning)
            {
                _launchFailureOverlayAnimationStopwatch.Stop();
            }

            _launchFailureOverlayBorderBrush.Color = LaunchFailureOverlayStartColor;

            if (LaunchFailureBorder != null && !ReferenceEquals(LaunchFailureBorder.BorderBrush, _launchFailureOverlayBorderBrush))
            {
                LaunchFailureBorder.BorderBrush = _launchFailureOverlayBorderBrush;
            }
        }

        private void OnLaunchFailureOverlayPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (_viewModel?.Launch == null || !_viewModel.Launch.IsLaunchFailureOverlayVisible)
            {
                return;
            }

            DismissLaunchFailureOverlay();
            e.Handled = true;
        }

        private void OnLaunchFailureOverlayAnimationTick(object sender, EventArgs e)
        {
            if (LaunchFailureOverlay == null
                || !LaunchFailureOverlay.IsVisible
                || _launchFailureOverlayAnimationStopwatch == null)
            {
                return;
            }

            double durationMilliseconds = LaunchFailureOverlayAnimationDuration.TotalMilliseconds;
            if (durationMilliseconds <= 0)
            {
                _launchFailureOverlayBorderBrush.Color = LaunchFailureOverlayStartColor;
                return;
            }

            double cycleProgress = (_launchFailureOverlayAnimationStopwatch.Elapsed.TotalMilliseconds / durationMilliseconds) % 2d;
            double pingPongProgress = cycleProgress <= 1d ? cycleProgress : 2d - cycleProgress;
            double easedProgress = EaseInOutCubic(Math.Clamp(pingPongProgress, 0d, 1d));
            _launchFailureOverlayBorderBrush.Color = InterpolateColor(LaunchFailureOverlayStartColor, LaunchFailureOverlayEndColor, easedProgress);
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
