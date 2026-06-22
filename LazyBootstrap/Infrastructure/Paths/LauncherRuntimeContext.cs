namespace LazyBootstrap.Infrastructure.Paths
{
    public sealed record LauncherRuntimeContext(
        string BaseDirectoryPath,
        string ApplicationDirectoryPath,
        string ConfigFilePath);
}
