namespace SystemToolkit.Core.Services;
using SystemToolkit.Core.Models;

using System.Diagnostics;
using System.Net.NetworkInformation;

public interface IMonitorService
{
    SystemResourceInfo GetSystemResourceInfo();
    Task<SystemResourceInfo> MonitorSystemResourcesAsync(int intervalSeconds = 5, int historyLength = 60, CancellationToken ct = default);
    DiskMonitorInfo GetDiskMonitorInfo(string driveName);
    Task<DiskMonitorInfo> MonitorDiskAsync(string driveName, int intervalSeconds = 5, int historyLength = 60, CancellationToken ct = default);
    NetworkMonitorInfo GetNetworkMonitorInfo();
    Task<NetworkMonitorInfo> MonitorNetworkAsync(int intervalSeconds = 5, int historyLength = 60, CancellationToken ct = default);
    Task SetApplicationMonitorAsync(string processName, bool autoRestart, CancellationToken ct = default);
    List<ApplicationMonitorInfo> GetMonitoredApplications();
    List<MonitorAlert> GetMonitorAlerts();
}

public class MonitorService : IMonitorService
{
    private static PerformanceCounter? _cpuCounter;
    private static PerformanceCounter? _memoryCounter;
    private static readonly Dictionary<string, ApplicationMonitorInfo> MonitoredApps = new();
    private static readonly List<MonitorAlert> Alerts = new();
    private static readonly double[] CpuHistory = new double[60];
    private static readonly double[] MemoryHistory = new double[60];

    public SystemResourceInfo GetSystemResourceInfo()
    {
        try
        {
            _cpuCounter ??= new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memoryCounter ??= new PerformanceCounter("Memory", "% Committed Bytes in Use");

            var cpuUsage = _cpuCounter.NextValue();
            var memoryUsage = _memoryCounter.NextValue();
            var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

            return new SystemResourceInfo
            {
                CpuUsage = cpuUsage,
                MemoryUsage = memoryUsage,
                TotalMemory = totalMemory,
                CpuUsageHistory = CpuHistory.ToArray(),
                MemoryUsageHistory = MemoryHistory.ToArray(),
                Timestamp = DateTime.Now
            };
        }
        catch (Exception)
        {
            // Fallback for systems without PerformanceCounter
            return new SystemResourceInfo
            {
                CpuUsage = 0,
                MemoryUsage = 0,
                TotalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                Timestamp = DateTime.Now
            };
        }
    }

    public async Task<SystemResourceInfo> MonitorSystemResourcesAsync(int intervalSeconds = 5, int historyLength = 60, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            var info = GetSystemResourceInfo();

            // Update history
            Array.Copy(CpuHistory, 1, CpuHistory, 0, CpuHistory.Length - 1);
            CpuHistory[CpuHistory.Length - 1] = info.CpuUsage;

            Array.Copy(MemoryHistory, 1, MemoryHistory, 0, MemoryHistory.Length - 1);
            MemoryHistory[MemoryHistory.Length - 1] = info.MemoryUsage;

            // Check for alerts
            if (info.CpuUsage > 90)
            {
                Alerts.Add(new MonitorAlert
                {
                    AlertType = "CPU",
                    Message = $"CPU 使用率过高：{info.CpuUsage:F1}%",
                    Level = AlertLevel.Critical,
                    Timestamp = DateTime.Now
                });
            }

            if (info.MemoryUsage > 90)
            {
                Alerts.Add(new MonitorAlert
                {
                    AlertType = "Memory",
                    Message = $"内存使用率过高：{info.MemoryUsage:F1}%",
                    Level = AlertLevel.Warning,
                    Timestamp = DateTime.Now
                });
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct);
        }

