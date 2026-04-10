using System;
using SystemEnvironment = System.Environment;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Microsoft.Extensions.Logging;

namespace LazyBootstrap.Services.Display
{
    public interface IDisplayWorkflowService
    {
        Task WarmDeferredAsync(DisplayConfigurationPageViewModel viewModel);

        Task PersistGeneralSettingsAsync(DisplayConfigurationPageViewModel viewModel);

        Task HandleConfigurationChangedAsync(DisplayConfigurationPageViewModel viewModel, bool refreshMainOptions, bool refreshSubOptions);

        Task PreviewDisplaySettingsAsync(DisplayConfigurationPageViewModel viewModel);

        Task OpenTouchPanelAsync();

        bool TryApplyForLaunch(DisplayConfigurationPageViewModel viewModel, out Dictionary<string, DisplayState> restoreStates, out List<string> messages);

        int RestoreDisplayStates(IReadOnlyDictionary<string, DisplayState> restoreStates, List<string> messages);
    }

    internal sealed class DisplayWorkflowService : IDisplayWorkflowService
    {
        private const string DisplaySectionName = AppConfigBootstrapper.DisplaySectionName;

        private readonly IConfigHandler _configHandler;
        private readonly IDisplayConfigurationService _displayConfigurationService;
        private readonly IDisplaySettingsTransactionCoordinator _displaySettingsTransactionCoordinator;
        private readonly IUiInteractionService _uiInteractionService;
        private readonly ILogger<DisplayWorkflowService> _logger;

