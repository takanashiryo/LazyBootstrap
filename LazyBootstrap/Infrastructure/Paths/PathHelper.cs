using System.IO;

namespace LazyBootstrap.Infrastructure.Paths
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
