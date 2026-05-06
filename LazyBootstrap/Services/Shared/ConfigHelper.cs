using LazyBootstrap.Services.Config;

namespace LazyBootstrap.Services.Shared
{
    internal static class ConfigHelper
    {
        public static string NormalizeNetworkValue(string value) => (value ?? string.Empty).Trim();

        public static bool TryReadBool(this IConfigHandler config, string section, string key, bool defaultValue)
        {
            return bool.TryParse(
                config.ReadString(section, key, defaultValue ? "true" : "false"),
                out var parsed) && parsed;
        }
    }
}
