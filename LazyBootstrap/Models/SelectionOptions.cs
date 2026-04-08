namespace LazyBootstrap.Models
{
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

        public override string ToString()
        {
            return DisplayName;
        }
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

        public override string ToString()
        {
            return DisplayName;
        }
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

        public override string ToString()
        {
            return DisplayName;
        }
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

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
