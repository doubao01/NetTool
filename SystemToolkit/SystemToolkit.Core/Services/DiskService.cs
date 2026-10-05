namespace SystemToolkit.Core.Services;
using SystemToolkit.Core.Models;

using System.IO;
using IoDirectoryInfo = System.IO.DirectoryInfo;
using DirectoryInfo = SystemToolkit.Core.Models.DirectoryInfo;

public interface IDiskService
{
    List<DiskInfo> GetAllDrives();
    Task<List<DirectoryInfo>> AnalyzeDirectoryAsync(string path, CancellationToken ct = default);
    Task<List<LargeFile>> FindLargeFilesAsync(string directory, long minSizeBytes, int count = 100, CancellationToken ct = default);
    List<SpaceCleanupSuggestion> GetCleanupSuggestions();
    Task<long> GetDirectorySizeAsync(string path, CancellationToken ct = default);
}

public class DiskService : IDiskService
{
    public List<DiskInfo> GetAllDrives()
    {
        var drives = new List<DiskInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady)
                {
                    drives.Add(new DiskInfo
                    {
                        Name = drive.Name,
                        VolumeLabel = drive.VolumeLabel,
                        DriveType = drive.DriveType.ToString(),
                        DriveFormat = drive.DriveFormat,
                        TotalSize = drive.TotalSize,
                        TotalFreeSpace = drive.TotalFreeSpace
                    });
                }
            }
            catch (Exception)
            {
                // Skip drives that can't be accessed
            }
        }

        return drives;
    }

    public async Task<List<DirectoryInfo>> AnalyzeDirectoryAsync(string path, CancellationToken ct = default)
    {
        var dirInfos = new List<DirectoryInfo>();
        var dirInfo = new IoDirectoryInfo(path);

        if (!dirInfo.Exists)
        {
            return dirInfos;
        }

        await ScanDirectoryAsync(dirInfo, dirInfos, ct);

        return dirInfos.OrderByDescending(d => d.Size).ToList();
    }

    private async Task ScanDirectoryAsync(IoDirectoryInfo dir, List<DirectoryInfo> results, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var dirInfo = new DirectoryInfo
        {
            Path = dir.FullName,
            FileCount = 0,
            DirectoryCount = 0,
            Size = 0,
            LastModified = dir.LastWriteTime
        };

        try
        {
            // Count files and calculate size
            foreach (var file in dir.GetFiles())
            {
                try
                {
                    dirInfo.FileCount++;
                    dirInfo.Size += file.Length;
                }
                catch (Exception)
                {
                    // Skip inaccessible files
                }
            }

            // Process subdirectories
            foreach (var subDir in dir.GetDirectories())
            {
                try
                {
                    dirInfo.DirectoryCount++;
                    await ScanDirectoryAsync(subDir, results, ct);
                }
                catch (Exception)
                {
                    // Skip inaccessible directories
                }
            }
        }
        catch (Exception)
        {
            // Handle root directory access issues
        }

        results.Add(dirInfo);
        await Task.CompletedTask;
    }

    public async Task<List<LargeFile>> FindLargeFilesAsync(string directory, long minSizeBytes, int count = 100, CancellationToken ct = default)
    {
        var largeFiles = new List<LargeFile>();
        var dirInfo = new IoDirectoryInfo(directory);

        if (!dirInfo.Exists)
        {
            return largeFiles;
        }

        foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            if (file.Length >= minSizeBytes)
            {
                largeFiles.Add(new LargeFile
                {
                    Path = file.FullName,
                    Size = file.Length,
                    ModifiedTime = file.LastWriteTime,
                    Extension = file.Extension
                });
            }
        }

        return await Task.FromResult(largeFiles.OrderByDescending(f => f.Size).Take(count).ToList());
    }

    public List<SpaceCleanupSuggestion> GetCleanupSuggestions()
    {
        var suggestions = new List<SpaceCleanupSuggestion>();

        // Common cleanup locations
        var cleanupLocations = new[]
        {
            new { Path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Temp", Category = "临时文件", Safe = true },
            new { Path = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache), Category = "浏览器缓存", Safe = true },
            new { Path = @"C:\Windows\Temp", Category = "系统临时文件", Safe = true },
            new { Path = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Microsoft\Windows\WER", Category = "错误报告", Safe = true },
        };

        foreach (var location in cleanupLocations)
        {
            try
            {
                if (Directory.Exists(location.Path))
                {
                    var size = GetDirectorySize(location.Path);
                    if (size > 0)
                    {
                        suggestions.Add(new SpaceCleanupSuggestion
                        {
                            Category = location.Category,
                            Path = location.Path,
                            Size = size,
                            SafeToDelete = location.Safe,
                            Description = $"可以安全清理 {FormatSize(size)}"
                        });
                    }
                }
            }
            catch (Exception)
            {
                // Skip inaccessible locations
            }
        }

        return suggestions;
    }

    public async Task<long> GetDirectorySizeAsync(string path, CancellationToken ct = default)
    {
        return await Task.Run(() => GetDirectorySize(path), ct);
    }

    private long GetDirectorySize(string path)
    {
        long size = 0;

        try
        {
            var dirInfo = new IoDirectoryInfo(path);
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    size += file.Length;
                }
                catch (Exception)
                {
                    // Skip inaccessible files
                }
            }
        }
        catch (Exception)
        {
            // Handle access issues
        }

        return size;
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}
