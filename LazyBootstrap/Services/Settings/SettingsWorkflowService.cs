using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using LazyBootstrap.Services.Launch;
using Microsoft.Extensions.Logging;

namespace LazyBootstrap.Services.Settings
{

    public sealed class SettingsWorkflowService
    {
        private const string NonePresetName = "无";
        private const string AsphyxiaPresetName = "Asphyxia";
        private const string AsphyxiaDefaultUrl = "http://localhost:8083";
        private const string UseSystemConfigKey = "use-system-config";
        private const string MissingSpiceConfigMessage = "未找到任何spice2x配置文件";

        private readonly ConfigHandler _configHandler;
        private readonly LauncherPaths _paths;
        private readonly SpiceConfigFileService _spiceConfigFileService;
        private readonly GpuCompatLayerService _gpuCompatLayerService;
        private readonly UiInteractionService _uiInteractionService;
        private readonly ILogger<SettingsWorkflowService> _logger;

        private sealed record SpiceOptionDescriptor(
            string XmlName,
            Func<SettingsConfigurationSnapshot, string> GetXmlValue,
            Action<SettingsConfigurationSnapshot, string> ApplyXmlValue);

        private static SpiceOptionDescriptor B(string name,
            Func<SettingsConfigurationSnapshot, bool> getter,
            Action<SettingsConfigurationSnapshot, bool> setter,
            string enabledValue) => new(name,
                vm => getter(vm) ? enabledValue : string.Empty,
                (vm, xmlValue) => setter(vm, string.Equals(xmlValue, enabledValue, StringComparison.OrdinalIgnoreCase)));

        private static SpiceOptionDescriptor S(string name,
            Func<SettingsConfigurationSnapshot, string> getter,
            Action<SettingsConfigurationSnapshot, string> setter) => new(name,
                vm => getter(vm) ?? string.Empty,
                (vm, xmlValue) => setter(vm, xmlValue ?? string.Empty));

        private static readonly SpiceOptionDescriptor[] GeneralSpiceOptions =
        [
            B("w", vm => vm.Windowed, (vm, v) => vm.Windowed = v, "/ENABLED"),
            S("k", vm => vm.DllInjection, (vm, v) => vm.DllInjection = v),
            B("sp2x-processefficiency", vm => vm.PCoreOptimization, (vm, v) => vm.PCoreOptimization = v, "pcores"),
            B("sp2x-sdvxnosub", vm => vm.DisableSubDisplay, (vm, v) => vm.DisableSubDisplay = v, "/ENABLED"),
            new("sp2x-windowborder",
                vm => vm.WindowModeIndex switch { 1 => "1", 2 => "2", _ => "" },
                (vm, v) => vm.WindowModeIndex = v switch { "1" => 1, "2" => 2, _ => 0 }),
            B("sdvxwsubborderless", vm => vm.SubBorderless, (vm, v) => vm.SubBorderless = v, "/ENABLED"),
            B("s", vm => vm.ShowCursorTouchSim, (vm, v) => vm.ShowCursorTouchSim = v, "/ENABLED"),
            B("sp2x-windowalwaysontop", vm => vm.WindowTopMost, (vm, v) => vm.WindowTopMost = v, "/ENABLED"),
            S("sp2x-windowsize", vm => vm.WindowSize, (vm, v) => vm.WindowSize = v),
            B("graphics-force-single-adapter", vm => vm.SingleAdapter, (vm, v) => vm.SingleAdapter = v, "/ENABLED"),
            B("sp2x-nvprofile", vm => vm.NvidiaPerformanceProfile, (vm, v) => vm.NvidiaPerformanceProfile = v, "/ENABLED"),
            B("sdvxwsubtop", vm => vm.SubWindowTopMost, (vm, v) => vm.SubWindowTopMost = v, "/ENABLED"),
            B("sp2x-sdvxsubredraw", vm => vm.SubForceRender, (vm, v) => vm.SubForceRender = v, "/ENABLED"),
            B("sdvxnativetouch", vm => vm.NativeTouch, (vm, v) => vm.NativeTouch = v, "/ENABLED"),
            new("sp2x-sdvxasio",
                vm => vm.SelectedAsioDriver?.Value ?? vm.AsioDriverValue ?? "",
                (vm, v) => vm.AsioDriverValue = v ?? ""),
            B("sdvxasio2ch", vm => vm.Asio2Ch, (vm, v) => vm.Asio2Ch = v, "/ENABLED"),
            new("volumeboost",
                vm => vm.VolumeBoostIndex switch
                {
                    1 => "3",
                    2 => "6",
                    3 => "9",
                    4 => "12",
                    5 => "15",
                    6 => "20",
                    7 => "25",
                    8 => "30",
                    _ => ""
                },
                (vm, v) => vm.VolumeBoostIndex = v switch
                {
                    "3" => 1,
                    "6" => 2,
                    "9" => 3,
                    "12" => 4,
                    "15" => 5,
                    "20" => 6,
                    "25" => 7,
                    "30" => 8,
                    _ => 0
                }),
            new("resample",
                vm => vm.ResampleIndex switch
                {
                    1 => "44100",
                    2 => "48000",
                    3 => "88200",
                    4 => "96000",
                    5 => "176400",
                    6 => "192000",
                    _ => ""
                },
                (vm, v) => vm.ResampleIndex = v switch
                {
                    "44100" => 1,
                    "48000" => 2,
                    "88200" => 3,
                    "96000" => 4,
                    "176400" => 5,
                    "192000" => 6,
                    _ => 0
                }),
            B("wasapishared", vm => vm.WasapiShared, (vm, v) => vm.WasapiShared = v, "/ENABLED"),
            B("sp2x-lowlatencysharedaudio", vm => vm.LowLatencySharedAudio, (vm, v) => vm.LowLatencySharedAudio = v, "/ENABLED"),
            B("cardio", vm => vm.CardIo, (vm, v) => vm.CardIo = v, "/ENABLED"),
            B("scard", vm => vm.HidSmartCard, (vm, v) => vm.HidSmartCard = v, "/ENABLED"),
            B("netdump", vm => vm.NetDump, (vm, v) => vm.NetDump = v, "/ENABLED"),
        ];

