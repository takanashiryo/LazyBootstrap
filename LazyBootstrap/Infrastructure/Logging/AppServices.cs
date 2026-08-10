using System;
using SystemEnvironment = System.Environment;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Serilog;
using LazyBootstrap.Infrastructure.DependencyInjection;
using LazyBootstrap.Infrastructure.Paths;

namespace LazyBootstrap.Infrastructure.Logging
{
    /// <summary>
    /// Startup bootstrap helpers: Serilog configuration, runtime-context resolution and
    /// global exception logging. The application object graph is built by the
    /// dependency-injection container (see <see cref="ServiceRegistration"/>), not here.
    /// </summary>
    public static class AppServices
    {
        private static bool _serilogInitialized;
        private static bool _globalExceptionLoggingRegistered;
        private static LegacyConfigMigrationResult _legacyConfigMigrationResult = LegacyConfigMigrationResult.NotRequired();
        private const string LogOutputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{ProcessId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

        public static LauncherRuntimeContext RuntimeContext { get; private set; }

        public static void InitializeSerilog(string[] args)
        {
            if (_serilogInitialized) return;

            EnsureRuntimeContext(args);

            Directory.CreateDirectory(RuntimeContext.ApplicationDirectoryPath);
            string logFilePath = Path.Combine(RuntimeContext.ApplicationDirectoryPath, "LazyBootstrap.log");
            string applicationVersion = ResolveApplicationVersion();
            ResetLogFile(logFilePath);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "LazyBootstrap")
                .Enrich.WithProperty("ApplicationVersion", applicationVersion)
                .Enrich.WithProperty("ProcessId", SystemEnvironment.ProcessId)
                .Enrich.WithProperty("BaseDirectory", RuntimeContext.BaseDirectoryPath)
                .Enrich.WithProperty("ApplicationDirectory", RuntimeContext.ApplicationDirectoryPath)
                .Enrich.WithProperty("ConfigPath", RuntimeContext.ConfigFilePath)
                .WriteTo.File(logFilePath, outputTemplate: LogOutputTemplate, shared: true)
                .CreateLogger();

            RegisterGlobalExceptionLogging();
            _serilogInitialized = true;

            LogLegacyConfigMigrationResult();
            Log.Information(
                "Serilog initialized. Version={Version}, ProcessId={ProcessId}, BaseDir={BaseDirectory}, ApplicationDir={ApplicationDirectory}, ConfigPath={ConfigPath}, LogPath={LogPath}",
                applicationVersion,
                SystemEnvironment.ProcessId,
                RuntimeContext.BaseDirectoryPath,
                RuntimeContext.ApplicationDirectoryPath,
                RuntimeContext.ConfigFilePath,
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

        private static void EnsureRuntimeContext(string[] args)
        {
            if (RuntimeContext != null) return;

            string baseDirectoryPath = AppPathResolver.ResolveBaseDir(
                args,
                SystemEnvironment.GetEnvironmentVariable("LAZYBOOTSTRAP_BASEDIR"),
                AppDomain.CurrentDomain.BaseDirectory);
            string applicationDirectoryPath = PathHelper.NormalizePath(AppDomain.CurrentDomain.BaseDirectory);
            string configFilePath = PathHelper.NormalizePath(Path.Combine(applicationDirectoryPath, "config.toml"));
            string legacyConfigFilePath = PathHelper.NormalizePath(Path.Combine(baseDirectoryPath, "config.toml"));

            _legacyConfigMigrationResult = MigrateLegacyConfig(legacyConfigFilePath, configFilePath);

            RuntimeContext = new LauncherRuntimeContext(baseDirectoryPath, applicationDirectoryPath, configFilePath);
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
