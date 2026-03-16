using System;
using SystemEnvironment = System.Environment;
using System.IO;

namespace LazyBootstrap.Services.Paths
{
    internal static class AppPathResolver
    {
        private const string BaseDirArgumentName = "--basedir";
        private const string BaseDirEnvironmentVariable = "LAZYBOOTSTRAP_BASEDIR";

        public static string ResolveBaseDir()
        {
            return ResolveBaseDir(
                SystemEnvironment.GetCommandLineArgs(),
                SystemEnvironment.GetEnvironmentVariable(BaseDirEnvironmentVariable),
                AppDomain.CurrentDomain.BaseDirectory);
        }

        internal static string ResolveBaseDir(string[] commandLineArgs, string environmentBaseDir, string applicationBaseDirectory)
        {
            var argumentBaseDir = TryGetBaseDirFromArguments(commandLineArgs);
            if (!string.IsNullOrWhiteSpace(argumentBaseDir))
            {
                return NormalizePath(argumentBaseDir);
            }

            if (!string.IsNullOrWhiteSpace(environmentBaseDir))
            {
                return NormalizePath(environmentBaseDir);
            }

            return InferBaseDirFromApplicationDirectory(applicationBaseDirectory);
        }

        private static string TryGetBaseDirFromArguments(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return string.Empty;
            }

            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];
                if (string.IsNullOrWhiteSpace(argument))
                {
                    continue;
                }

                if (argument.StartsWith($"{BaseDirArgumentName}=", StringComparison.OrdinalIgnoreCase))
                {
                    return argument.Substring(BaseDirArgumentName.Length + 1).Trim('"');
                }

                if (string.Equals(argument, BaseDirArgumentName, StringComparison.OrdinalIgnoreCase)
                    && index + 1 < args.Length)
                {
                    return (args[index + 1] ?? string.Empty).Trim('"');
                }
            }

            return string.Empty;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return System.IO.Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        private static string InferBaseDirFromApplicationDirectory(string applicationBaseDirectory)
        {
            var normalizedApplicationBaseDirectory = NormalizePath(applicationBaseDirectory);
            if (string.IsNullOrWhiteSpace(normalizedApplicationBaseDirectory))
            {
                return string.Empty;
            }

            var trimmedApplicationBaseDirectory = normalizedApplicationBaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var applicationDirectoryInfo = new DirectoryInfo(trimmedApplicationBaseDirectory);

            if (!string.Equals(applicationDirectoryInfo.Name, "launcher", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedApplicationBaseDirectory;
            }

            var parentDirectory = applicationDirectoryInfo.Parent;
            if (parentDirectory == null)
            {
                return normalizedApplicationBaseDirectory;
            }

            if (!Directory.Exists(Path.Combine(trimmedApplicationBaseDirectory, "libs")))
            {
                return normalizedApplicationBaseDirectory;
            }

            return NormalizePath(parentDirectory.FullName);
        }
    }
}
