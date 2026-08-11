using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LazyBootstrap.MediaUpdate
{
    internal static class MediaUpdateSecurity
    {
        public const string BlockedNonGamePathMessage =
            "检测到对非游戏路径的改动，疑似恶意文件，请勿下载来源不明的更新包！";

        private static readonly string[] ForbiddenSystemEnvTokens =
        {
            "%SYSTEMDRIVE%", "%SystemDrive%", "%systemdrive%",
            "%WINDIR%", "%WinDir%", "%windir%",
            "%SYSTEMROOT%", "%SystemRoot%", "%systemroot%",
            "%PROGRAMFILES%", "%ProgramFiles%", "%programfiles%",
            "%PROGRAMFILES(X86)%", "%ProgramFiles(x86)%",
            "%ProgramW6432%",
            "%ALLUSERSPROFILE%", "%USERPROFILE%", "%userprofile%",
            "%APPDATA%", "%LOCALAPPDATA%",
            "%TEMP%", "%TMP%", "%temp%", "%tmp%",
            "%PUBLIC%",
        };

        private static readonly Regex DriveAbsoluteRegex = new Regex(
            @"(?<![A-Za-z])([A-Za-z]:)(\\|/)([^""\s|&<>\r\n]*)",
            RegexOptions.Compiled);

        private static readonly Regex UncAbsoluteRegex = new Regex(
            @"\\\\([^""\s|&<>\r\n]+)",
            RegexOptions.Compiled);

        private static readonly Regex UncPathPrefixRegex = new Regex(@"\\\\\?\\", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool TryValidateStagingBatches(string stagingRoot, string gamePath, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(stagingRoot) || !Directory.Exists(stagingRoot))
            {
                return true;
            }

            string gameFull;
            string stagingFull;
            try
            {
                gameFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gamePath));
                stagingFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagingRoot));
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            foreach (string file in EnumerateBatchFiles(stagingRoot))
            {
                string text = ReadBatchText(file);
                if (ContainsForbiddenEnvToken(text))
                {
                    error = BlockedNonGamePathMessage;
                    return false;
                }

                if (!ValidateAbsolutePathsAllowed(text, gameFull, stagingFull))
                {
                    error = BlockedNonGamePathMessage;
                    return false;
                }
            }

            return true;
        }

        private static IEnumerable<string> EnumerateBatchFiles(string stagingRoot)
        {
            return Directory.EnumerateFiles(stagingRoot, "*.*", SearchOption.AllDirectories)
                .Where(static p =>
                {
                    string e = Path.GetExtension(p);
                    return string.Equals(e, ".bat", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(e, ".cmd", StringComparison.OrdinalIgnoreCase);
                });
        }

        private static string ReadBatchText(string path)
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }

        private static bool ContainsForbiddenEnvToken(string text)
        {
            foreach (string token in ForbiddenSystemEnvTokens)
            {
                if (text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizePathLiterals(string text)
        {
            text = Regex.Replace(text, @"\\\\\?\\UNC\\", @"\\", RegexOptions.IgnoreCase);
            text = UncPathPrefixRegex.Replace(text, string.Empty);
            return text;
        }

        private static bool ValidateAbsolutePathsAllowed(string text, string gameFull, string stagingFull)
        {
            string normalizedText = NormalizePathLiterals(text);

            foreach (Match m in DriveAbsoluteRegex.Matches(normalizedText))
            {
                string raw = TrimBatchPathNoise(m.Value.Trim());
                if (raw.Length < 3)
                {
                    continue;
                }

                raw = raw.Replace('/', '\\');
                if (!TryResolvePathForGuard(raw, out string resolved))
                {
                    return false;
                }

                if (!IsPathWithinAllowedRoots(resolved, gameFull, stagingFull))
                {
                    return false;
                }
            }

            foreach (Match m in UncAbsoluteRegex.Matches(normalizedText))
            {
                string raw = TrimBatchPathNoise(m.Value.Trim());
                if (raw.Length < 4 || !raw.StartsWith(@"\\", StringComparison.Ordinal))
                {
                    continue;
                }

                raw = raw.Replace('/', '\\');
                if (!TryResolvePathForGuard(raw, out string resolved))
                {
                    return false;
                }

                if (!IsPathWithinAllowedRoots(resolved, gameFull, stagingFull))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryResolvePathForGuard(string pathLike, out string fullPath)
        {
            fullPath = null;
            try
            {
                fullPath = Path.GetFullPath(pathLike);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string TrimBatchPathNoise(string pathFragment)
        {
            int cut = pathFragment.IndexOfAny(new[] { '&', '|' });
            if (cut > 0)
            {
                pathFragment = pathFragment.Substring(0, cut).TrimEnd();
            }

            return pathFragment.TrimEnd(',', ' ', '\t');
        }

        private static bool IsPathWithinAllowedRoots(string resolved, string gameFull, string stagingFull)
        {
            return IsUnderRoot(resolved, gameFull) || IsUnderRoot(resolved, stagingFull);
        }

        private static bool IsUnderRoot(string path, string root)
        {
            try
            {
                path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
                root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            }
            catch
            {
                return false;
            }

            if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }
}
