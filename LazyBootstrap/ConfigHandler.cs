using System;
using System.Collections.Generic;
using System.IO;
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
            var data = LoadToml();

            if (!data.TryGetValue(section, out var sectionData))
            {
                sectionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                data[section] = sectionData;
            }

            sectionData[key] = value ?? string.Empty;
            SaveToml(data);
        }
    }

    // 读取字符串
    public string ReadString(string section, string key, string defaultValue = "")
    {
        lock (_sync)
        {
            var data = LoadToml();
            if (data.TryGetValue(section, out var sectionData) && sectionData.TryGetValue(key, out var value))
            {
                return value;
            }

            return defaultValue;
        }
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
            string currentSection = "Settings";

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
                  .Append(EscapeTomlString(kv.Value ?? string.Empty))
                                    .Append('"')
                                    .AppendLine();
            }
        }

        File.WriteAllText(_path, sb.ToString(), new UTF8Encoding(false));
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

    private static string EscapeTomlString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}