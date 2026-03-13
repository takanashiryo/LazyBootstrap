namespace LazyBootstrap
{
    internal enum WindowsDefenderExclusionStatus
    {
        Added,
        AlreadyExcluded,
        Skipped,
        Failed
    }

    internal sealed class WindowsDefenderExclusionResult
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