        private static readonly SpiceOptionDescriptor[] ExtraSpiceOptions =
        [
            new("network",
                vm => vm.NetworkAdapterIp ?? "",
                (vm, v) => vm.NetworkAdapterIp = ConfigHelper.NormalizeNetworkValue(v)),
            new("subnet",
                vm => vm.NetworkAdapterSubnet ?? "",
                (vm, v) => vm.NetworkAdapterSubnet = ConfigHelper.NormalizeNetworkValue(v)),
            S("url", vm => vm.ServerAddress, (vm, v) => vm.ServerAddress = v),
            S("p", vm => vm.PcbId, (vm, v) => vm.PcbId = v),
        ];

        private static readonly SpiceOptionDescriptor[] SpiceOptions =
            [.. GeneralSpiceOptions, .. ExtraSpiceOptions];

        private sealed class DeferredSettingsState
        {
            public required Dictionary<string, string> SpiceOptionValues { get; init; }

            public required List<AsioDriverOption> AsioDrivers { get; init; }

            public required AsioDriverOption SelectedAsioDriver { get; init; }

            public required List<NetworkAdapterOption> NetworkAdapters { get; init; }

            public required NetworkAdapterOption SelectedNetworkAdapter { get; init; }
        }

        public SettingsWorkflowService(
            ConfigHandler configHandler,
            LauncherPaths paths,
            SpiceConfigFileService spiceConfigFileService,
            GpuCompatLayerService gpuCompatLayerService,
            UiInteractionService uiInteractionService,
            ILogger<SettingsWorkflowService> logger)
        {
            _configHandler = configHandler ?? throw new ArgumentNullException(nameof(configHandler));
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _spiceConfigFileService = spiceConfigFileService ?? throw new ArgumentNullException(nameof(spiceConfigFileService));
            _gpuCompatLayerService = gpuCompatLayerService ?? throw new ArgumentNullException(nameof(gpuCompatLayerService));
            _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task InitializeStartupAsync(SettingsConfigurationSnapshot settings)
        {
            _logger.LogInformation("Settings startup initialization started.");
            settings.RunSilently(() =>
            {
                settings.NoAsphyxia = _configHandler.TryReadBool(AppConfigBootstrapper.SettingSectionName, "noasphyxia", false);
                settings.UseSystemSpiceConfig = _configHandler.TryReadBool(AppConfigBootstrapper.SettingSectionName, UseSystemConfigKey, false);
                settings.GpuCompatLayerRenderMode = GpuCompatLayerService.NormalizeRenderMode(_configHandler.ReadString(AppConfigBootstrapper.SettingSectionName, "cl-rendermode", "dx9on12"));
                settings.IsSpiceConfigAvailable = IsSpiceConfigAvailable(settings.UseSystemSpiceConfig);
                settings.SpiceConfigEmptyStateMessage = MissingSpiceConfigMessage;
            });
            RefreshGpuCompatLayerState(settings);

            LoadServerPresets(settings);
            _logger.LogInformation("Settings startup initialization completed. SpiceConfigAvailable={SpiceConfigAvailable}", settings.IsSpiceConfigAvailable);
            return Task.CompletedTask;
        }

        public async Task WarmDeferredAsync(SettingsConfigurationSnapshot settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _logger.LogInformation("Deferred settings warm-up started.");

            if (!RefreshSpiceConfigAvailability(settings))
            {
                settings.AsioDrivers.Clear();
                settings.NetworkAdapters.Clear();
                _logger.LogWarning("Deferred settings warm-up skipped because the active spice config is unavailable.");
                return;
            }

            var currentAsioDriverValue = settings.AsioDriverValue;
            var currentNetworkIp = settings.NetworkAdapterIp;
            var currentNetworkSubnet = settings.NetworkAdapterSubnet;

            var deferredState = await Task.Run(() =>
            {
                string spiceXmlPath = _paths.ResolveSpiceXmlPath(settings.UseSystemSpiceConfig);
                var optionValues = ReadSpiceOptionValues(spiceXmlPath);

                var asioDriverValue = optionValues.TryGetValue("sp2x-sdvxasio", out var asioVal) ? asioVal : string.Empty;
                var asioDrivers = BuildAsioDriverOptions(asioDriverValue);
                var selectedAsioDriver = asioDrivers.FirstOrDefault(choice =>
                                             string.Equals(choice.Value, asioDriverValue, StringComparison.OrdinalIgnoreCase))
                                         ?? asioDrivers.FirstOrDefault()
                                         ?? new AsioDriverOption("无", string.Empty);

                var networkIp = optionValues.TryGetValue("network", out var netIp) ? netIp : string.Empty;
                var networkSubnet = optionValues.TryGetValue("subnet", out var netSub) ? netSub : string.Empty;
                var networkAdapters = BuildNetworkAdapterOptions(networkIp, networkSubnet);
                var selectedNetworkAdapter = networkAdapters.FirstOrDefault(choice =>
                                                   string.Equals(choice.IpAddress, networkIp, StringComparison.OrdinalIgnoreCase)
                                                   && string.Equals(choice.SubnetMask, networkSubnet, StringComparison.OrdinalIgnoreCase))
                                               ?? networkAdapters.FirstOrDefault()
                                               ?? new NetworkAdapterOption("无", string.Empty, string.Empty);

                return new DeferredSettingsState
                {
                    SpiceOptionValues = optionValues,
                    AsioDrivers = asioDrivers,
                    SelectedAsioDriver = selectedAsioDriver,
                    NetworkAdapters = networkAdapters,
                    SelectedNetworkAdapter = selectedNetworkAdapter
                };
            });

            ApplyDeferredSettingsState(settings, deferredState, currentAsioDriverValue, currentNetworkIp, currentNetworkSubnet);
            _logger.LogInformation(
                "Deferred settings warm-up completed. AsioDriverCount={AsioDriverCount}, NetworkAdapterCount={NetworkAdapterCount}",
                settings.AsioDrivers.Count,
                settings.NetworkAdapters.Count);
        }

        public Task PersistLauncherSettingsAsync(SettingsConfigurationSnapshot settings)
        {
            try
            {
                _configHandler.WriteString(AppConfigBootstrapper.SettingSectionName, "noasphyxia", settings.NoAsphyxia.ToString().ToLowerInvariant());
                _logger.LogInformation("Launcher settings persisted.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist launcher settings.");
                _uiInteractionService.ShowErrorToast("保存设置失败", ex.Message);
                settings.RunSilently(() => settings.NoAsphyxia = _configHandler.TryReadBool(AppConfigBootstrapper.SettingSectionName, "noasphyxia", false));
            }

            return Task.CompletedTask;
        }

        public Task PersistServerEndpointAsync(SettingsConfigurationSnapshot settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _logger.LogInformation("Server endpoint persistence started.");

            settings.ServerAddress = (settings.ServerAddress ?? string.Empty).Trim();
            settings.PcbId = (settings.PcbId ?? string.Empty).Trim();

            if (!TryApplySpiceUpdates(
                    _paths.ResolveSpiceXmlPath(settings.UseSystemSpiceConfig),
                    settings,
                    reloadSettingsOnSuccess: false,
                    new SpiceOptionUpdate("url", settings.ServerAddress, false),
                    new SpiceOptionUpdate("p", settings.PcbId, false)))
            {
                ReloadRuntimeState(settings);
                return Task.CompletedTask;
            }

            SyncSelectedServerPresetFromCurrentFields(settings);
            SaveServerPresets(settings);
            _logger.LogInformation("Server endpoint persistence completed.");
            return Task.CompletedTask;
        }

        public Task ApplySelectedNetworkAdapterAsync(SettingsConfigurationSnapshot settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _logger.LogInformation("Selected network adapter application started.");

            var selectedAdapter = settings.SelectedNetworkAdapter;
            settings.RunSilently(() =>
            {
                settings.NetworkAdapterIp = selectedAdapter?.IpAddress ?? string.Empty;
                settings.NetworkAdapterSubnet = selectedAdapter?.SubnetMask ?? string.Empty;
            });

            return PersistNetworkSettingsAsync(settings);
        }

        public async Task OpenNetworkAdapterPickerAsync(SettingsConfigurationSnapshot settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _logger.LogInformation("Network adapter picker opened.");

            var choices = BuildNetworkAdapterOptions(settings.NetworkAdapterIp, settings.NetworkAdapterSubnet);
            var selectedChoice = choices.FirstOrDefault(choice =>
                                     string.Equals(choice.IpAddress, settings.NetworkAdapterIp ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                                     && string.Equals(choice.SubnetMask, settings.NetworkAdapterSubnet ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                                 ?? choices.FirstOrDefault();

            var adapterListBox = new ListBox
            {
                ItemsSource = choices,
                SelectedItem = selectedChoice,
                MinHeight = 240,
                MaxHeight = 360
            };

            var content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "请选择要读取参数的网卡。" },
                    adapterListBox
                }
            };

            var confirmed = await _uiInteractionService.ShowDialogAsync(
                "选择网卡",
                content,
                "确定",
                "取消");

            if (!confirmed)
            {
                _logger.LogInformation("Network adapter picker was cancelled.");
                return;
            }

            if (adapterListBox.SelectedItem is not NetworkAdapterOption choice)
            {
                _logger.LogWarning("Network adapter picker confirmed without a selected adapter.");
                _uiInteractionService.ShowWarningToast("选择网卡", "请选择一个网卡。");
                return;
            }

            settings.RunSilently(() =>
            {
                settings.SelectedNetworkAdapter = choice;
                settings.NetworkAdapterIp = choice.IpAddress;
                settings.NetworkAdapterSubnet = choice.SubnetMask;
            });

            await PersistNetworkSettingsAsync(settings);
            _logger.LogInformation("Network adapter picker selection persisted.");
        }

        public Task PersistNetworkSettingsAsync(SettingsConfigurationSnapshot settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _logger.LogInformation("Network settings persistence started.");

            settings.NetworkAdapterIp = ConfigHelper.NormalizeNetworkValue(settings.NetworkAdapterIp);
            settings.NetworkAdapterSubnet = ConfigHelper.NormalizeNetworkValue(settings.NetworkAdapterSubnet);

            if (!TryApplySpiceUpdates(
                    _paths.ResolveSpiceXmlPath(settings.UseSystemSpiceConfig),
                    settings,
                    false,
                    new SpiceOptionUpdate("network", settings.NetworkAdapterIp, false),
                    new SpiceOptionUpdate("subnet", settings.NetworkAdapterSubnet, false)))
            {
                ReloadRuntimeState(settings);
                return Task.CompletedTask;
            }

            SyncSelectedNetworkAdapter(settings, settings.NetworkAdapterIp, settings.NetworkAdapterSubnet);
            _logger.LogInformation("Network settings persistence completed.");
            return Task.CompletedTask;
        }

        public Task PersistSpiceSettingsAsync(SettingsConfigurationSnapshot settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _logger.LogInformation("Spice settings persistence started.");

            if (!TryApplySpiceUpdates(
                    _paths.ResolveSpiceXmlPath(settings.UseSystemSpiceConfig),
                    settings,
                    false,
                    BuildSpiceOptionUpdates(settings).ToArray()))
            {
                ReloadRuntimeState(settings);
                return Task.CompletedTask;
            }

            _logger.LogInformation("Spice settings persistence completed.");
            return Task.CompletedTask;
        }

        public Task PersistGpuCompatLayerToggleAsync(SettingsConfigurationSnapshot settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _logger.LogInformation("GPU compatibility layer toggle persistence started.");

            if (settings.GpuCompatLayerEnabled && !GetGpuCompatLayerRuntimeState().IsFullyApplied)
            {
                return ConfirmAndEnableGpuCompatLayerAsync(settings);
            }

            var renderMode = GpuCompatLayerService.NormalizeRenderMode(settings.GpuCompatLayerRenderMode);
            string spiceXmlPath = _paths.ResolveSpiceXmlPath(settings.UseSystemSpiceConfig);
            if (_gpuCompatLayerService.TryToggleGpuCompatLayer(
                    settings.GpuCompatLayerEnabled,
                    renderMode,
                    spiceXmlPath,
                    out var error))
            {
                settings.RunSilently(() => settings.GpuCompatLayerRenderMode = renderMode);
                RefreshGpuCompatLayerState(settings);
                _logger.LogInformation("GPU compatibility layer toggle persistence completed.");
                return Task.CompletedTask;
            }

            _logger.LogWarning("GPU compatibility layer toggle persistence failed.");
            _uiInteractionService.ShowErrorToast("兼容层切换失败", string.IsNullOrWhiteSpace(error) ? "未知错误" : error);
            RefreshGpuCompatLayerState(settings);
            return Task.CompletedTask;
        }

        public Task PersistGpuCompatLayerRenderModeAsync(SettingsConfigurationSnapshot settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _logger.LogInformation("GPU compatibility layer render mode persistence started.");

            var renderMode = GpuCompatLayerService.NormalizeRenderMode(settings.GpuCompatLayerRenderMode);

            string spiceXmlPath = _paths.ResolveSpiceXmlPath(settings.UseSystemSpiceConfig);
            if (_gpuCompatLayerService.TryPersistGpuCompatLayerRenderMode(
                    renderMode,
                    settings.GpuCompatLayerEnabled,
                    spiceXmlPath,
                    out var error))
            {
                settings.RunSilently(() => settings.GpuCompatLayerRenderMode = renderMode);
                RefreshGpuCompatLayerState(settings);
                _logger.LogInformation("GPU compatibility layer render mode persistence completed.");
                return Task.CompletedTask;
            }

            _logger.LogWarning("GPU compatibility layer render mode persistence failed.");
            _uiInteractionService.ShowErrorToast("兼容模式切换失败", string.IsNullOrWhiteSpace(error) ? "未知错误" : error);
            RefreshGpuCompatLayerState(settings);
            return Task.CompletedTask;
        }

        public async Task EditConfigAsync(SettingsConfigurationSnapshot settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _logger.LogInformation("spicecfg editor launch requested.");

            string spicePath = _paths.GetSpicePath();

            if (!File.Exists(spicePath))
            {
                _logger.LogWarning("spicecfg editor launch failed because spice64.exe was not found: {SpicePath}", spicePath);
                _uiInteractionService.ShowErrorToast("无法启动 spice 配置", $"未找到程序: {spicePath}");
                return;
            }

            if (!File.Exists(_paths.ConfigFilePath))
            {
                _logger.LogWarning("spicecfg editor launch failed because config.toml was not found: {ConfigPath}", _paths.ConfigFilePath);
                _uiInteractionService.ShowErrorToast("无法启动 spice 配置", $"未找到配置文件: {_paths.ConfigFilePath}");
                return;
            }

            string arguments = Spice64CommandLine.BuildConfigEditorArguments(settings.UseSystemSpiceConfig);

            settings.IsSettingsBusy = true;

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = spicePath,
                    Arguments = arguments,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(spicePath)
                });

