using System;
using System.IO;
using System.Threading;
using Serilog;

namespace LazyBootstrap.Infrastructure.Processes
{
    internal static class MediaUpdaterPendingUpdateService
    {
        private const string MediaUpdaterFileName = "MediaUpdater.exe";
        private const string PendingExtension = ".pending";
        private const string BackupExtension = ".bak";
        private const int MaxAttempts = 20;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);

        public static void ApplyPendingUpdate(string applicationDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(applicationDirectoryPath))
            {
                return;
            }

            string applicationDirectory;
            try
            {
                applicationDirectory = Path.GetFullPath(applicationDirectoryPath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "MediaUpdater pending update skipped because the application directory is invalid.");
                return;
            }

            string targetPath = Path.Combine(applicationDirectory, MediaUpdaterFileName);
            string pendingPath = targetPath + PendingExtension;
            string backupPath = targetPath + BackupExtension;

            if (!File.Exists(pendingPath))
            {
                return;
            }

            Log.Information("MediaUpdater pending update found: {PendingPath}", pendingPath);

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    ApplyPendingUpdateCore(targetPath, pendingPath, backupPath);
                    Log.Information("MediaUpdater pending update applied successfully.");
                    return;
                }
                catch (Exception ex) when (IsRetriableFileAccessError(ex) && attempt < MaxAttempts)
                {
                    Log.Debug(
                        ex,
                        "MediaUpdater pending update attempt {Attempt}/{MaxAttempts} failed because the file is not ready.",
                        attempt,
                        MaxAttempts);
                    Thread.Sleep(RetryDelay);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "MediaUpdater pending update failed. Pending file will be retried on next startup.");
                    return;
                }
            }
        }

        private static void ApplyPendingUpdateCore(string targetPath, string pendingPath, string backupPath)
        {
            if (!File.Exists(pendingPath))
            {
                return;
            }

            SetNormalAttributesIfExists(pendingPath);
            SetNormalAttributesIfExists(targetPath);
            DeleteIfExists(backupPath);

            if (File.Exists(targetPath))
            {
                File.Replace(pendingPath, targetPath, backupPath, true);
                TryDeleteIfExists(backupPath);
                return;
            }

            File.Move(pendingPath, targetPath, true);
        }

        private static void SetNormalAttributesIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }

        private static void TryDeleteIfExists(string path)
        {
            try
            {
                DeleteIfExists(path);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "MediaUpdater replacement backup cleanup failed: {BackupPath}", path);
            }
        }

        private static bool IsRetriableFileAccessError(Exception ex)
        {
            return ex is IOException or UnauthorizedAccessException;
        }
    }
}
