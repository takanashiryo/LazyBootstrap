using LazyBootstrap.Models;

namespace LazyBootstrap.Models
{
    internal sealed record LaunchRequest(
        bool NoAsphyxia,
        bool UseSystemSpiceConfig,
        bool DisableSpiceFso,
        string ServerAddress,
        DisplayConfigurationRequest Display,
        bool AsphyxiaDevOnly);
}