                if (process == null)
                {
                    _logger.LogWarning("spicecfg editor process creation returned null.");
                    _uiInteractionService.ShowErrorToast("无法启动 spice 配置", "创建进程失败。");
                    return;
                }

                _logger.LogInformation("spicecfg editor process started. ProcessId={ProcessId}", process.Id);
                await process.WaitForExitAsync();
                _logger.LogInformation("spicecfg editor process exited. ExitCode={ExitCode}", process.ExitCode);
                await InitializeStartupAsync(settings);
                await WarmDeferredAsync(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "spicecfg editor launch failed.");
                _uiInteractionService.ShowErrorToast("启动 spice 配置失败", ex.Message);
            }
            finally
            {
                settings.IsSettingsBusy = false;
            }
        }

        public Task PersistUseSystemSpiceConfigAsync(SettingsConfigurationSnapshot settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _logger.LogInformation("Spice config mode persistence started.");

            try
            {
                _configHandler.WriteString(
                    AppConfigBootstrapper.SettingSectionName,
                    UseSystemConfigKey,
                    settings.UseSystemSpiceConfig.ToString().ToLowerInvariant());
                ReloadRuntimeState(settings);
                _logger.LogInformation("Spice config mode persistence completed. SpiceConfigAvailable={SpiceConfigAvailable}", settings.IsSpiceConfigAvailable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist use-system-config.");
                _uiInteractionService.ShowErrorToast("保存设置失败", ex.Message);
                settings.RunSilently(() => settings.UseSystemSpiceConfig = _configHandler.TryReadBool(AppConfigBootstrapper.SettingSectionName, UseSystemConfigKey, false));
            }

            return Task.CompletedTask;
        }

        private async Task ConfirmAndEnableGpuCompatLayerAsync(SettingsConfigurationSnapshot settings)
        {
            _logger.LogInformation("GPU compatibility layer confirmation dialog opened.");
            var confirmed = await _uiInteractionService.ShowDialogAsync(
                "启用显卡兼容层",
                "即将启用显卡兼容层，请确认你的显卡为 AMD 或者 Intel ，否则请勿开启。\n你确定要继续吗？",
                "确认",
                "取消",
                NotificationType.Warning);

            if (!confirmed)
            {
                _logger.LogInformation("GPU compatibility layer enable was cancelled.");
                settings.RunSilently(() => settings.GpuCompatLayerEnabled = false);
                return;
            }

            var renderMode = GpuCompatLayerService.NormalizeRenderMode(settings.GpuCompatLayerRenderMode);
            string spiceXmlPath = _paths.ResolveSpiceXmlPath(settings.UseSystemSpiceConfig);
            if (_gpuCompatLayerService.TryToggleGpuCompatLayer(
                    true,
                    renderMode,
                    spiceXmlPath,
                    out var error))
            {
                settings.RunSilently(() => settings.GpuCompatLayerRenderMode = renderMode);
                RefreshGpuCompatLayerState(settings);
                _logger.LogInformation("GPU compatibility layer enable completed after confirmation.");
                return;
            }

            _logger.LogWarning("GPU compatibility layer enable failed after confirmation.");
            _uiInteractionService.ShowErrorToast("兼容层切换失败", string.IsNullOrWhiteSpace(error) ? "未知错误" : error);
            RefreshGpuCompatLayerState(settings);
        }

        public bool HasGpuCompatLayerModulesDirectory()
        {
            return Directory.Exists(Path.Combine(_paths.GetContentsDirectoryPath(), "modules"));
        }

        public Task OpenAsioControlPanelAsync(SettingsConfigurationSnapshot settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var driverName = settings.SelectedAsioDriver?.Value ?? settings.AsioDriverValue;
            if (string.IsNullOrWhiteSpace(driverName))
            {
                _logger.LogInformation("ASIO control panel open skipped because no driver is selected.");
                return Task.CompletedTask;
            }

            _logger.LogInformation("ASIO control panel open requested.");
            if (!AsioDriverRegistry.TryOpenControlPanel(driverName, out var errorMessage))
            {
                _logger.LogWarning("ASIO control panel open failed.");
                _uiInteractionService.ShowWarningToast(
                    "ASIO 控制面板",
                    string.IsNullOrWhiteSpace(errorMessage) ? "无法打开当前选择的 ASIO 驱动控制面板。" : errorMessage);
            }

            return Task.CompletedTask;
        }

        public async Task AddServerPresetAsync(SettingsConfigurationSnapshot settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _logger.LogInformation("Add server preset dialog opened.");

            var nameBox = new TextBox { PlaceholderText = "预设名" };
            var urlBox = new TextBox { PlaceholderText = "http://SERVERURL:PORT" };
            var pcbBox = new TextBox { PlaceholderText = "PCBID" };

            var content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "请填写预设信息" },
                    nameBox,
                    urlBox,
                    pcbBox
                }
            };

            var confirmed = await _uiInteractionService.ShowDialogAsync(
                "新建服务器预设",
                content,
                "创建",
                "取消");

            if (!confirmed)
            {
                _logger.LogInformation("Add server preset dialog was cancelled.");
                return;
            }

            var presetName = (nameBox.Text ?? string.Empty).Trim();
            var serverUrl = (urlBox.Text ?? string.Empty).Trim();
            var pcbId = (pcbBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(presetName))
            {
                _logger.LogWarning("Add server preset rejected because the name is empty.");
                _uiInteractionService.ShowErrorToast("新建预设失败", "预设名不能为空。");
                return;
            }

            if (settings.ServerPresets.Any(preset => string.Equals(preset.Name, presetName, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("Add server preset rejected because the name already exists.");
                _uiInteractionService.ShowErrorToast("新建预设失败", $"已存在同名预设：{presetName}");
                return;
            }

            var newPreset = new ServerPresetItem
            {
                Name = presetName,
                ServerUrl = serverUrl,
                PcbId = pcbId
            };

            settings.ServerPresets.Add(newPreset);
            settings.RunSilently(() => settings.SelectedServerPreset = newPreset);
            await PersistSelectedServerPresetAsync(settings);
            _logger.LogInformation("Server preset added. PresetCount={PresetCount}", settings.ServerPresets.Count);
            _uiInteractionService.ShowInfoToast("新建预设", $"已创建预设：{presetName}");
        }

        public async Task DeleteServerPresetAsync(SettingsConfigurationSnapshot settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _logger.LogInformation("Delete server preset requested.");

            var preset = settings.SelectedServerPreset;
            if (preset == null)
            {
                _logger.LogWarning("Delete server preset skipped because no preset is selected.");
                _uiInteractionService.ShowWarningToast("删除预设", "请先选择要删除的预设。");
                return;
            }

            if (string.Equals(preset.Name, NonePresetName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Delete server preset skipped because the default none preset is selected.");
                _uiInteractionService.ShowWarningToast("删除预设", "「无」是默认项，不可删除。");
                return;
            }

            if (string.Equals(preset.Name, AsphyxiaPresetName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Delete server preset skipped because the built-in preset is selected.");
                _uiInteractionService.ShowWarningToast("删除预设", "Asphyxia 是内置预设，不可删除。");
                return;
            }

            var confirmed = await _uiInteractionService.ShowDialogAsync(
                "删除服务器预设",
                $"确定删除预设「{preset.Name}」？",
                "删除",
                "取消",
                NotificationType.Warning);

            if (!confirmed)
            {
                _logger.LogInformation("Delete server preset was cancelled.");
                return;
            }

            settings.ServerPresets.Remove(preset);
            var fallback = settings.ServerPresets.FirstOrDefault(item => string.Equals(item.Name, NonePresetName, StringComparison.OrdinalIgnoreCase))
                ?? settings.ServerPresets.FirstOrDefault();

            settings.RunSilently(() => settings.SelectedServerPreset = fallback);
            await PersistSelectedServerPresetAsync(settings);
            _logger.LogInformation("Server preset deleted. PresetCount={PresetCount}", settings.ServerPresets.Count);
            _uiInteractionService.ShowInfoToast("删除预设", $"已删除预设：{preset.Name}");
        }

        public async Task PersistSelectedServerPresetAsync(SettingsConfigurationSnapshot settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _logger.LogInformation("Selected server preset persistence started.");

            var preset = settings.SelectedServerPreset;
            if (preset == null)
            {
                _logger.LogWarning("Selected server preset persistence skipped because no preset is selected.");
                return;
            }

            settings.ActiveServerPreset = preset.Name ?? NonePresetName;
            settings.RunSilently(() =>
            {
                if (string.Equals(preset.Name, NonePresetName, StringComparison.OrdinalIgnoreCase))
                {
                    settings.ServerAddress = string.Empty;
                    settings.PcbId = string.Empty;
                }
                else
                {
                    settings.ServerAddress = (preset.ServerUrl ?? string.Empty).Trim();
                    settings.PcbId = (preset.PcbId ?? string.Empty).Trim();
                }
            });

            await PersistServerEndpointAsync(settings);
            _logger.LogInformation("Selected server preset persistence completed.");
        }

        private void LoadServerPresets(SettingsConfigurationSnapshot settings)
        {
            var result = _configHandler.LoadServerPresets(NonePresetName, AsphyxiaPresetName, AsphyxiaDefaultUrl);
            if (result.Mutated)
            {
                _configHandler.SaveServerPresets(result.Presets, result.ActivePreset, NonePresetName);
            }

            settings.ServerPresets.Clear();
            foreach (var preset in result.Presets)
            {
                settings.ServerPresets.Add(preset);
            }

            var selectedPreset = settings.ServerPresets.FirstOrDefault(p => string.Equals(p.Name, result.ActivePreset, StringComparison.OrdinalIgnoreCase))
                ?? settings.ServerPresets.FirstOrDefault(p => string.Equals(p.Name, NonePresetName, StringComparison.OrdinalIgnoreCase))
                ?? settings.ServerPresets.FirstOrDefault();

            settings.RunSilently(() => settings.SelectedServerPreset = selectedPreset);
            settings.ActiveServerPreset = selectedPreset?.Name ?? NonePresetName;
            _logger.LogInformation("Server presets loaded. PresetCount={PresetCount}", settings.ServerPresets.Count);
        }

        private void LoadSpiceSettings(SettingsConfigurationSnapshot settings)
        {
            if (!settings.IsSpiceConfigAvailable)
            {
                _logger.LogWarning("Spice settings load skipped because the active spice config is unavailable.");
                return;
            }

            string spiceXmlPath = _paths.ResolveSpiceXmlPath(settings.UseSystemSpiceConfig);
            var optionValues = ReadSpiceOptionValues(spiceXmlPath);
            settings.RunSilently(() => ApplySpiceOptionValues(settings, optionValues));
            SyncSelectedServerPresetFromCurrentFields(settings);
            _logger.LogInformation("Spice settings loaded from active config.");
        }

        private void ReloadRuntimeState(SettingsConfigurationSnapshot settings)
        {
            _logger.LogInformation("Settings runtime state reload started.");
            if (!RefreshSpiceConfigAvailability(settings))
            {
                ApplyUnavailableSpiceState(settings);
                _logger.LogWarning("Settings runtime state reload ended with unavailable spice config.");
                return;
            }

            RefreshGpuCompatLayerState(settings);
            LoadSpiceSettings(settings);
            RefreshAsioDrivers(settings, settings.AsioDriverValue);
            RefreshNetworkAdapters(settings, settings.NetworkAdapterIp, settings.NetworkAdapterSubnet);
            _logger.LogInformation("Settings runtime state reload completed.");
        }

        private void RefreshGpuCompatLayerState(SettingsConfigurationSnapshot settings)
        {
            var runtimeState = GetGpuCompatLayerRuntimeState();
            var configuredRenderMode = SyncGpuCompatLayerConfigToRuntimeState(runtimeState);

            settings.RunSilently(() =>
            {
                settings.GpuCompatLayerRenderMode = string.IsNullOrWhiteSpace(runtimeState.DetectedRenderMode)
                    ? configuredRenderMode
                    : runtimeState.DetectedRenderMode;
                settings.GpuCompatLayerEnabled = runtimeState.IsFullyApplied;
            });
            _logger.LogDebug("GPU compatibility layer runtime state refreshed. FullyApplied={FullyApplied}, InconsistentFiles={InconsistentFiles}", runtimeState.IsFullyApplied, runtimeState.HasInconsistentFiles);
        }

        private void ApplyUnavailableSpiceState(SettingsConfigurationSnapshot settings)
        {
            settings.AsioDrivers.Clear();
            settings.NetworkAdapters.Clear();
            var emptyValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            settings.RunSilently(() =>
            {
                ApplySpiceOptionValues(settings, emptyValues);
                settings.SelectedAsioDriver = null;
                settings.SelectedNetworkAdapter = null;
            });
            SyncSelectedServerPresetFromCurrentFields(settings);
        }

        private void SyncSelectedServerPresetFromCurrentFields(SettingsConfigurationSnapshot settings)
        {
            var serverUrl = (settings.ServerAddress ?? string.Empty).Trim();
            var pcbId = (settings.PcbId ?? string.Empty).Trim();

            var matchedPreset = settings.ServerPresets.FirstOrDefault(preset =>
                !string.Equals(preset.Name, NonePresetName, StringComparison.OrdinalIgnoreCase)
                && string.Equals((preset.ServerUrl ?? string.Empty).Trim(), serverUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals((preset.PcbId ?? string.Empty).Trim(), pcbId, StringComparison.OrdinalIgnoreCase));

            var fallbackPreset = settings.ServerPresets.FirstOrDefault(preset => string.Equals(preset.Name, NonePresetName, StringComparison.OrdinalIgnoreCase))
                ?? settings.ServerPresets.FirstOrDefault();
            var selectedPreset = matchedPreset ?? fallbackPreset;

            settings.RunSilently(() => settings.SelectedServerPreset = selectedPreset);
            settings.ActiveServerPreset = selectedPreset?.Name ?? NonePresetName;
        }

        private void RefreshAsioDrivers(SettingsConfigurationSnapshot settings, string selectedValue)
        {
            var choices = BuildAsioDriverOptions(selectedValue);
            settings.AsioDrivers.Clear();
            foreach (var choice in choices)
            {
                settings.AsioDrivers.Add(choice);
            }

            var selectedOption = settings.AsioDrivers.FirstOrDefault(choice => string.Equals(choice.Value, selectedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                ?? settings.AsioDrivers.FirstOrDefault();
            settings.RunSilently(() => settings.SelectedAsioDriver = selectedOption);
        }

        private void RefreshNetworkAdapters(SettingsConfigurationSnapshot settings, string selectedIpAddress, string selectedSubnetMask)
        {
            var choices = BuildNetworkAdapterOptions(selectedIpAddress, selectedSubnetMask);
            settings.NetworkAdapters.Clear();
            foreach (var choice in choices)
            {
                settings.NetworkAdapters.Add(choice);
            }

            var selectedOption = settings.NetworkAdapters.FirstOrDefault(choice =>
                    string.Equals(choice.IpAddress, selectedIpAddress ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(choice.SubnetMask, selectedSubnetMask ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                ?? settings.NetworkAdapters.FirstOrDefault();
            settings.RunSilently(() => settings.SelectedNetworkAdapter = selectedOption);
        }

        private void SyncSelectedNetworkAdapter(SettingsConfigurationSnapshot settings, string selectedIpAddress, string selectedSubnetMask)
        {
            var selectedOption = settings.NetworkAdapters.FirstOrDefault(choice =>
                    string.Equals(choice.IpAddress, selectedIpAddress ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(choice.SubnetMask, selectedSubnetMask ?? string.Empty, StringComparison.OrdinalIgnoreCase));

            if (selectedOption == null
                && string.IsNullOrWhiteSpace(selectedIpAddress)
                && string.IsNullOrWhiteSpace(selectedSubnetMask))
            {
                selectedOption = settings.NetworkAdapters.FirstOrDefault(choice =>
                    string.IsNullOrWhiteSpace(choice.IpAddress)
                    && string.IsNullOrWhiteSpace(choice.SubnetMask));
            }

            settings.RunSilently(() => settings.SelectedNetworkAdapter = selectedOption);
        }

        private Dictionary<string, string> ReadSpiceOptionValues(string spiceXmlPath)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!_spiceConfigFileService.TryLoadOptionsContext(
                    spiceXmlPath, LoadOptions.PreserveWhitespace, false, out var context, out _, out _))
            {
                _logger.LogWarning("Failed to load active spice config options.");
                return values;
            }

            foreach (var option in SpiceOptions)
            {
                values[option.XmlName] = context.GetOptionValue(option.XmlName) ?? string.Empty;
            }

            return values;
        }

        private void ApplySpiceOptionValues(SettingsConfigurationSnapshot settings, Dictionary<string, string> values)
        {
            foreach (var option in SpiceOptions)
            {
                if (values.TryGetValue(option.XmlName, out var xmlValue))
                {
                    option.ApplyXmlValue(settings, xmlValue);
                }
                else
                {
                    option.ApplyXmlValue(settings, string.Empty);
                }
            }
        }

        private void ApplyDeferredSettingsState(
            SettingsConfigurationSnapshot settings,
            DeferredSettingsState deferredState,
            string currentAsioDriverValue,
            string currentNetworkIp,
            string currentNetworkSubnet)
        {
            ApplySpiceOptionValues(settings, deferredState.SpiceOptionValues);

            settings.AsioDrivers.Clear();
            foreach (var option in deferredState.AsioDrivers)
            {
                settings.AsioDrivers.Add(option);
            }

            settings.NetworkAdapters.Clear();
            foreach (var option in deferredState.NetworkAdapters)
            {
                settings.NetworkAdapters.Add(option);
            }

            settings.RunSilently(() =>
            {
                settings.SelectedAsioDriver = deferredState.SelectedAsioDriver;
                settings.SelectedNetworkAdapter = deferredState.SelectedNetworkAdapter;
                if (!string.IsNullOrWhiteSpace(currentAsioDriverValue))
                {
                    settings.AsioDriverValue = currentAsioDriverValue;
                }

                if (!string.IsNullOrWhiteSpace(currentNetworkIp) || !string.IsNullOrWhiteSpace(currentNetworkSubnet))
                {
                    settings.NetworkAdapterIp = currentNetworkIp;
                    settings.NetworkAdapterSubnet = currentNetworkSubnet;
                }
            });

            SyncSelectedServerPresetFromCurrentFields(settings);
        }

        private static List<AsioDriverOption> BuildAsioDriverOptions(string selectedValue)
        {
            var choices = new List<AsioDriverOption> { new("无", string.Empty) };
            foreach (var driverName in AsioDriverRegistry.GetInstalledDriverNames())
            {
                choices.Add(new AsioDriverOption(driverName, driverName));
            }

            if (!string.IsNullOrWhiteSpace(selectedValue)
                && choices.All(choice => !string.Equals(choice.Value, selectedValue, StringComparison.OrdinalIgnoreCase)))
            {
                choices.Add(new AsioDriverOption($"{selectedValue}（当前配置）", selectedValue));
            }

            return choices;
        }

        private static List<NetworkAdapterOption> BuildNetworkAdapterOptions(string selectedIpAddress, string selectedSubnetMask)
        {
            var choices = new List<NetworkAdapterOption> { new("无", string.Empty, string.Empty) };
            foreach (var adapter in NetworkAdapterDiscovery.GetAvailableAdapters())
            {
                choices.Add(new NetworkAdapterOption(adapter.DisplayName, adapter.IpAddress, adapter.SubnetMask));
            }

            if ((!string.IsNullOrWhiteSpace(selectedIpAddress) || !string.IsNullOrWhiteSpace(selectedSubnetMask))
                && choices.All(choice =>
                    !string.Equals(choice.IpAddress, selectedIpAddress, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(choice.SubnetMask, selectedSubnetMask, StringComparison.OrdinalIgnoreCase)))
            {
                choices.Add(new NetworkAdapterOption($"{selectedIpAddress} / {selectedSubnetMask}（当前配置）".Trim(), selectedIpAddress, selectedSubnetMask));
            }

            return choices;
        }

        private GpuCompatLayerRuntimeState GetGpuCompatLayerRuntimeState()
        {
            try
            {
                return GpuCompatLayerService.DetectRuntimeState(
                    _paths.GetContentsDirectoryPath(),
                    _paths.GetBundledLibsDirectoryPath());
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to detect compatibility layer runtime state.");
                return new GpuCompatLayerRuntimeState(false, string.Empty, false);
            }
        }


        private bool RefreshSpiceConfigAvailability(SettingsConfigurationSnapshot settings)
        {
            bool isSpiceConfigAvailable = IsSpiceConfigAvailable(settings.UseSystemSpiceConfig);
            settings.RunSilently(() =>
            {
                settings.IsSpiceConfigAvailable = isSpiceConfigAvailable;
                settings.SpiceConfigEmptyStateMessage = MissingSpiceConfigMessage;
            });

            if (!isSpiceConfigAvailable)
            {
                _logger.LogWarning("Active spice config is unavailable.");
            }

            return isSpiceConfigAvailable;
        }

        private bool IsSpiceConfigAvailable(bool useSystemSpiceConfig)
        {
            if (!useSystemSpiceConfig)
            {
                return true;
            }

            string spiceXmlPath = _paths.ResolveSpiceXmlPath(useSystemSpiceConfig);

            try
            {
                return _spiceConfigFileService.TryLoadOptionsContext(
                    spiceXmlPath,
                    LoadOptions.None,
                    false,
                    out _,
                    out _,
                    out _);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "System spice config validation failed for {SpiceXmlPath}.", spiceXmlPath);
                return false;
            }
        }

        private string SyncGpuCompatLayerConfigToRuntimeState(GpuCompatLayerRuntimeState runtimeState)
        {
            var configuredRenderMode = GpuCompatLayerService.NormalizeRenderMode(
                _configHandler.ReadString(AppConfigBootstrapper.SettingSectionName, "cl-rendermode", "dx9on12"));
            var detectedRenderMode = string.IsNullOrWhiteSpace(runtimeState.DetectedRenderMode)
                ? string.Empty
                : GpuCompatLayerService.NormalizeRenderMode(runtimeState.DetectedRenderMode);
            var targetCompatEnabled = runtimeState.IsFullyApplied;

            try
            {
                var currentCompatEnabled = _configHandler.TryReadBool(AppConfigBootstrapper.SettingSectionName, "compatlayer", false);
                if (currentCompatEnabled != targetCompatEnabled)
                {
                    _configHandler.WriteString(
                        AppConfigBootstrapper.SettingSectionName,
                        "compatlayer",
                        targetCompatEnabled ? "true" : "false");
                }

                if (!string.IsNullOrWhiteSpace(detectedRenderMode)
                    && !string.Equals(configuredRenderMode, detectedRenderMode, StringComparison.OrdinalIgnoreCase))
                {
                    _configHandler.WriteString(AppConfigBootstrapper.SettingSectionName, "cl-rendermode", detectedRenderMode);
                    configuredRenderMode = detectedRenderMode;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync compatibility runtime state back to config.toml.");
            }

            return configuredRenderMode;
        }

        private void SaveServerPresets(SettingsConfigurationSnapshot settings)
        {
            _configHandler.SaveServerPresets(settings.ServerPresets, settings.ActiveServerPreset, NonePresetName);
        }

        private bool TryApplySpiceUpdates(
            string spiceXmlPath,
            SettingsConfigurationSnapshot settings,
            bool reloadSettingsOnSuccess = true,
            params SpiceOptionUpdate[] updates)
        {
            int updateCount = updates?.Length ?? 0;
            _logger.LogDebug("Applying spice option updates. UpdateCount={UpdateCount}", updateCount);
            if (!_spiceConfigFileService.ApplySpiceOptions(spiceXmlPath, updates, out var error))
            {
                _logger.LogWarning("Failed to apply spice option updates. UpdateCount={UpdateCount}", updateCount);
                _uiInteractionService.ShowErrorToast("写入配置失败", error);
                return false;
            }

            if (reloadSettingsOnSuccess
                && string.Equals(
                    spiceXmlPath,
                    _paths.ResolveSpiceXmlPath(settings.UseSystemSpiceConfig),
                    StringComparison.OrdinalIgnoreCase))
            {
                LoadSpiceSettings(settings);
            }

            _logger.LogInformation("Spice option updates applied. UpdateCount={UpdateCount}", updateCount);
            return true;
        }

        private static IEnumerable<SpiceOptionUpdate> BuildSpiceOptionUpdates(SettingsConfigurationSnapshot settings)
        {
            foreach (var option in GeneralSpiceOptions)
            {
                yield return new SpiceOptionUpdate(option.XmlName, option.GetXmlValue(settings), false);
            }
        }

    }
}
