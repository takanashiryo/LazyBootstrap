using Avalonia.Controls.Notifications;

namespace LazyBootstrap.Features.Launch
{
    public sealed record LaunchMessage(
        bool IsVisible,
        NotificationType MessageType,
        string Title,
        string AccentText,
        string BodyText)
    {
        public static LaunchMessage Hidden { get; } =
            new LaunchMessage(false, NotificationType.Error, string.Empty, string.Empty, string.Empty);
    }
}
