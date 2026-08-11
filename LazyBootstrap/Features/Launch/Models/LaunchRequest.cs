using LazyBootstrap.Features.Display;

namespace LazyBootstrap.Features.Launch
{
    internal sealed record LaunchRequest(
        bool NoAsphyxia,
        bool UseSystemSpiceConfig,
        bool DisableSpiceFso,
        string ServerAddress,
        DisplayConfigurationRequest Display,
        bool AsphyxiaDevOnly);
}
