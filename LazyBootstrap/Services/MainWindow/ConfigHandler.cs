using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// 一个用于处理 TOML 文件的帮助类（保留原有 ReadString/WriteString 接口）
public class ConfigHandler
{
    private readonly string _path;
    private readonly object _sync = new object();

    // 构造函数，接收 TOML 文件路径
    public ConfigHandler(string tomlPath)
    {
        _path = new FileInfo(tomlPath).FullName;
        TryMigrateLegacyIni();
    }

    // 写入字符串
    public void WriteString(string section, string key, string value)
    {
        lock (_sync)
        {
            var lines = File.Exists(_path)
                ? File.ReadAllLines(_path, Encoding.UTF8).ToList()
                : new List<string>();

            string sectionName = section?.Trim() ?? string.Empty;
            string keyName = key?.Trim() ?? string.Empty;
            string valueLine = BuildTomlLine(keyName, value ?? string.Empty);

            if (string.IsNullOrWhiteSpace(keyName))
            {
                return;
            }

            if (!TryUpsertInSection(lines, sectionName, keyName, valueLine))
            {
                AppendSectionWithKey(lines, sectionName, valueLine);
            }

            NormalizeBlankLines(lines);

            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_path, string.Join(Environment.NewLine, lines), TomlTextShared.Utf8NoBom);
        }
    }

    public void RenameSection(string sourceSection, string targetSection)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(sourceSection) || string.IsNullOrWhiteSpace(targetSection)
                || string.Equals(sourceSection, targetSection, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(_path))
            {
                return;
            }

            var lines = File.ReadAllLines(_path, Encoding.UTF8).ToList();
            int sourceHeaderIndex = -1;
            int targetHeaderIndex = -1;

            for (int i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].Trim();
                if (!TryGetStandardSectionName(trimmed, out var parsedSection))
                {
                    continue;
                }

                if (string.Equals(parsedSection, targetSection, StringComparison.OrdinalIgnoreCase))
                {
                    targetHeaderIndex = i;
                }

                if (string.Equals(parsedSection, sourceSection, StringComparison.OrdinalIgnoreCase))
                {
                    sourceHeaderIndex = i;
                }
            }

            if (sourceHeaderIndex < 0)
            {
                return;
            }

            if (targetHeaderIndex >= 0)
            {
                RemoveSection(lines, sourceSection);
            }
            else
            {
                lines[sourceHeaderIndex] = $"[{targetSection}]";
            }

            NormalizeBlankLines(lines);
            File.WriteAllText(_path, string.Join(Environment.NewLine, lines), TomlTextShared.Utf8NoBom);
        }
    }

    public void MoveKey(string sourceSection, string targetSection, string key)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(sourceSection)
                || string.IsNullOrWhiteSpace(targetSection)
                || string.IsNullOrWhiteSpace(key)
                || !File.Exists(_path))
            {
                return;
            }

            var lines = File.ReadAllLines(_path, Encoding.UTF8).ToList();
            if (!TryGetSectionBounds(lines, sourceSection, out var sourceHeaderIndex, out _, out var sourceEndExclusive))
            {
                return;
            }

            int keyLineIndex = -1;
            string keyValue = string.Empty;
            for (int i = sourceHeaderIndex + 1; i < sourceEndExclusive; i++)
            {
                var trimmed = lines[i].Trim();
                if (!TryParseTomlKeyValue(trimmed, out var parsedKey, out var parsedValue))
                {
                    continue;
                }

                if (string.Equals(parsedKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    keyLineIndex = i;
                    keyValue = parsedValue;
                    break;
                }
            }

            if (keyLineIndex < 0)
            {
                return;
            }

            lines.RemoveAt(keyLineIndex);
            if (TryGetSectionBounds(lines, sourceSection, out sourceHeaderIndex, out _, out sourceEndExclusive)
                && sourceEndExclusive <= sourceHeaderIndex + 1)
            {
                lines.RemoveAt(sourceHeaderIndex);
            }

            string valueLine = BuildTomlLine(key, keyValue);
            if (!TryUpsertInSection(lines, targetSection, key, valueLine))
            {
                AppendSectionWithKey(lines, targetSection, valueLine);
            }

            NormalizeBlankLines(lines);
            File.WriteAllText(_path, string.Join(Environment.NewLine, lines), TomlTextShared.Utf8NoBom);
        }
    }

    // 读取字符串
    public string ReadString(string section, string key, string defaultValue = "")
    {
        lock (_sync)
        {
            if (!File.Exists(_path))
            {
                return defaultValue;
            }

            string sectionName = section?.Trim() ?? string.Empty;
            string keyName = key?.Trim() ?? string.Empty;

            string currentSection = string.Empty;
            bool inArraySection = false;

            foreach (var rawLine in File.ReadAllLines(_path, Encoding.UTF8))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryGetStandardSectionName(line, out var parsedSection))
                {
                    currentSection = parsedSection;
                    inArraySection = false;
                    continue;
                }

                if (TryGetArraySectionName(line, out _))
                {
                    inArraySection = true;
                    continue;
                }

                if (inArraySection)
                {
                    continue;
                }

                if (!string.Equals(currentSection, sectionName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TryParseTomlKeyValue(line, out var parsedKey, out var parsedValue))
                {
                    continue;
                }

                if (string.Equals(parsedKey, keyName, StringComparison.OrdinalIgnoreCase))
                {
                    return parsedValue;
                }
            }

            return defaultValue;
        }
    }

    private static string BuildTomlLine(string key, string value)
    {
        return $"{key} = \"{TomlTextShared.EscapeTomlString(value)}\"";
    }

    private static void NormalizeBlankLines(List<string> lines)
    {
        TomlTextShared.NormalizeBlankLines(lines, preserveSectionSeparator: true);
    }

    private static bool TryGetSectionBounds(List<string> lines, string sectionName, out int headerIndex, out int contentStart, out int contentEndExclusive)
    {
        headerIndex = -1;
        contentStart = -1;
        contentEndExclusive = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (TryGetStandardSectionName(trimmed, out var parsedSection)
                && string.Equals(parsedSection, sectionName, StringComparison.OrdinalIgnoreCase))
            {
                headerIndex = i;
                contentStart = i + 1;
                break;
            }
        }

        if (headerIndex < 0)
        {
            return false;
        }

        contentEndExclusive = lines.Count;
        for (int i = contentStart; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (TryGetStandardSectionName(trimmed, out _) || TryGetArraySectionName(trimmed, out _))
            {
                contentEndExclusive = i;
                break;
            }
        }

        return true;
    }

    private static void RemoveSection(List<string> lines, string sectionName)
    {
        if (!TryGetSectionBounds(lines, sectionName, out var headerIndex, out _, out var contentEndExclusive))
        {
            return;
        }

        lines.RemoveRange(headerIndex, contentEndExclusive - headerIndex);
    }

    private static bool TryUpsertInSection(List<string> lines, string sectionName, string keyName, string valueLine)
    {
        int sectionHeaderIndex = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (TryGetStandardSectionName(trimmed, out var parsedSection)
                && string.Equals(parsedSection, sectionName, StringComparison.OrdinalIgnoreCase))
            {
                sectionHeaderIndex = i;
                break;
            }
        }

        if (sectionHeaderIndex < 0)
        {
            return false;
        }

        int insertIndex = sectionHeaderIndex + 1;
        for (int i = sectionHeaderIndex + 1; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();

            if (TryGetStandardSectionName(trimmed, out _))
            {
                insertIndex = i;
                break;
            }

            if (TryGetArraySectionName(trimmed, out _))
            {
                insertIndex = i;
                break;
            }

            if (TryParseTomlKeyValue(trimmed, out var parsedKey, out _)
                && string.Equals(parsedKey, keyName, StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = valueLine;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                insertIndex = i + 1;
            }
        }

        lines.Insert(insertIndex, valueLine);
        return true;
    }

    private static void AppendSectionWithKey(List<string> lines, string sectionName, string valueLine)
    {
        if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.Add(string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            lines.Add($"[{sectionName}]");
        }

        lines.Add(valueLine);
    }

    private static bool TryGetStandardSectionName(string line, out string sectionName)
    {
        sectionName = string.Empty;
        if (string.IsNullOrWhiteSpace(line)
            || !line.StartsWith("[", StringComparison.Ordinal)
            || !line.EndsWith("]", StringComparison.Ordinal)
            || line.StartsWith("[[", StringComparison.Ordinal)
            || line.EndsWith("]]", StringComparison.Ordinal))
        {
            return false;
        }

        sectionName = line.Substring(1, line.Length - 2).Trim();
        return true;
    }

    private static bool TryGetArraySectionName(string line, out string sectionName)
    {
        sectionName = string.Empty;
        if (string.IsNullOrWhiteSpace(line)
            || !line.StartsWith("[[", StringComparison.Ordinal)
            || !line.EndsWith("]]", StringComparison.Ordinal)
            || line.Length <= 4)
        {
            return false;
        }

        sectionName = line.Substring(2, line.Length - 4).Trim();
        return true;
    }

    private static bool TryParseTomlKeyValue(string line, out string key, out string value)
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

        string rawValue = line.Substring(eqIndex + 1).Trim();
        value = ParseTomlValue(rawValue);
        return true;
    }

    private Dictionary<string, Dictionary<string, string>> LoadToml()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(_path))
        {
            return result;
        }

        string currentSection = string.Empty;
        foreach (var rawLine in File.ReadAllLines(_path, Encoding.UTF8))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal) && line.Length > 2)
            {
                currentSection = line.Substring(1, line.Length - 2).Trim();
                if (!result.ContainsKey(currentSection))
                {
                    result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                continue;
            }

            int eqIndex = line.IndexOf('=');
            if (eqIndex <= 0)
            {
                continue;
            }

            string key = line.Substring(0, eqIndex).Trim();
            string rawValue = line.Substring(eqIndex + 1).Trim();
            string parsedValue = ParseTomlValue(rawValue);

            if (!result.TryGetValue(currentSection, out var sectionData))
            {
                sectionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                result[currentSection] = sectionData;
            }

            sectionData[key] = parsedValue;
        }

        return result;
    }

    private void TryMigrateLegacyIni()
    {
        try
        {
            if (File.Exists(_path))
            {
                return;
            }

            string iniPath = Path.ChangeExtension(_path, ".ini");
            if (!File.Exists(iniPath))
            {
                return;
            }

            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            string currentSection = "Setting";

            foreach (var rawLine in File.ReadAllLines(iniPath, Encoding.UTF8))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) ||
                    line.StartsWith(";", StringComparison.Ordinal) ||
                    line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal) && line.Length > 2)
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    if (!data.ContainsKey(currentSection))
                    {
                        data[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                    continue;
                }

                int eqIndex = line.IndexOf('=');
                if (eqIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, eqIndex).Trim();
                string value = line.Substring(eqIndex + 1).Trim();

                if (!data.TryGetValue(currentSection, out var sectionData))
                {
                    sectionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    data[currentSection] = sectionData;
                }

                sectionData[key] = value;
            }

            SaveToml(data);
        }
        catch
        {
            // 迁移失败时保持静默，后续按默认行为运行
        }
    }

    private void SaveToml(Dictionary<string, Dictionary<string, string>> data)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var sb = new StringBuilder();
        bool firstSection = true;
        foreach (var section in data)
        {
            if (!firstSection)
            {
                sb.AppendLine();
            }
            firstSection = false;

            if (!string.IsNullOrWhiteSpace(section.Key))
            {
                sb.Append('[').Append(section.Key).AppendLine("]");
            }

            foreach (var kv in section.Value)
            {
                sb.Append(kv.Key)
                  .Append(" = ")
                  .Append('"')
                                    .Append(TomlTextShared.EscapeTomlString(kv.Value ?? string.Empty))
                                    .Append('"')
                                    .AppendLine();
            }
        }

        File.WriteAllText(_path, sb.ToString(), TomlTextShared.Utf8NoBom);
    }

    private static string ParseTomlValue(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var value = StripInlineComment(rawValue).Trim();
        if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
        {
            var inner = value.Substring(1, value.Length - 2);
            return inner
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t");
        }

        if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'')
        {
            return value.Substring(1, value.Length - 2);
        }

        return value;
    }

    private static string StripInlineComment(string rawValue)
    {
        bool inDoubleQuote = false;
        bool inSingleQuote = false;
        bool escaped = false;

        for (int i = 0; i < rawValue.Length; i++)
        {
            char ch = rawValue[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (ch == '\\' && inDoubleQuote)
            {
                escaped = true;
                continue;
            }

            if (ch == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                continue;
            }

            if (ch == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (ch == '#' && !inDoubleQuote && !inSingleQuote)
            {
                return rawValue.Substring(0, i);
            }
        }

        return rawValue;
    }

}
