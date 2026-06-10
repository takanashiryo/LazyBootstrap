using System;
using SystemEnvironment = System.Environment;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;
using LazyBootstrap.Models;
using LazyBootstrap.Services.Config;
using LazyBootstrap.Services.Display;
using LazyBootstrap.Services.Launch;
using LazyBootstrap.Services.Paths;
using LazyBootstrap.Services.Processes;
using LazyBootstrap.Services.Savedata;
using LazyBootstrap.Services.Security;
using LazyBootstrap.Services.Settings;
using LazyBootstrap.Services.Shell;
using LazyBootstrap.Services.Tools;
using LazyBootstrap.Services.UI;
using LazyBootstrap.Services.Update;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace LazyBootstrap.Services
{
    public static class AppServices
    {
        private static bool _initialized;
        private static bool _globalExceptionLoggingRegistered;
        private static ILoggerFactory _loggerFactory;
        private const string LogOutputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{ProcessId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

        public static LauncherRuntimeContext RuntimeContext { get; private set; }
        public static ConfigHandler Config { get; private set; }
        public static LauncherPaths Paths { get; private set; }
        public static SpiceConfigFileService SpiceConfig { get; private set; }
        public static WindowsDisplayConfigurationService DisplayConfig { get; private set; }
        public static WindowsDefenderExclusionService DefenderExclusion { get; private set; }
        public static GameProcessTracker GameProcess { get; private set; }
        public static ShellStateService ShellState { get; private set; }
        public static ISukiDialogManager DialogManager { get; private set; }
        public static ISukiToastManager ToastManager { get; private set; }
        public static SavedataTransferPlanner Savedata { get; private set; }
        public static DisplaySettingsTransactionCoordinator DisplayTransaction { get; private set; }
        public static GpuCompatLayerService GpuCompat { get; private set; }
        public static UiInteractionService UI { get; private set; }
        public static DisplayWorkflowService DisplayWorkflow { get; private set; }
        public static LaunchWorkflowService LaunchWorkflow { get; private set; }
        public static SettingsWorkflowService SettingsWorkflow { get; private set; }
        public static ToolsWorkflowService ToolsWorkflow { get; private set; }
        public static UpdateWorkflowService UpdateWorkflow { get; private set; }
        public static EnvironmentScanService EnvironmentScan { get; private set; }

        public static ILogger<T> CreateLogger<T>() => _loggerFactory?.CreateLogger<T>();

        public static void InitializeSerilog(string[] args)
        {
            if (_loggerFactory != null) return;

            EnsureRuntimeContext(args);

            Directory.CreateDirectory(RuntimeContext.ApplicationDirectoryPath);
            string logFilePath = Path.Combine(RuntimeContext.ApplicationDirectoryPath, "LazyBootstrap.log");
            string applicationVersion = ResolveApplicationVersion();

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

            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(Log.Logger, dispose: false);
            });

            Log.Information(
                "Serilog initialized. Version={Version}, ProcessId={ProcessId}, BaseDir={BaseDirectory}, ApplicationDir={ApplicationDirectory}, ConfigPath={ConfigPath}, LogPath={LogPath}",
                applicationVersion,
                SystemEnvironment.ProcessId,
                RuntimeContext.BaseDirectoryPath,
                RuntimeContext.ApplicationDirectoryPath,
                RuntimeContext.ConfigFilePath,
                logFilePath);
        }

        public static void Initialize(string[] args)
        {
            if (_initialized) return;

            InitializeSerilog(args);
            EnsureRuntimeContext(args);

            Log.Information("Initializing core services.");
            Config = new ConfigHandler(RuntimeContext.ConfigFilePath);
            AppConfigBootstrapper.InitializeAndMigrate(RuntimeContext.ConfigFilePath, Config);
            Log.Information("Configuration initialized and migrated.");

            Paths = new LauncherPaths(RuntimeContext.BaseDirectoryPath, RuntimeContext.ApplicationDirectoryPath, RuntimeContext.ConfigFilePath);

            SpiceConfig = new SpiceConfigFileService();
            DisplayConfig = new WindowsDisplayConfigurationService();
            DefenderExclusion = new WindowsDefenderExclusionService(CreateLogger<WindowsDefenderExclusionService>());
            GameProcess = new GameProcessTracker();
            ShellState = new ShellStateService();
            // Suki managers must be created AFTER SukiTheme is loaded.
            // They are initialized lazily in InitSukiManagers() called from App.axaml.cs.

            Savedata = new SavedataTransferPlanner(Paths);
            DisplayTransaction = new DisplaySettingsTransactionCoordinator(DisplayConfig);

            GpuCompat = new GpuCompatLayerService(Config, Paths, SpiceConfig, CreateLogger<GpuCompatLayerService>());

            // Services that depend on SukiUI (DialogManager/ToastManager) are deferred
            // to InitSukiManagers() which must be called AFTER SukiTheme is loaded.

            _initialized = true;
            Log.Information("Core services initialized.");
        }

        private static void EnsureRuntimeContext(string[] args)
        {
            if (RuntimeContext != null) return;

            string baseDirectoryPath = AppPathResolver.ResolveBaseDir(
                args,
                SystemEnvironment.GetEnvironmentVariable("LAZYBOOTSTRAP_BASEDIR"),
                AppDomain.CurrentDomain.BaseDirectory);
            string applicationDirectoryPath = AppDomain.CurrentDomain.BaseDirectory;
            string configFilePath = System.IO.Path.Combine(baseDirectoryPath, "config.toml");

            RuntimeContext = new LauncherRuntimeContext(baseDirectoryPath, applicationDirectoryPath, configFilePath);
        }

        /// <summary>
        /// Initializes SukiUI-dependent services. Must be called from App.Initialize()
        /// after SukiTheme.Load() to avoid rendering issues.
        /// </summary>
        public static void InitSukiManagers()
        {
            if (DialogManager != null) return;

            Log.Information("Initializing SukiUI-dependent services.");
            DialogManager = new SukiDialogManager();
            ToastManager = new SukiToastManager();
            UI = new UiInteractionService(DialogManager, ToastManager);

            DisplayWorkflow = new DisplayWorkflowService(Config, Paths, SpiceConfig, DisplayConfig, DisplayTransaction, UI, CreateLogger<DisplayWorkflowService>());
            LaunchWorkflow = new LaunchWorkflowService(Paths, GameProcess, DisplayWorkflow, DefenderExclusion, UI, ShellState, CreateLogger<LaunchWorkflowService>());
            SettingsWorkflow = new SettingsWorkflowService(Config, Paths, SpiceConfig, GpuCompat, UI, CreateLogger<SettingsWorkflowService>());
            ToolsWorkflow = new ToolsWorkflowService(Paths, Savedata, UI, ShellState, CreateLogger<ToolsWorkflowService>());
            UpdateWorkflow = new UpdateWorkflowService(Paths, UI, ShellState, CreateLogger<UpdateWorkflowService>());
            EnvironmentScan = new EnvironmentScanService(Paths, ShellState, UI, CreateLogger<EnvironmentScanService>());
            Log.Information("SukiUI-dependent services initialized.");
        }

        public static void Dispose()
        {
            Log.Information("LazyBootstrap services are shutting down.");
            _loggerFactory?.Dispose();
            _loggerFactory = null;
            Log.CloseAndFlush();
            _initialized = false;
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
