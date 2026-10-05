namespace SystemToolkit.Core.Services;
using SystemToolkit.Core.Models;

using Microsoft.Win32;

public interface IRegistryService
{
    List<RegistryItem> GetRegistryValues(string keyPath);
    List<string> GetSubKeys(string keyPath);
    void DeleteValue(string keyPath, string valueName);
    void DeleteKey(string keyPath, bool recursive = false);
    void SetValue(string keyPath, string valueName, object value, RegistryValueKind kind = RegistryValueKind.String);
    Task<string> BackupRegistryAsync(string backupPath, CancellationToken ct = default);
    Task RestoreRegistryAsync(string backupPath, CancellationToken ct = default);
    List<RegistryItem> FindInvalidKeys();
}

public class RegistryService : IRegistryService
{
    private static readonly string[] RootKeys = {
        @"HKEY_CURRENT_USER",
        @"HKEY_LOCAL_MACHINE",
        @"HKEY_CLASSES_ROOT",
        @"HKEY_USERS",
        @"HKEY_CURRENT_CONFIG"
    };

    public List<RegistryItem> GetRegistryValues(string keyPath)
    {
        var items = new List<RegistryItem>();

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath.Replace(@"HKEY_CURRENT_USER\", "") ?? keyPath);
            if (key == null) return items;

            foreach (var valueName in key.GetValueNames())
            {
                var value = key.GetValue(valueName);
                var valueKind = key.GetValueKind(valueName);

                items.Add(new RegistryItem
                {
                    KeyPath = keyPath,
                    ValueName = valueName,
                    Value = value?.ToString(),
                    ValueType = valueKind.ToString(),
                    ModifiedTime = null
                });
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip unauthorized keys
        }
        catch (Exception)
        {
            // Skip invalid keys
        }

        return items;
    }

    public List<string> GetSubKeys(string keyPath)
    {
        var subKeys = new List<string>();

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath.Replace(@"HKEY_CURRENT_USER\", "") ?? keyPath);
            if (key == null) return subKeys;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                subKeys.Add($@"{keyPath}\{subKeyName}");
            }
        }
        catch (Exception)
        {
            // Skip invalid keys
        }

        return subKeys;
    }

    public void DeleteValue(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
            key?.DeleteValue(valueName, false);
        }
        catch (Exception)
        {
            throw new InvalidOperationException($"无法删除注册表值：{keyPath}\\{valueName}");
        }
    }

    public void DeleteKey(string keyPath, bool recursive = false)
    {
        try
        {
            var parts = keyPath.Split('\\');
            var parentPath = string.Join("\\", parts.Take(parts.Length - 1));
            var keyName = parts.Last();

            using var parentKey = Registry.CurrentUser.OpenSubKey(parentPath, true);
            if (parentKey != null)
            {
                parentKey.DeleteSubKeyTree(keyName, recursive);
            }
        }
        catch (Exception)
        {
            throw new InvalidOperationException($"无法删除注册表项：{keyPath}");
        }
    }

    public void SetValue(string keyPath, string valueName, object value, RegistryValueKind kind = RegistryValueKind.String)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(keyPath, true);
            key?.SetValue(valueName, value, kind);
        }
        catch (Exception)
        {
            throw new InvalidOperationException($"无法设置注册表值：{keyPath}\\{valueName}");
        }
    }

    public async Task<string> BackupRegistryAsync(string backupPath, CancellationToken ct = default)
    {
        var backupFile = Path.Combine(backupPath, $"registry_backup_{DateTime.Now:yyyyMMdd_HHmmss}.reg");

        await Task.Run(() =>
        {
            using var writer = new StreamWriter(backupFile);
            writer.WriteLine("Windows Registry Editor Version 5.00");
            writer.WriteLine();

            foreach (var rootKey in RootKeys)
            {
                ct.ThrowIfCancellationRequested();
                WriteKeyToBackup(writer, rootKey);
            }
        }, ct);

        return backupFile;
    }

    private void WriteKeyToBackup(StreamWriter writer, string keyPath)
    {
        try
        {
            using var key = GetRegistryKey(keyPath);
            if (key == null) return;

            writer.WriteLine($"[{keyPath}]");

            foreach (var valueName in key.GetValueNames())
            {
                var value = key.GetValue(valueName);
                var valueKind = key.GetValueKind(valueName);
                WriteRegistryValue(writer, valueName, value, valueKind);
            }

            writer.WriteLine();
        }
        catch (Exception)
        {
            // Skip keys that can't be read
        }
    }

    private RegistryKey? GetRegistryKey(string keyPath)
    {
        return keyPath.ToUpperInvariant() switch
        {
            string s when s.StartsWith(@"HKEY_CURRENT_USER\") =>
                Registry.CurrentUser.OpenSubKey(keyPath.Substring(18)),
            string s when s.StartsWith(@"HKEY_LOCAL_MACHINE\") =>
                Registry.LocalMachine.OpenSubKey(keyPath.Substring(20)),
            string s when s.StartsWith(@"HKEY_CLASSES_ROOT\") =>
                Registry.ClassesRoot.OpenSubKey(keyPath.Substring(19)),
            string s when s.StartsWith(@"HKEY_USERS\") =>
                Registry.Users.OpenSubKey(keyPath.Substring(12)),
            string s when s.StartsWith(@"HKEY_CURRENT_CONFIG\") =>
                Registry.CurrentConfig.OpenSubKey(keyPath.Substring(21)),
            _ => null
        };
    }

    private void WriteRegistryValue(StreamWriter writer, string valueName, object? value, RegistryValueKind kind)
    {
        var valueStr = value switch
        {
            string s => $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"",
            int i => $"dword:{i:x8}",
            long l => $"hex(b):{BitConverter.ToString(BitConverter.GetBytes(l)).Replace("-", ",").ToLower()}",
            byte[] bytes => $"hex:{BitConverter.ToString(bytes).Replace("-", ",").ToLower()}",
            _ => $"\"{value?.ToString() ?? ""}\""
        };

        writer.WriteLine($"\"{valueName}\"={valueStr}");
    }

    public Task RestoreRegistryAsync(string backupPath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "regedit",
                Arguments = $"/s \"{backupPath}\"",
                UseShellExecute = true,
                Verb = "runas"
            };

            using var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit();
        }, ct);
    }

    public List<RegistryItem> FindInvalidKeys()
    {
        var invalidKeys = new List<RegistryItem>();

        // 检查常见的无效注册表项位置
        var commonInvalidPaths = new[]
        {
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run",
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\RunOnce",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"
        };

        foreach (var path in commonInvalidPaths)
        {
            var values = GetRegistryValues(path);
            foreach (var value in values)
            {
                // 检查文件路径是否存在
                if (!string.IsNullOrEmpty(value.Value) &&
                    (value.Value.StartsWith("\"") || value.Value.Contains(":\\")))
                {
                    var filePath = value.Value.Trim('"');
                    if (!string.IsNullOrEmpty(filePath) &&
                        filePath.Contains(":\\" ) &&
                        !File.Exists(filePath) &&
                        !Directory.Exists(filePath))
                    {
                        invalidKeys.Add(value);
                    }
                }
            }
        }

        return invalidKeys;
    }
}
