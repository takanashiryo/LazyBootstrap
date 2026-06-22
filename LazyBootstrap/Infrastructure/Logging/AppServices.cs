using System;
using SystemEnvironment = System.Environment;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Serilog;
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
            string applicationDirectoryPath = AppDomain.CurrentDomain.BaseDirectory;
            string configFilePath = Path.Combine(baseDirectoryPath, "config.toml");

            RuntimeContext = new LauncherRuntimeContext(baseDirectoryPath, applicationDirectoryPath, configFilePath);
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
    }
}
