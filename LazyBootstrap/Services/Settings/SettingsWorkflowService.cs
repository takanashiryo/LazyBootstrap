using System;
using SystemEnvironment = System.Environment;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
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
        Task ImportRecommendedConfigAsync(SettingsPageViewModel viewModel);
        Task OpenAsioControlPanelAsync(SettingsPageViewModel viewModel);
        Task AddServerPresetAsync(SettingsPageViewModel viewModel);
        Task DeleteServerPresetAsync(SettingsPageViewModel viewModel);
    }

    internal sealed class SettingsWorkflowService : ISettingsWorkflowService
    {
        private const string NonePresetName = "无";
        private const string AsphyxiaPresetName = "Asphyxia";
        private const string AsphyxiaDefaultUrl = "http://localhost:8083";
        private const string SettingSectionName = AppConfigBootstrapper.SettingSectionName;
        private const string MissingSpiceConfigMessage = "未找到任何spice2x配置文件";

        private static readonly SpiceOptionUpdate[] RecommendedSpiceOptionUpdates =
        {
            new("k", "ifs_hook.dll", false),
            new("sp2x-nvprofile", "/ENABLED", false),
            new("sp2x-lowlatencysharedaudio", "/ENABLED", false),
            new("sp2x-dx9on12", "0", false),
            new("url", AsphyxiaDefaultUrl, false),
            // new("sp2x-sdvxsubredraw", "/ENABLED", false)
        };

        private readonly IConfigHandler _configHandler;
        private readonly ILauncherPaths _paths;
        private readonly ISpiceConfigFileService _spiceConfigFileService;
        private readonly ICompatibilitySettingsService _compatibilitySettingsService;
        private readonly IUiInteractionService _uiInteractionService;
        private readonly ILogger<SettingsWorkflowService> _logger;

        private sealed class DeferredSettingsState
        {
            public required SpiceSettingsSnapshot SpiceSettings { get; init; }

            public required List<AsioDriverOption> AsioDrivers { get; init; }

            public required AsioDriverOption SelectedAsioDriver { get; init; }

            public required List<NetworkAdapterOption> NetworkAdapters { get; init; }

            public required NetworkAdapterOption SelectedNetworkAdapter { get; init; }
        }

        private sealed class SpiceSettingsSnapshot
        {
            public bool Windowed { get; init; }
            public string DllInjection { get; init; } = string.Empty;
            public bool PCoreOptimization { get; init; }
            public bool DisableSubDisplay { get; init; }
            public int WindowModeIndex { get; init; }
            public bool SubBorderless { get; init; }
            public bool ShowCursorTouchSim { get; init; }
            public bool WindowTopMost { get; init; }
            public string WindowSize { get; init; } = string.Empty;
            public bool SingleAdapter { get; init; }
            public bool NvidiaPerformanceProfile { get; init; }
            public bool SubWindowTopMost { get; init; }
            public bool SubForceRender { get; init; }
            public bool NativeTouch { get; init; }
            public string AsioDriverValue { get; init; } = string.Empty;
            public bool LowLatencySharedAudio { get; init; }
            public bool CardIo { get; init; }
            public bool HidSmartCard { get; init; }
            public bool NetDump { get; init; }
            public string NetworkAdapterIp { get; init; } = string.Empty;
            public string NetworkAdapterSubnet { get; init; } = string.Empty;
            public string ServerAddress { get; init; } = string.Empty;
            public string PcbId { get; init; } = string.Empty;
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
            bool isSpiceConfigAvailable = IsSpiceConfigAvailable();

            viewModel.RunSilently(() =>
            {
                viewModel.NoAsphyxia = ReadBool(SettingSectionName, "noasphyxia", false);
                viewModel.CompatibilityRenderMode = CompatibilitySettingsService.NormalizeRenderMode(_configHandler.ReadString(SettingSectionName, "cl-rendermode", "dx9on12"));
                viewModel.IsSpiceConfigAvailable = isSpiceConfigAvailable;
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
                var spiceSettings = ReadSpiceSettingsSnapshot();
                var asioDrivers = BuildAsioDriverOptions(spiceSettings.AsioDriverValue);
                var selectedAsioDriver = asioDrivers.FirstOrDefault(choice =>
                                             string.Equals(choice.Value, spiceSettings.AsioDriverValue ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                                         ?? asioDrivers.FirstOrDefault()
                                         ?? new AsioDriverOption("无", string.Empty);

                var networkAdapters = BuildNetworkAdapterOptions(spiceSettings.NetworkAdapterIp, spiceSettings.NetworkAdapterSubnet);
                var selectedNetworkAdapter = networkAdapters.FirstOrDefault(choice =>
                                                   string.Equals(choice.IpAddress, spiceSettings.NetworkAdapterIp ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                                                   && string.Equals(choice.SubnetMask, spiceSettings.NetworkAdapterSubnet ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                                               ?? networkAdapters.FirstOrDefault()
                                               ?? new NetworkAdapterOption("无", string.Empty, string.Empty);

                return new DeferredSettingsState
                {
                    SpiceSettings = spiceSettings,
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
                _configHandler.WriteString(SettingSectionName, "noasphyxia", viewModel.NoAsphyxia.ToString().ToLowerInvariant());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist launcher settings.");
                _uiInteractionService.ShowErrorToast("保存设置失败", ex.Message);
                viewModel.RunSilently(() => viewModel.NoAsphyxia = ReadBool(SettingSectionName, "noasphyxia", false));
            }

            return Task.CompletedTask;
        }

        public Task PersistServerEndpointAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            viewModel.ServerAddress = (viewModel.ServerAddress ?? string.Empty).Trim();
            viewModel.PcbId = (viewModel.PcbId ?? string.Empty).Trim();

            if (!TryApplySpiceUpdates(
                    _paths.GetSpiceXmlPath(),
                    LoadOptions.PreserveWhitespace,
                    false,
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

            viewModel.NetworkAdapterIp = NormalizeNetworkValue(viewModel.NetworkAdapterIp);
            viewModel.NetworkAdapterSubnet = NormalizeNetworkValue(viewModel.NetworkAdapterSubnet);

            if (!TryApplySpiceUpdates(
                    _paths.GetSpiceXmlPath(),
                    LoadOptions.PreserveWhitespace,
                    false,
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
                    _paths.GetSpiceXmlPath(),
                    LoadOptions.PreserveWhitespace,
                    false,
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
            if (_compatibilitySettingsService.TryToggleCompatLayer(
                    viewModel.CompatibilityLayerEnabled,
                    renderMode,
                    dxModeValue => TryApplyCompatibilityDxMode(viewModel, dxModeValue),
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

            if (_compatibilitySettingsService.TryPersistRenderMode(
                    renderMode,
                    viewModel.CompatibilityLayerEnabled,
                    dxModeValue => TryApplyCompatibilityDxMode(viewModel, dxModeValue),
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

            string cfgToolPath = Path.Combine(_paths.GetContentsDirectoryPath(), "spicecfg.exe");

            if (!File.Exists(cfgToolPath))
            {
                _uiInteractionService.ShowErrorToast("无法启动 spicecfg", $"未找到程序: {cfgToolPath}");
                return;
            }

            if (!File.Exists(_paths.ConfigFilePath))
            {
                _uiInteractionService.ShowErrorToast("无法启动 spicecfg", $"未找到配置文件: {_paths.ConfigFilePath}");
                return;
            }

            viewModel.IsSettingsBusy = true;

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = cfgToolPath,
                    Arguments = string.Empty,
                    WorkingDirectory = Path.GetDirectoryName(cfgToolPath)
                });

                if (process == null)
                {
                    _uiInteractionService.ShowErrorToast("无法启动 spicecfg", "创建进程失败。");
                    return;
                }

                await process.WaitForExitAsync();
                await InitializeStartupAsync(viewModel);
                await WarmDeferredAsync(viewModel);
            }
            catch (Exception ex)
            {
                _uiInteractionService.ShowErrorToast("启动 spicecfg 失败", ex.Message);
            }
            finally
            {
                viewModel.IsSettingsBusy = false;
            }
        }

        public Task ImportRecommendedConfigAsync(SettingsPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            return ImportRecommendedConfigCoreAsync(viewModel);
        }

        private async Task ImportRecommendedConfigCoreAsync(SettingsPageViewModel viewModel)
        {
            var confirmed = await _uiInteractionService.ShowDialogAsync(
                "导入推荐spice2x配置",
                "导入推荐spice2x配置会清除以下页面的现有配置并导入新配置：\n\nOptions\nAdvanced\nDevelopment\n\n你确定要执行吗？",
                "确认",
                "取消",
                NotificationType.Warning);

            if (!confirmed)
            {
                return;
            }

            try
            {
                var appDataSpiceXmlPath = Path.Combine(
                    SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ApplicationData),
                    "spicetools.xml");

                if (!File.Exists(appDataSpiceXmlPath))
                {
                    _uiInteractionService.ShowErrorToast("导入失败", "未找到 %AppData%\\spicetools.xml，请先启动 spicecfg 重建配置文件再进行导入。");
                    return;
                }

                if (!TryGetSpiceOptionsContext(appDataSpiceXmlPath, LoadOptions.PreserveWhitespace, true, out var context))
                {
                    _uiInteractionService.ShowErrorToast("导入失败", "未找到 Sound Voltex 配置项，无法导入推荐配置。");
                    return;
                }

                var normalizationWarning = _spiceConfigFileService.ReplaceOptions(context, RecommendedSpiceOptionUpdates);
                if (!string.IsNullOrWhiteSpace(normalizationWarning))
                {
                    _uiInteractionService.ShowWarningToast("配置格式修复失败", normalizationWarning);
                }

                LoadSpiceSettings(viewModel);
                RefreshAsioDrivers(viewModel, viewModel.AsioDriverValue);
                RefreshNetworkAdapters(viewModel, viewModel.NetworkAdapterIp, viewModel.NetworkAdapterSubnet);
                _uiInteractionService.ShowInfoToast("导入完成", "推荐 spice2x 配置已导入。");
            }
            catch (Exception ex)
            {
                _uiInteractionService.ShowErrorToast("导入失败", ex.Message);
            }
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
            if (_compatibilitySettingsService.TryToggleCompatLayer(
                    true,
                    renderMode,
                    dxModeValue => TryApplyCompatibilityDxMode(viewModel, dxModeValue),
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

            var snapshot = ReadSpiceSettingsSnapshot();
            viewModel.RunSilently(() => ApplySpiceSettingsSnapshot(viewModel, snapshot));
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
            viewModel.RunSilently(() =>
            {
                ApplySpiceSettingsSnapshot(viewModel, new SpiceSettingsSnapshot());
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

        private bool TryGetSpiceOptionsContext(string spiceXmlPath, LoadOptions loadOptions, bool createOptionsWhenMissing, out SpiceOptionsContext context)
        {
            if (!_spiceConfigFileService.TryLoadOptionsContext(spiceXmlPath, loadOptions, createOptionsWhenMissing, out context, out var message, out var warning))
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    if (warning)
                    {
                        _uiInteractionService.ShowWarningToast("读取配置异常", message);
                    }
                    else
                    {
                        _uiInteractionService.ShowErrorToast("读取配置失败", message);
                    }
                }

                return false;
            }

            return true;
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

        private SpiceSettingsSnapshot ReadSpiceSettingsSnapshot()
        {
            if (!TryGetSpiceOptionsContext(_paths.GetSpiceXmlPath(), LoadOptions.PreserveWhitespace, false, out var context))
            {
                return new SpiceSettingsSnapshot();
            }

            return new SpiceSettingsSnapshot
            {
                Windowed = string.Equals(context.GetOptionValue("w"), "/ENABLED", StringComparison.OrdinalIgnoreCase),
                DllInjection = context.GetOptionValue("k") ?? string.Empty,
                PCoreOptimization = string.Equals(context.GetOptionValue("sp2x-processefficiency"), "pcores", StringComparison.OrdinalIgnoreCase),
                DisableSubDisplay = string.Equals(context.GetOptionValue("sp2x-sdvxnosub"), "/ENABLED", StringComparison.OrdinalIgnoreCase),
                WindowModeIndex = ResolveWindowModeIndex(context.GetOptionValue("sp2x-windowborder")),
                SubBorderless = string.Equals(context.GetOptionValue("sdvxwsubborderless"), "/ENABLED", StringComparison.OrdinalIgnoreCase),
                ShowCursorTouchSim = string.Equals(context.GetOptionValue("s"), "/ENABLED", StringComparison.OrdinalIgnoreCase),
                WindowTopMost = string.Equals(context.GetOptionValue("sp2x-windowalwaysontop"), "/ENABLED", StringComparison.OrdinalIgnoreCase),
                WindowSize = context.GetOptionValue("sp2x-windowsize") ?? string.Empty,
                SingleAdapter = string.Equals(context.GetOptionValue("graphics-force-single-adapter"), "/ENABLED", StringComparison.OrdinalIgnoreCase),
                NvidiaPerformanceProfile = string.Equals(context.GetOptionValue("sp2x-nvprofile"), "/ENABLED", StringComparison.OrdinalIgnoreCase),
                SubWindowTopMost = string.Equals(context.GetOptionValue("sdvxwsubtop"), "/ENABLED", StringComparison.OrdinalIgnoreCase),
                SubForceRender = string.Equals(context.GetOptionValue("sp2x-sdvxsubredraw"), "/ENABLED", StringComparison.OrdinalIgnoreCase),
                NativeTouch = string.Equals(context.GetOptionValue("sdvxnativetouch"), "/ENABLED", StringComparison.OrdinalIgnoreCase),
                AsioDriverValue = context.GetOptionValue("sp2x-sdvxasio") ?? string.Empty,
                LowLatencySharedAudio = string.Equals(context.GetOptionValue("sp2x-lowlatencysharedaudio"), "/ENABLED", StringComparison.OrdinalIgnoreCase),
                CardIo = string.Equals(context.GetOptionValue("cardio"), "/ENABLED", StringComparison.OrdinalIgnoreCase),
                HidSmartCard = string.Equals(context.GetOptionValue("scard"), "/ENABLED", StringComparison.OrdinalIgnoreCase),
                NetDump = string.Equals(context.GetOptionValue("netdump"), "/ENABLED", StringComparison.OrdinalIgnoreCase),
                NetworkAdapterIp = NormalizeNetworkValue(context.GetOptionValue("network")),
                NetworkAdapterSubnet = NormalizeNetworkValue(context.GetOptionValue("subnet")),
                ServerAddress = context.GetOptionValue("url") ?? string.Empty,
                PcbId = context.GetOptionValue("p") ?? string.Empty
            };
        }

        private static void ApplySpiceSettingsSnapshot(SettingsPageViewModel viewModel, SpiceSettingsSnapshot snapshot)
        {
            viewModel.Windowed = snapshot.Windowed;
            viewModel.DllInjection = snapshot.DllInjection;
            viewModel.PCoreOptimization = snapshot.PCoreOptimization;
            viewModel.DisableSubDisplay = snapshot.DisableSubDisplay;
            viewModel.WindowModeIndex = snapshot.WindowModeIndex;
            viewModel.SubBorderless = snapshot.SubBorderless;
            viewModel.ShowCursorTouchSim = snapshot.ShowCursorTouchSim;
            viewModel.WindowTopMost = snapshot.WindowTopMost;
            viewModel.WindowSize = snapshot.WindowSize;
            viewModel.SingleAdapter = snapshot.SingleAdapter;
            viewModel.NvidiaPerformanceProfile = snapshot.NvidiaPerformanceProfile;
            viewModel.SubWindowTopMost = snapshot.SubWindowTopMost;
            viewModel.SubForceRender = snapshot.SubForceRender;
            viewModel.NativeTouch = snapshot.NativeTouch;
            viewModel.AsioDriverValue = snapshot.AsioDriverValue;
            viewModel.LowLatencySharedAudio = snapshot.LowLatencySharedAudio;
            viewModel.CardIo = snapshot.CardIo;
            viewModel.HidSmartCard = snapshot.HidSmartCard;
            viewModel.NetDump = snapshot.NetDump;
            viewModel.NetworkAdapterIp = snapshot.NetworkAdapterIp;
            viewModel.NetworkAdapterSubnet = snapshot.NetworkAdapterSubnet;
            viewModel.ServerAddress = snapshot.ServerAddress;
            viewModel.PcbId = snapshot.PcbId;
        }

        private void ApplyDeferredSettingsState(
            SettingsPageViewModel viewModel,
            DeferredSettingsState deferredState,
            string currentAsioDriverValue,
            string currentNetworkIp,
            string currentNetworkSubnet)
        {
            viewModel.RunSilently(() =>
            {
                ApplySpiceSettingsSnapshot(viewModel, deferredState.SpiceSettings);
            });

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

        private bool ReadBool(string section, string key, bool defaultValue)
        {
            return bool.TryParse(_configHandler.ReadString(section, key, defaultValue ? "true" : "false"), out var value) && value;
        }

        private bool RefreshSpiceConfigAvailability(SettingsPageViewModel viewModel)
        {
            bool isSpiceConfigAvailable = IsSpiceConfigAvailable();
            viewModel.RunSilently(() =>
            {
                viewModel.IsSpiceConfigAvailable = isSpiceConfigAvailable;
                viewModel.SpiceConfigEmptyStateMessage = MissingSpiceConfigMessage;
            });

            return isSpiceConfigAvailable;
        }

        private bool IsSpiceConfigAvailable()
        {
            string spiceXmlPath = _paths.GetSpiceXmlPath();
            if (!File.Exists(spiceXmlPath))
            {
                return false;
            }

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
                _configHandler.ReadString(SettingSectionName, "cl-rendermode", "dx9on12"));
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
                var currentCompatEnabled = ReadBool(SettingSectionName, "compatlayer", false);
                if (currentCompatEnabled != targetCompatEnabled)
                {
                    _configHandler.WriteString(
                        SettingSectionName,
                        "compatlayer",
                        targetCompatEnabled ? "true" : "false");
                }

                if (!string.IsNullOrWhiteSpace(detectedRenderMode)
                    && !string.Equals(configuredRenderMode, detectedRenderMode, StringComparison.OrdinalIgnoreCase))
                {
                    _configHandler.WriteString(SettingSectionName, "cl-rendermode", detectedRenderMode);
                    configuredRenderMode = detectedRenderMode;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync compatibility runtime state back to config.toml.");
            }

            return configuredRenderMode;
        }

        private static int ResolveWindowModeIndex(string windowBorderValue)
        {
            return windowBorderValue switch
            {
                "1" => 1,
                "2" => 2,
                _ => 0
            };
        }

        private static string NormalizeNetworkValue(string value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private void SaveServerPresets(SettingsPageViewModel viewModel)
        {
            _configHandler.SaveServerPresets(viewModel.ServerPresets, viewModel.ActiveServerPreset, NonePresetName);
        }

        private bool TryApplySpiceUpdates(
            string spiceXmlPath,
            LoadOptions loadOptions,
            bool createOptionsWhenMissing,
            SettingsPageViewModel viewModel,
            bool reloadViewModelOnSuccess = true,
            params SpiceOptionUpdate[] updates)
        {
            if (!TryGetSpiceOptionsContext(spiceXmlPath, loadOptions, createOptionsWhenMissing, out var context))
            {
                return false;
            }

            var normalizationWarning = _spiceConfigFileService.ApplyUpdates(context, updates);
            if (!string.IsNullOrWhiteSpace(normalizationWarning))
            {
                _uiInteractionService.ShowWarningToast("配置格式修复失败", normalizationWarning);
            }

            if (reloadViewModelOnSuccess
                && string.Equals(spiceXmlPath, _paths.GetSpiceXmlPath(), StringComparison.OrdinalIgnoreCase))
            {
                LoadSpiceSettings(viewModel);
            }

            return true;
        }

        private bool TryApplyCompatibilityDxMode(SettingsPageViewModel viewModel, string dxModeValue)
        {
            string spiceXmlPath = _paths.GetSpiceXmlPath();
            var spiceSnapshot = FileStateSnapshot.Capture(spiceXmlPath);

            try
            {
                if (TryApplySpiceUpdates(
                        spiceXmlPath,
                        LoadOptions.PreserveWhitespace,
                        false,
                        viewModel,
                        reloadViewModelOnSuccess: false,
                        new SpiceOptionUpdate("sp2x-dx9on12", dxModeValue, false)))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update spice compatibility dx mode.");
            }

            try
            {
                spiceSnapshot.Restore();
                if (string.Equals(spiceXmlPath, _paths.GetSpiceXmlPath(), StringComparison.OrdinalIgnoreCase)
                    && File.Exists(spiceXmlPath))
                {
                    LoadSpiceSettings(viewModel);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore spice compatibility dx mode snapshot.");
            }

            return false;
        }

        private IEnumerable<SpiceOptionUpdate> BuildSpiceOptionUpdates(SettingsPageViewModel viewModel)
        {
            yield return new SpiceOptionUpdate("w", viewModel.Windowed ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("k", viewModel.DllInjection ?? string.Empty, false);
            yield return new SpiceOptionUpdate("sp2x-processefficiency", viewModel.PCoreOptimization ? "pcores" : string.Empty);
            yield return new SpiceOptionUpdate("sp2x-sdvxnosub", viewModel.DisableSubDisplay ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sp2x-windowborder", ResolveWindowBorderValue(viewModel.WindowModeIndex));
            yield return new SpiceOptionUpdate("sdvxwsubborderless", viewModel.SubBorderless ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("s", viewModel.ShowCursorTouchSim ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sp2x-windowalwaysontop", viewModel.WindowTopMost ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sp2x-windowsize", viewModel.WindowSize ?? string.Empty);
            yield return new SpiceOptionUpdate("graphics-force-single-adapter", viewModel.SingleAdapter ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sp2x-nvprofile", viewModel.NvidiaPerformanceProfile ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sdvxwsubtop", viewModel.SubWindowTopMost ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sp2x-sdvxsubredraw", viewModel.SubForceRender ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sdvxnativetouch", viewModel.NativeTouch ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("sp2x-sdvxasio", viewModel.SelectedAsioDriver?.Value ?? viewModel.AsioDriverValue ?? string.Empty);
            yield return new SpiceOptionUpdate("sp2x-lowlatencysharedaudio", viewModel.LowLatencySharedAudio ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("cardio", viewModel.CardIo ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("scard", viewModel.HidSmartCard ? "/ENABLED" : string.Empty);
            yield return new SpiceOptionUpdate("netdump", viewModel.NetDump ? "/ENABLED" : string.Empty);
        }

        private static string ResolveWindowBorderValue(int windowModeIndex)
        {
            return windowModeIndex switch
            {
                1 => "1",
                2 => "2",
                _ => string.Empty
            };
        }
    }
}
