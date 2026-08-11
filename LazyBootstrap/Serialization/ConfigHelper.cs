namespace LazyBootstrap.Serialization
{
    internal static class ConfigHelper
    {
        public static string NormalizeNetworkValue(string value) => (value ?? string.Empty).Trim();

        public static bool TryReadBool(this ConfigHandler config, string section, string key, bool defaultValue)
        {
            return config.ReadBool(section, key, defaultValue);
        }
    }
}
