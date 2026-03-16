using System;
using System.Collections.Generic;
using System.IO;

namespace LazyBootstrap.Services.Settings
{
    internal interface ICompatibilitySettingsService
    {
        bool TryToggleCompatLayer(bool enable, string renderMode, Func<string, bool> tryApplyDxModeValue, out string error);

        bool TryPersistRenderMode(string renderMode, bool compatLayerEnabled, Func<string, bool> tryApplyDxModeValue, out string error);
    }

    internal sealed class CompatibilitySettingsService : ICompatibilitySettingsService
    {
        private const string SettingSectionName = AppConfigBootstrapper.SettingSectionName;
        private static readonly string[] BaseCompatFiles = { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll" };
        private static readonly string[] ManagedCompatFiles = { "nvcuda.dll", "nvcuvid.dll", "nvEncodeAPI64.dll", "d3d9.dll" };

        private readonly IConfigHandler _configFile;
        private readonly ILauncherPaths _paths;

        public CompatibilitySettingsService(IConfigHandler configFile, ILauncherPaths paths)
        {
            ArgumentNullException.ThrowIfNull(configFile);
            ArgumentNullException.ThrowIfNull(paths);

            _configFile = configFile;
            _paths = paths;
        }

        public bool TryToggleCompatLayer(bool enable, string renderMode, Func<string, bool> tryApplyDxModeValue, out string error)
        {
            ArgumentNullException.ThrowIfNull(tryApplyDxModeValue);

            error = string.Empty;
            renderMode = NormalizeRenderMode(renderMode);

            var configSnapshot = FileStateSnapshot.Capture(_paths.ConfigFilePath);
            var moduleSnapshots = CaptureCompatModuleSnapshots();

            try
            {
                if (enable)
                {
                    if (!ApplyCompatLayerFiles(renderMode, out error))
                    {
                        error = CombineErrors(error, RestoreSnapshots(configSnapshot, moduleSnapshots));
                        return false;
                    }
                }
                else if (!RemoveCompatLayerFilesFromModules(out error))
                {
                    error = CombineErrors(error, RestoreSnapshots(configSnapshot, moduleSnapshots));
                    return false;
                }

                _configFile.WriteString(SettingSectionName, "compatlayer", enable ? "true" : "false");

                if (!tryApplyDxModeValue(ResolveDxModeValue(enable, renderMode)))
                {
                    error = CombineErrors("写入 spicetools.xml 失败，已恢复兼容层状态。", RestoreSnapshots(configSnapshot, moduleSnapshots));
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = CombineErrors(ex.Message, RestoreSnapshots(configSnapshot, moduleSnapshots));
                return false;
            }
        }

        public bool TryPersistRenderMode(string renderMode, bool compatLayerEnabled, Func<string, bool> tryApplyDxModeValue, out string error)
        {
            ArgumentNullException.ThrowIfNull(tryApplyDxModeValue);

            error = string.Empty;
            renderMode = NormalizeRenderMode(renderMode);
            var configSnapshot = FileStateSnapshot.Capture(_paths.ConfigFilePath);

            try
            {
                _configFile.WriteString(SettingSectionName, "cl-rendermode", renderMode);

                if (!compatLayerEnabled)
                {
                    return true;
                }

                if (!tryApplyDxModeValue(ResolveDxModeValue(true, renderMode)))
                {
                    error = CombineErrors("写入 spicetools.xml 失败，已恢复兼容模式。", RestoreSnapshot(configSnapshot));
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = CombineErrors(ex.Message, RestoreSnapshot(configSnapshot));
                return false;
            }
        }

        public static string NormalizeRenderMode(string renderMode)
        {
            if (string.Equals(renderMode, "dxvk", StringComparison.OrdinalIgnoreCase))
            {
                return "dxvk";
            }

            if (string.Equals(renderMode, "dx9on12_external", StringComparison.OrdinalIgnoreCase))
            {
                return "dx9on12_external";
            }

            return "dx9on12";
        }

        public static string ResolveDxModeValue(bool compatLayerEnabled, string renderMode)
        {
            if (!compatLayerEnabled)
            {
                return "0";
            }

            return string.Equals(NormalizeRenderMode(renderMode), "dx9on12", StringComparison.Ordinal)
                ? "1"
                : "0";
        }

        private List<FileStateSnapshot> CaptureCompatModuleSnapshots()
        {
            var modulesDirectoryPath = GetCompatModulesDirectoryPath();
            var snapshots = new List<FileStateSnapshot>(ManagedCompatFiles.Length);
            foreach (var fileName in ManagedCompatFiles)
            {
                snapshots.Add(FileStateSnapshot.Capture(Path.Combine(modulesDirectoryPath, fileName)));
            }

            return snapshots;
        }

        private bool ApplyCompatLayerFiles(string renderMode, out string error)
        {
            error = string.Empty;
            string stubsDir = _paths.GetBundledLibsDirectoryPath();
            string modulesDir = GetCompatModulesDirectoryPath();
            if (!Directory.Exists(stubsDir))
            {
                error = $"未找到兼容层资源目录: {stubsDir}";
                return false;
            }

            if (!Directory.Exists(modulesDir))
            {
                error = $"未找到兼容层目标目录: {modulesDir}";
                return false;
            }

            try
            {
                foreach (var fileName in BaseCompatFiles)
                {
                    string sourcePath = Path.Combine(stubsDir, fileName);
                    if (!File.Exists(sourcePath))
                    {
                        error = $"缺少文件: {fileName}";
                        return false;
                    }

                    File.Copy(sourcePath, Path.Combine(modulesDir, fileName), true);
                }

                string d3d9Path = Path.Combine(modulesDir, "d3d9.dll");
                if (File.Exists(d3d9Path))
                {
                    File.Delete(d3d9Path);
                }

                string stubName = ResolveD3d9StubName(renderMode);
                if (string.IsNullOrEmpty(stubName))
                {
                    return true;
                }

                string stubPath = Path.Combine(stubsDir, stubName);
                if (!File.Exists(stubPath))
                {
                    error = $"缺少文件: {stubName}";
                    return false;
                }

                File.Copy(stubPath, d3d9Path, true);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool RemoveCompatLayerFilesFromModules(out string error)
        {
            error = string.Empty;
            string modulesDir = GetCompatModulesDirectoryPath();
            if (!Directory.Exists(modulesDir))
            {
                return true;
            }

            try
            {
                foreach (var fileName in ManagedCompatFiles)
                {
                    string path = Path.Combine(modulesDir, fileName);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private string GetCompatModulesDirectoryPath()
        {
            return Path.Combine(_paths.GetContentsDirectoryPath(), "modules");
        }

        private static string ResolveD3d9StubName(string renderMode)
        {
            return NormalizeRenderMode(renderMode) switch
            {
                "dxvk" => "d3d9.dll.dxvk",
                "dx9on12_external" => "d3d9.dll.dx9on12",
                _ => string.Empty
            };
        }

        private static string RestoreSnapshots(FileStateSnapshot configSnapshot, IEnumerable<FileStateSnapshot> moduleSnapshots)
        {
            var errors = new List<string>();

            var configRestoreError = RestoreSnapshot(configSnapshot);
            if (!string.IsNullOrWhiteSpace(configRestoreError))
            {
                errors.Add($"启动器配置回滚失败: {configRestoreError}");
            }

            foreach (var moduleSnapshot in moduleSnapshots)
            {
                var moduleRestoreError = RestoreSnapshot(moduleSnapshot);
                if (!string.IsNullOrWhiteSpace(moduleRestoreError))
                {
                    errors.Add($"兼容层文件回滚失败: {moduleRestoreError}");
                }
            }

            return errors.Count == 0 ? string.Empty : string.Join(" ", errors);
        }

        private static string RestoreSnapshot(FileStateSnapshot snapshot)
        {
            try
            {
                snapshot.Restore();
                return string.Empty;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static string CombineErrors(string primaryError, string rollbackError)
        {
            if (string.IsNullOrWhiteSpace(rollbackError))
            {
                return primaryError ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(primaryError))
            {
                return rollbackError;
            }

            return $"{primaryError} {rollbackError}";
        }
    }

    internal sealed class FileStateSnapshot
    {
        private readonly byte[] _content;

        private FileStateSnapshot(string path, bool existed, byte[] content)
        {
            Path = path;
            Existed = existed;
            _content = content ?? Array.Empty<byte>();
        }

        public string Path { get; }

        public bool Existed { get; }

        public static FileStateSnapshot Capture(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            string fullPath = System.IO.Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return new FileStateSnapshot(fullPath, false, Array.Empty<byte>());
            }

            return new FileStateSnapshot(fullPath, true, File.ReadAllBytes(fullPath));
        }

        public void Restore()
        {
            if (Existed)
            {
                string directory = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(Path, _content);
                return;
            }

            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
