using System;
using SystemEnvironment = System.Environment;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls.Notifications;
using LazyBootstrap.Services.Config;
using Microsoft.Extensions.Logging;

namespace LazyBootstrap.Services.Display
{

    public sealed class DisplayWorkflowService
    {
        private const string MainMonitorOptionName = "mainmonitor";
        private const string SubMonitorOptionName = "sdvxsubmonitor";

        private readonly ConfigHandler _configHandler;
        private readonly LauncherPaths _paths;
        private readonly SpiceConfigFileService _spiceConfigFileService;
        private readonly WindowsDisplayConfigurationService _displayConfigurationService;
        private readonly DisplaySettingsTransactionCoordinator _displaySettingsTransactionCoordinator;
        private readonly UiInteractionService _uiInteractionService;
        private readonly ILogger<DisplayWorkflowService> _logger;

        public DisplayWorkflowService(
            ConfigHandler configHandler,
            LauncherPaths paths,
            SpiceConfigFileService spiceConfigFileService,
            WindowsDisplayConfigurationService displayConfigurationService,
            DisplaySettingsTransactionCoordinator displaySettingsTransactionCoordinator,
            UiInteractionService uiInteractionService,
            ILogger<DisplayWorkflowService> logger)
        {
            _configHandler = configHandler ?? throw new ArgumentNullException(nameof(configHandler));
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _spiceConfigFileService = spiceConfigFileService ?? throw new ArgumentNullException(nameof(spiceConfigFileService));
            _displayConfigurationService = displayConfigurationService ?? throw new ArgumentNullException(nameof(displayConfigurationService));
            _displaySettingsTransactionCoordinator = displaySettingsTransactionCoordinator ?? throw new ArgumentNullException(nameof(displaySettingsTransactionCoordinator));
            _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task WarmDeferredAsync(DisplayConfigurationSnapshot state)
        {
            ArgumentNullException.ThrowIfNull(state);
            _logger.LogInformation("Display configuration warm-up started.");

            var discoveryResult = _displayConfigurationService.GetDisplays();
            if (!discoveryResult.Succeeded)
            {
                _logger.LogWarning("Display discovery failed during warm-up: {Error}", discoveryResult.ErrorMessage);
                _uiInteractionService.ShowWarningToast("读取显示器列表失败", discoveryResult.ErrorMessage);
            }
            else
            {
                _logger.LogInformation("Display discovery completed. DisplayCount={DisplayCount}", discoveryResult.Displays.Count);
            }

            state.RunSilently(() =>
            {
                state.Displays.Clear();
                foreach (var display in discoveryResult.Displays)
                {
                    state.Displays.Add(new DisplayChoiceOption(display, BuildDisplayLabel(display)));
                }

                EnsureRotationOptions(state);

                state.IsDisplayConfigurationEnabled = _configHandler.TryReadBool(AppConfigBootstrapper.DisplaySectionName, "displayconfigure", false);
                state.IsDualDisplay = !string.Equals(_configHandler.ReadString(AppConfigBootstrapper.DisplaySectionName, "mode", "single"), "single", StringComparison.OrdinalIgnoreCase);
                state.ExitRestore = _configHandler.TryReadBool(AppConfigBootstrapper.DisplaySectionName, "exitrestore", true);

                int mainIndex = ReadInt(AppConfigBootstrapper.DisplaySectionName, "mainscreen", 0);
                int subIndex = ReadInt(AppConfigBootstrapper.DisplaySectionName, "subscreen", Math.Min(1, Math.Max(0, state.Displays.Count - 1)));
                int mainRotation = NormalizeRotationValue(ReadInt(AppConfigBootstrapper.DisplaySectionName, "mainrotation", 0));
                int subRotation = NormalizeRotationValue(ReadInt(AppConfigBootstrapper.DisplaySectionName, "subrotation", 0));

                state.SelectedMainDisplay = GetDisplayByIndex(state, mainIndex);
                state.SelectedSubDisplay = GetDisplayByIndex(state, subIndex);
                state.SelectedMainRotation = state.Rotations.FirstOrDefault(option => option.Angle == mainRotation) ?? state.Rotations.FirstOrDefault();
                state.SelectedSubRotation = state.Rotations.FirstOrDefault(option => option.Angle == subRotation) ?? state.Rotations.FirstOrDefault();
                state.SelectedMainResolution = _configHandler.ReadString(AppConfigBootstrapper.DisplaySectionName, "mainresolution", string.Empty);
                state.SelectedSubResolution = _configHandler.ReadString(AppConfigBootstrapper.DisplaySectionName, "subresolution", string.Empty);
                state.SelectedMainRefreshRate = _configHandler.ReadString(AppConfigBootstrapper.DisplaySectionName, "mainrefresh", string.Empty);
                state.SelectedSubRefreshRate = _configHandler.ReadString(AppConfigBootstrapper.DisplaySectionName, "subrefresh", string.Empty);
                state.SelectedTarget = DisplaySelectionTarget.None;
                state.ShowNoScreenSelected = true;
                state.ShowMainScreenConfig = false;
                state.ShowSubScreenConfig = false;
            });

            return HandleConfigurationChangedAsync(state, refreshMainOptions: true, refreshSubOptions: true);
        }

        public Task PersistGeneralSettingsAsync(DisplayConfigurationSnapshot state)
        {
            ArgumentNullException.ThrowIfNull(state);
            _logger.LogInformation("Display general settings persistence started.");

            _configHandler.WriteString(AppConfigBootstrapper.DisplaySectionName, "displayconfigure", state.IsDisplayConfigurationEnabled.ToString().ToLowerInvariant());
            _configHandler.WriteString(AppConfigBootstrapper.DisplaySectionName, "mode", state.IsDualDisplay ? "dual" : "single");
            _configHandler.WriteString(AppConfigBootstrapper.DisplaySectionName, "exitrestore", state.ExitRestore.ToString().ToLowerInvariant());
            SyncSpiceMonitorOverrides(state);
            _logger.LogInformation("Display general settings persisted. Enabled={Enabled}, DualDisplay={DualDisplay}, ExitRestore={ExitRestore}", state.IsDisplayConfigurationEnabled, state.IsDualDisplay, state.ExitRestore);
            return Task.CompletedTask;
        }

        public Task HandleConfigurationChangedAsync(DisplayConfigurationSnapshot state, bool refreshMainOptions, bool refreshSubOptions)
        {
            ArgumentNullException.ThrowIfNull(state);
            _logger.LogDebug("Display configuration change handling started. RefreshMainOptions={RefreshMainOptions}, RefreshSubOptions={RefreshSubOptions}", refreshMainOptions, refreshSubOptions);

            try
            {
                state.RunSilently(() =>
                {
                    if (refreshMainOptions)
                    {
                        var mainOptions = RefreshDisplayOptions(
                            state.SelectedMainDisplay,
                            state.SelectedMainRotation?.Angle ?? 0,
                            state.SelectedMainResolution,
                            state.SelectedMainRefreshRate);
                        ReplaceCollection(state.MainResolutions, mainOptions.Resolutions);
                        ReplaceCollection(state.MainRefreshRates, mainOptions.RefreshRates);
                        state.SelectedMainResolution = mainOptions.SelectedResolution;
                        state.SelectedMainRefreshRate = mainOptions.SelectedRefreshRate;
                        state.MainDiagnosticsTooltip = mainOptions.Tooltip;
                    }

                    if (refreshSubOptions)
                    {
                        var subOptions = RefreshDisplayOptions(
                            state.SelectedSubDisplay,
                            state.SelectedSubRotation?.Angle ?? 0,
                            state.SelectedSubResolution,
                            state.SelectedSubRefreshRate);
                        ReplaceCollection(state.SubResolutions, subOptions.Resolutions);
                        ReplaceCollection(state.SubRefreshRates, subOptions.RefreshRates);
                        state.SelectedSubResolution = subOptions.SelectedResolution;
                        state.SelectedSubRefreshRate = subOptions.SelectedRefreshRate;
                        state.SubDiagnosticsTooltip = subOptions.Tooltip;
                    }

                    if (!state.IsDualDisplay && state.SelectedTarget == DisplaySelectionTarget.Sub)
                    {
                        state.SelectedTarget = DisplaySelectionTarget.None;
                        state.ShowNoScreenSelected = true;
                        state.ShowMainScreenConfig = false;
                        state.ShowSubScreenConfig = false;
                    }

                    UpdateDisplayInfo(state, true);
                    UpdateDisplayInfo(state, false);
                });

                PersistSelectionState(state);
                _logger.LogInformation("Display configuration change handled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Display configuration refresh failed.");
                _uiInteractionService.ShowErrorToast("显示器配置失败", ex.Message);
            }

            return Task.CompletedTask;
        }

        public async Task PreviewDisplaySettingsAsync(DisplayConfigurationSnapshot state)
        {
            ArgumentNullException.ThrowIfNull(state);
            _logger.LogInformation("Display configuration preview requested.");

            if (!state.IsDisplayConfigurationEnabled)
            {
                _logger.LogWarning("Display configuration preview skipped because display configuration is disabled.");
                _uiInteractionService.ShowWarningToast("显示器预览", "显示配置未启用，无法预览。");
                return;
            }

            if (!TryApplyForLaunch(state, out var restoreStates, out var messages))
            {
                _logger.LogWarning("Display configuration preview failed. RestoreStateCount={RestoreStateCount}, MessageCount={MessageCount}", restoreStates.Count, messages.Count);
                if (messages.Count > 0)
                {
                    _uiInteractionService.ShowWarningToast("显示器预览", BuildDiagnosticsMessage(messages));
                }

                await HandleConfigurationChangedAsync(state, refreshMainOptions: false, refreshSubOptions: false);
                return;
            }

            bool keepCurrentState = await _uiInteractionService.ShowDialogAsync(
                "显示器预览",
                "已应用当前预览设置。\n\n点击“保持现状”将保留当前结果，点击“还原”将恢复预览前状态。",
                "保持现状",
                "还原",
                NotificationType.Information,
                "Basic",
                "Danger");

            if (!keepCurrentState)
            {
                var restoreMessages = new List<string>();
                int restored = RestoreDisplayStates(restoreStates, restoreMessages);
                _logger.LogInformation("Display configuration preview restored. RestoredCount={RestoredCount}, MessageCount={MessageCount}", restored, restoreMessages.Count);
                if (restoreMessages.Count > 0)
                {
                    _uiInteractionService.ShowWarningToast("显示器还原", BuildDiagnosticsMessage(restoreMessages));
                }
                else
                {
                    _uiInteractionService.ShowInfoToast("显示器还原", restored > 0 ? $"已还原 {restored} 个显示器设置。" : "未还原任何显示器设置。");
                }

                await HandleConfigurationChangedAsync(state, refreshMainOptions: false, refreshSubOptions: false);
                return;
            }

            _logger.LogInformation("Display configuration preview kept by user.");
            _uiInteractionService.ShowInfoToast("显示器预览", "已保留当前预览设置。");
        }

        public Task OpenTouchPanelAsync()
        {
            try
            {
                _logger.LogInformation("Opening touch panel settings.");
                ProcessExecutionHelper.OpenControlPanel("/name Microsoft.TabletPCSettings");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open touch panel settings.");
                _uiInteractionService.ShowErrorToast("打开面板失败", ex.Message);
            }

            return Task.CompletedTask;
        }

        public bool TryApplyForLaunch(DisplayConfigurationSnapshot state, out Dictionary<string, DisplayState> restoreStates, out List<string> messages)
        {
            restoreStates = new Dictionary<string, DisplayState>(StringComparer.OrdinalIgnoreCase);
            messages = new List<string>();

            if (!state.IsDisplayConfigurationEnabled)
            {
                _logger.LogDebug("Display configuration apply skipped because it is disabled.");
                return true;
            }

            _logger.LogInformation("Applying display configuration for launch.");
            var requests = new List<DisplaySettingsRequest>();
            bool allValid = true;
            allValid &= TryBuildRequest(state.SelectedMainDisplay, state.SelectedMainRotation?.Angle ?? 0, state.SelectedMainResolution, state.SelectedMainRefreshRate, "主显示器", requests, messages);

            if (state.IsDualDisplay)
            {
                allValid &= TryBuildRequest(state.SelectedSubDisplay, state.SelectedSubRotation?.Angle ?? 0, state.SelectedSubResolution, state.SelectedSubRefreshRate, "副显示器", requests, messages);
            }

            if (!allValid)
            {
                _logger.LogWarning("Display configuration request validation failed. MessageCount={MessageCount}", messages.Count);
                return false;
            }

            var transactionResult = _displaySettingsTransactionCoordinator.Apply(requests);
            restoreStates = new Dictionary<string, DisplayState>(transactionResult.RestoreStates, StringComparer.OrdinalIgnoreCase);
            messages.AddRange(transactionResult.Messages);
            _logger.LogInformation(
                "Display configuration transaction completed. Succeeded={Succeeded}, RequestCount={RequestCount}, RestoreStateCount={RestoreStateCount}, MessageCount={MessageCount}",
                transactionResult.Succeeded,
                requests.Count,
                restoreStates.Count,
                messages.Count);
            return transactionResult.Succeeded;
        }

        public int RestoreDisplayStates(IReadOnlyDictionary<string, DisplayState> restoreStates, List<string> messages)
        {
            int restored = 0;
            if (restoreStates == null)
            {
                return restored;
            }

            foreach (var state in restoreStates.Values)
            {
                var result = _displayConfigurationService.RestoreDisplaySettings(state);
                if (result.Succeeded)
                {
                    restored++;
                    continue;
                }

                messages?.Add($"还原 {state.DeviceName} 失败: {result.ErrorMessage}");
            }

            _logger.LogInformation("Display state restore completed. RestoredCount={RestoredCount}, RequestedCount={RequestedCount}, MessageCount={MessageCount}", restored, restoreStates.Count, messages?.Count ?? 0);
            return restored;
        }

        private DisplayOptionState RefreshDisplayOptions(
            DisplayChoiceOption selectedDisplay,
            int rotation,
            string selectedResolution,
            string selectedRefreshRate)
        {
            if (selectedDisplay?.Info == null)
            {
                return new DisplayOptionState(Array.Empty<string>(), Array.Empty<string>(), string.Empty, string.Empty, string.Empty);
            }

            var supportedModesResult = _displayConfigurationService.GetSupportedModes(selectedDisplay.Info.DeviceName);
            string tooltip = BuildTooltip(supportedModesResult);

            var resolutionItems = BuildResolutionItems(supportedModesResult.Modes, rotation, out string highestResolution);

            if (!resolutionItems.Contains(selectedResolution ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                selectedResolution = highestResolution;
            }

            var refreshItems = supportedModesResult.Modes
                .Where(mode => string.Equals(NormalizeResolutionByRotation(mode.Width, mode.Height, rotation), selectedResolution, StringComparison.OrdinalIgnoreCase))
                .Select(mode => mode.RefreshRate)
                .Distinct()
                .OrderBy(value => value)
                .Select(value => value.ToString())
                .ToList();

            if (!refreshItems.Contains(selectedRefreshRate ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                selectedRefreshRate = refreshItems.FirstOrDefault() ?? string.Empty;
            }

            return new DisplayOptionState(resolutionItems, refreshItems, selectedResolution, selectedRefreshRate, tooltip);
        }

        private static IReadOnlyList<string> BuildResolutionItems(IReadOnlyList<DisplayMode> modes, int rotation, out string highestResolution)
        {
            highestResolution = string.Empty;
            if (modes == null || modes.Count == 0)
            {
                return Array.Empty<string>();
            }

            var highestMode = modes
                .OrderByDescending(mode => mode.Width * mode.Height)
                .ThenByDescending(mode => mode.Width)
                .ThenByDescending(mode => mode.Height)
                .First();
            highestResolution = NormalizeResolutionByRotation(highestMode.Width, highestMode.Height, rotation);

            var supportedResolutions = new HashSet<string>(
                modes.Select(mode => NormalizeResolutionByRotation(mode.Width, mode.Height, rotation)),
                StringComparer.OrdinalIgnoreCase);
            var resolutionItems = new List<string>();

            AddSupportedResolution(resolutionItems, supportedResolutions, NormalizeResolutionByRotation(1280, 720, rotation));
            AddSupportedResolution(resolutionItems, supportedResolutions, NormalizeResolutionByRotation(1920, 1080, rotation));
            AddSupportedResolution(resolutionItems, supportedResolutions, highestResolution);

            return resolutionItems;
        }

        private static void AddSupportedResolution(ICollection<string> target, ISet<string> supportedResolutions, string resolution)
        {
            if (string.IsNullOrWhiteSpace(resolution) || !supportedResolutions.Contains(resolution))
            {
                return;
            }

            if (target.Contains(resolution, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            target.Add(resolution);
        }

        private void UpdateDisplayInfo(DisplayConfigurationSnapshot state, bool isMainTarget)
        {
            var selectedDisplay = isMainTarget ? state.SelectedMainDisplay : state.SelectedSubDisplay;
            var rotation = isMainTarget ? state.SelectedMainRotation?.Angle ?? 0 : state.SelectedSubRotation?.Angle ?? 0;
            var resolution = isMainTarget ? state.SelectedMainResolution : state.SelectedSubResolution;
            var refreshRate = isMainTarget ? state.SelectedMainRefreshRate : state.SelectedSubRefreshRate;

            if (selectedDisplay?.Info == null)
            {
                if (isMainTarget)
                {
                    state.MainOutputInfo = "未知";
                    state.MainStartupInfo = "未设置";
                }
                else
                {
                    state.SubOutputInfo = "未知";
                    state.SubStartupInfo = "未设置";
                }
                return;
            }

            var stateResult = _displayConfigurationService.GetCurrentState(selectedDisplay.Info.DeviceName);
            var outputInfo = stateResult.Succeeded
                ? $"设备: {selectedDisplay.Info.FriendlyName} ({selectedDisplay.Info.DeviceName})\n当前: {stateResult.State.Width}x{stateResult.State.Height} @ {stateResult.State.RefreshRate}Hz, {FormatRotationDisplay(_displayConfigurationService.OrientationToAngle(stateResult.State.Orientation))}"
                : $"设备: {selectedDisplay.Info.FriendlyName} ({selectedDisplay.Info.DeviceName})\n当前: 读取失败\n原因: {stateResult.ErrorMessage}";
            var startupInfo = $"旋转: {FormatRotationDisplay(rotation)}\n分辨率: {FormatTextOrFallback(resolution)}\n刷新率: {FormatRefreshRateDisplay(refreshRate)}";

            if (isMainTarget)
            {
                state.MainOutputInfo = outputInfo;
                state.MainStartupInfo = startupInfo;
            }
            else
            {
                state.SubOutputInfo = outputInfo;
                state.SubStartupInfo = startupInfo;
            }
        }

        private void PersistSelectionState(DisplayConfigurationSnapshot state)
        {
            _logger.LogDebug("Persisting display selection state.");
            _configHandler.WriteString(AppConfigBootstrapper.DisplaySectionName, "displayconfigure", state.IsDisplayConfigurationEnabled.ToString().ToLowerInvariant());
            _configHandler.WriteString(AppConfigBootstrapper.DisplaySectionName, "mode", state.IsDualDisplay ? "dual" : "single");
            _configHandler.WriteString(AppConfigBootstrapper.DisplaySectionName, "exitrestore", state.ExitRestore.ToString().ToLowerInvariant());
            _configHandler.WriteString(AppConfigBootstrapper.DisplaySectionName, "mainscreen", GetIndex(state.Displays, state.SelectedMainDisplay).ToString());
            _configHandler.WriteString(AppConfigBootstrapper.DisplaySectionName, "subscreen", GetIndex(state.Displays, state.SelectedSubDisplay).ToString());
            _configHandler.WriteString(AppConfigBootstrapper.DisplaySectionName, "mainrotation", (state.SelectedMainRotation?.Angle ?? 0).ToString());
            _configHandler.WriteString(AppConfigBootstrapper.DisplaySectionName, "subrotation", (state.SelectedSubRotation?.Angle ?? 0).ToString());
            _configHandler.WriteString(AppConfigBootstrapper.DisplaySectionName, "mainresolution", state.SelectedMainResolution ?? string.Empty);
            _configHandler.WriteString(AppConfigBootstrapper.DisplaySectionName, "subresolution", state.SelectedSubResolution ?? string.Empty);
            _configHandler.WriteString(AppConfigBootstrapper.DisplaySectionName, "mainrefresh", state.SelectedMainRefreshRate ?? string.Empty);
            _configHandler.WriteString(AppConfigBootstrapper.DisplaySectionName, "subrefresh", state.SelectedSubRefreshRate ?? string.Empty);
            SyncSpiceMonitorOverrides(state);
            _logger.LogDebug("Display selection state persisted.");
        }

        private string GetActiveSpiceXmlPathForMonitorSync()
        {
            bool useSystem = bool.TryParse(
                _configHandler.ReadString(AppConfigBootstrapper.SettingSectionName, "use-system-config", "false"),
                out var parsed)
                && parsed;
            return _paths.ResolveSpiceXmlPath(useSystem);
        }

        private void SyncSpiceMonitorOverrides(DisplayConfigurationSnapshot state)
        {
            string mainMonitorValue = string.Empty;
            string subMonitorValue = string.Empty;

            if (state.IsDisplayConfigurationEnabled)
            {
                mainMonitorValue = state.SelectedMainDisplay?.Info?.DeviceName ?? string.Empty;
                subMonitorValue = state.IsDualDisplay
                    ? state.SelectedSubDisplay?.Info?.DeviceName ?? string.Empty
                    : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(mainMonitorValue))
            {
                mainMonitorValue = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(subMonitorValue))
            {
                subMonitorValue = string.Empty;
            }

            try
            {
                string spiceXmlPath = GetActiveSpiceXmlPathForMonitorSync();
                if (string.IsNullOrWhiteSpace(spiceXmlPath) || !File.Exists(spiceXmlPath))
                {
                    _logger.LogDebug("Spice monitor override sync skipped because active spice XML is missing.");
                    return;
                }

                // Pre-check: skip write if the monitor values are already set correctly.
                if (_spiceConfigFileService.TryLoadOptionsContext(
                        spiceXmlPath,
                        LoadOptions.PreserveWhitespace,
                        false,
                        out var context,
                        out _,
                        out _))
                {
                    string currentMainMonitor = context.GetOptionValue(MainMonitorOptionName) ?? string.Empty;
                    string currentSubMonitor = context.GetOptionValue(SubMonitorOptionName) ?? string.Empty;
                    if (string.Equals(currentMainMonitor, mainMonitorValue, StringComparison.Ordinal)
                        && string.Equals(currentSubMonitor, subMonitorValue, StringComparison.Ordinal))
                    {
                        _logger.LogDebug("Spice monitor override sync skipped because values are already current.");
                        return;
                    }
                }

                if (!_spiceConfigFileService.ApplySpiceOptions(
                        spiceXmlPath,
                        new[]
                        {
                            new SpiceOptionUpdate(MainMonitorOptionName, mainMonitorValue, false),
                            new SpiceOptionUpdate(SubMonitorOptionName, subMonitorValue, false)
                        },
                        out var error))
                {
                    _logger.LogWarning("Failed to sync spice monitor overrides: {Error}", error);
                }
                else
                {
                    _logger.LogInformation("Spice monitor overrides synced.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync spice monitor overrides.");
            }
        }

        private static void EnsureRotationOptions(DisplayConfigurationSnapshot state)
        {
            if (state.Rotations.Count > 0)
            {
                return;
            }

            state.Rotations.Add(new RotationOption(0));
            state.Rotations.Add(new RotationOption(90));
            state.Rotations.Add(new RotationOption(180));
            state.Rotations.Add(new RotationOption(270));
        }

        private static DisplayChoiceOption GetDisplayByIndex(DisplayConfigurationSnapshot state, int index)
        {
            if (state.Displays.Count == 0)
            {
                return null;
            }

            if (index < 0 || index >= state.Displays.Count)
            {
                index = 0;
            }

            return state.Displays[index];
        }

        private static int GetIndex(System.Collections.ObjectModel.ObservableCollection<DisplayChoiceOption> options, DisplayChoiceOption selected)
        {
            int index = options.IndexOf(selected);
            return index < 0 ? 0 : index;
        }

        private static void ReplaceCollection(System.Collections.ObjectModel.ObservableCollection<string> target, IReadOnlyList<string> source)
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(item);
            }
        }

        private static bool TryBuildRequest(
            DisplayChoiceOption selectedDisplay,
            int rotation,
            string resolution,
            string refreshRate,
            string targetName,
            List<DisplaySettingsRequest> requests,
            List<string> messages)
        {
            if (selectedDisplay?.Info == null)
            {
                messages.Add($"{targetName}未选择有效的显示器。");
                return false;
            }

            if (!TryParseResolution(resolution, out var width, out var height))
            {
                messages.Add($"{targetName}分辨率无效: {resolution}");
                return false;
            }

            if (!int.TryParse(refreshRate, out var refreshValue))
            {
                messages.Add($"{targetName}刷新率无效: {refreshRate}");
                return false;
            }

            requests.Add(new DisplaySettingsRequest(targetName, selectedDisplay.Info.DeviceName, rotation, width, height, refreshValue));
            return true;
        }

        private static bool TryParseResolution(string resolution, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (string.IsNullOrWhiteSpace(resolution))
            {
                return false;
            }

            var parts = resolution.Split('x', 'X');
            return parts.Length == 2
                && int.TryParse(parts[0], out width)
                && int.TryParse(parts[1], out height);
        }

        private static string NormalizeResolutionByRotation(int width, int height, int rotation)
        {
            bool vertical = rotation == 90 || rotation == 270;
            int normalizedWidth = width;
            int normalizedHeight = height;

            if (vertical && normalizedWidth > normalizedHeight)
            {
                (normalizedWidth, normalizedHeight) = (normalizedHeight, normalizedWidth);
            }

            if (!vertical && normalizedWidth < normalizedHeight)
            {
                (normalizedWidth, normalizedHeight) = (normalizedHeight, normalizedWidth);
            }

            return $"{normalizedWidth}x{normalizedHeight}";
        }

        private static string BuildDisplayLabel(DisplayInfo display)
        {
            if (display == null)
            {
                return "未知显示器";
            }

            var deviceName = display.DeviceName ?? string.Empty;
            var displayId = deviceName.StartsWith(@"\.\", StringComparison.OrdinalIgnoreCase)
                ? deviceName[4..]
                : deviceName;

            if (string.IsNullOrWhiteSpace(displayId))
            {
                return string.IsNullOrWhiteSpace(display.FriendlyName) ? "未知显示器" : display.FriendlyName;
            }

            if (string.IsNullOrWhiteSpace(display.FriendlyName))
            {
                return display.IsPrimary ? $"{displayId} - Primary" : displayId;
            }

            var label = $"{displayId} - {display.FriendlyName}";
            return display.IsPrimary ? $"{label} - Primary" : label;
        }

        private static string BuildTooltip(DisplayModeQueryResult result)
        {
            if (result == null || result.Succeeded || string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                return string.Empty;
            }

            return result.Modes.Count > 0
                ? $"{result.ErrorMessage}{SystemEnvironment.NewLine}已显示可读取到的显示模式，结果可能不完整。"
                : result.ErrorMessage;
        }

        private static string BuildDiagnosticsMessage(IReadOnlyList<string> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                return "未知错误。";
            }

            const int maxMessageCount = 3;
            var visibleMessages = messages
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Take(maxMessageCount)
                .ToList();

            if (visibleMessages.Count == 0)
            {
                return "未知错误。";
            }

            if (messages.Count > maxMessageCount)
            {
                visibleMessages.Add($"其余 {messages.Count - maxMessageCount} 项请查看当前设置。");
            }

            return string.Join(SystemEnvironment.NewLine, visibleMessages);
        }

        private static string FormatRotationDisplay(int angle)
        {
            return RotationOption.GetDisplayName(angle);
        }

        private static string FormatTextOrFallback(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未设置" : value;
        }

        private static string FormatRefreshRateDisplay(string refreshRate)
        {
            return string.IsNullOrWhiteSpace(refreshRate) ? "未设置" : $"{refreshRate}Hz";
        }

        private readonly record struct DisplayOptionState(
            IReadOnlyList<string> Resolutions,
            IReadOnlyList<string> RefreshRates,
            string SelectedResolution,
            string SelectedRefreshRate,
            string Tooltip);


        private int ReadInt(string section, string key, int defaultValue)
        {
            return int.TryParse(_configHandler.ReadString(section, key, defaultValue.ToString()), out var value)
                ? value
                : defaultValue;
        }

        private static int NormalizeRotationValue(int value)
        {
            return value switch
            {
                0 => 0,
                1 => 90,
                2 => 180,
                3 => 270,
                90 => 90,
                180 => 180,
                270 => 270,
                _ => 0
            };
        }
    }
}
