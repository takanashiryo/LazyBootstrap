using System;
using System.Collections.Generic;
using System.IO;

namespace LazyBootstrap.Services.Savedata
{
    internal static class SavedataTransferOperations
    {
        public static void ReplaceDirectory(string sourceDirectory, string destinationDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

            if (!Directory.Exists(sourceDirectory))
            {
                throw new DirectoryNotFoundException($"Source directory was not found: {sourceDirectory}");
            }

            EnsureDirectoryDeleted(destinationDirectory);
            CopyDirectoryRecursive(sourceDirectory, destinationDirectory);
        }

        public static void CopyFile(string sourcePath, string destinationPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Source file was not found.", sourcePath);
            }

            string destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        public static void CopyDirectoryRecursive(string sourceDirectory, string destinationDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

            if (!Directory.Exists(sourceDirectory))
            {
                throw new DirectoryNotFoundException($"Source directory was not found: {sourceDirectory}");
            }

            Directory.CreateDirectory(destinationDirectory);

            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                string destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(file));
                File.Copy(file, destinationFile, overwrite: true);
            }

            foreach (var directory in Directory.GetDirectories(sourceDirectory))
            {
                string destinationSubDirectory = Path.Combine(destinationDirectory, Path.GetFileName(directory));
                CopyDirectoryRecursive(directory, destinationSubDirectory);
            }
        }

        public static void StageEntries(IEnumerable<SavedataTransferEntry> entries, string stagingDirectory)
        {
            foreach (var entry in entries)
            {
                string stagedPath = Path.Combine(stagingDirectory, entry.ArchiveRelativePath);
                if (entry.IsDirectory)
                {
                    CopyDirectoryRecursive(entry.SourcePath, stagedPath);
                    continue;
                }

                CopyFile(entry.SourcePath, stagedPath);
            }
        }

        public static void CopyEntries(IEnumerable<SavedataTransferEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry.IsDirectory)
                {
                    ReplaceDirectory(entry.SourcePath, entry.DestinationPath);
                    continue;
                }

                CopyFile(entry.SourcePath, entry.DestinationPath);
            }
        }

        public static string CreateTemporaryWorkingDirectory(string purpose)
        {
            string path = Path.Combine(Path.GetTempPath(), "LazyBootstrap", purpose, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        public static void DeleteDirectoryIfExists(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch
            {
            }
        }

        private static void EnsureDirectoryDeleted(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new IOException($"Failed to replace existing directory: {path}", ex);
            }

            if (Directory.Exists(path))
            {
                throw new IOException($"Failed to replace existing directory: {path}");
            }
        }
    }
}
