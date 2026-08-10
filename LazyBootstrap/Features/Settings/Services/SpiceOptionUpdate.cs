namespace LazyBootstrap.Features.Settings
{
    public sealed class SpiceOptionUpdate
    {
        public SpiceOptionUpdate(string name, string value, bool removeWhenEmpty = false)
        {
            Name = name ?? string.Empty;
            Value = value ?? string.Empty;
            RemoveWhenEmpty = removeWhenEmpty;
        }

        public string Name { get; }

        public string Value { get; }

        public bool RemoveWhenEmpty { get; }

        public bool ShouldRemove => RemoveWhenEmpty && string.IsNullOrEmpty(Value);
    }
}
