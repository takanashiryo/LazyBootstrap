using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LazyBootstrap.Models;

namespace LazyBootstrap.Services
{
    public sealed class SpiceCrashLogAnalyzer
    {
        private const string SignalPrefix = "W:signal: exception raised:";

        private readonly LauncherPaths _paths;
        private readonly ILogger<SpiceCrashLogAnalyzer> _logger;

        public SpiceCrashLogAnalyzer(LauncherPaths paths, ILogger<SpiceCrashLogAnalyzer> logger)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SpiceCrashDiagnostic> AnalyzeAsync()
        {
            string logPath = Path.Combine(_paths.GetContentsDirectoryPath(), "log.txt");

            try
            {
                if (!File.Exists(logPath))
                {
                    return new SpiceCrashDiagnostic(
                        SpiceCrashDiagnostic.UnknownSignal,
                        "未找到 log.txt，未能识别具体崩溃原因",
                        string.Empty,
                        string.Empty,
                        logPath,
                        readSucceeded: false);
                }

                string content = await File.ReadAllTextAsync(logPath, SpiceLogEncoding.ShiftJis);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return new SpiceCrashDiagnostic(
                        SpiceCrashDiagnostic.UnknownSignal,
                        "log.txt 为空，未能识别具体崩溃原因",
                        string.Empty,
                        string.Empty,
                        logPath,
                        readSucceeded: true);
                }

                string signal = ExtractSignal(content);
                var rule = SpiceCrashErrorCatalog.FindMatch(content, out string matchedLine);
                string reasonText = rule?.ReasonText ?? "未知";

                return new SpiceCrashDiagnostic(
                    signal,
                    reasonText,
                    rule?.Id ?? string.Empty,
                    matchedLine,
                    logPath,
                    readSucceeded: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze spice2x crash log. LogPath={LogPath}", logPath);
                return new SpiceCrashDiagnostic(
                    SpiceCrashDiagnostic.UnknownSignal,
                    "读取 log.txt 失败，未能识别具体崩溃原因",
                    string.Empty,
                    string.Empty,
                    logPath,
                    readSucceeded: false);
            }
        }

        private static string ExtractSignal(string logContent)
        {
            string[] lines = logContent
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');

            foreach (string line in lines)
            {
                int prefixIndex = line.IndexOf(SignalPrefix, StringComparison.Ordinal);
                if (prefixIndex < 0)
                {
                    continue;
                }

                string signal = line.Substring(prefixIndex + SignalPrefix.Length).Trim();
                if (!string.IsNullOrWhiteSpace(signal))
                {
                    return signal;
                }
            }

            return SpiceCrashDiagnostic.UnknownSignal;
        }
    }
}
