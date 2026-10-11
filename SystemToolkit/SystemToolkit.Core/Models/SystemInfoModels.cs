namespace SystemToolkit.Core.Models;

public class SystemOverviewInfo
{
    public string MachineName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductVersion { get; set; } = string.Empty;
    public string BuildNumber { get; set; } = string.Empty;
    public string OsCaption { get; set; } = string.Empty;
    public string CpuName { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public long TotalMemoryBytes { get; set; }
    public long AvailableMemoryBytes { get; set; }
    public string ClrVersion { get; set; } = string.Empty;
    public TimeSpan Uptime { get; set; }
}

public class DiskOverviewInfo
{
    public string Name { get; set; } = string.Empty;
    public string VolumeLabel { get; set; } = string.Empty;
    public string DriveType { get; set; } = string.Empty;
    public string FilesystemFormat { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public double UsedPercentage { get; set; }
}

public class NetworkAdapterOverview
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AdapterType { get; set; } = string.Empty;
    public string OperationalStatus { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Ipv4Address { get; set; } = string.Empty;
    public string DhcpEnabled { get; set; } = string.Empty;
}
