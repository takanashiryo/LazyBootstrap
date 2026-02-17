using System;
using System.Collections.Generic;

internal static class TomlTextShared
{
    public static string EscapeTomlString(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    public static bool TryParseTomlKeyValue(string line, Func<string, string> parseValue, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        int eqIndex = line.IndexOf('=');
        if (eqIndex <= 0)
        {
            return false;
        }

        key = line.Substring(0, eqIndex).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        value = parseValue?.Invoke(line.Substring(eqIndex + 1).Trim()) ?? string.Empty;
        return true;
    }

    public static void NormalizeBlankLines(List<string> lines, bool preserveSectionSeparator)
    {
        if (lines == null || lines.Count == 0)
        {
            return;
        }

        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                break;
            }

            lines.RemoveAt(i);
        }

        for (int i = lines.Count - 1; i > 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]) || !string.IsNullOrWhiteSpace(lines[i - 1]))
            {
                continue;
            }

            if (preserveSectionSeparator)
            {
                string prevNonBlank = string.Empty;
                string nextNonBlank = string.Empty;

                for (int p = i - 1; p >= 0; p--)
                {
                    if (!string.IsNullOrWhiteSpace(lines[p]))
                    {
                        prevNonBlank = lines[p].Trim();
                        break;
                    }
                }

                for (int n = i + 1; n < lines.Count; n++)
                {
                    if (!string.IsNullOrWhiteSpace(lines[n]))
                    {
                        nextNonBlank = lines[n].Trim();
                        break;
                    }
                }

                bool keepAsSectionSeparator = !string.IsNullOrWhiteSpace(prevNonBlank)
                    && !string.IsNullOrWhiteSpace(nextNonBlank)
                    && !prevNonBlank.StartsWith("[", StringComparison.Ordinal)
                    && nextNonBlank.StartsWith("[", StringComparison.Ordinal);

                if (keepAsSectionSeparator)
                {
                    continue;
                }
            }

            lines.RemoveAt(i);
        }
    }
}
