using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LazyBootstrap;

// 一个用于处理 TOML 文件的帮助类（保留原有 ReadString/WriteString 接口）
public class ConfigHandler
{
    private readonly string _path;
    private readonly object _sync = new object();

    // 构造函数，接收 TOML 文件路径
    public ConfigHandler(string tomlPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tomlPath);
        _path = new FileInfo(tomlPath).FullName;
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
            WriteLinesUnsafe(lines);
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
            WriteLinesUnsafe(lines);
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
            WriteLinesUnsafe(lines);
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

    public (List<ServerPresetItem> Presets, string ActivePreset, bool Mutated) LoadServerPresets(string nonePresetName, string asphyxiaPresetName, string asphyxiaDefaultUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nonePresetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(asphyxiaPresetName);
        ArgumentNullException.ThrowIfNull(asphyxiaDefaultUrl);

        lock (_sync)
        {
            var presets = new List<ServerPresetItem>
            {
                new ServerPresetItem { Name = nonePresetName }
            };

            string activePreset = nonePresetName;
            bool hasPresetSection = false;
            bool mutated = false;

            var lines = LoadLinesUnsafe();
            if (lines.Count > 0)
            {
                ServerPresetItem current = null;
                bool inServerSection = false;
                string fileActivePreset = string.Empty;

                void CommitCurrent()
                {
                    if (current == null || string.IsNullOrWhiteSpace(current.Name))
                    {
                        return;
                    }

                    if (presets.Any(p => string.Equals(p.Name, current.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        return;
                    }

                    presets.Add(current);
                }

                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();

                    if (line.StartsWith("[Server]", StringComparison.OrdinalIgnoreCase))
                    {
                        inServerSection = true;
                        continue;
                    }

                    if (line.StartsWith("[[Server.Presets]]", StringComparison.OrdinalIgnoreCase)
                        || line.StartsWith("[[ServerPresets]]", StringComparison.OrdinalIgnoreCase))
                    {
                        hasPresetSection = true;
                        inServerSection = false;
                        CommitCurrent();
                        current = new ServerPresetItem();
                        continue;
                    }

                    if (inServerSection)
                    {
                        if (line.StartsWith("[", StringComparison.Ordinal) && !line.StartsWith("[[", StringComparison.Ordinal))
                        {
                            inServerSection = false;
                        }
                        else
                        {
                            if (TryParseTomlKeyValue(line, out var serverKey, out var serverValue)
                                && string.Equals(serverKey, "activepreset", StringComparison.OrdinalIgnoreCase))
                            {
                                fileActivePreset = serverValue;
                            }

                            continue;
                        }
                    }

                    if (current == null)
                    {
                        continue;
                    }

                    if (line.StartsWith("[", StringComparison.Ordinal) && !line.StartsWith("[[", StringComparison.Ordinal))
                    {
                        CommitCurrent();
                        current = null;
                        continue;
                    }

                    if (!TryParseTomlKeyValue(line, out var key, out var value))
                    {
                        continue;
                    }

                    if (string.Equals(key, "name", StringComparison.OrdinalIgnoreCase))
                    {
                        current.Name = value;
                    }
                    else if (string.Equals(key, "serverurl", StringComparison.OrdinalIgnoreCase))
                    {
                        current.ServerUrl = value;
                    }
                    else if (string.Equals(key, "pcbid", StringComparison.OrdinalIgnoreCase))
                    {
                        current.PcbId = value;
                    }
                }

                CommitCurrent();

                if (!string.IsNullOrWhiteSpace(fileActivePreset))
                {
                    var matched = presets.FirstOrDefault(p => string.Equals(p.Name, fileActivePreset, StringComparison.OrdinalIgnoreCase));
                    if (matched != null)
                    {
                        activePreset = matched.Name;
                    }
                }
            }

            var existingAsphyxia = presets.FirstOrDefault(p => string.Equals(p.Name, asphyxiaPresetName, StringComparison.OrdinalIgnoreCase));
            if (existingAsphyxia == null)
            {
                presets.Add(new ServerPresetItem
                {
                    Name = asphyxiaPresetName,
                    ServerUrl = asphyxiaDefaultUrl,
                    PcbId = string.Empty
                });
                mutated = true;
            }
            else if (string.IsNullOrWhiteSpace(existingAsphyxia.ServerUrl))
            {
                existingAsphyxia.ServerUrl = asphyxiaDefaultUrl;
                mutated = true;
            }

            if (!hasPresetSection)
            {
                mutated = true;
            }

            return (presets, activePreset, mutated);
        }
    }

    public void SaveServerPresets(IEnumerable<ServerPresetItem> presets, string activePreset, string nonePresetName)
    {
        ArgumentNullException.ThrowIfNull(presets);
        ArgumentException.ThrowIfNullOrWhiteSpace(nonePresetName);

        lock (_sync)
        {
            var lines = LoadLinesUnsafe();
            var kept = new List<string>();
            bool skippingOldPresets = false;
            bool skippingServerSection = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("[Server]", StringComparison.OrdinalIgnoreCase))
                {
                    skippingServerSection = true;
                    continue;
                }

                if (trimmed.StartsWith("[[Server.Presets]]", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("[[ServerPresets]]", StringComparison.OrdinalIgnoreCase))
                {
                    skippingOldPresets = true;
                    continue;
                }

                if (skippingServerSection || skippingOldPresets)
                {
                    if (trimmed.StartsWith("[", StringComparison.Ordinal) && !trimmed.StartsWith("[[", StringComparison.Ordinal))
                    {
                        skippingServerSection = false;
                        skippingOldPresets = false;
                        kept.Add(line);
                    }

                    continue;
                }

                kept.Add(line);
            }

            while (kept.Count > 0 && string.IsNullOrWhiteSpace(kept[^1]))
            {
                kept.RemoveAt(kept.Count - 1);
            }

            if (kept.Count > 0)
            {
                kept.Add(string.Empty);
            }

            kept.Add("[Server]");
            kept.Add($"activepreset = \"{TomlTextShared.EscapeTomlString(activePreset ?? nonePresetName)}\"");

            foreach (var preset in presets.Where(p =>
                         p != null
                         && !string.Equals(p.Name, nonePresetName, StringComparison.OrdinalIgnoreCase)))
            {
                kept.Add(string.Empty);
                kept.Add("[[Server.Presets]]");
                kept.Add($"name = \"{TomlTextShared.EscapeTomlString(preset.Name)}\"");
                kept.Add($"serverurl = \"{TomlTextShared.EscapeTomlString(preset.ServerUrl)}\"");
                kept.Add($"pcbid = \"{TomlTextShared.EscapeTomlString(preset.PcbId)}\"");
            }

            TomlTextShared.NormalizeBlankLines(kept, preserveSectionSeparator: false);
            WriteLinesUnsafe(kept);
        }
    }

    private static string BuildTomlLine(string key, string value)
    {
        return $"{key} = \"{TomlTextShared.EscapeTomlString(value)}\"";
    }

    private List<string> LoadLinesUnsafe()
    {
        return File.Exists(_path)
            ? File.ReadAllLines(_path, Encoding.UTF8).ToList()
            : new List<string>();
    }

    private void WriteLinesUnsafe(List<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        WriteTextAtomically(string.Join(Environment.NewLine, lines));
    }

    private void WriteTextAtomically(string content)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = Path.Combine(
            directory ?? string.Empty,
            $"{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");

        File.WriteAllText(tempPath, content ?? string.Empty, TomlTextShared.Utf8NoBom);

        try
        {
            if (File.Exists(_path))
            {
                try
                {
                    File.Replace(tempPath, _path, null, true);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Move(tempPath, _path, true);
                }
            }
            else
            {
                File.Move(tempPath, _path);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
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
