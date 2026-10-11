using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace SystemToolkit.Core.Services;

using Microsoft.Win32;
using SystemToolkit.Core.Models;

public interface ISystemInfoService
{
    SystemOverviewInfo GetOverview();
    List<DiskOverviewInfo> GetDisks();
    List<NetworkAdapterOverview> GetNetworkAdapters();
}

public class SystemInfoService : ISystemInfoService
{
    public SystemOverviewInfo GetOverview()
    {
        var info = new SystemOverviewInfo
        {
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            OsCaption = RuntimeInformation.OSDescription,
            ProcessorCount = Environment.ProcessorCount,
            ClrVersion = Environment.Version.ToString(),
            Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
        };

        var memory = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref memory))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        info.TotalMemoryBytes = checked((long)memory.TotalPhysical);
        info.AvailableMemoryBytes = checked((long)memory.AvailablePhysical);
        ReadWindowsProductInfo(info);

        return info;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    private static void ReadWindowsProductInfo(SystemOverviewInfo info)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var ntc = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            info.ProductName = ntc?.GetValue("ProductName")?.ToString() ?? string.Empty;
            info.ProductVersion = ntc?.GetValue("DisplayVersion")?.ToString()
                                  ?? ntc?.GetValue("ReleaseId")?.ToString() ?? string.Empty;
            info.BuildNumber = ntc?.GetValue("CurrentBuildNumber")?.ToString() ?? string.Empty;

            using var cpu = baseKey.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            info.CpuName = cpu?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? string.Empty;
        }
        catch
        {
            // Registry reads are best-effort; overview still valid without them
        }
    }

    public List<DiskOverviewInfo> GetDisks()
    {
        var list = new List<DiskOverviewInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;

                var used = drive.TotalSize - drive.TotalFreeSpace;
                list.Add(new DiskOverviewInfo
                {
                    Name = drive.Name,
                    VolumeLabel = drive.VolumeLabel,
                    DriveType = drive.DriveType.ToString(),
                    FilesystemFormat = drive.DriveFormat,
                    TotalBytes = drive.TotalSize,
                    FreeBytes = drive.TotalFreeSpace,
                    UsedPercentage = drive.TotalSize > 0 ? used * 100.0 / drive.TotalSize : 0
                });
            }
            catch
            {
                // Skip drives that can't be queried
            }
        }

        return list;
    }

    public List<NetworkAdapterOverview> GetNetworkAdapters()
    {
        var list = new List<NetworkAdapterOverview>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            var entry = new NetworkAdapterOverview
            {
                Name = nic.Name,
                Description = nic.Description,
                AdapterType = nic.NetworkInterfaceType.ToString(),
                OperationalStatus = nic.OperationalStatus.ToString(),
                MacAddress = FormatMac(nic.GetPhysicalAddress().ToString())
            };

            try
            {
                var props = nic.GetIPProperties();
                entry.Ipv4Address = props.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.Address.ToString())
                    .FirstOrDefault() ?? string.Empty;
                entry.DhcpEnabled = props.GetIPv4Properties()?.IsDhcpEnabled == true ? "已启用" : "静态";
            }
            catch
            {
                entry.Ipv4Address = string.Empty;
                entry.DhcpEnabled = "未知";
            }

            list.Add(entry);
        }

        return list;
    }

    private static string FormatMac(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        var parts = new List<string>();
        for (int i = 0; i < raw.Length; i += 2)
        {
            parts.Add(raw.Substring(i, Math.Min(2, raw.Length - i)));
        }

        return string.Join(":", parts);
    }
}
