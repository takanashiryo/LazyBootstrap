using System;
using SystemEnvironment = System.Environment;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Serilog;
using LazyBootstrap.FileSystem;

namespace LazyBootstrap.Application
{
    /// <summary>
    /// Startup bootstrap helpers: Serilog configuration, runtime-context resolution and
    /// global exception logging. The application object graph is built explicitly by
    /// <see cref="ApplicationComposition"/>.
    /// </summary>
    internal static class AppServices
    {
        private static bool _serilogInitialized;
        private static bool _globalExceptionLoggingRegistered;
        private static LegacyConfigMigrationResult _legacyConfigMigrationResult = LegacyConfigMigrationResult.NotRequired();
        private const string LogOutputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{ProcessId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

        public static LauncherPaths Paths { get; private set; }

        public static void InitializeSerilog(string[] args)
        {
            if (_serilogInitialized) return;

            EnsurePaths(args);

            Directory.CreateDirectory(Paths.ApplicationDirectoryPath);
            string logFilePath = Path.Combine(Paths.ApplicationDirectoryPath, "LazyBootstrap.log");
            string applicationVersion = ResolveApplicationVersion();
            ResetLogFile(logFilePath);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "LazyBootstrap")
                .Enrich.WithProperty("ApplicationVersion", applicationVersion)
                .Enrich.WithProperty("ProcessId", SystemEnvironment.ProcessId)
                .Enrich.WithProperty("BaseDirectory", Paths.BaseDir)
                .Enrich.WithProperty("ApplicationDirectory", Paths.ApplicationDirectoryPath)
                .Enrich.WithProperty("ConfigPath", Paths.ConfigFilePath)
                .WriteTo.File(logFilePath, outputTemplate: LogOutputTemplate, shared: true)
                .CreateLogger();

            RegisterGlobalExceptionLogging();
            _serilogInitialized = true;

            LogLegacyConfigMigrationResult();
            Log.Information(
                "Serilog initialized. Version={Version}, ProcessId={ProcessId}, BaseDir={BaseDirectory}, ApplicationDir={ApplicationDirectory}, ConfigPath={ConfigPath}, LogPath={LogPath}",
                applicationVersion,
                SystemEnvironment.ProcessId,
                Paths.BaseDir,
                Paths.ApplicationDirectoryPath,
                Paths.ConfigFilePath,
                logFilePath);
        }

        private static void ResetLogFile(string logFilePath)
        {
            try
            {
                using var _ = new FileStream(logFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            }
            catch
            {
                // Keep startup tolerant if another process has a stricter lock on the log file.
            }
        }

        private static void EnsurePaths(string[] args)
        {
            if (Paths != null) return;

            Paths = LauncherPaths.Create(args);
            string legacyConfigFilePath = PathHelper.NormalizePath(Path.Combine(Paths.BaseDir, "config.toml"));
            _legacyConfigMigrationResult = MigrateLegacyConfig(legacyConfigFilePath, Paths.ConfigFilePath);
        }

        private static LegacyConfigMigrationResult MigrateLegacyConfig(string legacyConfigFilePath, string configFilePath)
        {
            if (string.IsNullOrWhiteSpace(legacyConfigFilePath)
                || string.IsNullOrWhiteSpace(configFilePath)
                || string.Equals(legacyConfigFilePath, configFilePath, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(legacyConfigFilePath))
            {
                return LegacyConfigMigrationResult.NotRequired();
            }

            try
            {
                string configDirectoryPath = Path.GetDirectoryName(configFilePath);
                if (!string.IsNullOrWhiteSpace(configDirectoryPath))
                {
                    Directory.CreateDirectory(configDirectoryPath);
                }

                File.Move(legacyConfigFilePath, configFilePath, true);
                return LegacyConfigMigrationResult.Migrated(legacyConfigFilePath, configFilePath);
            }
            catch (Exception ex)
            {
                return LegacyConfigMigrationResult.Failed(legacyConfigFilePath, configFilePath, ex.Message);
            }
        }

        private static void LogLegacyConfigMigrationResult()
        {
            if (_legacyConfigMigrationResult.Status == LegacyConfigMigrationStatus.Migrated)
            {
                Log.Information(
                    "Legacy config.toml migrated to launcher directory. SourcePath={SourcePath}, DestinationPath={DestinationPath}",
                    _legacyConfigMigrationResult.SourcePath,
                    _legacyConfigMigrationResult.DestinationPath);
                return;
            }

            if (_legacyConfigMigrationResult.Status == LegacyConfigMigrationStatus.Failed)
            {
                Log.Warning(
                    "Legacy config.toml migration failed. SourcePath={SourcePath}, DestinationPath={DestinationPath}, Error={Error}",
                    _legacyConfigMigrationResult.SourcePath,
                    _legacyConfigMigrationResult.DestinationPath,
                    _legacyConfigMigrationResult.Error);
            }
        }

        public static void Dispose()
        {
            Log.Information("LazyBootstrap services are shutting down.");
            Log.CloseAndFlush();
            _serilogInitialized = false;
        }

        private static void RegisterGlobalExceptionLogging()
        {
            if (_globalExceptionLoggingRegistered)
            {
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    Log.Fatal(ex, "Unhandled AppDomain exception. IsTerminating={IsTerminating}", e.IsTerminating);
                    return;
                }

                Log.Fatal("Unhandled AppDomain exception object: {ExceptionObject}. IsTerminating={IsTerminating}", e.ExceptionObject, e.IsTerminating);
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Log.Error(e.Exception, "Unobserved task exception.");
            };

            _globalExceptionLoggingRegistered = true;
        }

        private static string ResolveApplicationVersion()
        {
            try
            {
                return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private enum LegacyConfigMigrationStatus
        {
            NotRequired,
            Migrated,
            Failed
        }

        private readonly record struct LegacyConfigMigrationResult(
            LegacyConfigMigrationStatus Status,
            string SourcePath,
            string DestinationPath,
            string Error)
        {
            public static LegacyConfigMigrationResult NotRequired()
            {
                return new LegacyConfigMigrationResult(LegacyConfigMigrationStatus.NotRequired, string.Empty, string.Empty, string.Empty);
            }

            public static LegacyConfigMigrationResult Migrated(string sourcePath, string destinationPath)
            {
                return new LegacyConfigMigrationResult(LegacyConfigMigrationStatus.Migrated, sourcePath, destinationPath, string.Empty);
            }

            public static LegacyConfigMigrationResult Failed(string sourcePath, string destinationPath, string error)
            {
                return new LegacyConfigMigrationResult(LegacyConfigMigrationStatus.Failed, sourcePath, destinationPath, error ?? string.Empty);
            }
        }
    }
}
