using System;
using SystemEnvironment = System.Environment;
using System.IO;

namespace LazyBootstrap.Services.Paths
{
    internal interface ILauncherPaths
    {
        string BaseDir { get; }

        string ApplicationDirectoryPath { get; }

        string ConfigFilePath { get; }

        string GetContentsDirectoryPath();

        string GetAsphyxiaDirectoryPath();

        string GetSpicePath();

        string GetAsphyxiaPath();

        string GetSpiceXmlPath();

        string GetBundledLibsDirectoryPath();

        string GetBundledSevenZipExecutablePath();

        string GetLauncherExecutablePath();

        string GetRuntimeDirectoryPath();

        string GetSavedataBackupDirectoryPath();

        string ResolveSevenZipExecutablePath();
    }

    /// <summary>
    /// Encapsulates the launcher's installation layout and derived path rules.
    /// </summary>
    internal sealed class LauncherPaths : ILauncherPaths
    {
        private readonly string _defaultContentsDirectoryPath;
        private readonly string _defaultAsphyxiaDirectoryPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="LauncherPaths"/> class.
        /// </summary>
        /// <param name="baseDir">The launcher base directory.</param>
        /// <param name="applicationDirectoryPath">The current application directory.</param>
        /// <param name="configFilePath">The resolved configuration file path.</param>
        public LauncherPaths(string baseDir, string applicationDirectoryPath, string configFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);
            ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectoryPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(configFilePath);

            BaseDir = Path.GetFullPath(baseDir);
            ApplicationDirectoryPath = Path.GetFullPath(applicationDirectoryPath);
            ConfigFilePath = Path.GetFullPath(configFilePath);
            _defaultContentsDirectoryPath = Path.Combine(BaseDir, "contents");
            _defaultAsphyxiaDirectoryPath = Path.Combine(BaseDir, "asphyxia");
        }

        /// <summary>
        /// Gets the launcher base directory.
        /// </summary>
        public string BaseDir { get; }

        /// <summary>
        /// Gets the current application directory.
        /// </summary>
        public string ApplicationDirectoryPath { get; }

        /// <summary>
        /// Gets the configuration file path.
        /// </summary>
        public string ConfigFilePath { get; }

        /// <summary>
        /// Gets the resolved contents directory path.
        /// </summary>
        /// <returns>The effective contents directory path.</returns>
        public string GetContentsDirectoryPath()
        {
            return _defaultContentsDirectoryPath;
        }

        /// <summary>
        /// Gets the resolved Asphyxia directory path.
        /// </summary>
        /// <returns>The effective Asphyxia directory path.</returns>
        public string GetAsphyxiaDirectoryPath()
        {
            return _defaultAsphyxiaDirectoryPath;
        }

        /// <summary>
        /// Gets the resolved spice executable path.
        /// </summary>
        /// <returns>The effective spice executable path.</returns>
        public string GetSpicePath()
        {
            return Path.Combine(GetContentsDirectoryPath(), "spice64.exe");
        }

        /// <summary>
        /// Gets the resolved Asphyxia executable path.
        /// </summary>
        /// <returns>The effective Asphyxia executable path.</returns>
        public string GetAsphyxiaPath()
        {
            return Path.Combine(GetAsphyxiaDirectoryPath(), "asphyxia-core-x64.exe");
        }

        /// <summary>
        /// Gets the resolved SpiceTools XML path.
        /// </summary>
        /// <returns>The effective SpiceTools XML path.</returns>
        public string GetSpiceXmlPath()
        {
            string appDataDir = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataDir, "spicetools.xml");
        }

        /// <summary>
        /// Gets the bundled libraries directory path.
        /// </summary>
        /// <returns>The bundled libraries directory path.</returns>
        public string GetBundledLibsDirectoryPath()
        {
            return Path.Combine(ApplicationDirectoryPath, "libs");
        }

        /// <summary>
        /// Gets the bundled 7-Zip executable path.
        /// </summary>
        /// <returns>The bundled 7-Zip executable path.</returns>
        public string GetBundledSevenZipExecutablePath()
        {
            return Path.Combine(ApplicationDirectoryPath, "7za.exe");
        }

        /// <summary>
        /// Gets the launcher executable path inside the packaged launcher directory.
        /// </summary>
        /// <returns>The packaged launcher executable path.</returns>
        public string GetLauncherExecutablePath()
        {
            return Path.Combine(BaseDir, "launcher", "LazyBootstrap.exe");
        }

        /// <summary>
        /// Gets the runtime directory path.
        /// </summary>
        /// <returns>The runtime directory path.</returns>
        public string GetRuntimeDirectoryPath()
        {
            return Path.Combine(BaseDir, "runtime");
        }

        /// <summary>
        /// Gets the savedata backup directory path.
        /// </summary>
        /// <returns>The savedata backup directory path.</returns>
        public string GetSavedataBackupDirectoryPath()
        {
            return Path.Combine(BaseDir, "savedata_backup");
        }

        /// <summary>
        /// Resolves the preferred 7-Zip executable path from the supported locations.
        /// </summary>
        /// <returns>The first matching 7-Zip executable path, or the packaged launcher fallback path.</returns>
        public string ResolveSevenZipExecutablePath()
        {
            string bundledPath = GetBundledSevenZipExecutablePath();
            if (File.Exists(bundledPath))
            {
                return bundledPath;
            }

            string baseDirPath = Path.Combine(BaseDir, "7za.exe");
            if (File.Exists(baseDirPath))
            {
                return baseDirPath;
            }

            return Path.Combine(BaseDir, "launcher", "7za.exe");
        }

    }
}
