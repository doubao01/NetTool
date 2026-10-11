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
    Task<string> BackupRegistryAsync(string hivePath, string backupDirectory, CancellationToken ct = default);
    Task RestoreRegistryAsync(string backupPath, CancellationToken ct = default);
    List<RegistryItem> FindInvalidKeys();
}

public class RegistryService : IRegistryService
{
    public List<RegistryItem> GetRegistryValues(string keyPath)
    {
        var items = new List<RegistryItem>();

        try
        {
            using var key = OpenKey(keyPath, writable: false);
            if (key == null) return items;

            ReadValues(key, keyPath, items);
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
            using var key = OpenKey(keyPath, writable: false);
            if (key == null) return subKeys;

            var normalized = keyPath.TrimEnd('\\');
            foreach (var subKeyName in key.GetSubKeyNames())
            {
                subKeys.Add($@"{normalized}\{subKeyName}");
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
            using var key = OpenKey(keyPath, writable: true);
            if (key == null)
            {
                throw new InvalidOperationException($"注册表项不存在：{keyPath}");
            }
            key.DeleteValue(valueName, false);
        }
        catch (InvalidOperationException)
        {
            throw;
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
            var parts = keyPath.TrimEnd('\\').Split('\\');
            var parentPath = string.Join("\\", parts.Take(parts.Length - 1));
            var keyName = parts.Last();

            using var parentKey = OpenKey(parentPath, writable: true);
            if (parentKey != null)
            {
                if (recursive)
                {
                    parentKey.DeleteSubKeyTree(keyName, throwOnMissingSubKey: false);
                }
                else
                {
                    parentKey.DeleteSubKey(keyName, throwOnMissingSubKey: false);
                }
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
            using var key = OpenKey(keyPath, writable: true) ?? OpenWritableHiveOrCreateSubKey(keyPath);
            if (key == null)
            {
                throw new InvalidOperationException($"无法打开注册表项：{keyPath}");
            }
            key.SetValue(valueName, value, kind);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidOperationException($"无法设置注册表值：{keyPath}\\{valueName}");
        }
    }

    public async Task<string> BackupRegistryAsync(string hivePath, string backupDirectory, CancellationToken ct = default)
    {
        var dir = Path.GetFileName(hivePath.TrimEnd('\\'));
        var backupFile = Path.Combine(backupDirectory, $"{dir}_{DateTime.Now:yyyyMMdd_HHmmss}.reg");

        await Task.Run(() =>
        {
            using var writer = new StreamWriter(backupFile);
            writer.WriteLine("Windows Registry Editor Version 5.00");
            writer.WriteLine();

            ct.ThrowIfCancellationRequested();
            using var root = OpenKey(hivePath, writable: false)
                ?? throw new InvalidOperationException($"无法打开注册表根：{hivePath}");
            WriteKeyRecursive(writer, hivePath.TrimEnd('\\'), root, ct);
        }, ct);

        return backupFile;
    }

    private void WriteKeyRecursive(StreamWriter writer, string keyPath, RegistryKey key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        writer.WriteLine($"[{keyPath}]");
        foreach (var valueName in key.GetValueNames())
        {
            WriteRegistryValue(writer, valueName, key.GetValue(valueName), key.GetValueKind(valueName));
        }
        writer.WriteLine();

        foreach (var subName in key.GetSubKeyNames())
        {
            try
            {
                using var sub = key.OpenSubKey(subName);
                if (sub != null)
                {
                    WriteKeyRecursive(writer, $@"{keyPath}\{subName}", sub, ct);
                }
            }
            catch (Exception)
            {
                // Skip keys that can't be read
            }
        }
    }

    private void WriteRegistryValue(StreamWriter writer, string valueName, object? value, RegistryValueKind kind)
    {
        var namePart = valueName.Length == 0 ? "@" : $"\"{valueName.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        string valuePart;
        if (value == null)
        {
            valuePart = "\"\"";
        }
        else if (value is int i)
        {
            valuePart = $"dword:{i:x8}";
        }
        else if (value is long l)
        {
            valuePart = $"hex(b):{BitConverter.ToString(BitConverter.GetBytes(l)).Replace("-", ",")}";
        }
        else if (value is byte[] bytes)
        {
            valuePart = $"hex:{BitConverter.ToString(bytes).Replace("-", ",")}";
        }
        else if (value is string[] strs)
        {
            valuePart = $"hex(7):{BitConverter.ToString(System.Text.Encoding.Unicode.GetBytes(string.Join('\0', strs) + "\0\0")).Replace("-", ",")}";
        }
        else
        {
            valuePart = $"\"{value.ToString()?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? ""}\"";
        }

        writer.WriteLine($"{namePart}={valuePart}");
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
                if (!string.IsNullOrEmpty(value.Value) &&
                    (value.Value.StartsWith("\"") || value.Value.Contains(":\\")))
                {
                    var filePath = value.Value.Trim('"');
                    if (!string.IsNullOrEmpty(filePath) &&
                        filePath.Contains(":\\") &&
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

    private static void ReadValues(RegistryKey key, string keyPath, List<RegistryItem> items)
    {
        foreach (var valueName in key.GetValueNames())
        {
            items.Add(new RegistryItem
            {
                KeyPath = keyPath,
                ValueName = valueName,
                Value = key.GetValue(valueName)?.ToString(),
                ValueType = key.GetValueKind(valueName).ToString(),
                ModifiedTime = null
            });
        }
    }

    private static RegistryKey? OpenKey(string keyPath, bool writable)
    {
        if (string.IsNullOrWhiteSpace(keyPath)) return null;

        var trimmed = keyPath.TrimEnd('\\');
        var separator = trimmed.IndexOf('\\');
        var hiveName = separator < 0 ? trimmed : trimmed.Substring(0, separator);
        var relative = separator < 0 ? null : trimmed.Substring(separator + 1);

        var hive = hiveName.ToUpperInvariant() switch
        {
            "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
            "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
            "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
            "HKEY_USERS" or "HKU" => Registry.Users,
            "HKEY_CURRENT_CONFIG" or "HKCC" => Registry.CurrentConfig,
            "HKEY_PERFORMANCE_DATA" => Registry.PerformanceData,
            _ => null
        };

        if (hive == null) return null;
        if (string.IsNullOrEmpty(relative)) return hive;

        return hive.OpenSubKey(relative, writable);
    }

    private static RegistryKey? OpenWritableHiveOrCreateSubKey(string keyPath)
    {
        var trimmed = keyPath.TrimEnd('\\');
        var separator = trimmed.IndexOf('\\');
        if (separator < 0) return null;

        var hiveName = trimmed.Substring(0, separator);
        var relative = trimmed.Substring(separator + 1);

        var hive = hiveName.ToUpperInvariant() switch
        {
            "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
            "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
            "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
            "HKEY_USERS" or "HKU" => Registry.Users,
            "HKEY_CURRENT_CONFIG" or "HKCC" => Registry.CurrentConfig,
            _ => null
        };

        return hive?.CreateSubKey(relative);
    }
}
