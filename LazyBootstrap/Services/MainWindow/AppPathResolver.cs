using System;

namespace LazyBootstrap
{
    internal static class AppPathResolver
    {
        public static string ResolveBaseDir()
        {
            string argBaseDir = null;
            try
            {
                foreach (var arg in Environment.GetCommandLineArgs())
                {
                    if (arg.StartsWith("--basedir=", StringComparison.OrdinalIgnoreCase))
                    {
                        argBaseDir = arg.Substring("--basedir=".Length).Trim('"');
                        break;
                    }
                }
            }
            catch
            {
            }

            var envBaseDir = Environment.GetEnvironmentVariable("LAZYBOOTSTRAP_BASEDIR");
            return !string.IsNullOrWhiteSpace(argBaseDir)
                ? argBaseDir
                : (!string.IsNullOrWhiteSpace(envBaseDir) ? envBaseDir : AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
