using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

// LazyBootstrap Launcher — 最小化启动器
// 启动 launcher 文件夹内的 LazyBootstrap 主程序

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
    WorkingDirectory = Path.GetDirectoryName(targetExe) ?? baseDir,
    Arguments = BuildForwardedArguments(args, baseDir)
};

try
{
    Process.Start(startInfo);
}
catch
{
    Environment.Exit(1);
}

static string BuildForwardedArguments(string[] sourceArgs, string resolvedBaseDir)
{
    var allArgs = sourceArgs.Select(QuoteArg).ToList();
    allArgs.Add(QuoteArg($"--basedir={resolvedBaseDir}"));
    return string.Join(" ", allArgs);
}

static string QuoteArg(string arg)
{
    if (string.IsNullOrEmpty(arg))
    {
        return "\"\"";
    }

    return arg.IndexOfAny([' ', '\t', '"']) >= 0
        ? "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
        : arg;
}
