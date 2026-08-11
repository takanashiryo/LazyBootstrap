namespace LazyBootstrap.Models
{
    public sealed record LauncherRuntimeContext(
        string BaseDirectoryPath,
        string ApplicationDirectoryPath,
        string ConfigFilePath);
}
