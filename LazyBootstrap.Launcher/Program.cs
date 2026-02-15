using System;
using System.Diagnostics;
using System.IO;

// LazyBootstrap Launcher — 最小化启动器
// 启动 launcher 文件夹内的 LazyBootstrap 主程序

string baseDir = AppContext.BaseDirectory;
string targetExe = Path.Combine(baseDir, "launcher", "LazyBootstrap.exe");

if (!File.Exists(targetExe))
{
    Environment.Exit(1);
}

var startInfo = new ProcessStartInfo
{
    FileName = targetExe,
    UseShellExecute = false
};

// 传递基础目录，让主程序知道根目录位置
startInfo.Environment["LAZYBOOTSTRAP_BASEDIR"] = baseDir;

// 透传命令行参数
foreach (var arg in args)
{
    startInfo.ArgumentList.Add(arg);
}

// 显式传递根目录参数，避免提权重启后环境变量丢失
startInfo.ArgumentList.Add($"--basedir={baseDir}");

try
{
    Process.Start(startInfo);
}
catch
{
    Environment.Exit(1);
}
