using System;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace LazyBootstrap.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Composition root for the dependency-injection container. Registers every service
    /// the application needs and wires the object graph that previously lived in the
    /// <c>AppServices</c> static service locator.
    /// </summary>
    /// <remarks>
    /// SukiUI timing constraint: <see cref="SukiDialogManager"/> and <see cref="SukiToastManager"/>
    /// must only be instantiated after <c>SukiTheme</c> is loaded. They are registered as lazy
    /// singletons here and are first created when <see cref="LazyBootstrap.Shell.MainWindow"/> is
    /// resolved in <c>App.OnFrameworkInitializationCompleted</c> (which runs after
    /// <c>App.Initialize</c> loads the theme). The provider must therefore be built WITHOUT
    /// <c>ValidateOnBuild</c>, otherwise every singleton would be eagerly created up-front and
    /// the managers would be constructed before the theme exists.
    /// </remarks>
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLazyBootstrapServices(
            this IServiceCollection services,
            LauncherRuntimeContext context)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(context);

            // Logging: bridge Microsoft.Extensions.Logging onto the already-configured Serilog logger.
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(Log.Logger, dispose: false);
            });

            // Runtime context and configuration / paths (no SukiUI dependency).
            services.AddSingleton(context);
            services.AddSingleton(_ => new ConfigHandler(context.ConfigFilePath));
            services.AddSingleton(_ => new LauncherPaths(
                context.BaseDirectoryPath,
                context.ApplicationDirectoryPath,
                context.ConfigFilePath));

            // Workers (no SukiUI dependency).
            services.AddSingleton<SpiceConfigFile>();
            services.AddSingleton<WindowsDisplayConfigurationService>();
            services.AddSingleton<WindowsDefenderExclusionService>();
            services.AddSingleton<GameProcessTracker>();
            services.AddSingleton<AppShellState>();
            services.AddSingleton<SavedataTransferPlanner>();
            services.AddSingleton<DisplaySettingsTransactionCoordinator>();
            services.AddSingleton<GpuCompatLayerConfigurator>();
            services.AddSingleton<SpiceCrashLogAnalyzer>();

            // SukiUI-dependent services: lazy singletons (see remarks on the timing constraint).
            services.AddSingleton<ISukiDialogManager, SukiDialogManager>();
            services.AddSingleton<ISukiToastManager, SukiToastManager>();
            services.AddSingleton<UiInteractionService>();

            // Orchestrators (feature workflow coordinators).
            services.AddSingleton<DisplayOrchestrator>();
            services.AddSingleton<LaunchOrchestrator>();
            services.AddSingleton<SettingsOrchestrator>();
            services.AddSingleton<ToolsOrchestrator>();
            services.AddSingleton<UpdateOrchestrator>();
            services.AddSingleton<DiagnosticOrchestrator>();

            // Feature shared state.
            services.AddSingleton<DisplayConfigurationSnapshot>();
            services.AddSingleton<LaunchState>();
            services.AddSingleton<SettingsState>();
            services.AddSingleton<EnvironmentScanPresentation>();

            // Shell window. MainWindow exposes an internal parameterised constructor that the
            // default DI constructor selection cannot see, so build it through an explicit
            // same-assembly factory.
            services.AddSingleton(provider => new Shell.MainWindow(
                provider.GetRequiredService<AppShellState>(),
                provider.GetRequiredService<LauncherPaths>(),
                provider.GetRequiredService<LaunchState>(),
                provider.GetRequiredService<LaunchOrchestrator>(),
                provider.GetRequiredService<SettingsState>(),
                provider.GetRequiredService<SettingsOrchestrator>(),
                provider.GetRequiredService<DisplayConfigurationSnapshot>(),
                provider.GetRequiredService<DisplayOrchestrator>(),
                provider.GetRequiredService<EnvironmentScanPresentation>(),
                provider.GetRequiredService<DiagnosticOrchestrator>(),
                provider.GetRequiredService<ToolsOrchestrator>(),
                provider.GetRequiredService<UpdateOrchestrator>(),
                provider.GetRequiredService<ISukiDialogManager>(),
                provider.GetRequiredService<ISukiToastManager>(),
                provider.GetRequiredService<UiInteractionService>(),
                provider.GetRequiredService<ILogger<Shell.MainWindow>>()));

            return services;
        }
    }
}
