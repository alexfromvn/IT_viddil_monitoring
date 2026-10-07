using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace IT_viddil_monitoring;

public sealed record ProcessRecord(
    int Pid, string Name, string[] Services, bool Active,
    double CpuPercent, double CpuSeconds, double DiskBytesPerSecond,
    double DiskBytes, double MemoryBytes, double AverageMemoryBytes);

public sealed record MonitorSnapshot(TimeSpan Elapsed, List<ProcessRecord> Processes);

public sealed class ProcessMonitor
{
    internal static bool IsMonitorProcess(int pid, string name) => pid == Environment.ProcessId ||
        name.Equals("IT_viddil_monitoring", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("IT_viddil_monitoring_v2", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("IT_viddil_monitoring_v3", StringComparison.OrdinalIgnoreCase);

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<(int Pid, long Start), TrackedProcess> _tracked = [];
    private Dictionary<int, string[]> _services = [];
    private TimeSpan _lastServiceRefresh = TimeSpan.MinValue;
    private TimeSpan _lastSample;
    private int _sampleCount;

    public MonitorSnapshot Sample()
    {
        var now = _clock.Elapsed;
        var interval = Math.Max(0.001, (now - _lastSample).TotalSeconds);
        _lastSample = now;
        _sampleCount++;
        if (_lastServiceRefresh == TimeSpan.MinValue || now - _lastServiceRefresh >= TimeSpan.FromSeconds(30))
        {
            _services = ReadServices();
            _lastServiceRefresh = now;
        }

        foreach (var entry in _tracked.Values) { entry.Active = false; entry.CpuPercent = 0; entry.DiskBytesPerSecond = 0; }
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var pid = process.Id;
                    var name = process.ProcessName;
                    if (IsMonitorProcess(pid, name)) continue;
                    long started;
                    try { started = process.StartTime.ToUniversalTime().Ticks; }
                    catch { started = 0; }
                    var key = (pid, started);
                    if (!_tracked.TryGetValue(key, out var record))
                    {
                        record = new TrackedProcess { Pid = pid, Name = name };
                        _tracked[key] = record;
                    }
                    record.Active = true;
                    record.Name = name;
                    record.Services = _services.GetValueOrDefault(pid) ?? [];

                    try
                    {
                        var cpuSeconds = process.TotalProcessorTime.TotalSeconds;
                        if (record.LastCpuSeconds is double old)
                        {
                            var delta = Math.Max(0, cpuSeconds - old);
                            record.CpuSeconds += delta;
                            record.CpuPercent = Math.Clamp(delta / interval / Environment.ProcessorCount * 100, 0, 100);
                        }
                        record.LastCpuSeconds = cpuSeconds;
                    }
                    catch { /* Protected system processes may deny this counter. */ }

                    try
                    {
                        var memory = process.WorkingSet64;
                        record.MemoryBytes = memory;
                        record.MemorySum += memory;
                    }
                    catch { /* Keep the last accessible value. */ }

                    if (TryReadIo(pid, out var ioBytes))
                    {
                        if (record.LastIoBytes is ulong old)
                        {
                            var delta = ioBytes >= old ? ioBytes - old : 0;
                            record.DiskBytes += delta;
                            record.DiskBytesPerSecond = delta / interval;
                        }
                        record.LastIoBytes = ioBytes;
                    }
                }
                catch { /* A process may exit during enumeration. */ }
            }
        }
        var records = _tracked.Values.Select(p => new ProcessRecord(
            p.Pid, p.Name, p.Services, p.Active, p.CpuPercent, p.CpuSeconds,
            p.DiskBytesPerSecond, p.DiskBytes, p.MemoryBytes,
            p.MemorySum / _sampleCount)).ToList();
        return new MonitorSnapshot(now, records);
    }

    private static Dictionary<int, string[]> ReadServices()
    {
        try
        {
            const uint scManagerEnumerateService = 0x0004;
            const uint serviceWin32 = 0x0030;
            const uint serviceActive = 0x0001;
            const uint serviceRunning = 0x0004;
            const int errorMoreData = 234;
            const int bufferSize = 256 * 1024; // Maximum supported SCM enumeration buffer.

            using var manager = OpenSCManager(null, null, scManagerEnumerateService);
            if (manager.IsInvalid) return [];

            var buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                var map = new Dictionary<int, HashSet<string>>();
                var entrySize = Marshal.SizeOf<EnumServiceStatusProcess>();
                uint resumeHandle = 0;
                // A changing service database must not cause an unbounded refresh.
                for (var page = 0; page < 128; page++)
                {
                    var previousResume = resumeHandle;
                    var completed = EnumServicesStatusEx(manager, 0, serviceWin32, serviceActive,
                        buffer, bufferSize, out _, out var servicesReturned, ref resumeHandle, null);
                    var error = completed ? 0 : Marshal.GetLastPInvokeError();
                    if (!completed && error != errorMoreData) return [];
                    if (servicesReturned > bufferSize / entrySize) return [];

                    // ERROR_MORE_DATA can still return a valid page of entries.
                    for (uint index = 0; index < servicesReturned; index++)
                    {
                        var entry = Marshal.PtrToStructure<EnumServiceStatusProcess>(
                            IntPtr.Add(buffer, checked((int)index * entrySize)));
                        var status = entry.Status;
                        if (status.CurrentState != serviceRunning || status.ProcessId == 0 ||
                            status.ProcessId > int.MaxValue || entry.ServiceName == IntPtr.Zero) continue;

                        // Read a bounded Unicode name from the returned buffer, before reusing it.
                        var nameOffset = entry.ServiceName.ToInt64() - buffer.ToInt64();
                        if (nameOffset < 0 || nameOffset > bufferSize - sizeof(char)) continue;
                        var maxCharacters = (int)Math.Min(257, (bufferSize - nameOffset) / sizeof(char));
                        var name = Marshal.PtrToStringUni(entry.ServiceName, maxCharacters);
                        if (name is null) continue;
                        var terminator = name.IndexOf('\0');
                        if (terminator <= 0) continue;
                        name = name[..terminator];

                        var pid = (int)status.ProcessId;
                        if (!map.TryGetValue(pid, out var names))
                            map[pid] = names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        names.Add(name);
                    }

                    if (completed)
                        return map.ToDictionary(pair => pair.Key,
                            pair => pair.Value.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray());
                    if (resumeHandle == previousResume) return []; // No progress with the largest valid buffer.
                }
                return [];
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        catch { return []; } // Restricted SCM access or shutdown must not interrupt monitoring.
    }

    private static bool TryReadIo(int pid, out ulong bytes)
    {
        bytes = 0;
        var handle = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
        if (handle == IntPtr.Zero) return false;
        try
        {
            if (!GetProcessIoCounters(handle, out var counters)) return false;
            bytes = counters.ReadTransferCount + counters.WriteTransferCount;
            return true;
        }
        finally { CloseHandle(handle); }
    }

    private sealed class TrackedProcess
    {
        public int Pid;
        public string Name = "";
        public string[] Services = [];
        public bool Active;
        public double? LastCpuSeconds;
        public ulong? LastIoBytes;
        public double CpuPercent;
        public double CpuSeconds;
        public double DiskBytesPerSecond;
        public double DiskBytes;
        public double MemoryBytes;
        public double MemorySum;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatusProcess
    {
        public uint ServiceType, CurrentState, ControlsAccepted, Win32ExitCode;
        public uint ServiceSpecificExitCode, CheckPoint, WaitHint, ProcessId, ServiceFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EnumServiceStatusProcess
    {
        public IntPtr ServiceName, DisplayName;
        public ServiceStatusProcess Status;
    }

    private sealed class SafeServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeServiceHandle() : base(true) { }
        protected override bool ReleaseHandle() => CloseServiceHandle(handle);
    }

    [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true,
        CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeServiceHandle OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", EntryPoint = "EnumServicesStatusExW", ExactSpelling = true,
        CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumServicesStatusEx(SafeServiceHandle manager, int infoLevel,
        uint serviceType, uint serviceState, IntPtr services, int bufferSize, out uint bytesNeeded,
        out uint servicesReturned, ref uint resumeHandle, string? groupName);

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(IntPtr handle, out IoCounters counters);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
