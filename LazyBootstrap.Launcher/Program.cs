using System;
using System.Diagnostics;
using System.IO;

string baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
string targetExe = Path.Combine(baseDirectory, "launcher", "LazyBootstrap.exe");

if (!File.Exists(targetExe))
{
    return;
}

var startInfo = new ProcessStartInfo
{
    FileName = targetExe,
    UseShellExecute = true,
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
catch
{
}