        public DisplayWorkflowService(
            IConfigHandler configHandler,
            IDisplayConfigurationService displayConfigurationService,
            IDisplaySettingsTransactionCoordinator displaySettingsTransactionCoordinator,
            IUiInteractionService uiInteractionService,
            ILogger<DisplayWorkflowService> logger)
        {
            _configHandler = configHandler ?? throw new ArgumentNullException(nameof(configHandler));
            _displayConfigurationService = displayConfigurationService ?? throw new ArgumentNullException(nameof(displayConfigurationService));
            _displaySettingsTransactionCoordinator = displaySettingsTransactionCoordinator ?? throw new ArgumentNullException(nameof(displaySettingsTransactionCoordinator));
            _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task WarmDeferredAsync(DisplayConfigurationPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            var discoveryResult = _displayConfigurationService.GetDisplays();
            if (!discoveryResult.Succeeded)
            {
                _uiInteractionService.ShowWarningToast("读取显示器列表失败", discoveryResult.ErrorMessage);
            }

            viewModel.RunSilently(() =>
            {
                viewModel.Displays.Clear();
                foreach (var display in discoveryResult.Displays)
                {
                    viewModel.Displays.Add(new DisplayChoiceOption(display, BuildDisplayLabel(display)));
                }

                EnsureRotationOptions(viewModel);

                viewModel.IsDisplayConfigurationEnabled = ReadBool(DisplaySectionName, "displayconfigure", false);
                viewModel.IsDualDisplay = !string.Equals(_configHandler.ReadString(DisplaySectionName, "mode", "single"), "single", StringComparison.OrdinalIgnoreCase);
                viewModel.ExitRestore = ReadBool(DisplaySectionName, "exitrestore", true);

                int mainIndex = ReadInt(DisplaySectionName, "mainscreen", 0);
                int subIndex = ReadInt(DisplaySectionName, "subscreen", Math.Min(1, Math.Max(0, viewModel.Displays.Count - 1)));
                int mainRotation = NormalizeRotationValue(ReadInt(DisplaySectionName, "mainrotation", 0));
                int subRotation = NormalizeRotationValue(ReadInt(DisplaySectionName, "subrotation", 0));

                viewModel.SelectedMainDisplay = GetDisplayByIndex(viewModel, mainIndex);
                viewModel.SelectedSubDisplay = GetDisplayByIndex(viewModel, subIndex);
                viewModel.SelectedMainRotation = viewModel.Rotations.FirstOrDefault(option => option.Angle == mainRotation) ?? viewModel.Rotations.FirstOrDefault();
                viewModel.SelectedSubRotation = viewModel.Rotations.FirstOrDefault(option => option.Angle == subRotation) ?? viewModel.Rotations.FirstOrDefault();
                viewModel.SelectedMainResolution = _configHandler.ReadString(DisplaySectionName, "mainresolution", string.Empty);
                viewModel.SelectedSubResolution = _configHandler.ReadString(DisplaySectionName, "subresolution", string.Empty);
                viewModel.SelectedMainRefreshRate = _configHandler.ReadString(DisplaySectionName, "mainrefresh", string.Empty);
                viewModel.SelectedSubRefreshRate = _configHandler.ReadString(DisplaySectionName, "subrefresh", string.Empty);
                viewModel.SelectedTarget = DisplaySelectionTarget.None;
                viewModel.ShowNoScreenSelected = true;
                viewModel.ShowMainScreenConfig = false;
                viewModel.ShowSubScreenConfig = false;
            });

            return HandleConfigurationChangedAsync(viewModel, refreshMainOptions: true, refreshSubOptions: true);
        }

        public Task PersistGeneralSettingsAsync(DisplayConfigurationPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            _configHandler.WriteString(DisplaySectionName, "displayconfigure", viewModel.IsDisplayConfigurationEnabled.ToString().ToLowerInvariant());
            _configHandler.WriteString(DisplaySectionName, "mode", viewModel.IsDualDisplay ? "dual" : "single");
            _configHandler.WriteString(DisplaySectionName, "exitrestore", viewModel.ExitRestore.ToString().ToLowerInvariant());
            return Task.CompletedTask;
        }

        public Task HandleConfigurationChangedAsync(DisplayConfigurationPageViewModel viewModel, bool refreshMainOptions, bool refreshSubOptions)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            try
            {
                viewModel.RunSilently(() =>
                {
                    if (refreshMainOptions)
                    {
                        var mainOptions = RefreshDisplayOptions(
                            viewModel.SelectedMainDisplay,
                            viewModel.SelectedMainRotation?.Angle ?? 0,
                            viewModel.SelectedMainResolution,
                            viewModel.SelectedMainRefreshRate);
                        ReplaceCollection(viewModel.MainResolutions, mainOptions.Resolutions);
                        ReplaceCollection(viewModel.MainRefreshRates, mainOptions.RefreshRates);
                        viewModel.SelectedMainResolution = mainOptions.SelectedResolution;
                        viewModel.SelectedMainRefreshRate = mainOptions.SelectedRefreshRate;
                        viewModel.MainDiagnosticsTooltip = mainOptions.Tooltip;
                    }

                    if (refreshSubOptions)
                    {
                        var subOptions = RefreshDisplayOptions(
                            viewModel.SelectedSubDisplay,
                            viewModel.SelectedSubRotation?.Angle ?? 0,
                            viewModel.SelectedSubResolution,
                            viewModel.SelectedSubRefreshRate);
                        ReplaceCollection(viewModel.SubResolutions, subOptions.Resolutions);
                        ReplaceCollection(viewModel.SubRefreshRates, subOptions.RefreshRates);
                        viewModel.SelectedSubResolution = subOptions.SelectedResolution;
                        viewModel.SelectedSubRefreshRate = subOptions.SelectedRefreshRate;
                        viewModel.SubDiagnosticsTooltip = subOptions.Tooltip;
                    }

                    if (!viewModel.IsDualDisplay && viewModel.SelectedTarget == DisplaySelectionTarget.Sub)
                    {
                        viewModel.SelectedTarget = DisplaySelectionTarget.None;
                        viewModel.ShowNoScreenSelected = true;
                        viewModel.ShowMainScreenConfig = false;
                        viewModel.ShowSubScreenConfig = false;
                    }

                    UpdateDisplayInfo(viewModel, true);
                    UpdateDisplayInfo(viewModel, false);
                });

                PersistSelectionState(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Display configuration refresh failed.");
                _uiInteractionService.ShowErrorToast("显示器配置失败", ex.Message);
            }

            return Task.CompletedTask;
        }

        public async Task PreviewDisplaySettingsAsync(DisplayConfigurationPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            if (!viewModel.IsDisplayConfigurationEnabled)
            {
                _uiInteractionService.ShowWarningToast("显示器预览", "显示配置未启用，无法预览。");
                return;
            }

            if (!TryApplyForLaunch(viewModel, out var restoreStates, out var messages))
            {
                if (messages.Count > 0)
                {
                    _uiInteractionService.ShowWarningToast("显示器预览", BuildDiagnosticsMessage(messages));
                }

                await HandleConfigurationChangedAsync(viewModel, refreshMainOptions: false, refreshSubOptions: false);
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
                if (restoreMessages.Count > 0)
                {
                    _uiInteractionService.ShowWarningToast("显示器还原", BuildDiagnosticsMessage(restoreMessages));
                }
                else
                {
                    _uiInteractionService.ShowInfoToast("显示器还原", restored > 0 ? $"已还原 {restored} 个显示器设置。" : "未还原任何显示器设置。");
                }

                await HandleConfigurationChangedAsync(viewModel, refreshMainOptions: false, refreshSubOptions: false);
                return;
            }

            _uiInteractionService.ShowInfoToast("显示器预览", "已保留当前预览设置。");
        }

        public Task OpenTouchPanelAsync()
        {
            try
            {
                ProcessExecutionHelper.OpenControlPanel("/name Microsoft.TabletPCSettings");
            }
            catch (Exception ex)
            {
                _uiInteractionService.ShowErrorToast("打开面板失败", ex.Message);
            }

            return Task.CompletedTask;
        }

        public bool TryApplyForLaunch(DisplayConfigurationPageViewModel viewModel, out Dictionary<string, DisplayState> restoreStates, out List<string> messages)
        {
            restoreStates = new Dictionary<string, DisplayState>(StringComparer.OrdinalIgnoreCase);
            messages = new List<string>();

            if (!viewModel.IsDisplayConfigurationEnabled)
            {
                return true;
            }

            var requests = new List<DisplaySettingsRequest>();
            bool allValid = true;
            allValid &= TryBuildRequest(viewModel.SelectedMainDisplay, viewModel.SelectedMainRotation?.Angle ?? 0, viewModel.SelectedMainResolution, viewModel.SelectedMainRefreshRate, "主显示器", requests, messages);

            if (viewModel.IsDualDisplay)
            {
                allValid &= TryBuildRequest(viewModel.SelectedSubDisplay, viewModel.SelectedSubRotation?.Angle ?? 0, viewModel.SelectedSubResolution, viewModel.SelectedSubRefreshRate, "副显示器", requests, messages);
            }

            if (!allValid)
            {
                return false;
            }

            var transactionResult = _displaySettingsTransactionCoordinator.Apply(requests);
            restoreStates = new Dictionary<string, DisplayState>(transactionResult.RestoreStates, StringComparer.OrdinalIgnoreCase);
            messages.AddRange(transactionResult.Messages);
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

        private void UpdateDisplayInfo(DisplayConfigurationPageViewModel viewModel, bool isMainTarget)
        {
            var selectedDisplay = isMainTarget ? viewModel.SelectedMainDisplay : viewModel.SelectedSubDisplay;
            var rotation = isMainTarget ? viewModel.SelectedMainRotation?.Angle ?? 0 : viewModel.SelectedSubRotation?.Angle ?? 0;
            var resolution = isMainTarget ? viewModel.SelectedMainResolution : viewModel.SelectedSubResolution;
            var refreshRate = isMainTarget ? viewModel.SelectedMainRefreshRate : viewModel.SelectedSubRefreshRate;

            if (selectedDisplay?.Info == null)
            {
                if (isMainTarget)
                {
                    viewModel.MainOutputInfo = "未知";
                    viewModel.MainStartupInfo = "未设置";
                }
                else
                {
                    viewModel.SubOutputInfo = "未知";
                    viewModel.SubStartupInfo = "未设置";
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
                viewModel.MainOutputInfo = outputInfo;
                viewModel.MainStartupInfo = startupInfo;
            }
            else
            {
                viewModel.SubOutputInfo = outputInfo;
                viewModel.SubStartupInfo = startupInfo;
            }
        }

        private void PersistSelectionState(DisplayConfigurationPageViewModel viewModel)
        {
            _configHandler.WriteString(DisplaySectionName, "displayconfigure", viewModel.IsDisplayConfigurationEnabled.ToString().ToLowerInvariant());
            _configHandler.WriteString(DisplaySectionName, "mode", viewModel.IsDualDisplay ? "dual" : "single");
            _configHandler.WriteString(DisplaySectionName, "exitrestore", viewModel.ExitRestore.ToString().ToLowerInvariant());
            _configHandler.WriteString(DisplaySectionName, "mainscreen", GetIndex(viewModel.Displays, viewModel.SelectedMainDisplay).ToString());
            _configHandler.WriteString(DisplaySectionName, "subscreen", GetIndex(viewModel.Displays, viewModel.SelectedSubDisplay).ToString());
            _configHandler.WriteString(DisplaySectionName, "mainrotation", (viewModel.SelectedMainRotation?.Angle ?? 0).ToString());
            _configHandler.WriteString(DisplaySectionName, "subrotation", (viewModel.SelectedSubRotation?.Angle ?? 0).ToString());
            _configHandler.WriteString(DisplaySectionName, "mainresolution", viewModel.SelectedMainResolution ?? string.Empty);
            _configHandler.WriteString(DisplaySectionName, "subresolution", viewModel.SelectedSubResolution ?? string.Empty);
            _configHandler.WriteString(DisplaySectionName, "mainrefresh", viewModel.SelectedMainRefreshRate ?? string.Empty);
            _configHandler.WriteString(DisplaySectionName, "subrefresh", viewModel.SelectedSubRefreshRate ?? string.Empty);
        }

        private static void EnsureRotationOptions(DisplayConfigurationPageViewModel viewModel)
        {
            if (viewModel.Rotations.Count > 0)
            {
                return;
            }

            viewModel.Rotations.Add(new RotationOption(0));
            viewModel.Rotations.Add(new RotationOption(90));
            viewModel.Rotations.Add(new RotationOption(180));
            viewModel.Rotations.Add(new RotationOption(270));
        }

        private static DisplayChoiceOption GetDisplayByIndex(DisplayConfigurationPageViewModel viewModel, int index)
        {
            if (viewModel.Displays.Count == 0)
            {
                return null;
            }

            if (index < 0 || index >= viewModel.Displays.Count)
            {
                index = 0;
            }

            return viewModel.Displays[index];
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

        private bool ReadBool(string section, string key, bool defaultValue)
        {
            return bool.TryParse(_configHandler.ReadString(section, key, defaultValue ? "true" : "false"), out var value) && value;
        }

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