        return GetSystemResourceInfo();
    }

    public DiskMonitorInfo GetDiskMonitorInfo(string driveName)
    {
        try
        {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.Name == driveName);
            if (drive == null || !drive.IsReady)
            {
                return new DiskMonitorInfo { DriveName = driveName };
            }

            return new DiskMonitorInfo
            {
                DriveName = driveName,
                UsagePercentage = drive.TotalSize > 0 ? (float)(drive.TotalFreeSpace * 100.0 / drive.TotalSize) : 0,
                FreeSpace = drive.TotalFreeSpace,
                ReadSpeed = 0,
                WriteSpeed = 0,
                Alerts = new List<DiskAlert>()
            };
        }
        catch (Exception)
        {
            return new DiskMonitorInfo { DriveName = driveName };
        }
    }

    public async Task<DiskMonitorInfo> MonitorDiskAsync(string driveName, int intervalSeconds = 5, int historyLength = 60, CancellationToken ct = default)
    {
        var history = new List<float>();

        while (!ct.IsCancellationRequested)
        {
            var info = GetDiskMonitorInfo(driveName);
            history.Add(info.UsagePercentage);

            if (history.Count > historyLength)
            {
                history.RemoveAt(0);
            }

            info.UsageHistory = history.ToArray();

            // Check for low space alert
            if (info.UsagePercentage < 10)
            {
                Alerts.Add(new MonitorAlert
                {
                    AlertType = "Disk",
                    Message = $"磁盘空间不足 ({driveName}): 剩余 {info.FreeSpace / 1024.0 / 1024.0 / 1024.0:F1} GB",
                    Level = AlertLevel.Warning,
                    Timestamp = DateTime.Now
                });
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct);
        }

        return GetDiskMonitorInfo(driveName);
    }

    public NetworkMonitorInfo GetNetworkMonitorInfo()
    {
        try
        {
            var ipv4 = IPGlobalProperties.GetIPGlobalProperties();

            long bytesSent = 0;
            long bytesReceived = 0;
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                var nicStats = ni.GetIPv4Statistics();
                bytesSent += nicStats.BytesSent;
                bytesReceived += nicStats.BytesReceived;
            }

            return new NetworkMonitorInfo
            {
                BytesSent = bytesSent,
                BytesReceived = bytesReceived,
                BandwidthUsage = 0,
                ActiveConnections = ipv4.GetActiveTcpConnections().Length,
                Timestamp = DateTime.Now
            };
        }
        catch (Exception)
        {
            return new NetworkMonitorInfo();
        }
    }

    public async Task<NetworkMonitorInfo> MonitorNetworkAsync(int intervalSeconds = 5, int historyLength = 60, CancellationToken ct = default)
    {
        var history = new List<float>();
        long lastSent = 0;
        long lastReceived = 0;

        var currentInfo = GetNetworkMonitorInfo();
        lastSent = currentInfo.BytesSent;
        lastReceived = currentInfo.BytesReceived;

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct);

            var newInfo = GetNetworkMonitorInfo();
            var sentDelta = newInfo.BytesSent - lastSent;
            var receivedDelta = newInfo.BytesReceived - lastReceived;

            var bandwidthKbps = (sentDelta + receivedDelta) * 8.0 / intervalSeconds / 1024;
            history.Add((float)bandwidthKbps);

            if (history.Count > historyLength)
            {
                history.RemoveAt(0);
            }

            lastSent = newInfo.BytesSent;
            lastReceived = newInfo.BytesReceived;

            newInfo.BandwidthUsage = (float)bandwidthKbps;
            newInfo.BandwidthHistory = history.ToArray();
        }

        return GetNetworkMonitorInfo();
    }

    public async Task SetApplicationMonitorAsync(string processName, bool autoRestart, CancellationToken ct = default)
    {
        if (!MonitoredApps.ContainsKey(processName))
        {
            MonitoredApps[processName] = new ApplicationMonitorInfo
            {
                ProcessName = processName,
                AutoRestart = autoRestart,
                CreatedTime = DateTime.Now
            };
        }

        // Start monitoring
        await Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                var appInfo = MonitoredApps[processName];

                var isRunning = Process.GetProcessesByName(processName).Any();
                appInfo.IsRunning = isRunning;

                if (!isRunning)
                {
                    appInfo.CrashCount++;
                    appInfo.LastCrashTime = DateTime.Now;

                    if (autoRestart)
                    {
                        try
                        {
                            // Try to restart the process
                            Process.Start(new ProcessStartInfo(processName) { UseShellExecute = true });
                        }
                        catch (Exception ex)
                        {
                            Alerts.Add(new MonitorAlert
                            {
                                AlertType = "Application",
                                Message = $"{processName} 重启失败：{ex.Message}",
                                Level = AlertLevel.Critical,
                                Timestamp = DateTime.Now
                            });
                        }
                    }
                    else
                    {
                        Alerts.Add(new MonitorAlert
                        {
                            AlertType = "Application",
                            Message = $"{processName} 已停止运行",
                            Level = AlertLevel.Warning,
                            Timestamp = DateTime.Now
                        });
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }, ct);
    }

    public List<ApplicationMonitorInfo> GetMonitoredApplications()
    {
        return MonitoredApps.Values.ToList();
    }

    public List<MonitorAlert> GetMonitorAlerts()
    {
        return Alerts.OrderByDescending(a => a.Timestamp).Take(100).ToList();
    }
}
