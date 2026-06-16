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

    public sealed class EnvironmentScanService
    {
        private static readonly List<EnvironmentScan.ScanResultItem> EmptyScanBucket = [];
        private readonly LauncherPaths _paths;
        private readonly AppShellState _shellStateService;
        private readonly UiInteractionService _uiInteractionService;
        private readonly ILogger<EnvironmentScanService> _logger;

        public EnvironmentScanService(
            LauncherPaths paths,
            AppShellState shellStateService,
            UiInteractionService uiInteractionService,
            ILogger<EnvironmentScanService> logger)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _shellStateService = shellStateService ?? throw new ArgumentNullException(nameof(shellStateService));
            _uiInteractionService = uiInteractionService ?? throw new ArgumentNullException(nameof(uiInteractionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task InitializeInfoAsync(EnvironmentScanPresentation presentation)
        {
            ArgumentNullException.ThrowIfNull(presentation);
            _logger.LogInformation("Environment information initialization started.");

            presentation.MachineProperty = ResolveMachineProperty();
            presentation.GameVersion = ResolveCurrentGameVersion();
            presentation.LauncherVersion = ResolveLauncherVersion();
            _logger.LogInformation("Environment information initialization completed.");
            return Task.CompletedTask;
        }

        public async Task RunScanAsync(EnvironmentScanPresentation presentation)
        {
            ArgumentNullException.ThrowIfNull(presentation);
            _logger.LogInformation("Environment scan started.");
            var stopwatch = Stopwatch.StartNew();

            presentation.HasEnvironmentScanErrors = false;
            ResetScanPresentation(presentation);

            try
            {
                _shellStateService.StatusText = "正在进行环境检查...";

                var summary = await EnvironmentScan.RunAsync(
                    (_, _) => { },
                    _paths.GetContentsDirectoryPath(),
                    _paths.GetBundledLibsDirectoryPath());

                presentation.EnvironmentSummary = summary.ErrorSummary ?? string.Empty;
                presentation.HasEnvironmentScanErrors = summary.HadError;
                PopulateScanSlots(presentation, summary);
                stopwatch.Stop();
                _logger.LogInformation(
                    "Environment scan completed. HadError={HadError}, ItemCount={ItemCount}, ElapsedMs={ElapsedMs}",
                    summary.HadError,
                    summary.Items.Count,
                    stopwatch.ElapsedMilliseconds);
                if (summary.HadError)
                {
                    _logger.LogWarning("Environment scan reported errors.");
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Environment scan failed.");
                _uiInteractionService.ShowErrorToast("环境检查失败", ex.Message);
            }
            finally
            {
                _shellStateService.StatusText = "就绪";
            }
        }

        private static void ResetScanPresentation(EnvironmentScanPresentation vm)
        {
            vm.CpuPrimaryRow.ApplyResult(
                "—",
                string.Empty,
                false,
                false,
                EnvironmentScan.ScanResultLevel.Success,
                string.Empty);
            vm.GpuAdapterRows.Clear();
            vm.GpuAdapterRows.Add(CreateDashPlaceholderRow());
            vm.NvidiaSkipNoticeRow.Hide();
            vm.NvidiaDetailVisible = true;
            vm.NvidiaNvcuda.Hide();
            vm.NvidiaNvcuvid.Hide();
            vm.NvidiaEncodeApi.Hide();
            vm.DirectXRuntimeFaultRow.Hide();
            vm.DirectXD3d9.Hide();
            vm.DirectXD3Dx43.Hide();
            vm.MediaPackRuntimeFaultRow.Hide();
            vm.MediaPackMf.Hide();
            vm.MediaPackMfplat.Hide();
            vm.MediaPackWmvCore.Hide();
            vm.Vc2010X86RuntimeFaultRow.Hide();
            vm.Vc2010X86Msvcr.Hide();
            vm.Vc2010X86Msvcp.Hide();
            vm.Vc2010X64RuntimeFaultRow.Hide();
            vm.Vc2010X64Msvcr.Hide();
            vm.Vc2010X64Msvcp.Hide();
            vm.ScanRootAlerts.Clear();
            vm.HasScanRootAlerts = false;
            vm.ScanUiReady = false;
        }

        private static void PopulateScanSlots(EnvironmentScanPresentation vm, EnvironmentScan.ScanSummary summary)
        {
            PartitionSummaryItems(summary.Items, out var roots, out var grouped);

            ApplyCpuSlots(vm, grouped);
            ApplyGpuSlots(vm, grouped);

            PopulateDllGroup(vm, grouped, new DllGroupSpec
            {
                GroupKey = "NVIDIA API",
                FaultToken = "系统库检测",
                FaultTitle = "兼容层",
                NotFoundTitle = "NVIDIA API",
                FaultRow = vm.NvidiaSkipNoticeRow,
                Outcomes = [("nvcuda.dll", vm.NvidiaNvcuda), ("nvcuvid.dll", vm.NvidiaNvcuvid), ("nvEncodeAPI64.dll", vm.NvidiaEncodeApi)],
                OnFault = v => v.NvidiaDetailVisible = false
            });
            PopulateDllGroup(vm, grouped, new DllGroupSpec
            {
                GroupKey = "DirectX9",
                FaultToken = "运行时检测",
                FaultTitle = "DirectX 9 运行时",
                NotFoundTitle = "DirectX 9",
                FaultRow = vm.DirectXRuntimeFaultRow,
                Outcomes = [("d3d9.dll", vm.DirectXD3d9), ("d3dx9_43.dll", vm.DirectXD3Dx43)]
            });
            PopulateDllGroup(vm, grouped, new DllGroupSpec
            {
                GroupKey = "媒体功能包",
                FaultToken = "运行时检测",
                FaultTitle = "媒体功能包 运行时",
                NotFoundTitle = "系统媒体功能包",
                FaultRow = vm.MediaPackRuntimeFaultRow,
                Outcomes = [("MF.dll", vm.MediaPackMf), ("MFPLAT.dll", vm.MediaPackMfplat), ("WMVCore.dll", vm.MediaPackWmvCore)]
            });
            PopulateDllGroup(vm, grouped, new DllGroupSpec
            {
                GroupKey = "VC++2010 x86",
                FaultToken = "运行时检测",
                FaultTitle = "VC++2010 x86 运行时",
                NotFoundTitle = "VC++2010 x86 运行时",
                FaultRow = vm.Vc2010X86RuntimeFaultRow,
                Outcomes = [("msvcr100.dll", vm.Vc2010X86Msvcr), ("msvcp100.dll", vm.Vc2010X86Msvcp)]
            });
            PopulateDllGroup(vm, grouped, new DllGroupSpec
            {
                GroupKey = "VC++2010 x64",
                FaultToken = "运行时检测",
                FaultTitle = "VC++2010 x64 运行时",
                NotFoundTitle = "VC++2010 x64 运行时",
                FaultRow = vm.Vc2010X64RuntimeFaultRow,
                Outcomes = [("msvcr100.dll", vm.Vc2010X64Msvcr), ("msvcp100.dll", vm.Vc2010X64Msvcp)]
            });

            vm.ScanRootAlerts.Clear();
            foreach (var root in roots)
            {
                string line = string.IsNullOrWhiteSpace(root.Detail)
                    ? root.Item
                    : $"{root.Item} — {root.Detail.Trim()}";
                vm.ScanRootAlerts.Add(line);
            }

            vm.HasScanRootAlerts = vm.ScanRootAlerts.Count > 0;
            vm.ScanUiReady = true;
            vm.NotifyScanPresentationChanged();
        }

        private sealed class DllGroupSpec
        {
            public string GroupKey { get; init; }
            public string FaultToken { get; init; }
            public string FaultTitle { get; init; }
            public string NotFoundTitle { get; init; }
            public EnvironmentScanDisplayRow FaultRow { get; init; }
            public (string FileToken, EnvironmentScanLineOutcome Outcome)[] Outcomes { get; init; }
            public Action<EnvironmentScanPresentation> OnFault { get; init; }
        }

        private static void PopulateDllGroup(
            EnvironmentScanPresentation vm,
            Dictionary<string, List<EnvironmentScan.ScanResultItem>> grouped,
            DllGroupSpec spec)
        {
            if (!grouped.TryGetValue(spec.GroupKey, out var bucket) || bucket.Count == 0)
            {
                spec.FaultRow.ApplyResult(
                    spec.NotFoundTitle,
                    "未有检测输出",
                    true,
                    true,
                    EnvironmentScan.ScanResultLevel.Warning,
                    BadgeText(EnvironmentScan.ScanResultLevel.Warning));
                foreach (var (token, outcome) in spec.Outcomes)
                    MapLibraryOutcome(EmptyScanBucket, token, outcome);
                return;
            }

            var faultItem = bucket.Find(v =>
                v.Item.IndexOf(spec.FaultToken, StringComparison.OrdinalIgnoreCase) >= 0);

            if (faultItem != null)
            {
                bool detailVisible = !string.IsNullOrWhiteSpace(faultItem.Detail);
                spec.FaultRow.ApplyResult(
                    spec.FaultTitle,
                    detailVisible ? faultItem.Detail.Trim() : "检测过程中发生异常。",
                    detailVisible,
                    true,
                    faultItem.Level,
                    BadgeText(faultItem.Level));
                foreach (var (token, outcome) in spec.Outcomes)
                    MapLibraryOutcome(EmptyScanBucket, token, outcome);
                spec.OnFault?.Invoke(vm);
                return;
            }

            foreach (var (token, outcome) in spec.Outcomes)
                MapLibraryOutcome(bucket, token, outcome);
        }

        private static void PartitionSummaryItems(
            IReadOnlyList<EnvironmentScan.ScanResultItem> items,
            out List<EnvironmentScan.ScanResultItem> roots,
            out Dictionary<string, List<EnvironmentScan.ScanResultItem>> grouped)
        {
            roots = new List<EnvironmentScan.ScanResultItem>();
            grouped = new Dictionary<string, List<EnvironmentScan.ScanResultItem>>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                int slashIndex = item.Item.IndexOf('/');
                if (slashIndex <= 0 || slashIndex >= item.Item.Length - 1)
                {
                    roots.Add(item);
                    continue;
                }

                string groupName = item.Item[..slashIndex].Trim();
                if (!grouped.TryGetValue(groupName, out var bucket))
                {
                    bucket = new List<EnvironmentScan.ScanResultItem>();
                    grouped[groupName] = bucket;
                }

                bucket.Add(item);
            }
        }

        private static string ChildSegment(string itemKey)
        {
            int slash = itemKey.IndexOf('/');
            return slash >= 0 && slash < itemKey.Length - 1
                ? itemKey[(slash + 1)..].Trim()
                : itemKey.Trim();
        }

        private static string BadgeText(EnvironmentScan.ScanResultLevel level)
        {
            return level switch
            {
                EnvironmentScan.ScanResultLevel.Success => "通过",
                EnvironmentScan.ScanResultLevel.Warning => "警告",
                _ => "失败"
            };
        }

        private static EnvironmentScan.ScanResultLevel AggregateWorst(IEnumerable<EnvironmentScan.ScanResultItem> seq)
        {
            EnvironmentScan.ScanResultLevel lvl = EnvironmentScan.ScanResultLevel.Success;

            foreach (var item in seq)
            {
                lvl = WorstTwo(lvl, item.Level);
            }

            return lvl;
        }

        private static EnvironmentScan.ScanResultLevel WorstTwo(EnvironmentScan.ScanResultLevel a, EnvironmentScan.ScanResultLevel b)
        {
            static int Rank(EnvironmentScan.ScanResultLevel level) =>
                level switch
                {
                    EnvironmentScan.ScanResultLevel.Error => 2,
                    EnvironmentScan.ScanResultLevel.Warning => 1,
                    _ => 0,
                };

            return Rank(a) >= Rank(b) ? a : b;
        }

        private static void ApplyCpuSlots(EnvironmentScanPresentation vm, Dictionary<string, List<EnvironmentScan.ScanResultItem>> grouped)
        {
            if (!grouped.TryGetValue("CPU", out List<EnvironmentScan.ScanResultItem> bucket) || bucket.Count == 0)
            {
                vm.CpuPrimaryRow.ApplyResult(
                    "未产生检测结果",
                    string.Empty,
                    false,
                    false,
                    EnvironmentScan.ScanResultLevel.Warning,
                    string.Empty);
                return;
            }

            EnvironmentScan.ScanResultLevel groupLevel = AggregateWorst(bucket);
            EnvironmentScan.ScanResultItem anchor = bucket[0];
            string primary = !string.IsNullOrWhiteSpace(anchor.Detail)
                ? anchor.Detail.Trim()
                : ChildSegment(anchor.Item);
            vm.CpuPrimaryRow.ApplyResult(
                primary,
                string.Empty,
                false,
                false,
                groupLevel,
                string.Empty);
        }

        private static void ApplyGpuSlots(EnvironmentScanPresentation vm, Dictionary<string, List<EnvironmentScan.ScanResultItem>> grouped)
        {
            vm.GpuAdapterRows.Clear();

            if (!grouped.TryGetValue("GPU", out List<EnvironmentScan.ScanResultItem> bucket) || bucket.Count == 0)
            {
                var row = new EnvironmentScanDisplayRow();
                row.ApplyResult(
                    "未发现可用的显示适配器",
                    string.Empty,
                    false,
                    false,
                    EnvironmentScan.ScanResultLevel.Warning,
                    string.Empty);
                vm.GpuAdapterRows.Add(row);
                return;
            }

            foreach (EnvironmentScan.ScanResultItem entry in bucket)
            {
                string slug = ChildSegment(entry.Item);
                EnvironmentScan.ScanResultLevel rowLevel = entry.Level;

                var row = new EnvironmentScanDisplayRow();
                row.ApplyResult(
                    slug,
                    string.Empty,
                    false,
                    false,
                    rowLevel,
                    string.Empty);
                vm.GpuAdapterRows.Add(row);
            }
        }

        private static EnvironmentScanDisplayRow CreateDashPlaceholderRow()
        {
            var row = new EnvironmentScanDisplayRow();
            row.ApplyResult(
                "—",
                string.Empty,
                false,
                false,
                EnvironmentScan.ScanResultLevel.Success,
                string.Empty);
            return row;
        }

        private static void MapLibraryOutcome(
            List<EnvironmentScan.ScanResultItem> bucket,
            string fileToken,
            EnvironmentScanLineOutcome outcome)
        {
            EnvironmentScan.ScanResultItem hit = bucket.Find(value =>
                value.Item.IndexOf(fileToken, StringComparison.OrdinalIgnoreCase) >= 0);

            if (hit == null)
            {
                outcome.Apply(EnvironmentScan.ScanResultLevel.Error, BadgeText(EnvironmentScan.ScanResultLevel.Error));
            }
            else
            {
                outcome.Apply(hit.Level, BadgeText(hit.Level));
            }
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
