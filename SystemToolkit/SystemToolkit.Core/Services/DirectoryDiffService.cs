using System.Security.Cryptography;

namespace SystemToolkit.Core.Services;

using SystemToolkit.Core.Models;

public interface IDirectoryDiffService
{
    Task<DirectoryDiffResult> CompareAsync(string left, string right, bool compareContent = true, CancellationToken ct = default);
}

public class DirectoryDiffService : IDirectoryDiffService
{
    public async Task<DirectoryDiffResult> CompareAsync(string left, string right, bool compareContent = true, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        left = Path.GetFullPath(left);
        right = Path.GetFullPath(right);
        if (!Directory.Exists(left)) throw new DirectoryNotFoundException($"目录不存在：{left}");
        if (!Directory.Exists(right)) throw new DirectoryNotFoundException($"目录不存在：{right}");

        var leftFiles = await Task.Run(() => EnumerateFiles(left, ct), ct);
        var rightFiles = await Task.Run(() => EnumerateFiles(right, ct), ct);

        var result = new DirectoryDiffResult();

        foreach (var (path, length) in leftFiles)
        {
            ct.ThrowIfCancellationRequested();

            if (!rightFiles.TryGetValue(path, out var rightLength))
            {
                result.OnlyInLeft.Add(path);
                continue;
            }

            if (length != rightLength)
            {
                result.Different.Add(path);
                continue;
            }

            if (!compareContent)
            {
                result.Same.Add(path);
                continue;
            }

            if (await AreFilesEqualAsync(Path.Combine(left, path), Path.Combine(right, path), ct))
            {
                result.Same.Add(path);
            }
            else
            {
                result.Different.Add(path);
            }
        }

        foreach (var path in rightFiles.Keys)
        {
            ct.ThrowIfCancellationRequested();
            if (!leftFiles.ContainsKey(path))
            {
                result.OnlyInRight.Add(path);
            }
        }

        result.OnlyInLeft.Sort(StringComparer.OrdinalIgnoreCase);
        result.OnlyInRight.Sort(StringComparer.OrdinalIgnoreCase);
        result.Different.Sort(StringComparer.OrdinalIgnoreCase);
        result.Same.Sort(StringComparer.OrdinalIgnoreCase);

        ct.ThrowIfCancellationRequested();
        return result;
    }

    private static Dictionary<string, long> EnumerateFiles(string root, CancellationToken ct)
    {
        var files = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var current = pending.Pop();
            var attributes = File.GetAttributes(current);
            // An incomplete traversal must never produce an identical result.
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"无法对比链接或重解析点：{current}");
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(current))
                {
                    ct.ThrowIfCancellationRequested();
                    pending.Push(entry);
                }
            }
            else
            {
                files.Add(Path.GetRelativePath(root, current), new FileInfo(current).Length);
            }
        }

        return files;
    }

    private static async Task<bool> AreFilesEqualAsync(string fileA, string fileB, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        await using var streamA = File.OpenRead(fileA);
        await using var streamB = File.OpenRead(fileB);

        var hashA = await sha.ComputeHashAsync(streamA, ct);
        var hashB = await sha.ComputeHashAsync(streamB, ct);
        return hashA.AsSpan().SequenceEqual(hashB);
    }
}
