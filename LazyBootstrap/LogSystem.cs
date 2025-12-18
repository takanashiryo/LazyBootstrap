using System;
using System.Windows.Forms;

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

        private static RichTextBox _output;

        public static void Initialize(RichTextBox richTextBox)
        {
            _output = richTextBox;
            if (_output != null)
            {
                // 深色背景与浅色前景
                _output.BackColor = System.Drawing.Color.Black;
                _output.ForeColor = System.Drawing.Color.White;
            }
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
                    var line = raw; // 不 Trim，保留行内空格

                    if (string.IsNullOrEmpty(line))
                    {
                        // 空行直接换行，不加时间戳
                        _output.AppendText(Environment.NewLine);
                        continue;
                    }

                    string timestamp = $"[{DateTime.Now:HH:mm:ss}] ";

                    // 先输出时间戳（使用默认颜色）
                    _output.SelectionStart = _output.TextLength;
                    _output.SelectionLength = 0;
                    _output.SelectionColor = _output.ForeColor;
                    _output.AppendText(timestamp);

                    // 根据级别输出消息内容
                    if (level == LogLevel.Warning || level == LogLevel.Error)
                    {
                        _output.SelectionStart = _output.TextLength;
                        _output.SelectionLength = 0;
                        _output.SelectionColor = level == LogLevel.Error
                            ? System.Drawing.Color.Red
                            : System.Drawing.Color.Orange;
                        _output.AppendText(line);
                        // 重置颜色
                        _output.SelectionColor = _output.ForeColor;
                    }
                    else
                    {
                        _output.AppendText(line);
                    }

                    _output.AppendText(Environment.NewLine);
                }

                try
                {
                    _output.SelectionStart = _output.TextLength;
                    _output.ScrollToCaret();
                }
                catch { }
            };

            if (_output.InvokeRequired)
            {
                _output.Invoke((MethodInvoker)(() => append()));
            }
            else
            {
                append();
            }
        }
    }
}
