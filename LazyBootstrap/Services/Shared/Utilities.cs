using System;
using System.IO;
using System.Threading.Tasks;
using LazyBootstrap.Services.Config;
using Microsoft.Extensions.Logging;

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

    internal static class PathHelper
    {
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try { return Path.GetFullPath(path.Trim()); }
            catch { return path.Trim(); }
        }
    }

    internal static class TaskObservationExtensions
    {
        internal static void ForgetWithLogging(this Task task, ILogger logger, string errorMessage)
        {
            if (task is null) throw new ArgumentNullException(nameof(task));
            if (logger is null) throw new ArgumentNullException(nameof(logger));

            RunObserved(task, logger, errorMessage ?? string.Empty);
        }

        private static async void RunObserved(Task task, ILogger logger, string errorMessage)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Message}", errorMessage);
            }
        }
    }
}
