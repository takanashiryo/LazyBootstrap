using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class ScreenRotate
{
    [DllImport("user32.dll")]
    public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, int dwflags, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

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

    // Constants for dmDisplayOrientation
    public const int DMDO_DEFAULT = 0;
    public const int DMDO_90 = 1;
    public const int DMDO_180 = 2;
    public const int DMDO_270 = 3;

    // Constants for EnumDisplaySettings
    public const int ENUM_CURRENT_SETTINGS = -1;

    // Constants for ChangeDisplaySettingsEx flags
    public const int CDS_UPDATEREGISTRY = 0x01;
    public const int CDS_TEST = 0x02;

    public static bool Rotate(string deviceName, int angle)
    {
        try
        {
            DEVMODE devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

            if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref devMode))
            {
                return false;
            }

            // 如果已经是目标角度，则无需旋转
            if ((devMode.dmDisplayOrientation * 90) % 360 == angle % 360)
            {
                return true;
            }

            // 交换宽高以进行旋转
            if ((devMode.dmDisplayOrientation + 1) % 2 != (angle / 90 + 1) % 2)
            {
                int temp = devMode.dmPelsWidth;
                devMode.dmPelsWidth = devMode.dmPelsHeight;
                devMode.dmPelsHeight = temp;
            }

            switch (angle)
            {
                case 0:
                    devMode.dmDisplayOrientation = DMDO_DEFAULT;
                    break;
                case 90:
                    devMode.dmDisplayOrientation = DMDO_90;
                    break;
                case 180:
                    devMode.dmDisplayOrientation = DMDO_180;
                    break;
                case 270:
                    devMode.dmDisplayOrientation = DMDO_270;
                    break;
                default:
                    return false;
            }

            int result = ChangeDisplaySettingsEx(deviceName, ref devMode, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
            return result == 0; // DISP_CHANGE_SUCCESSFUL is 0
        }
        catch (Exception)
        {
            return false;
        }
    }
}