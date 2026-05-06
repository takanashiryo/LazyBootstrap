using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace LazyBootstrap.Services.Processes
{
    internal interface IGameProcessTracker
    {
        void PrepareTrackedSpiceSession(string spicePath);

        void RegisterTrackedSpiceProcess(Process process);

        Process TryFindRestartedSpiceProcess(DateTime exitedAtUtc);

        void TrackManagedAsphyxiaProcess(Process process);

        bool HasManagedAsphyxiaProcess();

        bool TryStopManagedAsphyxiaProcess(out string errorMessage);

        void ResetManagedAsphyxiaTracking();
    }

    internal sealed class GameProcessTracker : IGameProcessTracker
    {
        private static readonly TimeSpan RestartStartTimeSkewTolerance = TimeSpan.FromSeconds(2);

        private readonly HashSet<int> _trackedSpiceProcessIds = new HashSet<int>();
        private string _trackedSpiceExecutablePath = string.Empty;
        private Process _managedAsphyxiaProcess;

        public void PrepareTrackedSpiceSession(string spicePath)
        {
            _trackedSpiceProcessIds.Clear();
            _trackedSpiceExecutablePath = PathHelper.NormalizePath(spicePath);

            if (string.IsNullOrWhiteSpace(_trackedSpiceExecutablePath))
            {
                return;
            }

            foreach (var process in Process.GetProcessesByName("spice64"))
            {
                try
                {
                    var processPath = PathHelper.NormalizePath(TryGetProcessExecutablePath(process));
                    if (string.Equals(processPath, _trackedSpiceExecutablePath, StringComparison.OrdinalIgnoreCase))
                    {
                        _trackedSpiceProcessIds.Add(process.Id);
                    }
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        public void RegisterTrackedSpiceProcess(Process process)
        {
            if (process == null)
            {
                return;
            }

            try
            {
                _trackedSpiceProcessIds.Add(process.Id);
            }
            catch
            {
            }
        }

        public Process TryFindRestartedSpiceProcess(DateTime exitedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(_trackedSpiceExecutablePath) || exitedAtUtc == DateTime.MinValue)
            {
                return null;
            }

            Process matchedProcess = null;
            var matchedCount = 0;
            var probeUtc = DateTime.UtcNow;
            var earliestAcceptedStartUtc = exitedAtUtc - RestartStartTimeSkewTolerance;
            var latestAcceptedStartUtc = probeUtc + RestartStartTimeSkewTolerance;

            foreach (var process in Process.GetProcessesByName("spice64"))
            {
                var keepProcess = false;
                try
                {
                    if (_trackedSpiceProcessIds.Contains(process.Id))
                    {
                        continue;
                    }

                    var processPath = PathHelper.NormalizePath(TryGetProcessExecutablePath(process));
                    if (!string.Equals(processPath, _trackedSpiceExecutablePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    DateTime startTimeUtc;
                    try
                    {
                        startTimeUtc = process.StartTime.ToUniversalTime();
                    }
                    catch
                    {
                        continue;
                    }

                    if (startTimeUtc < earliestAcceptedStartUtc || startTimeUtc > latestAcceptedStartUtc)
                    {
                        continue;
                    }

                    matchedCount++;
                    if (matchedCount > 1)
                    {
                        matchedProcess?.Dispose();
                        matchedProcess = null;
                        break;
                    }

                    matchedProcess = process;
                    keepProcess = true;
                }
                catch
                {
                }
                finally
                {
                    if (!keepProcess)
                    {
                        process.Dispose();
                    }
                }
            }

            return matchedCount == 1 ? matchedProcess : null;
        }

        public void TrackManagedAsphyxiaProcess(Process process)
        {
            ResetManagedAsphyxiaTracking();
            _managedAsphyxiaProcess = process;
        }

        public bool HasManagedAsphyxiaProcess()
        {
            if (_managedAsphyxiaProcess == null)
            {
                return false;
            }

            try
            {
                return !_managedAsphyxiaProcess.HasExited;
            }
            catch
            {
                return false;
            }
        }

        public bool TryStopManagedAsphyxiaProcess(out string errorMessage)
        {
            errorMessage = string.Empty;
            var managedAsphyxiaProcess = _managedAsphyxiaProcess;
            _managedAsphyxiaProcess = null;

            if (managedAsphyxiaProcess == null)
            {
                return true;
            }

            try
            {
                if (!managedAsphyxiaProcess.HasExited)
                {
                    managedAsphyxiaProcess.Kill(true);
                    if (!managedAsphyxiaProcess.WaitForExit(3000))
                    {
                        errorMessage = "Asphyxia Core 未在预期时间内退出。";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
            finally
            {
                managedAsphyxiaProcess.Dispose();
            }
        }

        public void ResetManagedAsphyxiaTracking()
        {
            if (_managedAsphyxiaProcess == null)
            {
                return;
            }

            try
            {
                _managedAsphyxiaProcess.Dispose();
            }
            catch
            {
            }

            _managedAsphyxiaProcess = null;
        }

        private static string TryGetProcessExecutablePath(Process process)
        {
            try
            {
                return process?.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // Path normalization delegated to PathHelper.NormalizePath
    }
}
