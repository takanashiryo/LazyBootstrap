using LazyBootstrap.Features.Display;
using LazyBootstrap.Features.Settings;

namespace LazyBootstrap.Features.Launch
{
    public sealed record LaunchRequest(
        SettingsState Settings,
        DisplayConfigurationSnapshot Display,
        bool AsphyxiaDevOnly);
}
