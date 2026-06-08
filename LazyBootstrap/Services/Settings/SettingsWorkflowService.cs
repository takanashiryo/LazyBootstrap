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
    public interface ISettingsWorkflowService
    {
        Task InitializeStartupAsync(SettingsPageViewModel viewModel);
        Task WarmDeferredAsync(SettingsPageViewModel viewModel);
        Task PersistLauncherSettingsAsync(SettingsPageViewModel viewModel);
        Task PersistSelectedServerPresetAsync(SettingsPageViewModel viewModel);
        Task PersistServerEndpointAsync(SettingsPageViewModel viewModel);
        Task ApplySelectedNetworkAdapterAsync(SettingsPageViewModel viewModel);
        Task OpenNetworkAdapterPickerAsync(SettingsPageViewModel viewModel);
        Task PersistNetworkSettingsAsync(SettingsPageViewModel viewModel);
        Task PersistSpiceSettingsAsync(SettingsPageViewModel viewModel);
        Task PersistCompatibilityToggleAsync(SettingsPageViewModel viewModel);
        Task PersistCompatibilityRenderModeAsync(SettingsPageViewModel viewModel);
        Task EditConfigAsync(SettingsPageViewModel viewModel);
        Task PersistUseSystemSpiceConfigAsync(SettingsPageViewModel viewModel);
        Task OpenAsioControlPanelAsync(SettingsPageViewModel viewModel);
        Task AddServerPresetAsync(SettingsPageViewModel viewModel);
        Task DeleteServerPresetAsync(SettingsPageViewModel viewModel);
    }

    internal sealed class SettingsWorkflowService : ISettingsWorkflowService
    {
        private const string NonePresetName = "无";
        private const string AsphyxiaPresetName = "Asphyxia";
        private const string AsphyxiaDefaultUrl = "http://localhost:8083";
        private const string UseSystemConfigKey = "use-system-config";
        private const string MissingSpiceConfigMessage = "未找到任何spice2x配置文件";

        private readonly IConfigHandler _configHandler;
        private readonly ILauncherPaths _paths;
        private readonly ISpiceConfigFileService _spiceConfigFileService;
        private readonly ICompatibilitySettingsService _compatibilitySettingsService;
        private readonly IUiInteractionService _uiInteractionService;
        private readonly ILogger<SettingsWorkflowService> _logger;

        private sealed record SpiceOptionDescriptor(
            string XmlName,
            Func<SettingsPageViewModel, string> GetXmlValue,
            Action<SettingsPageViewModel, string> ApplyXmlValue);

        private static SpiceOptionDescriptor B(string name,
            Func<SettingsPageViewModel, bool> getter,
            Action<SettingsPageViewModel, bool> setter,
            string enabledValue) => new(name,
                vm => getter(vm) ? enabledValue : string.Empty,
                (vm, xmlValue) => setter(vm, string.Equals(xmlValue, enabledValue, StringComparison.OrdinalIgnoreCase)));

        private static SpiceOptionDescriptor S(string name,
            Func<SettingsPageViewModel, string> getter,
            Action<SettingsPageViewModel, string> setter) => new(name,
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
            IConfigHandler configHandler,
            ILauncherPaths paths,
            ISpiceConfigFileService spiceConfigFileService,
            ICompatibilitySettingsService compatibilitySettingsService,
            IUiInteractionService uiInteractionService,
            ILogger<SettingsWorkflowService> logger)
        {
            _configHandler = configHandler ?? throw new ArgumentNullException(nameof(configHandler));
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _spiceConfigFileService = spiceConfigFileService ?? throw new ArgumentNullException(nameof(spiceConfigFileService));
            _compatibilitySettingsService = compatibilitySettingsService ?? throw new ArgumentNullException(nameof(compatibilitySettingsService));
            _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task InitializeStartupAsync(SettingsPageViewModel viewModel)
        {
            viewModel.RunSilently(() =>
            {
                viewModel.NoAsphyxia = _configHandler.TryReadBool(AppConfigBootstrapper.SettingSectionName, "noasphyxia", false);
                viewModel.UseSystemSpiceConfig = _configHandler.TryReadBool(AppConfigBootstrapper.SettingSectionName, UseSystemConfigKey, false);
                viewModel.CompatibilityRenderMode = CompatibilitySettingsService.NormalizeRenderMode(_configHandler.ReadString(AppConfigBootstrapper.SettingSectionName, "cl-rendermode", "dx9on12"));
                viewModel.IsSpiceConfigAvailable = IsSpiceConfigAvailable(viewModel.UseSystemSpiceConfig);
                viewModel.SpiceConfigEmptyStateMessage = MissingSpiceConfigMessage;
            });
            RefreshCompatibilityState(viewModel);

            LoadServerPresets(viewModel);
            return Task.CompletedTask;
        }

        public async Task WarmDeferredAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            if (!RefreshSpiceConfigAvailability(viewModel))
            {
                viewModel.AsioDrivers.Clear();
                viewModel.NetworkAdapters.Clear();
                return;
            }

            var currentAsioDriverValue = viewModel.AsioDriverValue;
            var currentNetworkIp = viewModel.NetworkAdapterIp;
            var currentNetworkSubnet = viewModel.NetworkAdapterSubnet;

            var deferredState = await Task.Run(() =>
            {
                string spiceXmlPath = _paths.ResolveSpiceXmlPath(viewModel.UseSystemSpiceConfig);
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

            ApplyDeferredSettingsState(viewModel, deferredState, currentAsioDriverValue, currentNetworkIp, currentNetworkSubnet);
        }

        public Task PersistLauncherSettingsAsync(SettingsPageViewModel viewModel)
        {
            try
            {
                _configHandler.WriteString(AppConfigBootstrapper.SettingSectionName, "noasphyxia", viewModel.NoAsphyxia.ToString().ToLowerInvariant());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist launcher settings.");
                _uiInteractionService.ShowErrorToast("保存设置失败", ex.Message);
                viewModel.RunSilently(() => viewModel.NoAsphyxia = _configHandler.TryReadBool(AppConfigBootstrapper.SettingSectionName, "noasphyxia", false));
            }

            return Task.CompletedTask;
        }

        public Task PersistServerEndpointAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            viewModel.ServerAddress = (viewModel.ServerAddress ?? string.Empty).Trim();
            viewModel.PcbId = (viewModel.PcbId ?? string.Empty).Trim();

            if (!TryApplySpiceUpdates(
                    _paths.ResolveSpiceXmlPath(viewModel.UseSystemSpiceConfig),
                    viewModel,
                    reloadViewModelOnSuccess: false,
                    new SpiceOptionUpdate("url", viewModel.ServerAddress, false),
                    new SpiceOptionUpdate("p", viewModel.PcbId, false)))
            {
                ReloadRuntimeState(viewModel);
                return Task.CompletedTask;
            }

            SyncSelectedServerPresetFromCurrentFields(viewModel);
            SaveServerPresets(viewModel);
            return Task.CompletedTask;
        }

        public Task ApplySelectedNetworkAdapterAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            var selectedAdapter = viewModel.SelectedNetworkAdapter;
            viewModel.RunSilently(() =>
            {
                viewModel.NetworkAdapterIp = selectedAdapter?.IpAddress ?? string.Empty;
                viewModel.NetworkAdapterSubnet = selectedAdapter?.SubnetMask ?? string.Empty;
            });

            return PersistNetworkSettingsAsync(viewModel);
        }

        public async Task OpenNetworkAdapterPickerAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            var choices = BuildNetworkAdapterOptions(viewModel.NetworkAdapterIp, viewModel.NetworkAdapterSubnet);
            var selectedChoice = choices.FirstOrDefault(choice =>
                                     string.Equals(choice.IpAddress, viewModel.NetworkAdapterIp ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                                     && string.Equals(choice.SubnetMask, viewModel.NetworkAdapterSubnet ?? string.Empty, StringComparison.OrdinalIgnoreCase))
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
                return;
            }

            if (adapterListBox.SelectedItem is not NetworkAdapterOption choice)
            {
                _uiInteractionService.ShowWarningToast("选择网卡", "请选择一个网卡。");
                return;
            }

            viewModel.RunSilently(() =>
            {
                viewModel.SelectedNetworkAdapter = choice;
                viewModel.NetworkAdapterIp = choice.IpAddress;
                viewModel.NetworkAdapterSubnet = choice.SubnetMask;
            });

            await PersistNetworkSettingsAsync(viewModel);
        }

        public Task PersistNetworkSettingsAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            viewModel.NetworkAdapterIp = ConfigHelper.NormalizeNetworkValue(viewModel.NetworkAdapterIp);
            viewModel.NetworkAdapterSubnet = ConfigHelper.NormalizeNetworkValue(viewModel.NetworkAdapterSubnet);

            if (!TryApplySpiceUpdates(
                    _paths.ResolveSpiceXmlPath(viewModel.UseSystemSpiceConfig),
                    viewModel,
                    false,
                    new SpiceOptionUpdate("network", viewModel.NetworkAdapterIp, false),
                    new SpiceOptionUpdate("subnet", viewModel.NetworkAdapterSubnet, false)))
            {
                ReloadRuntimeState(viewModel);
                return Task.CompletedTask;
            }

            SyncSelectedNetworkAdapter(viewModel, viewModel.NetworkAdapterIp, viewModel.NetworkAdapterSubnet);
            return Task.CompletedTask;
        }

        public Task PersistSpiceSettingsAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            if (!TryApplySpiceUpdates(
                    _paths.ResolveSpiceXmlPath(viewModel.UseSystemSpiceConfig),
                    viewModel,
                    false,
                    BuildSpiceOptionUpdates(viewModel).ToArray()))
            {
                ReloadRuntimeState(viewModel);
                return Task.CompletedTask;
            }

            return Task.CompletedTask;
        }

        public Task PersistCompatibilityToggleAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            if (viewModel.CompatibilityLayerEnabled && !IsCompatLayerEffectivelyEnabled())
            {
                return ConfirmAndEnableCompatibilityLayerAsync(viewModel);
            }

            var renderMode = CompatibilitySettingsService.NormalizeRenderMode(viewModel.CompatibilityRenderMode);
            string spiceXmlPath = _paths.ResolveSpiceXmlPath(viewModel.UseSystemSpiceConfig);
            if (_compatibilitySettingsService.TryToggleCompatLayer(
                    viewModel.CompatibilityLayerEnabled,
                    renderMode,
                    spiceXmlPath,
                    out var error))
            {
                viewModel.RunSilently(() => viewModel.CompatibilityRenderMode = renderMode);
                RefreshCompatibilityState(viewModel);
                return Task.CompletedTask;
            }

            _uiInteractionService.ShowErrorToast("兼容层切换失败", string.IsNullOrWhiteSpace(error) ? "未知错误" : error);
            RefreshCompatibilityState(viewModel);
            return Task.CompletedTask;
        }

        public Task PersistCompatibilityRenderModeAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            var renderMode = CompatibilitySettingsService.NormalizeRenderMode(viewModel.CompatibilityRenderMode);

            string spiceXmlPath = _paths.ResolveSpiceXmlPath(viewModel.UseSystemSpiceConfig);
            if (_compatibilitySettingsService.TryPersistRenderMode(
                    renderMode,
                    viewModel.CompatibilityLayerEnabled,
                    spiceXmlPath,
                    out var error))
            {
                viewModel.RunSilently(() => viewModel.CompatibilityRenderMode = renderMode);
                RefreshCompatibilityState(viewModel);
                return Task.CompletedTask;
            }

            _uiInteractionService.ShowErrorToast("兼容模式切换失败", string.IsNullOrWhiteSpace(error) ? "未知错误" : error);
            RefreshCompatibilityState(viewModel);
            return Task.CompletedTask;
        }

        public async Task EditConfigAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            string spicePath = _paths.GetSpicePath();

            if (!File.Exists(spicePath))
            {
                _uiInteractionService.ShowErrorToast("无法启动 spice 配置", $"未找到程序: {spicePath}");
                return;
            }

            if (!File.Exists(_paths.ConfigFilePath))
            {
                _uiInteractionService.ShowErrorToast("无法启动 spice 配置", $"未找到配置文件: {_paths.ConfigFilePath}");
                return;
            }

            string arguments = Spice64CommandLine.BuildConfigEditorArguments(viewModel.UseSystemSpiceConfig);

            viewModel.IsSettingsBusy = true;

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
                    _uiInteractionService.ShowErrorToast("无法启动 spice 配置", "创建进程失败。");
                    return;
                }

                await process.WaitForExitAsync();
                await InitializeStartupAsync(viewModel);
                await WarmDeferredAsync(viewModel);
            }
            catch (Exception ex)
            {
                _uiInteractionService.ShowErrorToast("启动 spice 配置失败", ex.Message);
            }
            finally
            {
                viewModel.IsSettingsBusy = false;
            }
        }

        public Task PersistUseSystemSpiceConfigAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            try
            {
                _configHandler.WriteString(
                    AppConfigBootstrapper.SettingSectionName,
                    UseSystemConfigKey,
                    viewModel.UseSystemSpiceConfig.ToString().ToLowerInvariant());
                ReloadRuntimeState(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist use-system-config.");
                _uiInteractionService.ShowErrorToast("保存设置失败", ex.Message);
                viewModel.RunSilently(() => viewModel.UseSystemSpiceConfig = _configHandler.TryReadBool(AppConfigBootstrapper.SettingSectionName, UseSystemConfigKey, false));
            }

            return Task.CompletedTask;
        }

        private async Task ConfirmAndEnableCompatibilityLayerAsync(SettingsPageViewModel viewModel)
        {
            var confirmed = await _uiInteractionService.ShowDialogAsync(
                "启用显卡兼容层",
                "即将启用显卡兼容层，请确认你的显卡为 AMD 或者 Intel ，否则请勿开启。\n你确定要继续吗？",
                "确认",
                "取消",
                NotificationType.Warning);

            if (!confirmed)
            {
                viewModel.RunSilently(() => viewModel.CompatibilityLayerEnabled = false);
                return;
            }

            var renderMode = CompatibilitySettingsService.NormalizeRenderMode(viewModel.CompatibilityRenderMode);
            string spiceXmlPath = _paths.ResolveSpiceXmlPath(viewModel.UseSystemSpiceConfig);
            if (_compatibilitySettingsService.TryToggleCompatLayer(
                    true,
                    renderMode,
                    spiceXmlPath,
                    out var error))
            {
                viewModel.RunSilently(() => viewModel.CompatibilityRenderMode = renderMode);
                RefreshCompatibilityState(viewModel);
                return;
            }

            _uiInteractionService.ShowErrorToast("兼容层切换失败", string.IsNullOrWhiteSpace(error) ? "未知错误" : error);
            RefreshCompatibilityState(viewModel);
        }

        public Task OpenAsioControlPanelAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            var driverName = viewModel.SelectedAsioDriver?.Value ?? viewModel.AsioDriverValue;
            if (string.IsNullOrWhiteSpace(driverName))
            {
                return Task.CompletedTask;
            }

            if (!AsioDriverRegistry.TryOpenControlPanel(driverName, out var errorMessage))
            {
                _uiInteractionService.ShowWarningToast(
                    "ASIO 控制面板",
                    string.IsNullOrWhiteSpace(errorMessage) ? "无法打开当前选择的 ASIO 驱动控制面板。" : errorMessage);
            }

            return Task.CompletedTask;
        }

        public async Task AddServerPresetAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            var nameBox = new TextBox { Watermark = "预设名" };
            var urlBox = new TextBox { Watermark = "http://SERVERURL:PORT" };
            var pcbBox = new TextBox { Watermark = "PCBID" };

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
                return;
            }

            var presetName = (nameBox.Text ?? string.Empty).Trim();
            var serverUrl = (urlBox.Text ?? string.Empty).Trim();
            var pcbId = (pcbBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(presetName))
            {
                _uiInteractionService.ShowErrorToast("新建预设失败", "预设名不能为空。");
                return;
            }

            if (viewModel.ServerPresets.Any(preset => string.Equals(preset.Name, presetName, StringComparison.OrdinalIgnoreCase)))
            {
                _uiInteractionService.ShowErrorToast("新建预设失败", $"已存在同名预设：{presetName}");
                return;
            }

            var newPreset = new ServerPresetItem
            {
                Name = presetName,
                ServerUrl = serverUrl,
                PcbId = pcbId
            };

            viewModel.ServerPresets.Add(newPreset);
            viewModel.RunSilently(() => viewModel.SelectedServerPreset = newPreset);
            await PersistSelectedServerPresetAsync(viewModel);
            _uiInteractionService.ShowInfoToast("新建预设", $"已创建预设：{presetName}");
        }

        public async Task DeleteServerPresetAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            var preset = viewModel.SelectedServerPreset;
            if (preset == null)
            {
                _uiInteractionService.ShowWarningToast("删除预设", "请先选择要删除的预设。");
                return;
            }

            if (string.Equals(preset.Name, NonePresetName, StringComparison.OrdinalIgnoreCase))
            {
                _uiInteractionService.ShowWarningToast("删除预设", "「无」是默认项，不可删除。");
                return;
            }

            if (string.Equals(preset.Name, AsphyxiaPresetName, StringComparison.OrdinalIgnoreCase))
            {
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
                return;
            }

            viewModel.ServerPresets.Remove(preset);
            var fallback = viewModel.ServerPresets.FirstOrDefault(item => string.Equals(item.Name, NonePresetName, StringComparison.OrdinalIgnoreCase))
                ?? viewModel.ServerPresets.FirstOrDefault();

            viewModel.RunSilently(() => viewModel.SelectedServerPreset = fallback);
            await PersistSelectedServerPresetAsync(viewModel);
            _uiInteractionService.ShowInfoToast("删除预设", $"已删除预设：{preset.Name}");
        }

        public async Task PersistSelectedServerPresetAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            var preset = viewModel.SelectedServerPreset;
            if (preset == null)
            {
                return;
            }

            viewModel.ActiveServerPreset = preset.Name ?? NonePresetName;
            viewModel.RunSilently(() =>
            {
                if (string.Equals(preset.Name, NonePresetName, StringComparison.OrdinalIgnoreCase))
                {
                    viewModel.ServerAddress = string.Empty;
                    viewModel.PcbId = string.Empty;
                }
                else
                {
                    viewModel.ServerAddress = (preset.ServerUrl ?? string.Empty).Trim();
                    viewModel.PcbId = (preset.PcbId ?? string.Empty).Trim();
                }
            });

            await PersistServerEndpointAsync(viewModel);
        }

        private void LoadServerPresets(SettingsPageViewModel viewModel)
        {
            var result = _configHandler.LoadServerPresets(NonePresetName, AsphyxiaPresetName, AsphyxiaDefaultUrl);
            if (result.Mutated)
            {
                _configHandler.SaveServerPresets(result.Presets, result.ActivePreset, NonePresetName);
            }

            viewModel.ServerPresets.Clear();
            foreach (var preset in result.Presets)
            {
                viewModel.ServerPresets.Add(preset);
            }

            var selectedPreset = viewModel.ServerPresets.FirstOrDefault(p => string.Equals(p.Name, result.ActivePreset, StringComparison.OrdinalIgnoreCase))
                ?? viewModel.ServerPresets.FirstOrDefault(p => string.Equals(p.Name, NonePresetName, StringComparison.OrdinalIgnoreCase))
                ?? viewModel.ServerPresets.FirstOrDefault();

            viewModel.RunSilently(() => viewModel.SelectedServerPreset = selectedPreset);
            viewModel.ActiveServerPreset = selectedPreset?.Name ?? NonePresetName;
        }

        private void LoadSpiceSettings(SettingsPageViewModel viewModel)
        {
            if (!viewModel.IsSpiceConfigAvailable)
            {
                return;
            }

            string spiceXmlPath = _paths.ResolveSpiceXmlPath(viewModel.UseSystemSpiceConfig);
            var optionValues = ReadSpiceOptionValues(spiceXmlPath);
            viewModel.RunSilently(() => ApplySpiceOptionValues(viewModel, optionValues));
            SyncSelectedServerPresetFromCurrentFields(viewModel);
        }

        private void ReloadRuntimeState(SettingsPageViewModel viewModel)
        {
            if (!RefreshSpiceConfigAvailability(viewModel))
            {
                ApplyUnavailableSpiceState(viewModel);
                return;
            }

            RefreshCompatibilityState(viewModel);
            LoadSpiceSettings(viewModel);
            RefreshAsioDrivers(viewModel, viewModel.AsioDriverValue);
            RefreshNetworkAdapters(viewModel, viewModel.NetworkAdapterIp, viewModel.NetworkAdapterSubnet);
        }

        private void RefreshCompatibilityState(SettingsPageViewModel viewModel)
        {
            var runtimeState = GetCompatibilityLayerRuntimeState();
            var configuredRenderMode = SyncCompatibilityConfigToRuntimeState(runtimeState);

            viewModel.RunSilently(() =>
            {
                viewModel.CompatibilityRenderMode = string.IsNullOrWhiteSpace(runtimeState.DetectedRenderMode)
                    ? configuredRenderMode
                    : runtimeState.DetectedRenderMode;
                viewModel.CompatibilityLayerEnabled = runtimeState.IsFullyApplied;
            });
        }

        private void ApplyUnavailableSpiceState(SettingsPageViewModel viewModel)
        {
            viewModel.AsioDrivers.Clear();
            viewModel.NetworkAdapters.Clear();
            var emptyValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            viewModel.RunSilently(() =>
            {
                ApplySpiceOptionValues(viewModel, emptyValues);
                viewModel.SelectedAsioDriver = null;
                viewModel.SelectedNetworkAdapter = null;
            });
            SyncSelectedServerPresetFromCurrentFields(viewModel);
        }

        private void SyncSelectedServerPresetFromCurrentFields(SettingsPageViewModel viewModel)
        {
            var serverUrl = (viewModel.ServerAddress ?? string.Empty).Trim();
            var pcbId = (viewModel.PcbId ?? string.Empty).Trim();

            var matchedPreset = viewModel.ServerPresets.FirstOrDefault(preset =>
                !string.Equals(preset.Name, NonePresetName, StringComparison.OrdinalIgnoreCase)
                && string.Equals((preset.ServerUrl ?? string.Empty).Trim(), serverUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals((preset.PcbId ?? string.Empty).Trim(), pcbId, StringComparison.OrdinalIgnoreCase));

            var fallbackPreset = viewModel.ServerPresets.FirstOrDefault(preset => string.Equals(preset.Name, NonePresetName, StringComparison.OrdinalIgnoreCase))
                ?? viewModel.ServerPresets.FirstOrDefault();
            var selectedPreset = matchedPreset ?? fallbackPreset;

            viewModel.RunSilently(() => viewModel.SelectedServerPreset = selectedPreset);
            viewModel.ActiveServerPreset = selectedPreset?.Name ?? NonePresetName;
        }

        private void RefreshAsioDrivers(SettingsPageViewModel viewModel, string selectedValue)
        {
            var choices = BuildAsioDriverOptions(selectedValue);
            viewModel.AsioDrivers.Clear();
            foreach (var choice in choices)
            {
                viewModel.AsioDrivers.Add(choice);
            }

            var selectedOption = viewModel.AsioDrivers.FirstOrDefault(choice => string.Equals(choice.Value, selectedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                ?? viewModel.AsioDrivers.FirstOrDefault();
            viewModel.RunSilently(() => viewModel.SelectedAsioDriver = selectedOption);
        }

        private void RefreshNetworkAdapters(SettingsPageViewModel viewModel, string selectedIpAddress, string selectedSubnetMask)
        {
            var choices = BuildNetworkAdapterOptions(selectedIpAddress, selectedSubnetMask);
            viewModel.NetworkAdapters.Clear();
            foreach (var choice in choices)
            {
                viewModel.NetworkAdapters.Add(choice);
            }

            var selectedOption = viewModel.NetworkAdapters.FirstOrDefault(choice =>
                    string.Equals(choice.IpAddress, selectedIpAddress ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(choice.SubnetMask, selectedSubnetMask ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                ?? viewModel.NetworkAdapters.FirstOrDefault();
            viewModel.RunSilently(() => viewModel.SelectedNetworkAdapter = selectedOption);
        }

        private void SyncSelectedNetworkAdapter(SettingsPageViewModel viewModel, string selectedIpAddress, string selectedSubnetMask)
        {
            var selectedOption = viewModel.NetworkAdapters.FirstOrDefault(choice =>
                    string.Equals(choice.IpAddress, selectedIpAddress ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(choice.SubnetMask, selectedSubnetMask ?? string.Empty, StringComparison.OrdinalIgnoreCase));

            if (selectedOption == null
                && string.IsNullOrWhiteSpace(selectedIpAddress)
                && string.IsNullOrWhiteSpace(selectedSubnetMask))
            {
                selectedOption = viewModel.NetworkAdapters.FirstOrDefault(choice =>
                    string.IsNullOrWhiteSpace(choice.IpAddress)
                    && string.IsNullOrWhiteSpace(choice.SubnetMask));
            }

            viewModel.RunSilently(() => viewModel.SelectedNetworkAdapter = selectedOption);
        }

        private Dictionary<string, string> ReadSpiceOptionValues(string spiceXmlPath)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!_spiceConfigFileService.TryLoadOptionsContext(
                    spiceXmlPath, LoadOptions.PreserveWhitespace, false, out var context, out _, out _))
            {
                return values;
            }

            foreach (var option in SpiceOptions)
            {
                values[option.XmlName] = context.GetOptionValue(option.XmlName) ?? string.Empty;
            }

            return values;
        }

        private void ApplySpiceOptionValues(SettingsPageViewModel viewModel, Dictionary<string, string> values)
        {
            foreach (var option in SpiceOptions)
            {
                if (values.TryGetValue(option.XmlName, out var xmlValue))
                {
                    option.ApplyXmlValue(viewModel, xmlValue);
                }
                else
                {
                    option.ApplyXmlValue(viewModel, string.Empty);
                }
            }
        }

        private void ApplyDeferredSettingsState(
            SettingsPageViewModel viewModel,
            DeferredSettingsState deferredState,
            string currentAsioDriverValue,
            string currentNetworkIp,
            string currentNetworkSubnet)
        {
            ApplySpiceOptionValues(viewModel, deferredState.SpiceOptionValues);

            viewModel.AsioDrivers.Clear();
            foreach (var option in deferredState.AsioDrivers)
            {
                viewModel.AsioDrivers.Add(option);
            }

            viewModel.NetworkAdapters.Clear();
            foreach (var option in deferredState.NetworkAdapters)
            {
                viewModel.NetworkAdapters.Add(option);
            }

            viewModel.RunSilently(() =>
            {
                viewModel.SelectedAsioDriver = deferredState.SelectedAsioDriver;
                viewModel.SelectedNetworkAdapter = deferredState.SelectedNetworkAdapter;
                if (!string.IsNullOrWhiteSpace(currentAsioDriverValue))
                {
                    viewModel.AsioDriverValue = currentAsioDriverValue;
                }

                if (!string.IsNullOrWhiteSpace(currentNetworkIp) || !string.IsNullOrWhiteSpace(currentNetworkSubnet))
                {
                    viewModel.NetworkAdapterIp = currentNetworkIp;
                    viewModel.NetworkAdapterSubnet = currentNetworkSubnet;
                }
            });

            SyncSelectedServerPresetFromCurrentFields(viewModel);
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

        private bool IsCompatLayerEffectivelyEnabled()
        {
            return GetCompatibilityLayerRuntimeState().IsFullyApplied;
        }

        private CompatibilityLayerRuntimeState GetCompatibilityLayerRuntimeState()
        {
            try
            {
                return CompatibilitySettingsService.DetectRuntimeState(
                    _paths.GetContentsDirectoryPath(),
                    _paths.GetBundledLibsDirectoryPath());
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to detect compatibility layer runtime state.");
                return new CompatibilityLayerRuntimeState(false, string.Empty, false);
            }
        }


        private bool RefreshSpiceConfigAvailability(SettingsPageViewModel viewModel)
        {
            bool isSpiceConfigAvailable = IsSpiceConfigAvailable(viewModel.UseSystemSpiceConfig);
            viewModel.RunSilently(() =>
            {
                viewModel.IsSpiceConfigAvailable = isSpiceConfigAvailable;
                viewModel.SpiceConfigEmptyStateMessage = MissingSpiceConfigMessage;
            });

            return isSpiceConfigAvailable;
        }

        private bool IsSpiceConfigAvailable(bool useSystemSpiceConfig)
        {
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
                _logger.LogDebug(ex, "AppData spice config validation failed for {SpiceXmlPath}.", spiceXmlPath);
                return false;
            }
        }

        private string ReadConfiguredCompatibilityRenderMode()
        {
            return CompatibilitySettingsService.NormalizeRenderMode(
                _configHandler.ReadString(AppConfigBootstrapper.SettingSectionName, "cl-rendermode", "dx9on12"));
        }

        private string SyncCompatibilityConfigToRuntimeState(CompatibilityLayerRuntimeState runtimeState)
        {
            var configuredRenderMode = ReadConfiguredCompatibilityRenderMode();
            var detectedRenderMode = string.IsNullOrWhiteSpace(runtimeState.DetectedRenderMode)
                ? string.Empty
                : CompatibilitySettingsService.NormalizeRenderMode(runtimeState.DetectedRenderMode);
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

        private void SaveServerPresets(SettingsPageViewModel viewModel)
        {
            _configHandler.SaveServerPresets(viewModel.ServerPresets, viewModel.ActiveServerPreset, NonePresetName);
        }

        private bool TryApplySpiceUpdates(
            string spiceXmlPath,
            SettingsPageViewModel viewModel,
            bool reloadViewModelOnSuccess = true,
            params SpiceOptionUpdate[] updates)
        {
            if (!_spiceConfigFileService.ApplySpiceOptions(spiceXmlPath, updates, out var error))
            {
                _uiInteractionService.ShowErrorToast("写入配置失败", error);
                return false;
            }

            if (reloadViewModelOnSuccess
                && string.Equals(
                    spiceXmlPath,
                    _paths.ResolveSpiceXmlPath(viewModel.UseSystemSpiceConfig),
                    StringComparison.OrdinalIgnoreCase))
            {
                LoadSpiceSettings(viewModel);
            }

            return true;
        }

        private static IEnumerable<SpiceOptionUpdate> BuildSpiceOptionUpdates(SettingsPageViewModel viewModel)
        {
            foreach (var option in GeneralSpiceOptions)
            {
                yield return new SpiceOptionUpdate(option.XmlName, option.GetXmlValue(viewModel), false);
            }
        }

    }
}
