using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace LazyBootstrap.Services.Config
{
    public sealed class SpiceConfigFileService
    {
        public bool TryLoadOptionsContext(string spiceXmlPath, LoadOptions loadOptions, bool createOptionsWhenMissing, out SpiceOptionsContext context, out string message, out bool warning)
        {
            context = null;
            message = string.Empty;
            warning = false;

            if (!File.Exists(spiceXmlPath))
            {
                return false;
            }

            var document = XDocument.Load(spiceXmlPath, loadOptions);
            var root = document.Root;
            if (root == null)
            {
                message = "SpiceTools XML 根节点为空。";
                return false;
            }

            var soundVoltex = root.Elements("game").FirstOrDefault(game =>
            {
                var nameAttribute = game.Attribute("name");
                return nameAttribute != null
                    && string.Equals(nameAttribute.Value, "Sound Voltex", StringComparison.OrdinalIgnoreCase);
            });
            if (soundVoltex == null)
            {
                message = "未找到游戏条目: Sound Voltex。";
                warning = true;
                return false;
            }

            var options = soundVoltex.Element("options");
            if (options == null)
            {
                if (!createOptionsWhenMissing)
                {
                    return false;
                }

                options = new XElement("options");
                soundVoltex.Add(options);
            }

            var lookup = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (var option in options.Elements("option"))
            {
                var nameAttribute = option.Attribute("name");
                if (nameAttribute == null)
                {
                    continue;
                }

                var key = nameAttribute.Value;
                if (!lookup.ContainsKey(key))
                {
                    lookup[key] = option;
                }
            }

            context = new SpiceOptionsContext(spiceXmlPath, document, soundVoltex, options, lookup);
            return true;
        }

        public void ApplyUpdates(SpiceOptionsContext context, IEnumerable<SpiceOptionUpdate> updates)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(updates);

            var updateList = updates
                .Where(update => update != null && !string.IsNullOrEmpty(update.Name))
                .ToList();
            if (updateList.Count == 0)
            {
                return;
            }

            var options = context.OptionsElement;
            var soundVoltex = context.SoundVoltex;
            string newline = "\r\n";
            string optionsIndent = ExtractIndentation(options.PreviousNode as XText, ref newline) ?? string.Empty;
            string indentStep = DetermineIndentStep(soundVoltex, ref newline) ?? new string(' ', 4);

            string optionIndent = ExtractIndentation(options.Elements("option").FirstOrDefault()?.PreviousNode as XText, ref newline);
            if (string.IsNullOrEmpty(optionIndent))
            {
                optionIndent = optionsIndent + indentStep;
            }

            string optionLinePrefix = newline + optionIndent;
            string closingLinePrefix = newline + optionsIndent;
            var closingWhitespace = EnsureClosingWhitespace(options, closingLinePrefix);

            foreach (var update in updateList)
            {
                context.OptionLookup.TryGetValue(update.Name, out var existing);

                if (existing == null)
                {
                    if (update.ShouldRemove || string.IsNullOrEmpty(update.Value))
                    {
                        continue;
                    }

                    if (closingWhitespace == null)
                    {
                        closingWhitespace = EnsureClosingWhitespace(options, closingLinePrefix);
                    }

                    closingWhitespace.AddBeforeSelf(new XText(optionLinePrefix));
                    var newOption = CreateOptionElement(update);
                    closingWhitespace.AddBeforeSelf(newOption);
                    context.OptionLookup[update.Name] = newOption;
                    continue;
                }

                if (update.ShouldRemove)
                {
                    var whitespace = existing.PreviousNode as XText;
                    existing.Remove();
                    if (whitespace != null && string.IsNullOrWhiteSpace(whitespace.Value))
                    {
                        whitespace.Remove();
                    }

                    context.OptionLookup.Remove(update.Name);
                    continue;
                }

                existing.SetAttributeValue("value", update.Value ?? string.Empty);
            }

            SaveDocument(context.Document, context.FilePath, newline);
        }

        public bool ApplySpiceOptions(string spiceXmlPath, IEnumerable<SpiceOptionUpdate> updates, out string error)
        {
            error = string.Empty;

            if (!TryLoadOptionsContext(spiceXmlPath, LoadOptions.PreserveWhitespace, true, out var context, out var message, out _))
            {
                error = string.IsNullOrWhiteSpace(message) ? "Unable to load spice config file." : message;
                return false;
            }

            ApplyUpdates(context, updates);
            return true;
        }

        private static XElement CreateOptionElement(SpiceOptionUpdate update)
        {
            return new XElement(
                "option",
                new XAttribute("name", update.Name),
                new XAttribute("value", update.Value ?? string.Empty));
        }

        private static string SaveDocument(XDocument document, string filePath, string newline)
        {
            var settings = new XmlWriterSettings
            {
                Indent = false,
                NewLineHandling = NewLineHandling.None,
                Encoding = new UTF8Encoding(false),
                OmitXmlDeclaration = false,
                NewLineChars = newline,
                NewLineOnAttributes = false
            };

            using var stream = new MemoryStream();
            using (var writer = XmlWriter.Create(stream, settings))
            {
                document.Save(writer);
            }

            string content = settings.Encoding.GetString(stream.ToArray());
            string normalizationWarning = TryNormalizeSelfClosingTags(ref content);
            File.WriteAllText(filePath, content, TomlTextShared.Utf8NoBom);
            return normalizationWarning;
        }

        private static string ExtractIndentation(XText textNode, ref string newlineChars)
        {
            if (textNode == null)
            {
                return null;
            }

            var text = textNode.Value;
            if (text.Contains("\r\n"))
            {
                newlineChars = "\r\n";
            }
            else if (text.Contains("\n"))
            {
                newlineChars = "\n";
            }
            else if (text.Contains("\r"))
            {
                newlineChars = "\r";
            }

            int lastNewlineIndex = text.LastIndexOf('\n');
            if (lastNewlineIndex < text.LastIndexOf('\r'))
            {
                lastNewlineIndex = text.LastIndexOf('\r');
            }

            if (lastNewlineIndex >= 0 && lastNewlineIndex + 1 < text.Length)
            {
                int start = lastNewlineIndex + 1;
                while (start < text.Length && (text[start] == '\r' || text[start] == '\n'))
                {
                    start++;
                }

                return text.Substring(start);
            }

            return text;
        }

        private static string DetermineIndentStep(XElement parentElement, ref string newlineChars)
        {
            if (parentElement == null)
            {
                return null;
            }

            foreach (var container in parentElement.Elements())
            {
                if (!container.HasElements)
                {
                    continue;
                }

                var containerIndent = ExtractIndentation(container.PreviousNode as XText, ref newlineChars);
                var child = container.Elements().FirstOrDefault();
                var childIndent = ExtractIndentation(child?.PreviousNode as XText, ref newlineChars);

                if (!string.IsNullOrEmpty(containerIndent)
                    && !string.IsNullOrEmpty(childIndent)
                    && childIndent.StartsWith(containerIndent, StringComparison.Ordinal))
                {
                    return childIndent.Substring(containerIndent.Length);
                }
            }

            return null;
        }

        private static XText EnsureClosingWhitespace(XElement optionsElement, string desiredValue)
        {
            var lastNode = optionsElement.Nodes().LastOrDefault();
            if (lastNode is XText textNode)
            {
                textNode.Value = desiredValue;
                return textNode;
            }

            var newTextNode = new XText(desiredValue);
            optionsElement.Add(newTextNode);
            return newTextNode;
        }

        private static string TryNormalizeSelfClosingTags(ref string content)
        {
            try
            {
                content = Regex.Replace(content ?? string.Empty, "(?<=\\S)[ \\\t]+/>", "/>");

                return string.Empty;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }

    public sealed class SpiceOptionsContext
    {
        public SpiceOptionsContext(string filePath, XDocument document, XElement soundVoltex, XElement optionsElement, Dictionary<string, XElement> optionLookup)
        {
            FilePath = filePath;
            Document = document;
            SoundVoltex = soundVoltex;
            OptionsElement = optionsElement;
            OptionLookup = optionLookup;
        }

        public string FilePath { get; }

        public XDocument Document { get; }

        public XElement SoundVoltex { get; }

        public XElement OptionsElement { get; }

        public Dictionary<string, XElement> OptionLookup { get; }

        public string GetOptionValue(string name)
        {
            if (OptionLookup.TryGetValue(name, out var element))
            {
                return element.Attribute("value")?.Value ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
