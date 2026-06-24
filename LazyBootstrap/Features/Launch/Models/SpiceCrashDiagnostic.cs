namespace LazyBootstrap.Features.Launch
{
    public sealed class SpiceCrashDiagnostic
    {
        public const string UnknownSignal = "UNKNOWN_SIGNAL";

        public SpiceCrashDiagnostic(
            string signal,
            string reasonText,
            string matchedRuleId,
            string matchedLine,
            string logPath,
            bool readSucceeded)
        {
            Signal = string.IsNullOrWhiteSpace(signal) ? UnknownSignal : signal.Trim();
            ReasonText = string.IsNullOrWhiteSpace(reasonText) ? "未识别具体崩溃原因" : reasonText.Trim();
            MatchedRuleId = matchedRuleId ?? string.Empty;
            MatchedLine = matchedLine ?? string.Empty;
            LogPath = logPath ?? string.Empty;
            ReadSucceeded = readSucceeded;
        }

        public string Signal { get; }

        public string ReasonText { get; }

        public string MatchedRuleId { get; }

        public string MatchedLine { get; }

        public string LogPath { get; }

        public bool ReadSucceeded { get; }

        public bool HasKnownReason => !string.IsNullOrWhiteSpace(MatchedRuleId);
    }
}
