namespace LazyBootstrap.Features.Display
{
    public sealed record DisplayUpdateRequest(DisplayConfigurationSnapshot Display, bool RefreshMainOptions, bool RefreshSubOptions);
}
