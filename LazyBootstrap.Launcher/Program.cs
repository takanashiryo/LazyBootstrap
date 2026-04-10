using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

string baseDir = Path.GetFullPath(AppContext.BaseDirectory);
string targetExe = Path.Combine(baseDir, "launcher", "LazyBootstrap.exe");

if (!File.Exists(targetExe))
{
    Environment.Exit(1);
}

var startInfo = new ProcessStartInfo
{
    FileName = targetExe,
    UseShellExecute = true,
    WorkingDirectory = Path.GetDirectoryName(targetExe) ?? baseDir
};

foreach (var arg in args)
{
    startInfo.ArgumentList.Add(arg);
}

if (!HasBaseDirArgument(args))
{
    startInfo.ArgumentList.Add($"--basedir={baseDir}");
}

try
{
    Process.Start(startInfo);
}
catch
{
    Environment.Exit(1);
}

static bool HasBaseDirArgument(string[] sourceArgs)
{
    return sourceArgs.Any(static arg =>
        arg.StartsWith("--basedir=", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "--basedir", StringComparison.OrdinalIgnoreCase));
}
