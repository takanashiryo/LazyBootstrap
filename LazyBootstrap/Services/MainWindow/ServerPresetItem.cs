namespace LazyBootstrap
{
    public sealed class ServerPresetItem
    {
        public string Name { get; set; } = string.Empty;
        public string ServerUrl { get; set; } = string.Empty;
        public string PcbId { get; set; } = string.Empty;

        public override string ToString() => Name;
    }
}
