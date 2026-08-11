using System;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using LazyBootstrap.FileSystem;
using LazyBootstrap.Platform;
using LazyBootstrap.Services;
using LazyBootstrap.Serialization;

namespace LazyBootstrap.UI
{
    public enum ShellPage
    {
        Launch,
        Settings,
        Display,
        Tools,
        Update,
        Diag,
        About
    }

    public partial class MainWindow : SukiWindow
    {
        private readonly LauncherPaths _paths = null!;
        private readonly ISukiDialogManager _dialogManager = null!;
        private readonly ISukiToastManager _toastManager = null!;
        private readonly ConfigHandler _configHandler = null!;
        private readonly ILogger<MainWindow> _logger = null!;

        public MainWindow()
        {
            InitializeComponent();

            if (!Design.IsDesignMode)
            {
                throw new InvalidOperationException("MainWindow must be created from the application composition root.");
            }
        }

        internal MainWindow(
            LauncherPaths paths,
            SpiceCrashLogAnalyzer spiceCrashLogAnalyzer,
            GameProcessTracker gameProcessTracker,
            WindowsDefenderExclusionService windowsDefenderExclusionService,
            WindowsAppCompatLayerService appCompatLayerService,
            SpiceConfigFile spiceConfigFile,
            GpuCompatLayerConfigurator gpuCompatLayerConfigurator,
            WindowsStartupService windowsStartupService,
            WindowsDisplayConfigurationService displayConfigurationService,
            DisplaySettingsTransactionCoordinator displaySettingsTransactionCoordinator,
            SavedataTransferService savedataTransferService,
            ISukiDialogManager dialogManager,
            ISukiToastManager toastManager,
            ConfigHandler configHandler,
            ILogger<MainWindow> logger)
        {
            InitializeComponent();

            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _spiceCrashLogAnalyzer = spiceCrashLogAnalyzer ?? throw new ArgumentNullException(nameof(spiceCrashLogAnalyzer));
            _gameProcessTracker = gameProcessTracker ?? throw new ArgumentNullException(nameof(gameProcessTracker));
            _windowsDefenderExclusionService = windowsDefenderExclusionService ?? throw new ArgumentNullException(nameof(windowsDefenderExclusionService));
            InitializeSettingsServices(spiceConfigFile, gpuCompatLayerConfigurator, appCompatLayerService, windowsStartupService);
            InitializeDisplayServices(displayConfigurationService, displaySettingsTransactionCoordinator);
            InitializeToolsServices(savedataTransferService);
            _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
            _toastManager = toastManager ?? throw new ArgumentNullException(nameof(toastManager));
            _configHandler = configHandler ?? throw new ArgumentNullException(nameof(configHandler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (DialogHost != null)
            {
                DialogHost.Manager = _dialogManager;
            }

            if (ToastHost != null)
            {
                ToastHost.Manager = _toastManager;
            }

            Opened += OnWindowOpened;
            Closed += OnWindowClosed;
            if (MainSideMenu != null)
            {
                MainSideMenu.SelectionChanged += OnMainSideMenuSelectionChanged;
            }

            InitializeCustomComponents();
            _logger.LogInformation("Main window initialized for base directory {BaseDirectory}.", _paths.BaseDir);
        }
    }
}
