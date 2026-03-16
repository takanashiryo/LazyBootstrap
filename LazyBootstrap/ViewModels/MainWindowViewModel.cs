using System;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LazyBootstrap.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private bool _startupInitialized;
        private bool _secondaryPagesWarmed;

        public MainWindowViewModel()
            : this(
                new ShellStateService(),
                new LaunchPageViewModel(),
                new SettingsPageViewModel(),
                new DisplayConfigurationPageViewModel(),
                new ToolsPageViewModel(),
                new InfoPageViewModel())
        {
        }

        public MainWindowViewModel(
            IShellStateService shellStateService,
            LaunchPageViewModel launch,
            SettingsPageViewModel settings,
            DisplayConfigurationPageViewModel display,
            ToolsPageViewModel tools,
            InfoPageViewModel info)
        {
            _shellStateService = shellStateService ?? throw new ArgumentNullException(nameof(shellStateService));
            Launch = launch;
            Settings = settings;
            Display = display;
            Tools = tools;
            Info = info;
            _shellStateService.PropertyChanged += OnShellStatePropertyChanged;
        }

        private readonly IShellStateService _shellStateService;

        public LaunchPageViewModel Launch { get; }

        public SettingsPageViewModel Settings { get; }

        public DisplayConfigurationPageViewModel Display { get; }

        public ToolsPageViewModel Tools { get; }

        public InfoPageViewModel Info { get; }

        public string StatusText
        {
            get => _shellStateService.StatusText;
            set => _shellStateService.StatusText = value ?? string.Empty;
        }

        public double StatusProgressValue
        {
            get => _shellStateService.StatusProgressValue;
            set => _shellStateService.StatusProgressValue = value;
        }

        public bool IsStatusProgressVisible
        {
            get => _shellStateService.IsStatusProgressVisible;
            set => _shellStateService.IsStatusProgressVisible = value;
        }

        public bool IsInteractionEnabled
        {
            get => _shellStateService.IsInteractionEnabled;
            set => _shellStateService.IsInteractionEnabled = value;
        }

        public ShellPage SelectedPage
        {
            get => _shellStateService.SelectedPage;
            set => _shellStateService.SelectedPage = value;
        }

        public async Task InitializeStartupAsync()
        {
            if (_startupInitialized)
            {
                return;
            }

            await Settings.InitializeStartupAsync();
            Launch.AttachContext(Settings, Display);
            await Launch.InitializeStartupAsync();
            _startupInitialized = true;
        }

        public async Task WarmSecondaryPagesAsync()
        {
            if (_secondaryPagesWarmed)
            {
                return;
            }

            await Settings.WarmDeferredAsync();
            await Display.WarmDeferredAsync();
            await Info.InitializeInfoAsync();
            await Info.RunEnvironmentScanAsync();
            _secondaryPagesWarmed = true;
        }

        public Task HandleClosingAsync()
        {
            return Launch.HandleClosingAsync();
        }

        private void OnShellStatePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IShellStateService.StatusText):
                    OnPropertyChanged(nameof(StatusText));
                    break;
                case nameof(IShellStateService.StatusProgressValue):
                    OnPropertyChanged(nameof(StatusProgressValue));
                    break;
                case nameof(IShellStateService.IsStatusProgressVisible):
                    OnPropertyChanged(nameof(IsStatusProgressVisible));
                    break;
                case nameof(IShellStateService.IsInteractionEnabled):
                    OnPropertyChanged(nameof(IsInteractionEnabled));
                    break;
                case nameof(IShellStateService.SelectedPage):
                    OnPropertyChanged(nameof(SelectedPage));
                    break;
            }
        }
    }
}
