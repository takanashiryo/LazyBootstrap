using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// 一个用于处理 INI 文件的帮助类
public class ConfigHandler
{
    private readonly string _path;
    private const int InitialBufferSize = 256;
    private const int MaxBufferSize = 8192;

    // 导入 Windows API 函数用于写入 INI 文件
    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern long WritePrivateProfileString(string section, string key, string value, string filePath);

    // 导入 Windows API 函数用于读取 INI 文件
    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

    // 构造函数，接收 INI 文件的路径
    public ConfigHandler(string iniPath)
    {
        // 将相对路径转换为绝对路径，以避免工作目录问题
        _path = new FileInfo(iniPath).FullName;
    }

    // 写入字符串
    public void WriteString(string section, string key, string value)
    {
        WritePrivateProfileString(section, key, value, _path);
    }

    // 读取字符串
    public string ReadString(string section, string key, string defaultValue = "")
    {
        int bufferSize = InitialBufferSize;
        while (true)
        {
            var retVal = new StringBuilder(bufferSize);
            int length = GetPrivateProfileString(section, key, defaultValue, retVal, retVal.Capacity, _path);

            // GetPrivateProfileString 返回的长度不包含终止符。若值被截断，长度会等于缓冲区容量-1
            if (length < retVal.Capacity - 1 || bufferSize >= MaxBufferSize)
            {
                return retVal.ToString();
            }

            bufferSize = Math.Min(bufferSize * 2, MaxBufferSize);
        }
    }
}