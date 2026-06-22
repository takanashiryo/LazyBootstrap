namespace LazyBootstrap.Features.Settings
{
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
}
