namespace SystemToolkit.Core.Models;

public class SystemResourceInfo
{
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public long TotalMemory { get; set; }
    public double[] CpuUsageHistory { get; set; } = Array.Empty<double>();
    public double[] MemoryUsageHistory { get; set; } = Array.Empty<double>();
    public DateTime Timestamp { get; set; }
}

public class DiskMonitorInfo
{
    public string DriveName { get; set; } = string.Empty;
    public float UsagePercentage { get; set; }
    public long FreeSpace { get; set; }
    public float ReadSpeed { get; set; }
    public float WriteSpeed { get; set; }
    public float[] UsageHistory { get; set; } = Array.Empty<float>();
    public List<DiskAlert> Alerts { get; set; } = new();
}

public class DiskAlert
{
    public string Message { get; set; } = string.Empty;
    public AlertLevel Level { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum AlertLevel
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public class NetworkMonitorInfo
{
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public float BandwidthUsage { get; set; }
    public int ActiveConnections { get; set; }
    public float[] BandwidthHistory { get; set; } = Array.Empty<float>();
    public DateTime Timestamp { get; set; }
}

public class ApplicationMonitorInfo
{
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public bool IsRunning { get; set; }
    public TimeSpan Uptime { get; set; }
    public int CrashCount { get; set; }
    public bool AutoRestart { get; set; }
    public bool WasRunning { get; set; }
    public DateTime? LastCrashTime { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class MonitorAlert
{
    public string AlertType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertLevel Level { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }
}
