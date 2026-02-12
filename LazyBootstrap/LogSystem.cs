using System;
using Avalonia.Controls;
using Avalonia.Threading;

namespace LazyBootstrap
{
    public static class LogSystem
    {
        public enum LogLevel
        {
            Info,
            Warning,
            Error
        }

        private static TextBox _output;

        public static void Initialize(TextBox textBox)
        {
            _output = textBox;
        }

        public static void Log(string message)
        {
            Log(message, LogLevel.Info);
        }

        public static void Log(string message, LogLevel level)
        {
            if (_output == null || string.IsNullOrEmpty(message)) return;

            Action append = () =>
            {
                // 逐行添加时间戳，兼容 \n 和 \r\n
                var normalized = message.Replace("\r\n", "\n");
                var lines = normalized.Split('\n');
                foreach (var raw in lines)
                {
                    var line = raw;

                    if (string.IsNullOrEmpty(line))
                    {
                        _output.Text += Environment.NewLine;
                        continue;
                    }

                    string timestamp = $"[{DateTime.Now:HH:mm:ss}] ";

                    // Avalonia TextBox 不支持富文本着色，使用前缀标记
                    string prefix = "";
                    switch (level)
                    {
                        case LogLevel.Error:
                            prefix = "[错误] ";
                            break;
                        case LogLevel.Warning:
                            prefix = "[警告] ";
                            break;
                    }

                    _output.Text += timestamp + prefix + line + Environment.NewLine;
                }

                // 滚动到底部
                try
                {
                    _output.CaretIndex = _output.Text?.Length ?? 0;
                }
                catch { }
            };

            if (Dispatcher.UIThread.CheckAccess())
            {
                append();
            }
            else
            {
                Dispatcher.UIThread.Post(append);
            }
        }
    }
}
