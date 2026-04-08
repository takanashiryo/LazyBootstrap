using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LazyBootstrap.Services.Display
{
    internal interface IDisplayConfigurationService
    {
        DisplayDiscoveryResult GetDisplays();

        DisplayModeQueryResult GetSupportedModes(string deviceName);

        DisplayStateQueryResult GetCurrentState(string deviceName);

        DisplayConfigurationResult ApplyDisplaySettings(string deviceName, int angle, int width, int height, int refreshRate);

        DisplayConfigurationResult RestoreDisplaySettings(DisplayState state);

        int OrientationToAngle(int orientation);
    }

    internal sealed class DisplayDiscoveryResult
    {
        public DisplayDiscoveryResult(IReadOnlyList<DisplayInfo> displays, string errorMessage = "")
        {
            Displays = displays ?? Array.Empty<DisplayInfo>();
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public IReadOnlyList<DisplayInfo> Displays { get; }

        public string ErrorMessage { get; }

        public bool Succeeded => string.IsNullOrWhiteSpace(ErrorMessage);
    }

    internal sealed class DisplayModeQueryResult
    {
        public DisplayModeQueryResult(IReadOnlyList<DisplayMode> modes, string errorMessage = "")
        {
            Modes = modes ?? Array.Empty<DisplayMode>();
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public IReadOnlyList<DisplayMode> Modes { get; }

        public string ErrorMessage { get; }

        public bool Succeeded => string.IsNullOrWhiteSpace(ErrorMessage);
    }

    internal sealed class DisplayStateQueryResult
    {
        public DisplayStateQueryResult(DisplayState state, string errorMessage = "")
        {
            State = state;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public DisplayState State { get; }

        public string ErrorMessage { get; }

        public bool Succeeded => State != null && string.IsNullOrWhiteSpace(ErrorMessage);
    }

    internal sealed class DisplayConfigurationResult
    {
        public DisplayConfigurationResult(bool succeeded, string errorMessage = "")
        {
            Succeeded = succeeded;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool Succeeded { get; }

        public string ErrorMessage { get; }

        public static DisplayConfigurationResult Success()
        {
            return new DisplayConfigurationResult(true);
        }

        public static DisplayConfigurationResult Failure(string errorMessage)
        {
            return new DisplayConfigurationResult(false, errorMessage);
        }
    }

    public sealed class DisplayInfo
    {
        public string DeviceName { get; init; } = string.Empty;

        public string FriendlyName { get; init; } = string.Empty;

        public bool IsPrimary { get; init; }
    }

    internal sealed class DisplayMode
    {
        public int Width { get; init; }

        public int Height { get; init; }

        public int RefreshRate { get; init; }
    }

    public sealed class DisplayState
    {
        public string DeviceName { get; init; } = string.Empty;

        public int Orientation { get; init; }

        public int Width { get; init; }

        public int Height { get; init; }

        public int RefreshRate { get; init; }
    }

    internal class WindowsDisplayConfigurationService : IDisplayConfigurationService
    {
        private const int DisplayDeviceActive = 0x1;
        private const int DisplayDevicePrimaryDevice = 0x4;
        private const int DisplayDeviceMirroringDriver = 0x8;

        private const int DmdoDefault = 0;
        private const int Dmdo90 = 1;
        private const int Dmdo180 = 2;
        private const int Dmdo270 = 3;

        private const int EnumCurrentSettings = -1;

        private const int CdsUpdateRegistry = 0x01;
        private const int CdsTest = 0x02;

        private const int DmDisplayOrientation = 0x00000080;
        private const int DmPelsWidth = 0x00080000;
        private const int DmPelsHeight = 0x00100000;
        private const int DmDisplayFrequency = 0x00400000;

        private const int DispChangeSuccessful = 0;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        protected internal struct DisplayDevice
        {
            public int Cb;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;

            public int StateFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceId;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        protected internal struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;

            public short SpecVersion;
            public short DriverVersion;
            public short Size;
            public short DriverExtra;
            public int Fields;
            public int PositionX;
            public int PositionY;
            public int DisplayOrientation;
            public int DisplayFixedOutput;
            public short Color;
            public short Duplex;
            public short YResolution;
            public short TTOption;
            public short Collate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string FormName;

            public short LogPixels;
            public int BitsPerPel;
            public int PelsWidth;
            public int PelsHeight;
            public int DisplayFlags;
            public int DisplayFrequency;
            public int IcmMethod;
            public int IcmIntent;
            public int MediaType;
            public int DitherType;
            public int Reserved1;
            public int Reserved2;
            public int PanningWidth;
            public int PanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DisplayDevice lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DevMode lpDevMode, IntPtr hwnd, int dwFlags, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DevMode lpDevMode);

        public DisplayDiscoveryResult GetDisplays()
        {
            var result = new List<DisplayInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var wmiMonitorNames = OperatingSystem.IsWindows()
                ? LoadWmiMonitorFriendlyNames()
                : Array.Empty<WmiMonitorFriendlyName>();

            try
            {
                uint adapterIndex = 0;
                while (true)
                {
                    var adapter = CreateDisplayDevice();
                    if (!TryEnumDisplayDevices(null, adapterIndex, ref adapter))
                    {
                        break;
                    }

                    bool adapterActive = (adapter.StateFlags & DisplayDeviceActive) != 0;
                    bool adapterMirroring = (adapter.StateFlags & DisplayDeviceMirroringDriver) != 0;
                    if (!adapterActive || adapterMirroring || string.IsNullOrWhiteSpace(adapter.DeviceName))
                    {
                        adapterIndex++;
                        continue;
                    }

                    var activeMonitors = EnumerateActiveMonitors(adapter.DeviceName);
                    string friendly = ResolveFriendlyName(adapter, activeMonitors, wmiMonitorNames);

                    if (seen.Add(adapter.DeviceName))
                    {
                        result.Add(new DisplayInfo
                        {
                            DeviceName = adapter.DeviceName,
                            FriendlyName = friendly,
                            IsPrimary = (adapter.StateFlags & DisplayDevicePrimaryDevice) != 0
                        });
                    }

                    adapterIndex++;
                }

                return new DisplayDiscoveryResult(result);
            }
            catch (Exception ex)
            {
                return new DisplayDiscoveryResult(result, $"枚举显示器失败: {ex.Message}");
            }
        }

        public DisplayModeQueryResult GetSupportedModes(string deviceName)
        {
            var modes = new List<DisplayMode>();
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return new DisplayModeQueryResult(modes, "未提供显示器设备名称。");
            }

            try
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool enumeratedAnyMode = false;

                for (int index = 0; index < 512; index++)
                {
                    var current = CreateDevMode();
                    bool success = TryEnumDisplaySettings(deviceName, index, ref current);
                    if (!success)
                    {
                        if (index == 0)
                        {
                            return new DisplayModeQueryResult(modes, $"无法读取 {deviceName} 的显示模式。");
                        }

                        break;
                    }

                    enumeratedAnyMode = true;
                    if (current.PelsWidth <= 0 || current.PelsHeight <= 0 || current.DisplayFrequency <= 0)
                    {
                        continue;
                    }

                    string key = $"{current.PelsWidth}x{current.PelsHeight}@{current.DisplayFrequency}";
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    modes.Add(new DisplayMode
                    {
                        Width = current.PelsWidth,
                        Height = current.PelsHeight,
                        RefreshRate = current.DisplayFrequency
                    });
                }

                var orderedModes = modes
                    .OrderBy(mode => mode.Width * mode.Height)
                    .ThenBy(mode => mode.RefreshRate)
                    .ToList();

                return enumeratedAnyMode
                    ? new DisplayModeQueryResult(orderedModes)
                    : new DisplayModeQueryResult(orderedModes, $"未读取到 {deviceName} 的任何显示模式。");
            }
            catch (Exception ex)
            {
                return new DisplayModeQueryResult(modes, $"读取 {deviceName} 显示模式失败: {ex.Message}");
            }
        }

        public DisplayStateQueryResult GetCurrentState(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return new DisplayStateQueryResult(null, "未提供显示器设备名称。");
            }

            try
            {
                var devMode = CreateDevMode();
                if (!TryEnumDisplaySettings(deviceName, EnumCurrentSettings, ref devMode))
                {
                    return new DisplayStateQueryResult(null, $"无法读取 {deviceName} 的当前显示状态。");
                }

                return new DisplayStateQueryResult(new DisplayState
                {
                    DeviceName = deviceName,
                    Orientation = devMode.DisplayOrientation,
                    Width = devMode.PelsWidth,
                    Height = devMode.PelsHeight,
                    RefreshRate = devMode.DisplayFrequency
                });
            }
            catch (Exception ex)
            {
                return new DisplayStateQueryResult(null, $"读取 {deviceName} 当前显示状态失败: {ex.Message}");
            }
        }

        public int OrientationToAngle(int orientation)
        {
            return orientation switch
            {
                Dmdo90 => 90,
                Dmdo180 => 180,
                Dmdo270 => 270,
                _ => 0
            };
        }

        public DisplayConfigurationResult ApplyDisplaySettings(string deviceName, int angle, int width, int height, int refreshRate)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return DisplayConfigurationResult.Failure("未提供显示器设备名称。");
            }

            if (width <= 0 || height <= 0 || refreshRate <= 0)
            {
                return DisplayConfigurationResult.Failure("显示器分辨率或刷新率无效。");
            }

            try
            {
                int orientation = AngleToOrientation(angle);
                if (orientation < 0)
                {
                    return DisplayConfigurationResult.Failure($"不支持的旋转角度: {angle}。");
                }

                var devMode = CreateDevMode();
                if (!TryEnumDisplaySettings(deviceName, EnumCurrentSettings, ref devMode))
                {
                    return DisplayConfigurationResult.Failure($"无法读取 {deviceName} 的当前显示状态。");
                }

                devMode.DisplayOrientation = orientation;
                devMode.PelsWidth = width;
                devMode.PelsHeight = height;
                devMode.DisplayFrequency = refreshRate;
                devMode.Fields = DmDisplayOrientation | DmPelsWidth | DmPelsHeight | DmDisplayFrequency;

                int testResult = TryChangeDisplaySettings(deviceName, ref devMode, CdsTest);
                if (testResult != DispChangeSuccessful)
                {
                    return DisplayConfigurationResult.Failure($"显示器设置校验失败，返回码: {testResult}。");
                }

                int result = TryChangeDisplaySettings(deviceName, ref devMode, CdsUpdateRegistry);
                return result == DispChangeSuccessful
                    ? DisplayConfigurationResult.Success()
                    : DisplayConfigurationResult.Failure($"应用显示器设置失败，返回码: {result}。");
            }
            catch (Exception ex)
            {
                return DisplayConfigurationResult.Failure($"应用显示器设置失败: {ex.Message}");
            }
        }

        public DisplayConfigurationResult RestoreDisplaySettings(DisplayState state)
        {
            if (state == null)
            {
                return DisplayConfigurationResult.Failure("未提供需要恢复的显示器状态。");
            }

            if (string.IsNullOrWhiteSpace(state.DeviceName))
            {
                return DisplayConfigurationResult.Failure("显示器状态缺少设备名称。");
            }

            if (state.Width <= 0 || state.Height <= 0 || state.RefreshRate <= 0)
            {
                return DisplayConfigurationResult.Failure("显示器状态包含无效的分辨率或刷新率。");
            }

            try
            {
                var devMode = CreateDevMode();
                if (!TryEnumDisplaySettings(state.DeviceName, EnumCurrentSettings, ref devMode))
                {
                    return DisplayConfigurationResult.Failure($"无法读取 {state.DeviceName} 的当前显示状态。");
                }

                devMode.DisplayOrientation = state.Orientation;
                devMode.PelsWidth = state.Width;
                devMode.PelsHeight = state.Height;
                devMode.DisplayFrequency = state.RefreshRate;
                devMode.Fields = DmDisplayOrientation | DmPelsWidth | DmPelsHeight | DmDisplayFrequency;

                int testResult = TryChangeDisplaySettings(state.DeviceName, ref devMode, CdsTest);
                if (testResult != DispChangeSuccessful)
                {
                    return DisplayConfigurationResult.Failure($"显示器还原校验失败，返回码: {testResult}。");
                }

                int result = TryChangeDisplaySettings(state.DeviceName, ref devMode, CdsUpdateRegistry);
                return result == DispChangeSuccessful
                    ? DisplayConfigurationResult.Success()
                    : DisplayConfigurationResult.Failure($"还原显示器设置失败，返回码: {result}。");
            }
            catch (Exception ex)
            {
                return DisplayConfigurationResult.Failure($"还原显示器设置失败: {ex.Message}");
            }
        }

        protected virtual bool TryEnumDisplayDevices(string deviceName, uint deviceIndex, ref DisplayDevice device)
        {
            return EnumDisplayDevices(deviceName, deviceIndex, ref device, 0);
        }

        protected virtual bool TryEnumDisplaySettings(string deviceName, int modeIndex, ref DevMode devMode)
        {
            return EnumDisplaySettings(deviceName, modeIndex, ref devMode);
        }

        protected virtual int TryChangeDisplaySettings(string deviceName, ref DevMode devMode, int flags)
        {
            return ChangeDisplaySettingsEx(deviceName, ref devMode, IntPtr.Zero, flags, IntPtr.Zero);
        }

        private List<DisplayDevice> EnumerateActiveMonitors(string adapterDeviceName)
        {
            var monitors = new List<DisplayDevice>();
            uint monitorIndex = 0;

            while (true)
            {
                var monitor = CreateDisplayDevice();
                if (!TryEnumDisplayDevices(adapterDeviceName, monitorIndex, ref monitor))
                {
                    break;
                }

                if ((monitor.StateFlags & DisplayDeviceActive) != 0)
                {
                    monitors.Add(monitor);
                }

                monitorIndex++;
            }

            return monitors;
        }

        private static string ResolveFriendlyName(DisplayDevice adapter, IReadOnlyList<DisplayDevice> monitors, IReadOnlyList<WmiMonitorFriendlyName> wmiMonitorNames)
        {
            foreach (var monitor in monitors)
            {
                string friendlyName = ResolveFriendlyNameFromWmi(monitor, wmiMonitorNames);
                if (!string.IsNullOrWhiteSpace(friendlyName))
                {
                    return friendlyName;
                }
            }

            foreach (var monitor in monitors)
            {
                string monitorName = monitor.DeviceString?.Trim() ?? string.Empty;
                if (!IsGenericMonitorName(monitorName))
                {
                    return monitorName;
                }
            }

            string adapterName = adapter.DeviceString?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(adapterName))
            {
                return adapterName;
            }

            return adapter.DeviceName?.Trim() ?? string.Empty;
        }

        private static string ResolveFriendlyNameFromWmi(DisplayDevice monitor, IReadOnlyList<WmiMonitorFriendlyName> wmiMonitorNames)
        {
            if (wmiMonitorNames == null || wmiMonitorNames.Count == 0)
            {
                return string.Empty;
            }

            foreach (var candidate in BuildMonitorIdentityCandidates(monitor))
            {
                var exactMatch = wmiMonitorNames.FirstOrDefault(entry =>
                    string.Equals(entry.InstanceName, candidate, StringComparison.OrdinalIgnoreCase));
                if (exactMatch != null)
                {
                    return exactMatch.FriendlyName;
                }

                var fuzzyMatch = wmiMonitorNames.FirstOrDefault(entry =>
                    entry.InstanceName.Contains(candidate, StringComparison.OrdinalIgnoreCase)
                    || candidate.Contains(entry.InstanceName, StringComparison.OrdinalIgnoreCase));
                if (fuzzyMatch != null)
                {
                    return fuzzyMatch.FriendlyName;
                }
            }

            foreach (var hardwareKey in BuildMonitorHardwareKeys(monitor))
            {
                var hardwareMatch = wmiMonitorNames.FirstOrDefault(entry =>
                    string.Equals(entry.HardwareKey, hardwareKey, StringComparison.OrdinalIgnoreCase));
                if (hardwareMatch != null)
                {
                    return hardwareMatch.FriendlyName;
                }
            }

            return string.Empty;
        }

        [SupportedOSPlatform("windows")]
        private static IReadOnlyList<WmiMonitorFriendlyName> LoadWmiMonitorFriendlyNames()
        {
            var monitors = new List<WmiMonitorFriendlyName>();

            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, UserFriendlyName FROM WmiMonitorID");
                using var results = searcher.Get();

                foreach (ManagementObject monitor in results)
                {
                    string instanceName = NormalizeMonitorIdentity(monitor["InstanceName"]?.ToString());
                    string friendlyName = DecodeMonitorFriendlyName(monitor["UserFriendlyName"] as ushort[]);
                    if (string.IsNullOrWhiteSpace(instanceName) || string.IsNullOrWhiteSpace(friendlyName))
                    {
                        continue;
                    }

                    monitors.Add(new WmiMonitorFriendlyName(instanceName, ExtractMonitorHardwareKey(instanceName), friendlyName));
                }
            }
            catch
            {
                return Array.Empty<WmiMonitorFriendlyName>();
            }

            return monitors;
        }

        private static string DecodeMonitorFriendlyName(ushort[] rawName)
        {
            if (rawName == null || rawName.Length == 0)
            {
                return string.Empty;
            }

            var characters = rawName
                .TakeWhile(value => value != 0)
                .Select(value => (char)value)
                .ToArray();

            return new string(characters).Trim();
        }

        private static IEnumerable<string> BuildMonitorIdentityCandidates(DisplayDevice monitor)
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddMonitorIdentityCandidate(candidates, monitor.DeviceId);
            AddMonitorIdentityCandidate(candidates, ConvertMonitorDeviceIdToDisplayIdentity(monitor.DeviceId));
            AddMonitorIdentityCandidate(candidates, ExtractEnumIdentityFromRegistryPath(monitor.DeviceKey));
            AddMonitorIdentityCandidate(candidates, monitor.DeviceName);
            return candidates;
        }

        private static IEnumerable<string> BuildMonitorHardwareKeys(DisplayDevice monitor)
        {
            var hardwareKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in BuildMonitorIdentityCandidates(monitor))
            {
                string hardwareKey = ExtractMonitorHardwareKey(candidate);
                if (!string.IsNullOrWhiteSpace(hardwareKey))
                {
                    hardwareKeys.Add(hardwareKey);
                }
            }

            return hardwareKeys;
        }

        private static void AddMonitorIdentityCandidate(ISet<string> target, string value)
        {
            string normalized = NormalizeMonitorIdentity(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                target.Add(normalized);
            }
        }

        private static string NormalizeMonitorIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim().Replace('/', '\\').Trim('\\').ToUpperInvariant();
            if (normalized.StartsWith(@"MONITOR\", StringComparison.OrdinalIgnoreCase))
            {
                normalized = $@"DISPLAY\{normalized[8..]}";
            }

            return normalized;
        }

        private static string ConvertMonitorDeviceIdToDisplayIdentity(string deviceId)
        {
            string normalized = NormalizeMonitorIdentity(deviceId);
            if (normalized.StartsWith(@"DISPLAY\", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            if (normalized.StartsWith(@"MONITOR\", StringComparison.OrdinalIgnoreCase))
            {
                return $@"DISPLAY\{normalized[8..]}";
            }

            return normalized;
        }

        private static string ExtractEnumIdentityFromRegistryPath(string deviceKey)
        {
            if (string.IsNullOrWhiteSpace(deviceKey))
            {
                return string.Empty;
            }

            const string enumMarker = @"ENUM\";
            int enumIndex = deviceKey.IndexOf(enumMarker, StringComparison.OrdinalIgnoreCase);
            if (enumIndex < 0)
            {
                return string.Empty;
            }

            return NormalizeMonitorIdentity(deviceKey[(enumIndex + enumMarker.Length)..]);
        }

        private static string ExtractMonitorHardwareKey(string value)
        {
            string normalized = NormalizeMonitorIdentity(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            var parts = normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return string.Empty;
            }

            return parts[1];
        }

        private static bool IsGenericMonitorName(string monitorName)
        {
            return string.IsNullOrWhiteSpace(monitorName)
                || string.Equals(monitorName.Trim(), "Generic PnP Monitor", StringComparison.OrdinalIgnoreCase);
        }

        private static DisplayDevice CreateDisplayDevice()
        {
            return new DisplayDevice
            {
                Cb = Marshal.SizeOf<DisplayDevice>()
            };
        }

        private static DevMode CreateDevMode()
        {
            return new DevMode
            {
                Size = (short)Marshal.SizeOf<DevMode>()
            };
        }

        private static int AngleToOrientation(int angle)
        {
            int normalizedAngle = ((angle % 360) + 360) % 360;
            return normalizedAngle switch
            {
                0 => DmdoDefault,
                90 => Dmdo90,
                180 => Dmdo180,
                270 => Dmdo270,
                _ => -1
            };
        }

        private sealed record WmiMonitorFriendlyName(string InstanceName, string HardwareKey, string FriendlyName);
    }
}
