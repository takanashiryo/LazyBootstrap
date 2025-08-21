using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// 一个用于处理 INI 文件的帮助类
public class ConfigHandler
{
    private readonly string _path;

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
        StringBuilder retVal = new StringBuilder(255);
        GetPrivateProfileString(section, key, defaultValue, retVal, 255, _path);
        return retVal.ToString();
    }
}