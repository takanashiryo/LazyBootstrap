using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace LazyBootstrap.Services.Environment
{
    public interface IEnvironmentScanService
    {
        Task InitializeInfoAsync(InfoPageViewModel viewModel);

        Task RunScanAsync(InfoPageViewModel viewModel);
    }

    internal sealed class EnvironmentScanService : IEnvironmentScanService
    {
        private readonly ILauncherPaths _paths;
        private readonly IShellStateService _shellStateService;
        private readonly IUiInteractionService _uiInteractionService;
        private readonly ILogger<EnvironmentScanService> _logger;

        public EnvironmentScanService(
            ILauncherPaths paths,
            IShellStateService shellStateService,
            IUiInteractionService uiInteractionService,
            ILogger<EnvironmentScanService> logger)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _shellStateService = shellStateService ?? throw new ArgumentNullException(nameof(shellStateService));
            _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task InitializeInfoAsync(InfoPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            viewModel.MachineProperty = ResolveMachineProperty();
            viewModel.GameVersion = ResolveCurrentGameVersion();
            viewModel.LauncherVersion = ResolveLauncherVersion();
            return Task.CompletedTask;
        }

        public async Task RunScanAsync(InfoPageViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            viewModel.HasEnvironmentScanErrors = false;

            try
            {
                _shellStateService.StatusText = "正在进行环境检查...";
                _shellStateService.IsStatusProgressVisible = true;
                _shellStateService.StatusProgressValue = 0d;

                var summary = await EnvironmentScan.RunAsync(
                    (progress, _) =>
                    {
                        _shellStateService.StatusProgressValue = Math.Clamp(progress, 0, 100);
                    },
                    _paths.GetContentsDirectoryPath(),
                    _paths.GetBundledLibsDirectoryPath());

                viewModel.EnvironmentSummary = summary.ErrorSummary ?? string.Empty;
                viewModel.HasEnvironmentScanErrors = summary.HadError;
                PopulateGroups(viewModel, summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Environment scan failed.");
                _uiInteractionService.ShowErrorToast("环境检查失败", ex.Message);
            }
            finally
            {
                _shellStateService.StatusText = "就绪";
                _shellStateService.IsStatusProgressVisible = false;
                _shellStateService.StatusProgressValue = 0d;
            }
        }

        private static void PopulateGroups(InfoPageViewModel viewModel, EnvironmentScan.ScanSummary summary)
        {
            viewModel.Groups.Clear();

            var groupedItems = new Dictionary<string, List<EnvironmentScan.ScanResultItem>>(StringComparer.OrdinalIgnoreCase);
            var rootItems = new List<EnvironmentScan.ScanResultItem>();

            foreach (var item in summary.Items)
            {
                int slashIndex = item.Item.IndexOf('/');
                if (slashIndex <= 0 || slashIndex >= item.Item.Length - 1)
                {
                    rootItems.Add(item);
                    continue;
                }

                string groupName = item.Item[..slashIndex].Trim();
                if (!groupedItems.TryGetValue(groupName, out var list))
                {
                    list = new List<EnvironmentScan.ScanResultItem>();
                    groupedItems[groupName] = list;
                }

                list.Add(item);
            }

            foreach (var item in rootItems)
            {
                var group = new EnvironmentScanGroup
                {
                    Title = item.Item,
                    ShowStatus = true,
                    Level = item.Level
                };
                group.Items.Add(BuildItem(item.Item, item.Detail, true, item.Level));
                viewModel.Groups.Add(group);
            }

            foreach (var pair in groupedItems)
            {
                var level = pair.Value.Any(value => value.Level == EnvironmentScan.ScanResultLevel.Error)
                    ? EnvironmentScan.ScanResultLevel.Error
                    : pair.Value.Any(value => value.Level == EnvironmentScan.ScanResultLevel.Warning)
                        ? EnvironmentScan.ScanResultLevel.Warning
                        : EnvironmentScan.ScanResultLevel.Success;

                var group = new EnvironmentScanGroup
                {
                    Title = pair.Key,
                    ShowStatus = false,
                    Level = level
                };

                bool isHardwareInfoGroup =
                    string.Equals(pair.Key, "CPU", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pair.Key, "GPU", StringComparison.OrdinalIgnoreCase);

                foreach (var child in pair.Value)
                {
                    int slashIndex = child.Item.IndexOf('/');
                    string childLabel = slashIndex >= 0 && slashIndex < child.Item.Length - 1
                        ? child.Item[(slashIndex + 1)..]
                        : child.Item;
                    bool isVirtualMachine = !string.IsNullOrWhiteSpace(child.Detail)
                        && child.Detail.Contains("虚拟机", StringComparison.OrdinalIgnoreCase);

                    string labelText;
                    if (isHardwareInfoGroup)
                    {
                        labelText = string.Equals(pair.Key, "CPU", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(child.Detail)
                            ? child.Detail
                            : childLabel;
                    }
                    else
                    {
                        labelText = string.IsNullOrWhiteSpace(child.Detail)
                            ? childLabel
                            : $"{childLabel} - {child.Detail}";
                    }

                    group.Items.Add(BuildItem(
                        labelText,
                        child.Detail,
                        !isHardwareInfoGroup || isVirtualMachine,
                        child.Level,
                        isVirtualMachine));
                }

                viewModel.Groups.Add(group);
            }
        }

        private static EnvironmentScanItem BuildItem(
            string label,
            string detail,
            bool showStatus,
            EnvironmentScan.ScanResultLevel level,
            bool isVirtualMachine = false)
        {
            return new EnvironmentScanItem
            {
                Label = label ?? string.Empty,
                Detail = detail ?? string.Empty,
                ShowStatus = showStatus,
                IsVirtualMachine = isVirtualMachine,
                Level = level,
                StatusText = isVirtualMachine
                    ? "虚拟机"
                    : level switch
                    {
                        EnvironmentScan.ScanResultLevel.Success => "通过",
                        EnvironmentScan.ScanResultLevel.Warning => "警告",
                        _ => "失败"
                    }
            };
        }

        private string ResolveMachineProperty()
        {
            var identPath = Path.Combine(_paths.GetContentsDirectoryPath(), "prop", "ea3-ident.xml");
            var result = TryReadMachinePropertyFromEa3(identPath);
            if (!string.IsNullOrWhiteSpace(result))
            {
                return result;
            }

            var configPath = Path.Combine(_paths.GetContentsDirectoryPath(), "prop", "ea3-config.xml");
            result = TryReadMachinePropertyFromEa3(configPath);
            return string.IsNullOrWhiteSpace(result) ? "未知" : result;
        }

        private string ResolveCurrentGameVersion()
        {
            try
            {
                var bootstrapPath = Path.Combine(_paths.GetContentsDirectoryPath(), "prop", "bootstrap.xml");
                if (!File.Exists(bootstrapPath))
                {
                    return "未知";
                }

                var doc = XDocument.Load(bootstrapPath);
                var releaseCode = doc.Root?.Element("release_code")?.Value?.Trim();
                return string.IsNullOrWhiteSpace(releaseCode) ? "未知" : releaseCode;
            }
            catch
            {
                return "未知";
            }
        }

        private string ResolveLauncherVersion()
        {
            try
            {
                var launcherExe = _paths.GetLauncherExecutablePath();
                if (!File.Exists(launcherExe))
                {
                    return "未知";
                }

                var versionInfo = FileVersionInfo.GetVersionInfo(launcherExe);
                if (!string.IsNullOrWhiteSpace(versionInfo.FileVersion))
                {
                    return versionInfo.FileVersion;
                }

                if (!string.IsNullOrWhiteSpace(versionInfo.ProductVersion))
                {
                    return versionInfo.ProductVersion;
                }
            }
            catch
            {
            }

            return "未知";
        }

        private static string TryReadMachinePropertyFromEa3(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var doc = XDocument.Load(filePath);
                var softNode = doc.Root?.Element("soft");
                if (softNode == null)
                {
                    return null;
                }

                var model = softNode.Element("model")?.Value?.Trim();
                var dest = softNode.Element("dest")?.Value?.Trim();
                var spec = softNode.Element("spec")?.Value?.Trim();
                var rev = softNode.Element("rev")?.Value?.Trim();

                if (string.IsNullOrWhiteSpace(model)
                    || string.IsNullOrWhiteSpace(dest)
                    || string.IsNullOrWhiteSpace(spec)
                    || string.IsNullOrWhiteSpace(rev))
                {
                    return null;
                }

                return $"{model}:{dest}:{spec}:{rev}";
            }
            catch
            {
                return null;
            }
        }
    }
}
