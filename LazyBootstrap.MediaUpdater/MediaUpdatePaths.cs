using System.IO;
using System.Linq;

namespace LazyBootstrap.MediaUpdate
{
    public static class MediaUpdatePaths
    {
        public static bool IsValidGameRoot(string baseDir)
        {
            return Directory.Exists(Path.Combine(baseDir, "contents"))
                   && Directory.Exists(Path.Combine(baseDir, "asphyxia"));
        }

        public static string FindShallowestFile(string root, string fileName)
        {
            if (!Directory.Exists(root))
            {
                return string.Empty;
            }

            return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories)
                .OrderBy(p => p.Length)
                .FirstOrDefault() ?? string.Empty;
        }
    }
}
