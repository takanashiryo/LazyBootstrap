using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Threading;

namespace LazyBootstrap.Shared.Controls
{
    public partial class ArcadeMessageOverlay : UserControl
    {
        private static readonly Color ErrorStartColor = Color.Parse("#FFFF0000");
        private static readonly Color WarningStartColor = Color.Parse("#FFFFD200");
        private static readonly Color BorderEndColor = Color.Parse("#FFFFFFFF");
        private static readonly TimeSpan AnimationDuration = TimeSpan.FromSeconds(1.4);

        private readonly SolidColorBrush _borderBrush = new SolidColorBrush(ErrorStartColor);
        private DispatcherTimer _animationTimer;
        private Stopwatch _animationStopwatch;
        private Color _startColor = ErrorStartColor;

        public ArcadeMessageOverlay()
        {
            InitializeComponent();
            EnsureBorderBrush();
        }

        public void Show(NotificationType messageType, string title, string accentText, string bodyText)
        {
            _startColor = ResolveStartColor(messageType);
            _borderBrush.Color = _startColor;
            EnsureBorderBrush();
            UpdateContent(title, accentText, bodyText);
            IsVisible = true;
            StartAnimation();
        }

        public void Hide()
        {
            StopAnimation();
            IsVisible = false;
        }

        public void StopAnimation()
        {
            if (_animationTimer != null)
            {
                _animationTimer.Stop();
            }

            if (_animationStopwatch != null && _animationStopwatch.IsRunning)
            {
                _animationStopwatch.Stop();
            }

            _borderBrush.Color = _startColor;
            EnsureBorderBrush();
        }

        private void UpdateContent(string title, string accentText, string bodyText)
        {
            if (ArcadeMessageTitleTextBlock != null)
            {
                ArcadeMessageTitleTextBlock.Text = title ?? string.Empty;
            }

            if (ArcadeMessageAccentTextBlock != null)
            {
                string normalizedAccentText = accentText ?? string.Empty;
                ArcadeMessageAccentTextBlock.Text = normalizedAccentText;
                ArcadeMessageAccentTextBlock.IsVisible = !string.IsNullOrWhiteSpace(normalizedAccentText);
            }

            if (ArcadeMessageBodyTextBlock != null)
            {
                ArcadeMessageBodyTextBlock.Text = bodyText ?? string.Empty;
            }
        }

        private void StartAnimation()
        {
            if (_animationTimer == null)
            {
                _animationTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16)
                };
                _animationTimer.Tick += OnAnimationTick;
            }

            if (_animationStopwatch == null)
            {
                _animationStopwatch = new Stopwatch();
            }

            _borderBrush.Color = _startColor;
            _animationStopwatch.Restart();

            if (!_animationTimer.IsEnabled)
            {
                _animationTimer.Start();
            }
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            if (!IsVisible || _animationStopwatch == null)
            {
                return;
            }

            double durationMilliseconds = AnimationDuration.TotalMilliseconds;
            if (durationMilliseconds <= 0)
            {
                _borderBrush.Color = _startColor;
                return;
            }

            double cycleProgress = (_animationStopwatch.Elapsed.TotalMilliseconds / durationMilliseconds) % 2d;
            double pingPongProgress = cycleProgress <= 1d ? cycleProgress : 2d - cycleProgress;
            double easedProgress = EaseInOutCubic(Math.Clamp(pingPongProgress, 0d, 1d));
            _borderBrush.Color = InterpolateColor(_startColor, BorderEndColor, easedProgress);
        }

        private void EnsureBorderBrush()
        {
            if (ArcadeMessageBorder != null && !ReferenceEquals(ArcadeMessageBorder.BorderBrush, _borderBrush))
            {
                ArcadeMessageBorder.BorderBrush = _borderBrush;
            }
        }

        private static Color ResolveStartColor(NotificationType messageType)
        {
            return messageType switch
            {
                NotificationType.Warning => WarningStartColor,
                _ => ErrorStartColor
            };
        }

        private static double EaseInOutCubic(double progress)
        {
            return progress < 0.5d
                ? 4d * progress * progress * progress
                : 1d - Math.Pow(-2d * progress + 2d, 3d) / 2d;
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
