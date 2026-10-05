namespace SystemToolkit.Core.Services;
using SystemToolkit.Core.Models;
using DirectoryInfo = System.IO.DirectoryInfo;

using System.Security.Cryptography;
using System.Text;

public interface IFileService
{
    Task<List<FileItem>> GetFilesAsync(string directory, bool recursive = false);
    Task<List<FileItem>> FindLargeFilesAsync(string directory, long minSizeBytes, CancellationToken ct = default);
    Task<List<DuplicateFile>> FindDuplicateFilesAsync(string directory, CancellationToken ct = default);
    Task BatchRenameAsync(List<FileItem> files, RenameRule rule, CancellationToken ct = default);
    Task<string> CalculateFileHashAsync(string filePath, string algorithm = "SHA256");
    Task DeleteFilesAsync(List<string> paths, CancellationToken ct = default);
}

public class FileService : IFileService
{
    public Task<List<FileItem>> GetFilesAsync(string directory, bool recursive = false)
    {
        var files = new List<FileItem>();
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        try
        {
            var dirInfo = new DirectoryInfo(directory);
            foreach (var file in dirInfo.EnumerateFiles("*", searchOption))
            {
                files.Add(new FileItem
                {
                    Path = file.FullName,
                    Name = file.Name,
                    Size = file.Length,
                    CreatedTime = file.CreationTime,
                    ModifiedTime = file.LastWriteTime,
                    IsDirectory = false
                });
            }

            if (recursive)
            {
                foreach (var dir in dirInfo.EnumerateDirectories("*", searchOption))
                {
                    files.Add(new FileItem
                    {
                        Path = dir.FullName,
                        Name = dir.Name,
                        Size = 0,
                        CreatedTime = dir.CreationTime,
                        ModifiedTime = dir.LastWriteTime,
                        IsDirectory = true
                    });
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip unauthorized directories
        }

        return Task.FromResult(files);
    }

    public async Task<List<FileItem>> FindLargeFilesAsync(string directory, long minSizeBytes, CancellationToken ct = default)
    {
        var largeFiles = new List<FileItem>();

        try
        {
            var dirInfo = new DirectoryInfo(directory);
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();

                if (file.Length >= minSizeBytes)
                {
                    largeFiles.Add(new FileItem
                    {
                        Path = file.FullName,
                        Name = file.Name,
                        Size = file.Length,
                        CreatedTime = file.CreationTime,
                        ModifiedTime = file.LastWriteTime,
                        IsDirectory = false
                    });
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip unauthorized directories
        }

        return await Task.FromResult(largeFiles.OrderByDescending(f => f.Size).ToList());
    }

    public async Task<List<DuplicateFile>> FindDuplicateFilesAsync(string directory, CancellationToken ct = default)
    {
        var hashGroups = new Dictionary<string, List<FileItem>>();

        try
        {
            var dirInfo = new DirectoryInfo(directory);
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();

                var hash = await CalculateFileHashAsync(file.FullName);
                if (!hashGroups.ContainsKey(hash))
                {
                    hashGroups[hash] = new List<FileItem>();
                }

                hashGroups[hash].Add(new FileItem
                {
                    Path = file.FullName,
                    Name = file.Name,
                    Size = file.Length,
                    CreatedTime = file.CreationTime,
                    ModifiedTime = file.LastWriteTime,
                    IsDirectory = false,
                    Hash = hash
                });
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip unauthorized directories
        }

        var duplicates = hashGroups
            .Where(g => g.Value.Count > 1)
            .Select(g => new DuplicateFile
            {
                Hash = g.Key,
                Paths = g.Value.Select(f => f.Path).ToList(),
                Size = g.Value.First().Size
            })
            .ToList();

        return await Task.FromResult(duplicates);
    }

    public async Task BatchRenameAsync(List<FileItem> files, RenameRule rule, CancellationToken ct = default)
    {
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            string newName = file.Name;

            if (rule.UseRegex)
            {
                newName = System.Text.RegularExpressions.Regex.Replace(newName, rule.SearchPattern, rule.ReplacePattern);
            }
            else if (!string.IsNullOrEmpty(rule.SearchPattern))
            {
                newName = newName.Replace(rule.SearchPattern, rule.ReplacePattern);
            }

            newName = $"{rule.Prefix}{newName}{rule.Suffix}";

            if (!string.Equals(newName, file.Name))
            {
                string newPath = Path.Combine(Path.GetDirectoryName(file.Path)!, newName);
                File.Move(file.Path, newPath, true);
            }
        }

        await Task.CompletedTask;
    }

    public async Task<string> CalculateFileHashAsync(string filePath, string algorithm = "SHA256")
    {
        using System.Security.Cryptography.HashAlgorithm hashAlgorithm = algorithm.ToUpperInvariant() switch
        {
            "MD5" => MD5.Create(),
            "SHA1" => SHA1.Create(),
            "SHA256" => SHA256.Create(),
            "SHA384" => SHA384.Create(),
            "SHA512" => SHA512.Create(),
            _ => SHA256.Create()
        };

        await using var stream = File.OpenRead(filePath);
        var hashBytes = await hashAlgorithm.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    public async Task DeleteFilesAsync(List<string> paths, CancellationToken ct = default)
    {
        foreach (var path in paths)
        {
            ct.ThrowIfCancellationRequested();

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        await Task.CompletedTask;
    }
}
