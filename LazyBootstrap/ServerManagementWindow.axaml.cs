using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SukiUI.Controls;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace LazyBootstrap
{
    public partial class ServerManagementWindow : SukiWindow
    {
        private readonly string _configFilePath;
        private readonly ConfigHandler _configFile;
        private bool _isLoadingData = false;
        public bool Confirmed { get; private set; } = false;

        // 用于检测用户是否自定义了配置
        private string _originalServerAddress = string.Empty;
        private string _originalPcbId = string.Empty;

        public ServerManagementWindow()
        {
            InitializeComponent();

            // 获取路径
            var envBaseDir = Environment.GetEnvironmentVariable("LAZYBOOTSTRAP_BASEDIR");
            string baseDir = !string.IsNullOrWhiteSpace(envBaseDir) ? envBaseDir : AppDomain.CurrentDomain.BaseDirectory;
            _configFilePath = Path.Combine(baseDir, "config.toml");
            _configFile = new ConfigHandler(_configFilePath);

            // 初始化预设下拉框
            if (cmbPreset.Items.Count > 0)
            {
                cmbPreset.SelectedIndex = 0;
            }

            // 加载当前配置（优先 XML，config.toml 仅用于记录）
            LoadServerConfig();
        }

        private string GetSpiceXmlPath()
        {
            // 读取是否使用预配置
            string usePreconfigStr = _configFile.ReadString("Settings", "usepreconfig", "true");
            bool usePreconfig = true;
            if (!bool.TryParse(usePreconfigStr, out usePreconfig))
            {
                usePreconfig = true;
            }

            if (usePreconfig)
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string contentsDir = Path.Combine(baseDir, "contents");
                return Path.Combine(contentsDir, "lazy", "spicetools.xml");
            }
            else
            {
                string appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appDataDir, "spicetools.xml");
            }
        }

        private void cmbPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingData) return;

            if (cmbPreset.SelectedIndex == 0)
            {
                txtServerAddress.Text = "http://localhost:8083";
                txtPcbId.Text = string.Empty;
            }
        }

        private void LoadServerConfig()
        {
            _isLoadingData = true;
            try
            {
                string spiceXmlPath = GetSpiceXmlPath();
                if (!File.Exists(spiceXmlPath))
                {
                    return;
                }

                var doc = XDocument.Load(spiceXmlPath, LoadOptions.PreserveWhitespace);
                var root = doc.Root;
                if (root == null) return;

                var soundVoltex = root.Elements("game").FirstOrDefault(g =>
                {
                    var nameAttr = g.Attribute("name");
                    return nameAttr != null && string.Equals(nameAttr.Value, "Sound Voltex", StringComparison.OrdinalIgnoreCase);
                });

                if (soundVoltex == null) return;

                var options = soundVoltex.Element("options");
                if (options == null) return;

                var urlOption = options.Elements("option").FirstOrDefault(o =>
                {
                    var nameAttr = o.Attribute("name");
                    return nameAttr != null && string.Equals(nameAttr.Value, "url", StringComparison.Ordinal);
                });
                if (urlOption != null)
                {
                    txtServerAddress.Text = urlOption.Attribute("value")?.Value ?? string.Empty;
                }
                else
                {
                    txtServerAddress.Text = string.Empty;
                }

                _originalServerAddress = txtServerAddress.Text;

                var pcbIdOption = options.Elements("option").FirstOrDefault(o =>
                {
                    var nameAttr = o.Attribute("name");
                    return nameAttr != null && string.Equals(nameAttr.Value, "p", StringComparison.Ordinal);
                });
                if (pcbIdOption != null)
                {
                    txtPcbId.Text = pcbIdOption.Attribute("value")?.Value ?? string.Empty;
                }
                else
                {
                    txtPcbId.Text = string.Empty;
                }

                _originalPcbId = txtPcbId.Text;
            }
            catch (Exception ex)
            {
                // Avalonia 没有内置同步 MessageBox，用日志记录
                System.Diagnostics.Debug.WriteLine($"加载服务器配置失败: {ex.Message}");
            }
            finally
            {
                _isLoadingData = false;
            }
        }

        private void SaveServerConfig()
        {
            try
            {
                string spiceXmlPath = GetSpiceXmlPath();
                if (!File.Exists(spiceXmlPath))
                {
                    System.Diagnostics.Debug.WriteLine("未找到 SpiceTools 配置文件");
                    return;
                }

                var doc = XDocument.Load(spiceXmlPath, LoadOptions.PreserveWhitespace);
                var root = doc.Root;
                if (root == null) return;

                var soundVoltex = root.Elements("game").FirstOrDefault(g =>
                {
                    var nameAttr = g.Attribute("name");
                    return nameAttr != null && string.Equals(nameAttr.Value, "Sound Voltex", StringComparison.OrdinalIgnoreCase);
                });

                if (soundVoltex == null) return;

                var options = soundVoltex.Element("options");
                if (options == null)
                {
                    options = new XElement("options");
                    soundVoltex.Add(options);
                }

                var optionLookup = new Dictionary<string, XElement>(StringComparer.Ordinal);
                foreach (var option in options.Elements("option"))
                {
                    var nameAttr = option.Attribute("name");
                    if (nameAttr == null) continue;
                    var key = nameAttr.Value;
                    if (!optionLookup.ContainsKey(key))
                    {
                        optionLookup[key] = option;
                    }
                }

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

                UpdateOrCreateOption(optionLookup, options, "url", txtServerAddress.Text, optionLinePrefix, ref closingWhitespace);
                UpdateOrCreateOption(optionLookup, options, "p", txtPcbId.Text, optionLinePrefix, ref closingWhitespace);

                var settings = new XmlWriterSettings
                {
                    Indent = false,
                    NewLineHandling = NewLineHandling.None,
                    Encoding = new UTF8Encoding(false),
                    OmitXmlDeclaration = false,
                    NewLineChars = newline,
                    NewLineOnAttributes = false
                };
                using (var writer = XmlWriter.Create(spiceXmlPath, settings))
                {
                    doc.Save(writer);
                }

                NormalizeSelfClosingTags(spiceXmlPath);
                SaveCustomConfig();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存服务器配置失败: {ex.Message}");
            }
        }

        private void SaveCustomConfig()
        {
            try
            {
                bool isCustomUrl = !string.Equals(txtServerAddress.Text, _originalServerAddress, StringComparison.Ordinal);
                bool isCustomPcbId = !string.Equals(txtPcbId.Text, _originalPcbId, StringComparison.Ordinal);

                bool isUsingPreset = string.Equals(txtServerAddress.Text, "http://localhost:8083", StringComparison.OrdinalIgnoreCase)
                                    && string.IsNullOrEmpty(txtPcbId.Text);

                if (isCustomUrl && !isUsingPreset)
                {
                    _configFile.WriteString("Server", "customurl", txtServerAddress.Text);
                }
                else
                {
                    _configFile.WriteString("Server", "customurl", string.Empty);
                }

                if (isCustomPcbId && !isUsingPreset)
                {
                    _configFile.WriteString("Server", "custompcbid", txtPcbId.Text);
                }
                else
                {
                    _configFile.WriteString("Server", "custompcbid", string.Empty);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存自定义配置失败: {ex.Message}");
            }
        }

        private void UpdateOrCreateOption(Dictionary<string, XElement> optionLookup, XElement options, string name, string value, string optionLinePrefix, ref XText closingWhitespace)
        {
            if (optionLookup.TryGetValue(name, out var existing))
            {
                existing.SetAttributeValue("value", value ?? string.Empty);
            }
            else
            {
                if (closingWhitespace == null)
                {
                    closingWhitespace = EnsureClosingWhitespace(options, optionLinePrefix.Substring(0, optionLinePrefix.LastIndexOf('\n') + 1));
                }

                closingWhitespace.AddBeforeSelf(new XText(optionLinePrefix));
                var newOpt = new XElement("option",
                    new XAttribute("name", name),
                    new XAttribute("value", value ?? string.Empty));
                closingWhitespace.AddBeforeSelf(newOpt);
                optionLookup[name] = newOpt;
            }
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
                var normalized = Regex.Replace(original, "(?<=\\S)[ \\\\\\t]+/>", "/>");
                if (!string.Equals(original, normalized, StringComparison.Ordinal))
                {
                    File.WriteAllText(filePath, normalized, new UTF8Encoding(false));
                }
            }
            catch
            {
                // 忽略规范化错误
            }
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            SaveServerConfig();
            Confirmed = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }
    }
}
