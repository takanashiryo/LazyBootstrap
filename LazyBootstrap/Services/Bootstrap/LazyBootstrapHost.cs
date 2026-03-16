using System;
using SystemEnvironment = System.Environment;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace LazyBootstrap.Services.Bootstrap
{
    internal static class LazyBootstrapHost
    {
        public static IServiceProvider Services { get; private set; }

        public static LauncherRuntimeContext RuntimeContext { get; private set; }

        public static void Initialize(string[] args)
        {
            if (Services != null)
            {
                return;
            }

            string baseDirectoryPath = AppPathResolver.ResolveBaseDir(
                args,
                SystemEnvironment.GetEnvironmentVariable("LAZYBOOTSTRAP_BASEDIR"),
                AppDomain.CurrentDomain.BaseDirectory);
            string applicationDirectoryPath = AppDomain.CurrentDomain.BaseDirectory;
            string configFilePath = Path.Combine(baseDirectoryPath, "config.toml");

            RuntimeContext = new LauncherRuntimeContext(
                baseDirectoryPath,
                applicationDirectoryPath,
                configFilePath);

            Log.Logger = CreateLogger(RuntimeContext);

            var services = new ServiceCollection();
            ConfigureServices(services, RuntimeContext);
            Services = services.BuildServiceProvider();
        }

        public static void Dispose()
        {
            if (Services is IDisposable disposable)
            {
                disposable.Dispose();
            }

            Services = null;
            Log.CloseAndFlush();
        }

        private static void ConfigureServices(IServiceCollection services, LauncherRuntimeContext runtimeContext)
        {
            services.AddSingleton(runtimeContext);
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(Log.Logger, dispose: false);
            });

            services.AddSingleton<IConfigHandler>(_ =>
            {
                var configHandler = new ConfigHandler(runtimeContext.ConfigFilePath);
                AppConfigBootstrapper.InitializeAndMigrate(runtimeContext.ConfigFilePath, configHandler);
                return configHandler;
            });
            services.AddSingleton<ILauncherPaths>(_ => new LauncherPaths(
                runtimeContext.BaseDirectoryPath,
                runtimeContext.ApplicationDirectoryPath,
                runtimeContext.ConfigFilePath));
            services.AddSingleton<ISavedataTransferPlanner, SavedataTransferPlanner>();
            services.AddSingleton<IGameProcessTracker, GameProcessTracker>();
            services.AddSingleton<ISpiceConfigFileService, SpiceConfigFileService>();
            services.AddSingleton<IWindowsDefenderExclusionService, WindowsDefenderExclusionService>();
            services.AddSingleton<IDisplayConfigurationService, WindowsDisplayConfigurationService>();
            services.AddSingleton<IDisplaySettingsTransactionCoordinator, DisplaySettingsTransactionCoordinator>();
            services.AddSingleton<ICompatibilitySettingsService, CompatibilitySettingsService>();
            services.AddSingleton<ISukiDialogManager>(_ => new SukiDialogManager());
            services.AddSingleton<ISukiToastManager>(_ => new SukiToastManager());
            services.AddSingleton<IShellStateService, ShellStateService>();
            services.AddSingleton<IUiInteractionService, UiInteractionService>();
            services.AddSingleton<ILaunchWorkflowService, LaunchWorkflowService>();
            services.AddSingleton<ISettingsWorkflowService, SettingsWorkflowService>();
            services.AddSingleton<IDisplayWorkflowService, DisplayWorkflowService>();
            services.AddSingleton<IToolsWorkflowService, ToolsWorkflowService>();
            services.AddSingleton<IEnvironmentScanService, EnvironmentScanService>();

            services.AddSingleton<LaunchPageViewModel>();
            services.AddSingleton<SettingsPageViewModel>();
            services.AddSingleton<DisplayConfigurationPageViewModel>();
            services.AddSingleton<ToolsPageViewModel>();
            services.AddSingleton<InfoPageViewModel>();
            services.AddSingleton<MainWindowViewModel>();

            services.AddTransient(sp => new MainWindow(
                sp.GetRequiredService<MainWindowViewModel>(),
                sp.GetRequiredService<IConfigHandler>(),
                sp.GetRequiredService<ILauncherPaths>(),
                sp.GetRequiredService<ISpiceConfigFileService>(),
                sp.GetRequiredService<IDisplayConfigurationService>(),
                sp.GetRequiredService<IDisplaySettingsTransactionCoordinator>(),
                sp.GetRequiredService<ISettingsWorkflowService>(),
                sp.GetRequiredService<ISukiDialogManager>(),
                sp.GetRequiredService<ISukiToastManager>(),
                sp.GetRequiredService<IUiInteractionService>(),
                sp.GetRequiredService<ILogger<MainWindow>>()));
        }

        private static Serilog.ILogger CreateLogger(LauncherRuntimeContext runtimeContext)
        {
            Directory.CreateDirectory(runtimeContext.ApplicationDirectoryPath);
            string logFilePath = Path.Combine(runtimeContext.ApplicationDirectoryPath, "LazyBootstrap.log");

            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    logFilePath,
                    shared: true)
                .CreateLogger();
        }
    }
}
