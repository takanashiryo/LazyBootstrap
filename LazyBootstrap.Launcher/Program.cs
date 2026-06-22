using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

const int ErrorCancelled = 1223;
string baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
string targetExe = Path.Combine(baseDirectory, "launcher", "LazyBootstrap.exe");

if (!File.Exists(targetExe))
{
    ShowError($"未找到主程序：\r\n{targetExe}");
    return;
}

var startInfo = new ProcessStartInfo
{
    FileName = targetExe,
    UseShellExecute = true,
    Verb = "runas",
    WorkingDirectory = Path.GetDirectoryName(targetExe) ?? baseDirectory
};

foreach (var arg in args)
{
    startInfo.ArgumentList.Add(arg);
}

try
{
    Process.Start(startInfo);
}
catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
{
}
catch (Win32Exception ex)
{
    ShowError($"启动主程序失败。\r\n\r\n错误码：{ex.NativeErrorCode}\r\n{ex.Message}");
}
catch (Exception ex)
{
    ShowError($"启动主程序失败。\r\n\r\n{ex.Message}");
}

static void ShowError(string message)
{
    NativeMethods.MessageBoxW(nint.Zero, message, "LazyBootstrap Launcher", NativeMethods.MbOk | NativeMethods.MbIconError);
}

internal static class NativeMethods
{
    internal const uint MbOk = 0x00000000;
    internal const uint MbIconError = 0x00000010;

    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
    internal static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);
}
