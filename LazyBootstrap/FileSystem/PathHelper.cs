using System.IO;

namespace LazyBootstrap.FileSystem
{
    internal static class PathHelper
    {
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try { return Path.GetFullPath(path.Trim()); }
            catch { return path.Trim(); }
        }
    }
}
