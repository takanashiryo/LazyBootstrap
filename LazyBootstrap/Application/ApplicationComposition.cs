using System;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using LazyBootstrap.Models;
using LazyBootstrap.Services;
using LazyBootstrap.Platform;
using LazyBootstrap.Serialization;
using LazyBootstrap.UI;

namespace LazyBootstrap.Application
{
    /// <summary>
    /// Explicitly constructs the application's fixed object graph without a DI container.
    /// </summary>
    internal sealed class ApplicationComposition : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly LauncherPaths _paths;
        private SukiToastManager _toastManager;
        private MainWindow _mainWindow;
        private bool _disposed;

        public ApplicationComposition(LauncherRuntimeContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            _loggerFactory = new SerilogLoggerFactory(Log.Logger, dispose: false);
            _paths = new LauncherPaths(
                context.BaseDirectoryPath,
                context.ApplicationDirectoryPath,
                context.ConfigFilePath);
            ConfigHandler = new ConfigHandler(
                context.ConfigFilePath,
                CreateLogger<ConfigHandler>());
        }

        public ConfigHandler ConfigHandler { get; }

        public MainWindow CreateMainWindow()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_mainWindow != null)
            {
                return _mainWindow;
            }

            // SukiUI managers must be constructed only after App.Initialize loads SukiTheme.
            var dialogManager = new SukiDialogManager();
            var toastManager = new SukiToastManager();

            try
            {
                var spiceConfigFile = new SpiceConfigFile();
                var displayConfigurationService = new WindowsDisplayConfigurationService();
                var defenderExclusionService = new WindowsDefenderExclusionService(
                    CreateLogger<WindowsDefenderExclusionService>());
                var appCompatLayerService = new WindowsAppCompatLayerService();
                var startupService = new WindowsStartupService();
                var gameProcessTracker = new GameProcessTracker();
                var savedataTransferPlanner = new SavedataTransferPlanner(_paths);
                var displayTransactionCoordinator = new DisplaySettingsTransactionCoordinator(
                    displayConfigurationService);
                var gpuCompatLayerConfigurator = new GpuCompatLayerConfigurator(
                    ConfigHandler,
                    _paths,
                    spiceConfigFile,
                    CreateLogger<GpuCompatLayerConfigurator>());
                var spiceCrashLogAnalyzer = new SpiceCrashLogAnalyzer(
                    _paths,
                    CreateLogger<SpiceCrashLogAnalyzer>());
                var uiInteractionService = new UiInteractionService(dialogManager, toastManager);

                var displayOrchestrator = new DisplayOrchestrator(
                    ConfigHandler,
                    _paths,
                    spiceConfigFile,
                    displayConfigurationService,
                    displayTransactionCoordinator,
                    uiInteractionService,
                    CreateLogger<DisplayOrchestrator>());
                var launchOrchestrator = new LaunchOrchestrator(
                    _paths,
                    spiceCrashLogAnalyzer,
                    gameProcessTracker,
                    displayOrchestrator,
                    defenderExclusionService,
                    appCompatLayerService,
                    uiInteractionService,
                    CreateLogger<LaunchOrchestrator>());
                var settingsOrchestrator = new SettingsOrchestrator(
                    ConfigHandler,
                    _paths,
                    spiceConfigFile,
                    gpuCompatLayerConfigurator,
                    appCompatLayerService,
                    startupService,
                    uiInteractionService,
                    CreateLogger<SettingsOrchestrator>());
                var toolsOrchestrator = new ToolsOrchestrator(
                    _paths,
                    savedataTransferPlanner,
                    uiInteractionService,
                    CreateLogger<ToolsOrchestrator>());
                var updateOrchestrator = new UpdateOrchestrator(
                    _paths,
                    uiInteractionService,
                    CreateLogger<UpdateOrchestrator>());
                var diagnosticOrchestrator = new DiagnosticOrchestrator(
                    _paths,
                    uiInteractionService,
                    CreateLogger<DiagnosticOrchestrator>());

                _mainWindow = new MainWindow(
                    _paths,
                    launchOrchestrator,
                    settingsOrchestrator,
                    displayOrchestrator,
                    diagnosticOrchestrator,
                    toolsOrchestrator,
                    updateOrchestrator,
                    dialogManager,
                    toastManager,
                    uiInteractionService,
                    ConfigHandler,
                    CreateLogger<MainWindow>());
                _toastManager = toastManager;

                return _mainWindow;
            }
            catch
            {
                toastManager.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _toastManager?.Dispose();
            _loggerFactory.Dispose();
        }

        private ILogger<T> CreateLogger<T>()
        {
            return _loggerFactory.CreateLogger<T>();
        }
    }
}
