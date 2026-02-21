using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using SukiUI.Dialogs;

namespace LazyBootstrap
{
    public partial class MainWindow
    {
        private static readonly OptionUpdate[] RecommendedSpiceOptionUpdates =
        {
            new OptionUpdate("k", "ifs_hook.dll", false),
            new OptionUpdate("sp2x-nvprofile", "/ENABLED", false),
            new OptionUpdate("sp2x-lowlatencysharedaudio", "/ENABLED", false),
            new OptionUpdate("sp2x-dx9on12", "0", false),
            new OptionUpdate("url", "http://localhost:8083", false),
            new OptionUpdate("sp2x-sdvxsubredraw", "/ENABLED", false)
        };

        private async void OnImportRecommendedSpiceConfigClick(object sender, RoutedEventArgs e)
        {
            if (_portableMode)
            {
                return;
            }

            var dialogBuilder = _dialogManager
                .CreateDialog()
                .OfType(NotificationType.Warning)
                .WithTitle("导入推荐spice2x配置")
                .WithContent("导入推荐spice2x配置会清除以下页面的现有配置并导入新配置：\n\nOptions\nAdvanced\nDevelopment\n\n你确定要执行吗？")
                .WithYesNoResult("确认", "取消", "Flat")
                .Dismiss().ByClickingBackground();
            ApplyDialogNotificationIcon(dialogBuilder, NotificationType.Warning);

            var confirmed = await dialogBuilder.TryShowAsync();
            if (!confirmed)
            {
                return;
            }

            ImportRecommendedSpiceConfigCore();
        }

        private void ImportRecommendedSpiceConfigCore()
        {
            try
            {
                var appDataSpiceXmlPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "spicetools.xml");
                if (!File.Exists(appDataSpiceXmlPath))
                {
                    ShowErrorToast("导入失败", "未找到 %AppData%\\spicetools.xml，请先启动 spicecfg 重建配置文件再进行导入。");
                    return;
                }

                if (!TryGetSpiceOptionsContext(appDataSpiceXmlPath, LoadOptions.PreserveWhitespace, true, out var context))
                {
                    ShowErrorToast("导入失败", "未找到 Sound Voltex 配置项，无法导入推荐配置。");
                    return;
                }

                var doc = context.Document;
                var soundVoltex = context.SoundVoltex;
                var options = context.OptionsElement;

                string newline = "\r\n";
                string optionsIndent = ExtractIndentation(options.PreviousNode as XText, ref newline) ?? string.Empty;
                string indentStep = DetermineIndentStep(soundVoltex, ref newline) ?? new string(' ', 4);
                string optionIndent = optionsIndent + indentStep;
                string optionLinePrefix = newline + optionIndent;
                string closingLinePrefix = newline + optionsIndent;

                options.RemoveNodes();
                context.OptionLookup.Clear();

                var closingWhitespace = EnsureClosingWhitespace(options, closingLinePrefix);
                foreach (var update in RecommendedSpiceOptionUpdates)
                {
                    closingWhitespace.AddBeforeSelf(new XText(optionLinePrefix));
                    var optionElement = new XElement("option",
                        new XAttribute("name", update.Name),
                        new XAttribute("value", update.Value));
                    closingWhitespace.AddBeforeSelf(optionElement);
                    context.OptionLookup[update.Name] = optionElement;
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

                LoadSpiceConfig();
                SelectPresetByCurrentFields();
                ShowInfoToast("导入完成", "推荐 spice2x 配置已导入。");
            }
            catch (Exception ex)
            {
                ShowErrorToast("导入失败", ex.Message);
            }
        }

        private void UpdateRecommendedSpiceConfigButtonVisibility()
        {
            if (ImportRecommendedSpiceConfigButton != null)
            {
                ImportRecommendedSpiceConfigButton.IsVisible = !_portableMode;
            }
        }
    }
}