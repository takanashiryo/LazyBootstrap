namespace LazyBootstrap.Features.Launch
{
    public sealed record LaunchRequest(
        SettingsState Settings,
        DisplayConfigurationSnapshot Display,
        bool AsphyxiaDevOnly);
}
