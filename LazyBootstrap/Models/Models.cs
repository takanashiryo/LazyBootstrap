namespace LazyBootstrap.Models
{
    public sealed record LauncherRuntimeContext(
        string BaseDirectoryPath,
        string ApplicationDirectoryPath,
        string ConfigFilePath);

    public sealed class ServerPresetItem
    {
        public string Name { get; set; } = string.Empty;

        public string ServerUrl { get; set; } = string.Empty;

        public string PcbId { get; set; } = string.Empty;

        public override string ToString() => Name;
    }

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
