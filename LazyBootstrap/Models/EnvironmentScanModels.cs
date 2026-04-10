using System.Collections.ObjectModel;

namespace LazyBootstrap.Models
{
    public sealed class EnvironmentScanItem
    {
        public string Label { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public string StatusText { get; set; } = string.Empty;

        public bool ShowStatus { get; set; }

        public bool IsVirtualMachine { get; set; }

        public EnvironmentScan.ScanResultLevel Level { get; set; }
    }

    public sealed class EnvironmentScanGroup
    {
        public string Title { get; set; } = string.Empty;

        public bool ShowStatus { get; set; }

        public EnvironmentScan.ScanResultLevel Level { get; set; }

        public ObservableCollection<EnvironmentScanItem> Items { get; } = new ObservableCollection<EnvironmentScanItem>();
    }
}
