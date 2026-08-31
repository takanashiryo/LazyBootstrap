using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace LazyBootstrap
{
    internal sealed class GameProcessInfo
    {
        internal readonly int Id;
        internal readonly DateTime StartTimeUtc;
        internal readonly string ExecutablePath;

        internal GameProcessInfo(int id, DateTime startTimeUtc, string executablePath)
        {
            Id = id;
            StartTimeUtc = startTimeUtc;
            ExecutablePath = executablePath;
        }

        // A PID can be reused after a process exits.
        internal string Identity => Id + ":" + StartTimeUtc.Ticks;
    }

    internal sealed class GameProcessScan
    {
        internal readonly List<GameProcessInfo> Processes = new List<GameProcessInfo>();
        internal string Error;
        internal bool IsComplete => Error == null;
    }

    // Called only on the UI thread. Process exit events merely request another check.
    internal sealed class GameSessionMonitor : IDisposable
    {
        internal const int PollIntervalMilliseconds = 500;
        private static readonly TimeSpan ExitConfirmationTime = TimeSpan.FromSeconds(2);
        private readonly string _executablePath;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Action<string, bool> _log;
        private readonly HashSet<string> _preexisting;
        private Dictionary<string, GameProcessInfo> _known = new Dictionary<string, GameProcessInfo>();
        private Process _initialProcess;
        private TimeSpan? _emptySince;
        private string _lastError;
        private bool _initialExitLogged;
        private bool _completed;
        private bool _disposed;

        internal GameSessionMonitor(string executablePath, Action<string, bool> log)
        {
            _executablePath = Path.GetFullPath(executablePath);
            _log = log;
            var baseline = GameProcessReader.Scan(_executablePath);
            if (!baseline.IsComplete)
                throw new InvalidOperationException("无法建立游戏进程基线: " + baseline.Error);
            _preexisting = new HashSet<string>(baseline.Processes.Where(IsGame).Select(p => p.Identity));
        }

        private bool IsGame(GameProcessInfo process)
        {
            return string.Equals(_executablePath, process.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }

        internal void AttachInitialProcess(Process process)
        {
            _initialProcess = process; // Owned and disposed by the form.
            _log($"监测启动进程: PID={process.Id}, 路径={_executablePath}", false);
        }

        // Returns true once, and only after a complete, continuously empty confirmation period.
        internal bool CheckForExit()
        {
            if (_disposed || _completed) return false;

            bool initialAlive = false;
            string error = null;
            if (_initialProcess != null)
            {
                try
                {
                    initialAlive = !_initialProcess.HasExited;
                    if (!initialAlive && !_initialExitLogged)
                    {
                        _initialExitLogged = true;
                        string exitCode;
                        try { exitCode = _initialProcess.ExitCode.ToString(); }
                        catch { exitCode = "不可读取"; }
                        _log($"启动进程已退出: PID={_initialProcess.Id}, 退出码={exitCode}；正在复核其他游戏进程。", false);
                    }
                }
                catch (Exception ex)
                {
                    error = "无法查询启动进程: " + ex.Message;
                }
            }

            GameProcessScan snapshot;
            try { snapshot = GameProcessReader.Scan(_executablePath); }
            catch (Exception ex) { snapshot = new GameProcessScan { Error = ex.Message }; }
            if (!snapshot.IsComplete) error = snapshot.Error;

            if (error != null)
            {
                _emptySince = null;
                if (_lastError != error)
                    _log("游戏进程查询失败，暂不恢复屏幕: " + error, true);
                _lastError = error;
                return false;
            }

            if (_lastError != null) _log("游戏进程查询已恢复。", false);
            _lastError = null;
            var current = snapshot.Processes.Where(p => IsGame(p) && !_preexisting.Contains(p.Identity))
                .ToDictionary(p => p.Identity);
            foreach (var entry in current)
            {
                if (!_known.ContainsKey(entry.Key))
                    _log($"发现游戏进程: PID={entry.Value.Id}, 启动时间={entry.Value.StartTimeUtc:O}, 路径={entry.Value.ExecutablePath}", false);
            }
            foreach (var entry in _known)
            {
                if (!current.ContainsKey(entry.Key))
                    _log($"游戏进程已结束: PID={entry.Value.Id}, 启动时间={entry.Value.StartTimeUtc:O}（扫描发现退出，退出码不可读取）。", false);
            }
            _known = current;

            if (initialAlive || current.Count != 0)
            {
                _emptySince = null;
                return false;
            }

            var now = _clock.Elapsed;
            if (!_emptySince.HasValue) _emptySince = now;
            if (now - _emptySince.Value < ExitConfirmationTime) return false;
            _completed = true;
            return true;
        }

        public void Dispose()
        {
            _disposed = true;
            _clock.Stop();
            _initialProcess = null;
            _known.Clear();
        }
    }

    internal static class GameProcessReader
    {
        // Limited query access works across bitness and does not require module enumeration.
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(IntPtr process, uint flags,
            StringBuilder path, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessTimes(IntPtr process, out long creationTime,
            out long exitTime, out long kernelTime, out long userTime);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        internal static GameProcessScan Scan(string executablePath)
        {
            var result = new GameProcessScan();
            Process[] processes;
            try { processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executablePath)); }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }

            foreach (var process in processes)
            {
                using (process)
                {
                    try
                    {
                        var info = ReadProcess(process.Id);
                        if (info != null) result.Processes.Add(info);
                    }
                    catch (Exception ex)
                    {
                        // A process may disappear mid-scan. Only ignore a confirmed exit.
                        bool exited = false;
                        try { exited = process.HasExited; } catch { }
                        if (!exited) result.Error = "PID=" + process.Id + ": " + ex.Message;
                    }
                }
            }
            return result;
        }

        private static GameProcessInfo ReadProcess(int processId)
        {
            const uint QueryLimitedInformation = 0x1000;
            IntPtr handle = OpenProcess(QueryLimitedInformation, false, processId);
            if (handle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == 87) return null; // The PID disappeared after enumeration.
                throw new System.ComponentModel.Win32Exception(error);
            }
            try
            {
                // Read path and creation time through the same handle, even if the PID is reused.
                var path = new StringBuilder(32768);
                int length = path.Capacity;
                if (!QueryFullProcessImageName(handle, 0, path, ref length))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                long created, exited, kernel, user;
                if (!GetProcessTimes(handle, out created, out exited, out kernel, out user))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                if (exited != 0) return null;
                return new GameProcessInfo(processId, DateTime.FromFileTimeUtc(created), Path.GetFullPath(path.ToString()));
            }
            finally { CloseHandle(handle); }
        }
    }
}
