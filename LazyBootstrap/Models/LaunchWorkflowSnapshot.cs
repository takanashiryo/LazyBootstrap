using Avalonia.Controls.Notifications;

namespace LazyBootstrap.Models
{
    internal sealed class LaunchWorkflowSnapshot
    {
        public string LaunchLogText { get; set; } = string.Empty;

        public bool IsLaunchLogVisible { get; set; }

        public string ToggleLaunchLogText { get; set; } = "显示启动日志";

        public bool IsLaunching { get; set; }

        public bool IsGameRunning { get; set; }

        public bool IsMessageVisible { get; set; }

        public NotificationType MessageType { get; set; } = NotificationType.Error;

        public string MessageTitle { get; set; } = string.Empty;

        public string MessageAccentText { get; set; } = string.Empty;

        public string MessageBodyText { get; set; } = string.Empty;

        public bool CanStartLaunch => !IsLaunching && !IsGameRunning;

        public LaunchMessage ToMessage() =>
            new LaunchMessage(IsMessageVisible, MessageType, MessageTitle, MessageAccentText, MessageBodyText);
    }

    internal enum LaunchWorkflowChangeKind
    {
        State,
        LogVisibility,
        Log,
        Message
    }

    internal sealed record LaunchWorkflowChange(
        LaunchWorkflowSnapshot Snapshot,
        LaunchWorkflowChangeKind Kind);
}
