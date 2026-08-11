namespace LazyBootstrap.Models;

internal enum ConfigFileHealthStatus
{
    Missing,
    Valid,
    InvalidToml,
    Inaccessible
}

internal readonly record struct ConfigFileHealth(ConfigFileHealthStatus Status, string ErrorMessage, string Content)
{
    public static ConfigFileHealth Missing()
    {
        return new ConfigFileHealth(ConfigFileHealthStatus.Missing, string.Empty, string.Empty);
    }

    public static ConfigFileHealth Valid(string content)
    {
        return new ConfigFileHealth(ConfigFileHealthStatus.Valid, string.Empty, content ?? string.Empty);
    }

    public static ConfigFileHealth InvalidToml(string errorMessage, string content)
    {
        return new ConfigFileHealth(ConfigFileHealthStatus.InvalidToml, errorMessage ?? string.Empty, content ?? string.Empty);
    }

    public static ConfigFileHealth Inaccessible(string errorMessage, string content = "")
    {
        return new ConfigFileHealth(ConfigFileHealthStatus.Inaccessible, errorMessage ?? string.Empty, content ?? string.Empty);
    }
}
