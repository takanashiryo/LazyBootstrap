namespace LazyBootstrap.Models
{
    public enum WindowsDefenderExclusionStatus
    {
        Added,
        AlreadyExcluded,
        Skipped,
        Failed
    }

    public sealed class WindowsDefenderExclusionResult
    {
        public WindowsDefenderExclusionResult(WindowsDefenderExclusionStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public WindowsDefenderExclusionStatus Status { get; }

        public string Message { get; }
    }
}
