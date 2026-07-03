using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Parsing;

namespace LazyBootstrap.Infrastructure.Serialization;

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

    public static string BuildStringLine(string key, string value)
    {
        return $"{key} = \"{EscapeTomlString(value)}\"";
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

            if (preserveSectionSeparator && IsSectionSeparator(lines, i))
            {
                continue;
            }

            lines.RemoveAt(i);
        }
    }

    private static bool IsSectionSeparator(List<string> lines, int blankLineIndex)
    {
        string previous = string.Empty;
        string next = string.Empty;

        for (int i = blankLineIndex - 1; i >= 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                previous = lines[i].Trim();
                break;
            }
        }

        for (int i = blankLineIndex + 1; i < lines.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                next = lines[i].Trim();
                break;
            }
        }

        return !string.IsNullOrWhiteSpace(previous)
            && !string.IsNullOrWhiteSpace(next)
            && !previous.StartsWith("[", StringComparison.Ordinal)
            && next.StartsWith("[", StringComparison.Ordinal);
    }
}

public class ConfigHandler
{
    private const string InvalidBackupSuffix = "invalid";
    private readonly string _path;
    private readonly object _sync = new object();
    private readonly ILogger<ConfigHandler> _logger;

    public ConfigHandler(string tomlPath)
        : this(tomlPath, null)
    {
    }

