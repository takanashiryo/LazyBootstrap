namespace LazyBootstrap.Infrastructure.Serialization
{
    internal static class ConfigHelper
    {
        public static string NormalizeNetworkValue(string value) => (value ?? string.Empty).Trim();

        public static bool TryReadBool(this ConfigHandler config, string section, string key, bool defaultValue)
        {
            return bool.TryParse(
                config.ReadString(section, key, defaultValue ? "true" : "false"),
                out var parsed) && parsed;
        }
    }
}
