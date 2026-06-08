namespace LazyBootstrap.Models
{
    public enum ShellPage
    {
        Launch = 0,
        Settings = 1,
        Display = 2,
        Tools = 3,
        Update = 4,
        Info = 5,
        About = 6
    }

    internal sealed record LauncherRuntimeContext(
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

    public enum DisplaySelectionTarget
    {
        None,
        Main,
        Sub
    }

    public sealed class AsioDriverOption
    {
        public AsioDriverOption(string displayName, string value)
        {
            DisplayName = displayName ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string DisplayName { get; }

        public string Value { get; }

        public override string ToString() => DisplayName;
    }

    public sealed class NetworkAdapterOption
    {
        public NetworkAdapterOption(string displayName, string ipAddress, string subnetMask)
        {
            DisplayName = displayName ?? string.Empty;
            IpAddress = ipAddress ?? string.Empty;
            SubnetMask = subnetMask ?? string.Empty;
        }

        public string DisplayName { get; }

        public string IpAddress { get; }

        public string SubnetMask { get; }

        public override string ToString() => DisplayName;
    }

    public sealed class DisplayChoiceOption
    {
        internal DisplayChoiceOption(DisplayInfo info, string displayName)
        {
            Info = info;
            DisplayName = displayName ?? string.Empty;
        }

        internal DisplayInfo Info { get; }

        public string DisplayName { get; }

        public override string ToString() => DisplayName;
    }

    public sealed class RotationOption
    {
        public RotationOption(int angle)
        {
            Angle = angle;
            DisplayName = GetDisplayName(angle);
        }

        public int Angle { get; }

        public string DisplayName { get; }

        public static string GetDisplayName(int angle)
        {
            int normalizedAngle = ((angle % 360) + 360) % 360;
            return normalizedAngle switch
            {
                0 => "横向",
                90 => "纵向",
                180 => "横向（翻转）",
                270 => "纵向（翻转）",
                _ => $"{normalizedAngle}°"
            };
        }

        public override string ToString() => DisplayName;
    }
}
