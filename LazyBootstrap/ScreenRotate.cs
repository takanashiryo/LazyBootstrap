using System;
using System.Runtime.InteropServices;

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

    // Constants for dmFields
    public const int DM_DISPLAYORIENTATION = 0x00000080;
    public const int DM_PELSWIDTH = 0x00080000;
    public const int DM_PELSHEIGHT = 0x00100000;

    // Return codes
    public const int DISP_CHANGE_SUCCESSFUL = 0;

    public static bool Rotate(string deviceName, int angle)
    {
        try
        {
            // 归一化角度到 0/90/180/270
            int angleNorm = ((angle % 360) + 360) % 360;
            int targetOrientation;
            switch (angleNorm)
            {
                case 0: targetOrientation = DMDO_DEFAULT; break;
                case 90: targetOrientation = DMDO_90; break;
                case 180: targetOrientation = DMDO_180; break;
                case 270: targetOrientation = DMDO_270; break;
                default: return false;
            }

            DEVMODE devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

            if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref devMode))
            {
                return false;
            }

            // 如果已经是目标角度，则无需旋转
            if (devMode.dmDisplayOrientation == targetOrientation)
            {
                return true;
            }

            // 判定是否需要交换分辨率的宽高（当奇偶不同，即 0/180 与 90/270 之间切换）
            bool swapDimensions = ((devMode.dmDisplayOrientation % 2) != (targetOrientation % 2));
            if (swapDimensions)
            {
                int temp = devMode.dmPelsWidth;
                devMode.dmPelsWidth = devMode.dmPelsHeight;
                devMode.dmPelsHeight = temp;
            }

            devMode.dmDisplayOrientation = targetOrientation;

            // 设置需要更新的字段
            devMode.dmFields = DM_DISPLAYORIENTATION;
            if (swapDimensions)
            {
                devMode.dmFields |= DM_PELSWIDTH | DM_PELSHEIGHT;
            }

            // 先测试
            int testResult = ChangeDisplaySettingsEx(deviceName, ref devMode, IntPtr.Zero, CDS_TEST, IntPtr.Zero);
            if (testResult != DISP_CHANGE_SUCCESSFUL)
            {
                return false;
            }

            // 测试成功后提交到注册表
            int result = ChangeDisplaySettingsEx(deviceName, ref devMode, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
            return result == DISP_CHANGE_SUCCESSFUL;
        }
        catch (Exception)
        {
            return false;
        }
    }
}