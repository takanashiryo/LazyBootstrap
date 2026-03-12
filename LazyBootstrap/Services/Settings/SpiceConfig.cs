using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Avalonia.Controls;

namespace LazyBootstrap
{
    public partial class MainWindow
    {
        private void UpdateSpiceConfig(params OptionUpdate[] updates)
        {
            try
            {
                if (updates == null || updates.Length == 0)
                {
                    updates = BuildDefaultOptionUpdates().ToArray();
                }

                if (updates.Length == 0) return;

                string spiceXmlPath = GetSpiceXmlPath();
                if (!File.Exists(spiceXmlPath))
                {
                    ShowErrorToast("保存设定失败", "未找到 spicetools.xml。");
                    RestoreUiFromLastKnownSpiceValues();
                    return;
                }

                if (!TryGetSpiceOptionsContext(LoadOptions.PreserveWhitespace, true, out var context))
                {
                    ShowErrorToast("保存设定失败", "配置写入失败。");
                    RestoreUiFromLastKnownSpiceValues();
                    return;
                }

                var doc = context.Document;
                var soundVoltex = context.SoundVoltex;
                var options = context.OptionsElement;

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

                foreach (var update in updates)
                {
                    if (update == null || string.IsNullOrEmpty(update.Name)) continue;

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
                        var newOpt = new XElement("option",
                            new XAttribute("name", update.Name),
                            new XAttribute("value", update.Value ?? string.Empty));
                        closingWhitespace.AddBeforeSelf(newOpt);
                        context.OptionLookup[update.Name] = newOpt;
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

                var settings = new XmlWriterSettings
                {
                    Indent = false,
                    NewLineHandling = NewLineHandling.None,
                    Encoding = new System.Text.UTF8Encoding(false),
                    OmitXmlDeclaration = false,
                    NewLineChars = newline,
                    NewLineOnAttributes = false
                };
                using (var writer = XmlWriter.Create(context.FilePath, settings))
                {
                    doc.Save(writer);
                }

                NormalizeSelfClosingTags(context.FilePath);
            }
            catch (Exception ex)
            {
                ShowErrorToast("保存设定失败", ex.Message);
            }
        }

        private void LoadSpiceConfig()
        {
            bool previousLoadingState = _isLoadingSettings;
            _isLoadingSettings = true;
            try
            {
                if (!TryGetSpiceOptionsContext(LoadOptions.PreserveWhitespace, false, out var context))
                {
                    return;
                }

                string GetValue(string name) => context.GetOptionValue(name);

                CacheLastKnownSpiceValue("w", GetValue("w"));
                CacheLastKnownSpiceValue("sp2x-processefficiency", GetValue("sp2x-processefficiency"));
                CacheLastKnownSpiceValue("sp2x-sdvxnosub", GetValue("sp2x-sdvxnosub"));
                CacheLastKnownSpiceValue("sp2x-windowborder", GetValue("sp2x-windowborder"));
                CacheLastKnownSpiceValue("sdvxwsubborderless", GetValue("sdvxwsubborderless"));
                CacheLastKnownSpiceValue("s", GetValue("s"));
                CacheLastKnownSpiceValue("sp2x-windowalwaysontop", GetValue("sp2x-windowalwaysontop"));
                CacheLastKnownSpiceValue("sp2x-windowsize", GetValue("sp2x-windowsize"));
                CacheLastKnownSpiceValue("graphics-force-single-adapter", GetValue("graphics-force-single-adapter"));
                CacheLastKnownSpiceValue("sdvxwsubtop", GetValue("sdvxwsubtop"));
                CacheLastKnownSpiceValue("sp2x-sdvxsubredraw", GetValue("sp2x-sdvxsubredraw"));
                CacheLastKnownSpiceValue("sdvxnativetouch", GetValue("sdvxnativetouch"));
                CacheLastKnownSpiceValue("sp2x-sdvxasio", GetValue("sp2x-sdvxasio"));
                CacheLastKnownSpiceValue("cardio", GetValue("cardio"));
                CacheLastKnownSpiceValue("scard", GetValue("scard"));
                CacheLastKnownSpiceValue("netdump", GetValue("netdump"));
                CacheLastKnownSpiceValue("url", GetValue("url"));
                CacheLastKnownSpiceValue("p", GetValue("p"));

                var wVal = GetValue("w");
                bool windowed = string.Equals(wVal, "/ENABLED", StringComparison.OrdinalIgnoreCase);
                if (WindowedToggleSwitch != null) WindowedToggleSwitch.IsChecked = windowed;

                var peVal = GetValue("sp2x-processefficiency");
                _pCoreOptimization = string.Equals(peVal, "pcores", StringComparison.OrdinalIgnoreCase);

                _disableSubDisplay = string.Equals(GetValue("sp2x-sdvxnosub"), "/ENABLED", StringComparison.Ordinal);
                var wborder = GetValue("sp2x-windowborder");
                if (string.Equals(wborder, "1", StringComparison.Ordinal)) _windowModeIndex = 1;
                else if (string.Equals(wborder, "2", StringComparison.Ordinal)) _windowModeIndex = 2;
                else _windowModeIndex = 0;
                _subBorderless = string.Equals(GetValue("sdvxwsubborderless"), "/ENABLED", StringComparison.Ordinal);
                _showCursorTouchSim = string.Equals(GetValue("s"), "/ENABLED", StringComparison.Ordinal);
                _windowTopMost = string.Equals(GetValue("sp2x-windowalwaysontop"), "/ENABLED", StringComparison.Ordinal);
                _windowSize = GetValue("sp2x-windowsize") ?? string.Empty;
                _singleAdapter = string.Equals(GetValue("graphics-force-single-adapter"), "/ENABLED", StringComparison.Ordinal);
                _subWindowTopMost = string.Equals(GetValue("sdvxwsubtop"), "/ENABLED", StringComparison.Ordinal);
                _subForceRender = string.Equals(GetValue("sp2x-sdvxsubredraw"), "/ENABLED", StringComparison.Ordinal);
                _nativeTouch = string.Equals(GetValue("sdvxnativetouch"), "/ENABLED", StringComparison.Ordinal);
                _asioDriver = GetValue("sp2x-sdvxasio") ?? string.Empty;
                _cardIo = string.Equals(GetValue("cardio"), "/ENABLED", StringComparison.Ordinal);
                _hidSmartCard = string.Equals(GetValue("scard"), "/ENABLED", StringComparison.Ordinal);
                _dbgNetDump = string.Equals(GetValue("netdump"), "/ENABLED", StringComparison.Ordinal);
                if (ServerAddressTextBox != null) ServerAddressTextBox.Text = GetValue("url");
                if (PcbIdTextBox != null) PcbIdTextBox.Text = GetValue("p");

                if (NetDumpToggleSwitch != null) NetDumpToggleSwitch.IsChecked = _dbgNetDump;
                if (DisableSubDisplayToggleSwitch != null) DisableSubDisplayToggleSwitch.IsChecked = _disableSubDisplay;
                if (WindowModeComboBox != null) WindowModeComboBox.SelectedIndex = _windowModeIndex;
                if (PCoreOptimizationToggleSwitch != null) PCoreOptimizationToggleSwitch.IsChecked = _pCoreOptimization;
                if (SubBorderlessToggleSwitch != null) SubBorderlessToggleSwitch.IsChecked = _subBorderless;
                if (ShowCursorTouchSimToggleSwitch != null) ShowCursorTouchSimToggleSwitch.IsChecked = _showCursorTouchSim;
                if (WindowTopMostToggleSwitch != null) WindowTopMostToggleSwitch.IsChecked = _windowTopMost;
                if (WindowSizeTextBox != null) WindowSizeTextBox.Text = _windowSize;
                if (SingleAdapterToggleSwitch != null) SingleAdapterToggleSwitch.IsChecked = _singleAdapter;
                if (SubWindowTopMostToggleSwitch != null) SubWindowTopMostToggleSwitch.IsChecked = _subWindowTopMost;
                if (SubForceRenderToggleSwitch != null) SubForceRenderToggleSwitch.IsChecked = _subForceRender;
                if (NativeTouchToggleSwitch != null) NativeTouchToggleSwitch.IsChecked = _nativeTouch;
                RefreshAsioDriverChoices(_asioDriver);
                if (CardIoToggleSwitch != null) CardIoToggleSwitch.IsChecked = _cardIo;
                if (HidSmartCardToggleSwitch != null) HidSmartCardToggleSwitch.IsChecked = _hidSmartCard;

                SelectPresetByCurrentFields();
            }
            catch (Exception ex)
            {
                ShowErrorToast("读取配置失败", ex.Message);
            }
            finally
            {
                _isLoadingSettings = previousLoadingState;
            }
        }

        private void CacheLastKnownSpiceValue(string key, string value)
        {
            _lastKnownSpiceValues[key] = value ?? string.Empty;
        }

        private string GetLastKnownSpiceValue(string key)
        {
            return _lastKnownSpiceValues.TryGetValue(key, out var value) ? value : string.Empty;
        }

        private void RestoreUiFromLastKnownSpiceValues()
        {
            if (_lastKnownSpiceValues.Count == 0)
            {
                return;
            }

            bool previousLoadingState = _isLoadingSettings;
            _isLoadingSettings = true;
            try
            {
                if (WindowedToggleSwitch != null)
                {
                    WindowedToggleSwitch.IsChecked = string.Equals(GetLastKnownSpiceValue("w"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                }

                _pCoreOptimization = string.Equals(GetLastKnownSpiceValue("sp2x-processefficiency"), "pcores", StringComparison.OrdinalIgnoreCase);
                _disableSubDisplay = string.Equals(GetLastKnownSpiceValue("sp2x-sdvxnosub"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _subBorderless = string.Equals(GetLastKnownSpiceValue("sdvxwsubborderless"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _showCursorTouchSim = string.Equals(GetLastKnownSpiceValue("s"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _windowTopMost = string.Equals(GetLastKnownSpiceValue("sp2x-windowalwaysontop"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _windowSize = GetLastKnownSpiceValue("sp2x-windowsize");
                _singleAdapter = string.Equals(GetLastKnownSpiceValue("graphics-force-single-adapter"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _subWindowTopMost = string.Equals(GetLastKnownSpiceValue("sdvxwsubtop"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _subForceRender = string.Equals(GetLastKnownSpiceValue("sp2x-sdvxsubredraw"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _nativeTouch = string.Equals(GetLastKnownSpiceValue("sdvxnativetouch"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _asioDriver = GetLastKnownSpiceValue("sp2x-sdvxasio");
                _cardIo = string.Equals(GetLastKnownSpiceValue("cardio"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _hidSmartCard = string.Equals(GetLastKnownSpiceValue("scard"), "/ENABLED", StringComparison.OrdinalIgnoreCase);
                _dbgNetDump = string.Equals(GetLastKnownSpiceValue("netdump"), "/ENABLED", StringComparison.OrdinalIgnoreCase);

                var wborder = GetLastKnownSpiceValue("sp2x-windowborder");
                if (string.Equals(wborder, "1", StringComparison.Ordinal)) _windowModeIndex = 1;
                else if (string.Equals(wborder, "2", StringComparison.Ordinal)) _windowModeIndex = 2;
                else _windowModeIndex = 0;

                if (DisableSubDisplayToggleSwitch != null) DisableSubDisplayToggleSwitch.IsChecked = _disableSubDisplay;
                if (NetDumpToggleSwitch != null) NetDumpToggleSwitch.IsChecked = _dbgNetDump;
                if (PCoreOptimizationToggleSwitch != null) PCoreOptimizationToggleSwitch.IsChecked = _pCoreOptimization;
                if (SubBorderlessToggleSwitch != null) SubBorderlessToggleSwitch.IsChecked = _subBorderless;
                if (ShowCursorTouchSimToggleSwitch != null) ShowCursorTouchSimToggleSwitch.IsChecked = _showCursorTouchSim;
                if (WindowTopMostToggleSwitch != null) WindowTopMostToggleSwitch.IsChecked = _windowTopMost;
                if (WindowSizeTextBox != null) WindowSizeTextBox.Text = _windowSize;
                if (SingleAdapterToggleSwitch != null) SingleAdapterToggleSwitch.IsChecked = _singleAdapter;
                if (SubWindowTopMostToggleSwitch != null) SubWindowTopMostToggleSwitch.IsChecked = _subWindowTopMost;
                if (SubForceRenderToggleSwitch != null) SubForceRenderToggleSwitch.IsChecked = _subForceRender;
                if (NativeTouchToggleSwitch != null) NativeTouchToggleSwitch.IsChecked = _nativeTouch;
                RefreshAsioDriverChoices(_asioDriver);
                if (CardIoToggleSwitch != null) CardIoToggleSwitch.IsChecked = _cardIo;
                if (HidSmartCardToggleSwitch != null) HidSmartCardToggleSwitch.IsChecked = _hidSmartCard;
                if (WindowModeComboBox != null) WindowModeComboBox.SelectedIndex = _windowModeIndex;

                if (ServerAddressTextBox != null) ServerAddressTextBox.Text = GetLastKnownSpiceValue("url");
                if (PcbIdTextBox != null) PcbIdTextBox.Text = GetLastKnownSpiceValue("p");

                SelectPresetByCurrentFields();
            }
            finally
            {
                _isLoadingSettings = previousLoadingState;
            }
        }

        private IEnumerable<OptionUpdate> BuildDefaultOptionUpdates()
        {
            yield return new OptionUpdate("w", WindowedToggleSwitch != null && WindowedToggleSwitch.IsChecked == true ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sp2x-processefficiency", _pCoreOptimization ? "pcores" : string.Empty);
            yield return new OptionUpdate("sp2x-dx9on12", ResolveDxModeValue(), false);
            yield return new OptionUpdate("sp2x-sdvxnosub", _disableSubDisplay ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sp2x-windowborder", ResolveWindowBorderValue());
            yield return new OptionUpdate("sdvxwsubborderless", _subBorderless ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("s", _showCursorTouchSim ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sp2x-windowalwaysontop", _windowTopMost ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sp2x-windowsize", _windowSize ?? string.Empty);
            yield return new OptionUpdate("graphics-force-single-adapter", _singleAdapter ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sdvxwsubtop", _subWindowTopMost ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sp2x-sdvxsubredraw", _subForceRender ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sdvxnativetouch", _nativeTouch ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("sp2x-sdvxasio", _asioDriver ?? string.Empty);
            yield return new OptionUpdate("cardio", _cardIo ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("scard", _hidSmartCard ? "/ENABLED" : string.Empty);
            yield return new OptionUpdate("netdump", _dbgNetDump ? "/ENABLED" : string.Empty);
            if (ServerAddressTextBox != null) yield return new OptionUpdate("url", ServerAddressTextBox.Text ?? string.Empty, false);
            if (PcbIdTextBox != null) yield return new OptionUpdate("p", PcbIdTextBox.Text ?? string.Empty, false);
        }

        private bool EnsureSpiceXmlExistsForTextOrRevert(TextBox textBox, string optionName)
        {
            var xmlPath = GetSpiceXmlPath();
            if (File.Exists(xmlPath))
            {
                return true;
            }

            ShowErrorToast("保存设定失败", "未找到 spicetools.xml。");

            _isUpdatingSpiceToggleUi = true;
            try
            {
                if (textBox != null)
                {
                    textBox.Text = GetLastKnownSpiceValue(optionName);
                }
            }
            finally
            {
                _isUpdatingSpiceToggleUi = false;
            }

            return false;
        }

        private string ResolveWindowBorderValue()
        {
            switch (_windowModeIndex)
            {
                case 1:
                    return "1";
                case 2:
                    return "2";
                default:
                    return string.Empty;
            }
        }

        private bool TryGetSpiceOptionsContext(LoadOptions loadOptions, bool createOptionsWhenMissing, out SpiceOptionsContext context)
        {
            string spiceXmlPath = GetSpiceXmlPath();
            return TryGetSpiceOptionsContext(spiceXmlPath, loadOptions, createOptionsWhenMissing, out context);
        }

        private bool TryGetSpiceOptionsContext(string spiceXmlPath, LoadOptions loadOptions, bool createOptionsWhenMissing, out SpiceOptionsContext context)
        {
            context = null;
            if (!File.Exists(spiceXmlPath))
            {
                return false;
            }

            var doc = XDocument.Load(spiceXmlPath, loadOptions);
            var root = doc.Root;
            if (root == null)
            {
                ShowErrorToast("读取配置失败", "SpiceTools XML 根节点为空。");
                return false;
            }

            var soundVoltex = root.Elements("game").FirstOrDefault(g =>
            {
                var nameAttr = g.Attribute("name");
                return nameAttr != null && string.Equals(nameAttr.Value, "Sound Voltex", StringComparison.OrdinalIgnoreCase);
            });
            if (soundVoltex == null)
            {
                ShowWarningToast("读取配置异常", "未找到游戏条目: Sound Voltex。");
                return false;
            }

            var options = soundVoltex.Element("options");
            if (options == null)
            {
                if (createOptionsWhenMissing)
                {
                    options = new XElement("options");
                    soundVoltex.Add(options);
                }
                else
                {
                    return false;
                }
            }

            var lookup = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (var option in options.Elements("option"))
            {
                var nameAttr = option.Attribute("name");
                if (nameAttr == null) continue;
                var key = nameAttr.Value;
                if (!lookup.ContainsKey(key))
                {
                    lookup[key] = option;
                }
            }

            context = new SpiceOptionsContext(spiceXmlPath, doc, soundVoltex, options, lookup);
            return true;
        }

        private string ExtractIndentation(XText textNode, ref string newlineChars)
        {
            if (textNode == null) return null;

            var text = textNode.Value;
            if (text.Contains("\r\n")) newlineChars = "\r\n";
            else if (text.Contains("\n")) newlineChars = "\n";
            else if (text.Contains("\r")) newlineChars = "\r";

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

        private string DetermineIndentStep(XElement parentElement, ref string newlineChars)
        {
            if (parentElement == null) return null;

            foreach (var container in parentElement.Elements())
            {
                if (!container.HasElements) continue;

                var containerIndent = ExtractIndentation(container.PreviousNode as XText, ref newlineChars);
                var child = container.Elements().FirstOrDefault();
                var childIndent = ExtractIndentation(child?.PreviousNode as XText, ref newlineChars);

                if (!string.IsNullOrEmpty(containerIndent) && !string.IsNullOrEmpty(childIndent) && childIndent.StartsWith(containerIndent))
                {
                    return childIndent.Substring(containerIndent.Length);
                }
            }

            return null;
        }

        private XText EnsureClosingWhitespace(XElement optionsElement, string desiredValue)
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

        private void NormalizeSelfClosingTags(string filePath)
        {
            try
            {
                var original = File.ReadAllText(filePath, Encoding.UTF8);
                var normalized = Regex.Replace(original, "(?<=\\S)[ \\\t]+/>", "/>" );
                if (!string.Equals(original, normalized, StringComparison.Ordinal))
                {
                    File.WriteAllText(filePath, normalized, TomlTextShared.Utf8NoBom);
                }
            }
            catch (Exception ex)
            {
                ShowWarningToast("配置格式修复失败", ex.Message);
            }
        }

        private sealed class SpiceOptionsContext
        {
            public string FilePath { get; }
            public XDocument Document { get; }
            public XElement SoundVoltex { get; }
            public XElement OptionsElement { get; }
            public Dictionary<string, XElement> OptionLookup { get; }

            public SpiceOptionsContext(string filePath, XDocument document, XElement soundVoltex, XElement optionsElement, Dictionary<string, XElement> optionLookup)
            {
                FilePath = filePath;
                Document = document;
                SoundVoltex = soundVoltex;
                OptionsElement = optionsElement;
                OptionLookup = optionLookup;
            }

            public string GetOptionValue(string name)
            {
                if (OptionLookup.TryGetValue(name, out var element))
                {
                    return element.Attribute("value")?.Value ?? string.Empty;
                }
                return string.Empty;
            }
        }

        private sealed class OptionUpdate
        {
            public string Name { get; }
            public string Value { get; }
            public bool RemoveWhenEmpty { get; }

            public OptionUpdate(string name, string value, bool removeWhenEmpty = false)
            {
                Name = name;
                Value = value ?? string.Empty;
                RemoveWhenEmpty = removeWhenEmpty;
            }

            public bool ShouldRemove => RemoveWhenEmpty && string.IsNullOrEmpty(Value);
        }
    }
}