    public ConfigHandler(string tomlPath, ILogger<ConfigHandler> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tomlPath);
        _path = new FileInfo(tomlPath).FullName;
        _logger = logger;
    }

    public bool TryValidate(out string error)
    {
        lock (_sync)
        {
            if (!File.Exists(_path))
            {
                error = string.Empty;
                return true;
            }

            try
            {
                error = ValidateTomlText(File.ReadAllText(_path, Encoding.UTF8));
                return string.IsNullOrWhiteSpace(error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    public void ReplaceWithText(string content)
    {
        lock (_sync)
        {
            WriteTextUnsafe(content ?? string.Empty);
        }
    }

    public string BackupInvalidAndReplace(string replacementContent)
    {
        lock (_sync)
        {
            string backupPath = string.Empty;
            if (File.Exists(_path))
            {
                backupPath = CreateUniqueBackupPath();
                File.Move(_path, backupPath);
                _logger?.LogWarning("Invalid config.toml was moved to {BackupPath}.", backupPath);
            }

            try
            {
                WriteTextUnsafe(replacementContent ?? string.Empty);
                return backupPath;
            }
            catch
            {
                RestoreBackupAfterFailedReset(backupPath);
                throw;
            }
        }
    }

    public void WriteString(string section, string key, string value)
    {
        lock (_sync)
        {
            string sectionName = NormalizeName(section);
            string keyName = NormalizeName(key);
            if (string.IsNullOrWhiteSpace(keyName))
            {
                return;
            }

            var document = LoadDocumentUnsafe();
            document.UpsertString(sectionName, keyName, value ?? string.Empty);
            WriteDocumentUnsafe(document);
        }
    }

    public void WriteBool(string section, string key, bool value)
    {
        WriteString(section, key, value ? "true" : "false");
    }

    public void WriteInt(string section, string key, int value)
    {
        WriteString(section, key, value.ToString(CultureInfo.InvariantCulture));
    }

    public void RenameSection(string sourceSection, string targetSection)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(sourceSection)
                || string.IsNullOrWhiteSpace(targetSection)
                || string.Equals(sourceSection, targetSection, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(_path))
            {
                return;
            }

            var document = LoadDocumentUnsafe();
            if (document.RenameSection(NormalizeName(sourceSection), NormalizeName(targetSection)))
            {
                WriteDocumentUnsafe(document);
            }
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

            var document = LoadDocumentUnsafe();
            if (document.MoveKey(NormalizeName(sourceSection), NormalizeName(targetSection), NormalizeName(key)))
            {
                WriteDocumentUnsafe(document);
            }
        }
    }

    public void DeleteKey(string section, string key)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(section)
                || string.IsNullOrWhiteSpace(key)
                || !File.Exists(_path))
            {
                return;
            }

            var document = LoadDocumentUnsafe();
            if (document.DeleteKey(NormalizeName(section), NormalizeName(key)))
            {
                WriteDocumentUnsafe(document);
            }
        }
    }

    public string ReadString(string section, string key, string defaultValue = "")
    {
        lock (_sync)
        {
            if (!File.Exists(_path))
            {
                return defaultValue;
            }

            string sectionName = NormalizeName(section);
            string keyName = NormalizeName(key);
            if (string.IsNullOrWhiteSpace(keyName))
            {
                return defaultValue;
            }

            if (TryLoadModelUnsafe(out var model, out var error)
                && TryReadModelValue(model, sectionName, keyName, out var modelValue))
            {
                return modelValue;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger?.LogWarning("Falling back to text-level config read because TOML model loading failed: {Error}", error);
            }

            var document = LoadDocumentUnsafe();
            return document.TryReadString(sectionName, keyName, out var textValue)
                ? textValue
                : defaultValue;
        }
    }

    public bool ReadBool(string section, string key, bool defaultValue)
    {
        var value = ReadString(section, key, defaultValue ? "true" : "false");
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    public int ReadInt(string section, string key, int defaultValue)
    {
        var value = ReadString(section, key, defaultValue.ToString(CultureInfo.InvariantCulture));
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }

    public (List<ServerPresetItem> Presets, string ActivePreset, bool Mutated) LoadServerPresets(
        string nonePresetName,
        string asphyxiaPresetName,
        string asphyxiaDefaultUrl)
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
            string modelError = string.Empty;

            if (File.Exists(_path) && TryLoadModelUnsafe(out var model, out modelError))
            {
                LoadServerPresetsFromModel(model, presets, ref activePreset, ref hasPresetSection);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(modelError))
                {
                    _logger?.LogWarning("Falling back to text-level server preset read because TOML model loading failed: {Error}", modelError);
                }

                LoadServerPresetsFromText(LoadDocumentUnsafe(), presets, ref activePreset, ref hasPresetSection);
            }

            bool mutated = EnsureServerPresetDefaults(presets, asphyxiaPresetName, asphyxiaDefaultUrl);
            if (!hasPresetSection)
            {
                mutated = true;
            }

            var active = presets.FirstOrDefault(p => string.Equals(p.Name, activePreset, StringComparison.OrdinalIgnoreCase));
            activePreset = active?.Name ?? nonePresetName;

            return (presets, activePreset, mutated);
        }
    }

    public void SaveServerPresets(IEnumerable<ServerPresetItem> presets, string activePreset, string nonePresetName)
    {
        ArgumentNullException.ThrowIfNull(presets);
        ArgumentException.ThrowIfNullOrWhiteSpace(nonePresetName);

        lock (_sync)
        {
            var document = LoadDocumentUnsafe();
            document.RemoveArrayTableBlocks("Server.Presets");
            document.RemoveArrayTableBlocks("ServerPresets");
            document.UpsertString("Server", "activepreset", activePreset ?? nonePresetName);

            foreach (var preset in presets.Where(p =>
                         p != null
                         && !string.IsNullOrWhiteSpace(p.Name)
                         && !string.Equals(p.Name, nonePresetName, StringComparison.OrdinalIgnoreCase)))
            {
                document.AppendServerPreset(preset);
            }

            WriteDocumentUnsafe(document, preserveSectionSeparator: false);
        }
    }

    private TomlLineDocument LoadDocumentUnsafe()
    {
        return File.Exists(_path)
            ? TomlLineDocument.FromLines(File.ReadAllLines(_path, Encoding.UTF8))
            : TomlLineDocument.Empty();
    }

    private void WriteDocumentUnsafe(TomlLineDocument document, bool preserveSectionSeparator = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.NormalizeBlankLines(preserveSectionSeparator);
        WriteTextUnsafe(document.ToText());
    }

    private void WriteTextUnsafe(string content)
    {
        var validationError = ValidateTomlText(content ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            throw new InvalidDataException($"Serialized TOML failed validation: {validationError}");
        }

        if (!SafeFileWriter.TryWriteAllText(_path, content ?? string.Empty, ValidateTomlFile, out var error))
        {
            throw new IOException(error);
        }
    }

    private bool TryLoadModelUnsafe(out TomlTable model, out string error)
    {
        model = null;
        error = string.Empty;

        try
        {
            string text = File.ReadAllText(_path, Encoding.UTF8);
            error = ValidateTomlText(text);
            if (!string.IsNullOrWhiteSpace(error))
            {
                return false;
            }

            model = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private string CreateUniqueBackupPath()
    {
        var directory = Path.GetDirectoryName(_path) ?? string.Empty;
        var fileName = Path.GetFileName(_path);
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        string basePath = Path.Combine(directory, $"{fileName}.{InvalidBackupSuffix}.{timestamp}.bak");
        if (!File.Exists(basePath))
        {
            return basePath;
        }

        for (int i = 1; ; i++)
        {
            string candidate = Path.Combine(directory, $"{fileName}.{InvalidBackupSuffix}.{timestamp}.{i}.bak");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private void RestoreBackupAfterFailedReset(string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath) || File.Exists(_path))
        {
            return;
        }

        try
        {
            File.Move(backupPath, _path);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to restore invalid config backup after reset failure.");
        }
    }

    private static string ValidateTomlFile(string path)
    {
        try
        {
            return ValidateTomlText(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static string ValidateTomlText(string text)
    {
        try
        {
            SyntaxParser.ParseStrict(text ?? string.Empty, "config.toml", true);
            return string.Empty;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static bool TryReadModelValue(TomlTable model, string sectionName, string keyName, out string value)
    {
        value = string.Empty;
        if (!TryGetSectionTable(model, sectionName, out var section))
        {
            return false;
        }

        if (!TryGetValue(section, keyName, out var rawValue))
        {
            return false;
        }

        value = ConvertTomlValue(rawValue);
        return true;
    }

    private static bool TryGetSectionTable(TomlTable model, string sectionName, out TomlTable section)
    {
        section = null;
        if (model == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(sectionName))
        {
            section = model;
            return true;
        }

        TomlTable current = model;
        foreach (var segment in sectionName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryGetValue(current, segment, out var next) || next is not TomlTable nextTable)
            {
                return false;
            }

            current = nextTable;
        }

        section = current;
        return true;
    }

    private static bool TryGetValue(TomlTable table, string key, out object value)
    {
        value = null;
        if (table == null || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (table.TryGetValue(key, out value))
        {
            return true;
        }

        foreach (var entry in table)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        return false;
    }

    private static string ConvertTomlValue(object value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            bool boolean => boolean ? "true" : "false",
            int number => number.ToString(CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture),
            float number => number.ToString(CultureInfo.InvariantCulture),
            double number => number.ToString(CultureInfo.InvariantCulture),
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static void LoadServerPresetsFromModel(
        TomlTable model,
        List<ServerPresetItem> presets,
        ref string activePreset,
        ref bool hasPresetSection)
    {
        if (TryGetSectionTable(model, "Server", out var serverTable))
        {
            if (TryGetValue(serverTable, "activepreset", out var active))
            {
                activePreset = ConvertTomlValue(active);
            }

            if (TryGetValue(serverTable, "Presets", out var serverPresets))
            {
                hasPresetSection = true;
                AddPresetsFromObject(serverPresets, presets);
            }
        }

        if (TryGetValue(model, "ServerPresets", out var legacyPresets))
        {
            hasPresetSection = true;
            AddPresetsFromObject(legacyPresets, presets);
        }
    }

    private static void AddPresetsFromObject(object value, List<ServerPresetItem> presets)
    {
        if (value is TomlTableArray array)
        {
            foreach (var table in array)
            {
                AddPresetFromTable(table, presets);
            }

            return;
        }

        if (value is TomlArray tomlArray)
        {
            foreach (var item in tomlArray)
            {
                if (item is TomlTable table)
                {
                    AddPresetFromTable(table, presets);
                }
            }
        }
    }

    private static void AddPresetFromTable(TomlTable table, List<ServerPresetItem> presets)
    {
        if (table == null)
        {
            return;
        }

        string name = TryGetValue(table, "name", out var rawName) ? ConvertTomlValue(rawName) : string.Empty;
        if (string.IsNullOrWhiteSpace(name)
            || presets.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        presets.Add(new ServerPresetItem
        {
            Name = name,
            ServerUrl = TryGetValue(table, "serverurl", out var serverUrl) ? ConvertTomlValue(serverUrl) : string.Empty,
            PcbId = TryGetValue(table, "pcbid", out var pcbId) ? ConvertTomlValue(pcbId) : string.Empty
        });
    }

    private static void LoadServerPresetsFromText(
        TomlLineDocument document,
        List<ServerPresetItem> presets,
        ref string activePreset,
        ref bool hasPresetSection)
    {
        document.LoadServerPresetsFromText(presets, ref activePreset, ref hasPresetSection);
    }

    private static bool EnsureServerPresetDefaults(
        List<ServerPresetItem> presets,
        string asphyxiaPresetName,
        string asphyxiaDefaultUrl)
    {
        var existing = presets.FirstOrDefault(p => string.Equals(p.Name, asphyxiaPresetName, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            presets.Add(new ServerPresetItem
            {
                Name = asphyxiaPresetName,
                ServerUrl = asphyxiaDefaultUrl,
                PcbId = string.Empty
            });
            return true;
        }

        if (!string.IsNullOrWhiteSpace(existing.ServerUrl))
        {
            return false;
        }

        existing.ServerUrl = asphyxiaDefaultUrl;
        return true;
    }

    private static string NormalizeName(string value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private sealed class TomlLineDocument
    {
        private readonly List<string> _lines;

        private TomlLineDocument(IEnumerable<string> lines)
        {
            _lines = lines?.ToList() ?? new List<string>();
        }

        public static TomlLineDocument Empty()
        {
            return new TomlLineDocument(Array.Empty<string>());
        }

        public static TomlLineDocument FromLines(IEnumerable<string> lines)
        {
            return new TomlLineDocument(lines);
        }

        public string ToText()
        {
            return string.Join(Environment.NewLine, _lines);
        }

        public void NormalizeBlankLines(bool preserveSectionSeparator)
        {
            TomlTextShared.NormalizeBlankLines(_lines, preserveSectionSeparator);
        }

        public bool TryReadString(string sectionName, string keyName, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(keyName))
            {
                return false;
            }

            if (!TryGetSectionBounds(sectionName, out _, out var contentStart, out var contentEndExclusive))
            {
                return false;
            }

            for (int i = contentStart; i < contentEndExclusive; i++)
            {
                if (!TrySplitKeyValue(_lines[i], out var parsedKey, out var rawValue, out _)
                    || !string.Equals(parsedKey, keyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value = ParseScalarToString(rawValue);
                return true;
            }

            return false;
        }

        public void UpsertString(string sectionName, string keyName, string value)
        {
            string valueLine = TomlTextShared.BuildStringLine(keyName, value ?? string.Empty);
            if (!TryGetSectionBounds(sectionName, out var headerIndex, out var contentStart, out var contentEndExclusive))
            {
                AppendSectionWithLine(sectionName, valueLine);
                return;
            }

            for (int i = contentStart; i < contentEndExclusive; i++)
            {
                if (!TrySplitKeyValue(_lines[i], out var parsedKey, out _, out var trailingComment)
                    || !string.Equals(parsedKey, keyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string indent = GetIndent(_lines[i]);
                _lines[i] = AppendTrailingComment(indent + valueLine, trailingComment);
                return;
            }

            int insertIndex = contentEndExclusive;
            while (insertIndex > contentStart && string.IsNullOrWhiteSpace(_lines[insertIndex - 1]))
            {
                insertIndex--;
            }

            if (headerIndex >= 0 || string.IsNullOrWhiteSpace(sectionName))
            {
                _lines.Insert(insertIndex, valueLine);
            }
        }

        public bool RenameSection(string sourceSection, string targetSection)
        {
            if (!TryGetSectionBounds(sourceSection, out var sourceHeaderIndex, out var sourceContentStart, out var sourceEndExclusive))
            {
                return false;
            }

            if (TryGetSectionBounds(targetSection, out _, out _, out _))
            {
                var sourceEntries = ReadSectionEntries(sourceContentStart, sourceEndExclusive);
                foreach (var entry in sourceEntries)
                {
                    if (!SectionContainsKey(targetSection, entry.Key))
                    {
                        UpsertString(targetSection, entry.Key, entry.Value);
                    }
                }

                RemoveRange(sourceHeaderIndex, sourceEndExclusive - sourceHeaderIndex);
                return true;
            }

            string trailingComment = TryGetHeaderTrailingComment(_lines[sourceHeaderIndex], out var comment)
                ? comment
                : string.Empty;
            _lines[sourceHeaderIndex] = AppendTrailingComment($"[{targetSection}]", trailingComment);
            return true;
        }

        public bool MoveKey(string sourceSection, string targetSection, string keyName)
        {
            if (!TryGetSectionBounds(sourceSection, out _, out var sourceContentStart, out var sourceEndExclusive))
            {
                return false;
            }

            for (int i = sourceContentStart; i < sourceEndExclusive; i++)
            {
                if (!TrySplitKeyValue(_lines[i], out var parsedKey, out var rawValue, out _)
                    || !string.Equals(parsedKey, keyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = ParseScalarToString(rawValue);
                _lines.RemoveAt(i);
                RemoveSectionIfEmpty(sourceSection);
                UpsertString(targetSection, keyName, value);
                return true;
            }

            return false;
        }

        public bool DeleteKey(string sectionName, string keyName)
        {
            if (!TryGetSectionBounds(sectionName, out _, out var contentStart, out var contentEndExclusive))
            {
                return false;
            }

            for (int i = contentStart; i < contentEndExclusive; i++)
            {
                if (!TrySplitKeyValue(_lines[i], out var parsedKey, out _, out _)
                    || !string.Equals(parsedKey, keyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _lines.RemoveAt(i);
                RemoveSectionIfEmpty(sectionName);
                return true;
            }

            return false;
        }

        public void RemoveArrayTableBlocks(string sectionName)
        {
            for (int i = 0; i < _lines.Count;)
            {
                if (!TryGetArraySectionName(_lines[i], out var parsedSection)
                    || !string.Equals(parsedSection, sectionName, StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    continue;
                }

                int end = i + 1;
                while (end < _lines.Count && !IsAnySectionHeader(_lines[end]))
                {
                    end++;
                }

                RemoveRange(i, end - i);
            }
        }

        public void AppendServerPreset(ServerPresetItem preset)
        {
            if (_lines.Count > 0 && !string.IsNullOrWhiteSpace(_lines[^1]))
            {
                _lines.Add(string.Empty);
            }

            _lines.Add("[[Server.Presets]]");
            _lines.Add(TomlTextShared.BuildStringLine("name", preset.Name ?? string.Empty));
            _lines.Add(TomlTextShared.BuildStringLine("serverurl", preset.ServerUrl ?? string.Empty));
            _lines.Add(TomlTextShared.BuildStringLine("pcbid", preset.PcbId ?? string.Empty));
        }

        public void LoadServerPresetsFromText(
            List<ServerPresetItem> presets,
            ref string activePreset,
            ref bool hasPresetSection)
        {
            ServerPresetItem current = null;
            bool inServerSection = false;

            void CommitCurrent()
            {
                if (current == null
                    || string.IsNullOrWhiteSpace(current.Name)
                    || presets.Any(p => string.Equals(p.Name, current.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                presets.Add(current);
            }

            foreach (var rawLine in _lines)
            {
                if (TryGetStandardSectionName(rawLine, out var standardSection))
                {
                    if (current != null)
                    {
                        CommitCurrent();
                        current = null;
                    }

                    inServerSection = string.Equals(standardSection, "Server", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (TryGetArraySectionName(rawLine, out var arraySection))
                {
                    if (current != null)
                    {
                        CommitCurrent();
                    }

                    bool isPresetSection = string.Equals(arraySection, "Server.Presets", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(arraySection, "ServerPresets", StringComparison.OrdinalIgnoreCase);
                    hasPresetSection |= isPresetSection;
                    current = isPresetSection ? new ServerPresetItem() : null;
                    inServerSection = false;
                    continue;
                }

                if (inServerSection)
                {
                    if (TrySplitKeyValue(rawLine, out var key, out var rawValue, out _)
                        && string.Equals(key, "activepreset", StringComparison.OrdinalIgnoreCase))
                    {
                        activePreset = ParseScalarToString(rawValue);
                    }

                    continue;
                }

                if (current == null
                    || !TrySplitKeyValue(rawLine, out var presetKey, out var presetRawValue, out _))
                {
                    continue;
                }

                string value = ParseScalarToString(presetRawValue);
                if (string.Equals(presetKey, "name", StringComparison.OrdinalIgnoreCase))
                {
                    current.Name = value;
                }
                else if (string.Equals(presetKey, "serverurl", StringComparison.OrdinalIgnoreCase))
                {
                    current.ServerUrl = value;
                }
                else if (string.Equals(presetKey, "pcbid", StringComparison.OrdinalIgnoreCase))
                {
                    current.PcbId = value;
                }
            }

            CommitCurrent();
        }

        private bool TryGetSectionBounds(
            string sectionName,
            out int headerIndex,
            out int contentStart,
            out int contentEndExclusive)
        {
            headerIndex = -1;
            contentStart = 0;
            contentEndExclusive = _lines.Count;

            if (string.IsNullOrWhiteSpace(sectionName))
            {
                for (int i = 0; i < _lines.Count; i++)
                {
                    if (IsAnySectionHeader(_lines[i]))
                    {
                        contentEndExclusive = i;
                        break;
                    }
                }

                return true;
            }

            for (int i = 0; i < _lines.Count; i++)
            {
                if (TryGetStandardSectionName(_lines[i], out var parsedSection)
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

            contentEndExclusive = _lines.Count;
            for (int i = contentStart; i < _lines.Count; i++)
            {
                if (IsAnySectionHeader(_lines[i]))
                {
                    contentEndExclusive = i;
                    break;
                }
            }

            return true;
        }

        private List<(string Key, string Value)> ReadSectionEntries(int contentStart, int contentEndExclusive)
        {
            var entries = new List<(string Key, string Value)>();
            for (int i = contentStart; i < contentEndExclusive; i++)
            {
                if (TrySplitKeyValue(_lines[i], out var key, out var rawValue, out _))
                {
                    entries.Add((key, ParseScalarToString(rawValue)));
                }
            }

            return entries;
        }

        private bool SectionContainsKey(string sectionName, string keyName)
        {
            if (!TryGetSectionBounds(sectionName, out _, out var contentStart, out var contentEndExclusive))
            {
                return false;
            }

            for (int i = contentStart; i < contentEndExclusive; i++)
            {
                if (TrySplitKeyValue(_lines[i], out var parsedKey, out _, out _)
                    && string.Equals(parsedKey, keyName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveSectionIfEmpty(string sectionName)
        {
            if (string.IsNullOrWhiteSpace(sectionName)
                || !TryGetSectionBounds(sectionName, out var headerIndex, out var contentStart, out var contentEndExclusive))
            {
                return;
            }

            if (contentEndExclusive <= contentStart)
            {
                RemoveRange(headerIndex, contentEndExclusive - headerIndex);
            }
        }

        private void AppendSectionWithLine(string sectionName, string valueLine)
        {
            if (_lines.Count > 0 && !string.IsNullOrWhiteSpace(_lines[^1]))
            {
                _lines.Add(string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(sectionName))
            {
                _lines.Add($"[{sectionName}]");
            }

            _lines.Add(valueLine);
        }

        private void RemoveRange(int index, int count)
        {
            if (count <= 0)
            {
                return;
            }

            _lines.RemoveRange(index, count);
        }

        private static bool IsAnySectionHeader(string line)
        {
            return TryGetStandardSectionName(line, out _) || TryGetArraySectionName(line, out _);
        }

        private static bool TryGetStandardSectionName(string line, out string sectionName)
        {
            sectionName = string.Empty;
            string header = StripInlineComment(line).Trim();
            if (string.IsNullOrWhiteSpace(header)
                || !header.StartsWith("[", StringComparison.Ordinal)
                || !header.EndsWith("]", StringComparison.Ordinal)
                || header.StartsWith("[[", StringComparison.Ordinal)
                || header.EndsWith("]]", StringComparison.Ordinal))
            {
                return false;
            }

            sectionName = header.Substring(1, header.Length - 2).Trim();
            return !string.IsNullOrWhiteSpace(sectionName);
        }

        private static bool TryGetArraySectionName(string line, out string sectionName)
        {
            sectionName = string.Empty;
            string header = StripInlineComment(line).Trim();
            if (string.IsNullOrWhiteSpace(header)
                || !header.StartsWith("[[", StringComparison.Ordinal)
                || !header.EndsWith("]]", StringComparison.Ordinal)
                || header.Length <= 4)
            {
                return false;
            }

            sectionName = header.Substring(2, header.Length - 4).Trim();
            return !string.IsNullOrWhiteSpace(sectionName);
        }

        private static bool TryGetHeaderTrailingComment(string line, out string comment)
        {
            comment = ExtractTrailingComment(line, out _);
            return !string.IsNullOrWhiteSpace(comment);
        }

        private static bool TrySplitKeyValue(string line, out string key, out string rawValue, out string trailingComment)
        {
            key = string.Empty;
            rawValue = string.Empty;
            trailingComment = string.Empty;

            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                return false;
            }

            int equalsIndex = FindUnquotedEquals(line);
            if (equalsIndex <= 0)
            {
                return false;
            }

            key = NormalizeKeyText(line.Substring(0, equalsIndex).Trim());
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            string valueWithComment = line.Substring(equalsIndex + 1);
            trailingComment = ExtractTrailingComment(valueWithComment, out rawValue);
            rawValue = rawValue.Trim();
            return true;
        }

        private static int FindUnquotedEquals(string line)
        {
            bool inDoubleQuote = false;
            bool inSingleQuote = false;
            bool escaped = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
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

                if (ch == '=' && !inDoubleQuote && !inSingleQuote)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string ExtractTrailingComment(string text, out string valuePart)
        {
            bool inDoubleQuote = false;
            bool inSingleQuote = false;
            bool escaped = false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
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
                    valuePart = text.Substring(0, i);
                    return text.Substring(i).TrimEnd();
                }
            }

            valuePart = text;
            return string.Empty;
        }

        private static string StripInlineComment(string line)
        {
            ExtractTrailingComment(line ?? string.Empty, out var valuePart);
            return valuePart;
        }

        private static string ParseScalarToString(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return string.Empty;
            }

            try
            {
                var table = TomlSerializer.Deserialize<TomlTable>($"value = {rawValue}") ?? new TomlTable();
                return table.TryGetValue("value", out var value)
                    ? ConvertTomlValue(value)
                    : string.Empty;
            }
            catch
            {
                return ParseLegacyScalar(rawValue);
            }
        }

        private static string ParseLegacyScalar(string rawValue)
        {
            string value = StripInlineComment(rawValue).Trim();
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                return UnescapeBasicString(value.Substring(1, value.Length - 2));
            }

            if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
            {
                return value.Substring(1, value.Length - 2);
            }

            return value;
        }

        private static string UnescapeBasicString(string value)
        {
            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (ch != '\\' || i + 1 >= value.Length)
                {
                    builder.Append(ch);
                    continue;
                }

                char escaped = value[++i];
                builder.Append(escaped switch
                {
                    'b' => '\b',
                    't' => '\t',
                    'n' => '\n',
                    'f' => '\f',
                    'r' => '\r',
                    '"' => '"',
                    '\\' => '\\',
                    _ => escaped
                });
            }

            return builder.ToString();
        }

        private static string NormalizeKeyText(string key)
        {
            if (key.Length >= 2 && key[0] == '"' && key[^1] == '"')
            {
                return ParseLegacyScalar(key);
            }

            if (key.Length >= 2 && key[0] == '\'' && key[^1] == '\'')
            {
                return key.Substring(1, key.Length - 2);
            }

            return key;
        }

        private static string GetIndent(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return string.Empty;
            }

            int length = 0;
            while (length < line.Length && char.IsWhiteSpace(line[length]))
            {
                length++;
            }

            return length == 0 ? string.Empty : line.Substring(0, length);
        }

        private static string AppendTrailingComment(string line, string comment)
        {
            return string.IsNullOrWhiteSpace(comment)
                ? line
                : $"{line} {comment.TrimStart()}";
        }
    }
}
