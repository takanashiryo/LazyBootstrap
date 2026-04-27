using System;
using System.Collections.Generic;
using System.Text;

internal static class TomlTextShared
{
    public static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

    public static string EscapeTomlString(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
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
