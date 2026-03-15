using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace LazyBootstrap
{
    public static class DisplayConfigure
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        public sealed class DisplayInfo
        {
            public string DeviceName { get; set; }
            public string FriendlyName { get; set; }
            public bool IsPrimary { get; set; }
        }

        public sealed class DisplayMode
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int RefreshRate { get; set; }
        }

        public sealed class DisplayState
        {
            public string DeviceName { get; set; }
            public int Orientation { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int RefreshRate { get; set; }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, int dwflags, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        private const int DISPLAY_DEVICE_ACTIVE = 0x1;
        private const int DISPLAY_DEVICE_PRIMARY_DEVICE = 0x4;
        private const int DISPLAY_DEVICE_MIRRORING_DRIVER = 0x8;

        public const int DMDO_DEFAULT = 0;
        public const int DMDO_90 = 1;
        public const int DMDO_180 = 2;
        public const int DMDO_270 = 3;

        public const int ENUM_CURRENT_SETTINGS = -1;

        public const int CDS_UPDATEREGISTRY = 0x01;
        public const int CDS_TEST = 0x02;

        public const int DM_DISPLAYORIENTATION = 0x00000080;
        public const int DM_PELSWIDTH = 0x00080000;
        public const int DM_PELSHEIGHT = 0x00100000;
        public const int DM_DISPLAYFREQUENCY = 0x00400000;

        public const int DISP_CHANGE_SUCCESSFUL = 0;

        public static List<DisplayInfo> GetDisplays()
        {
            var result = new List<DisplayInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                uint adapterIndex = 0;
                while (true)
                {
                    var adapter = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                    if (!EnumDisplayDevices(null, adapterIndex, ref adapter, 0))
                    {
                        break;
                    }

                    bool adapterActive = (adapter.StateFlags & DISPLAY_DEVICE_ACTIVE) != 0;
                    bool adapterMirroring = (adapter.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER) != 0;
                    if (!adapterActive || adapterMirroring || string.IsNullOrWhiteSpace(adapter.DeviceName))
                    {
                        adapterIndex++;
                        continue;
                    }

                    string friendly = adapter.DeviceString?.Trim() ?? adapter.DeviceName.Trim();

                    uint monitorIndex = 0;
                    while (true)
                    {
                        var monitor = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                        if (!EnumDisplayDevices(adapter.DeviceName, monitorIndex, ref monitor, 0))
                        {
                            break;
                        }

                        bool monitorActive = (monitor.StateFlags & DISPLAY_DEVICE_ACTIVE) != 0;
                        if (monitorActive && !string.IsNullOrWhiteSpace(monitor.DeviceString))
                        {
                            friendly = monitor.DeviceString.Trim();
                            break;
                        }

                        monitorIndex++;
                    }

                    if (seen.Add(adapter.DeviceName))
                    {
                        result.Add(new DisplayInfo
                        {
                            DeviceName = adapter.DeviceName,
                            FriendlyName = friendly,
                            IsPrimary = (adapter.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0
                        });
                    }

                    adapterIndex++;
                }
            }
            catch
            {
            }

            return result;
        }

        public static List<DisplayMode> GetSupportedModes(string deviceName)
        {
            var modes = new List<DisplayMode>();
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return modes;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf<DEVMODE>();

            for (int i = 0; i < 512; i++)
            {
                var current = devMode;
                if (!EnumDisplaySettings(deviceName, i, ref current))
                {
                    break;
                }

                if (current.dmPelsWidth <= 0 || current.dmPelsHeight <= 0 || current.dmDisplayFrequency <= 0)
                {
                    continue;
                }

                string key = $"{current.dmPelsWidth}x{current.dmPelsHeight}@{current.dmDisplayFrequency}";
                if (!seen.Add(key))
                {
                    continue;
                }

                modes.Add(new DisplayMode
                {
                    Width = current.dmPelsWidth,
                    Height = current.dmPelsHeight,
                    RefreshRate = current.dmDisplayFrequency
                });
            }

            return modes
                .OrderBy(m => m.Width * m.Height)
                .ThenBy(m => m.RefreshRate)
                .ToList();
        }

        public static bool TryGetCurrentState(string deviceName, out DisplayState state)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return false;
            }

            try
            {
                var devMode = new DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf<DEVMODE>();
                if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref devMode))
                {
                    return false;
                }

                state = new DisplayState
                {
                    DeviceName = deviceName,
                    Orientation = devMode.dmDisplayOrientation,
                    Width = devMode.dmPelsWidth,
                    Height = devMode.dmPelsHeight,
                    RefreshRate = devMode.dmDisplayFrequency
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static int OrientationToAngle(int orientation)
        {
            switch (orientation)
            {
                case DMDO_90:
                    return 90;
                case DMDO_180:
                    return 180;
                case DMDO_270:
                    return 270;
                default:
                    return 0;
            }
        }

        public static bool ApplyDisplaySettings(string deviceName, int angle, int width, int height, int refreshRate)
        {
            try
            {
                int orientation = AngleToOrientation(angle);
                if (orientation < 0)
                {
                    return false;
                }

                var devMode = new DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf<DEVMODE>();
                if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref devMode))
                {
                    return false;
                }

                devMode.dmDisplayOrientation = orientation;
                devMode.dmPelsWidth = width;
                devMode.dmPelsHeight = height;
                devMode.dmDisplayFrequency = refreshRate;
                devMode.dmFields = DM_DISPLAYORIENTATION | DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;

                int testResult = ChangeDisplaySettingsEx(deviceName, ref devMode, IntPtr.Zero, CDS_TEST, IntPtr.Zero);
                if (testResult != DISP_CHANGE_SUCCESSFUL)
                {
                    return false;
                }

                int result = ChangeDisplaySettingsEx(deviceName, ref devMode, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
                return result == DISP_CHANGE_SUCCESSFUL;
            }
            catch
            {
                return false;
            }
        }

        public static bool RestoreDisplaySettings(DisplayState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.DeviceName))
            {
                return false;
            }

            try
            {
                var devMode = new DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf<DEVMODE>();
                if (!EnumDisplaySettings(state.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode))
                {
                    return false;
                }

                devMode.dmDisplayOrientation = state.Orientation;
                devMode.dmPelsWidth = state.Width;
                devMode.dmPelsHeight = state.Height;
                devMode.dmDisplayFrequency = state.RefreshRate;
                devMode.dmFields = DM_DISPLAYORIENTATION | DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;

                int testResult = ChangeDisplaySettingsEx(state.DeviceName, ref devMode, IntPtr.Zero, CDS_TEST, IntPtr.Zero);
                if (testResult != DISP_CHANGE_SUCCESSFUL)
                {
                    return false;
                }

                int result = ChangeDisplaySettingsEx(state.DeviceName, ref devMode, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
                return result == DISP_CHANGE_SUCCESSFUL;
            }
            catch
            {
                return false;
            }
        }

        private static int AngleToOrientation(int angle)
        {
            int angleNorm = ((angle % 360) + 360) % 360;
            switch (angleNorm)
            {
                case 0: return DMDO_DEFAULT;
                case 90: return DMDO_90;
                case 180: return DMDO_180;
                case 270: return DMDO_270;
                default: return -1;
            }
        }
    }
}
