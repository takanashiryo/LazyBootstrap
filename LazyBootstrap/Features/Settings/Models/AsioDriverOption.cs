namespace LazyBootstrap.Features.Settings
{
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
}
