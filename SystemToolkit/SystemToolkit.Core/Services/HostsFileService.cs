namespace SystemToolkit.Core.Services;

public interface IHostsFileService
{
    string GetHostsPath();
    Task<string> ReadHostsAsync(CancellationToken ct = default);
    Task WriteHostsAsync(string content, CancellationToken ct = default);
    Task<string> BackupAsync(CancellationToken ct = default);
    IReadOnlyList<string> ValidateLines(string content);
}

public class HostsFileService : IHostsFileService
{
    public string GetHostsPath()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return Path.Combine(windows, "System32", "drivers", "etc", "hosts");
    }

    public async Task<string> ReadHostsAsync(CancellationToken ct = default)
    {
        var path = GetHostsPath();
        if (!File.Exists(path)) return string.Empty;

        // Share read so the file can be inspected while locked by other tools
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }

    public async Task WriteHostsAsync(string content, CancellationToken ct = default)
    {
        var path = GetHostsPath();
        try
        {
            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content.AsMemory(), ct);
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException("保存 hosts 需要管理员权限：请以管理员身份运行 SystemToolkit 后重试。");
        }
    }

    public async Task<string> BackupAsync(CancellationToken ct = default)
    {
        var path = GetHostsPath();
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SystemToolkit", "hosts_backup");
        Directory.CreateDirectory(dir);

        var target = Path.Combine(dir, $"hosts_{DateTime.Now:yyyyMMdd_HHmmss}");
        if (File.Exists(path))
        {
            await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            await using var dest = new FileStream(target, FileMode.Create, FileAccess.Write);
            await source.CopyToAsync(dest, ct);
        }
        else
        {
            await File.WriteAllTextAsync(target, string.Empty, ct);
        }

        return target;
    }

    public IReadOnlyList<string> ValidateLines(string content)
    {
        var problems = new List<string>();
        var lines = content.Replace("\r\n", "\n").Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                problems.Add($"第 {i + 1} 行缺少主机名：{line}");
                continue;
            }

            if (!System.Net.IPAddress.TryParse(parts[0], out _))
            {
                problems.Add($"第 {i + 1} 行 IP 格式无效：{parts[0]}");
            }
        }

        return problems;
    }
}
