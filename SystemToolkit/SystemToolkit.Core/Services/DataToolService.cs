namespace SystemToolkit.Core.Services;
using SystemToolkit.Core.Models;

using System.Security.Cryptography;
using System.Text;

public interface IDataToolService
{
    HashResult CalculateHash(string input, string algorithm);
    EncryptionResult EncryptAes(string input, string key, string iv);
    DecryptionResult DecryptAes(string encrypted, string key, string iv);
    CsvData ParseCsv(string filePath);
    void ExportCsv(string filePath, CsvData data);
    LogStatistics AnalyzeLogFile(string filePath);
    List<LogEntry> ParseLogFile(string filePath);
}

public class DataToolService : IDataToolService
{
    public HashResult CalculateHash(string input, string algorithm)
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

        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = hashAlgorithm.ComputeHash(inputBytes);
        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        return new HashResult
        {
            Algorithm = algorithm,
            Hash = hash,
            Input = input
        };
    }

    public EncryptionResult EncryptAes(string input, string key, string iv)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));
            aes.IV = Encoding.UTF8.GetBytes(iv.PadRight(16).Substring(0, 16));

            using var encryptor = aes.CreateEncryptor();
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var encryptedBytes = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);

            return new EncryptionResult
            {
                Algorithm = "AES",
                EncryptedData = Convert.ToBase64String(encryptedBytes),
                Key = key,
                IV = iv
            };
        }
        catch (Exception ex)
        {
            return new EncryptionResult
            {
                Algorithm = "AES",
                EncryptedData = string.Empty,
                Error = ex.Message
            };
        }
    }

    public DecryptionResult DecryptAes(string encrypted, string key, string iv)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));
            aes.IV = Encoding.UTF8.GetBytes(iv.PadRight(16).Substring(0, 16));

            using var decryptor = aes.CreateDecryptor();
            var encryptedBytes = Convert.FromBase64String(encrypted);
            var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

            return new DecryptionResult
            {
                Algorithm = "AES",
                DecryptedData = Encoding.UTF8.GetString(decryptedBytes)
            };
        }
        catch (Exception ex)
        {
            return new DecryptionResult
            {
                Algorithm = "AES",
                DecryptedData = string.Empty,
                Error = ex.Message
            };
        }
    }

    public CsvData ParseCsv(string filePath)
    {
        var data = new CsvData();
        var lines = File.ReadAllLines(filePath);

        if (lines.Length == 0)
        {
            return data;
        }

        // Parse header
        var headers = ParseCsvLine(lines[0]);
        for (int i = 0; i < headers.Count; i++)
        {
            data.Columns.Add(new CsvColumn
            {
                Name = headers[i],
                Type = "string",
                Index = i
            });
        }

        // Parse data rows
        for (int i = 1; i < lines.Length; i++)
        {
            var values = ParseCsvLine(lines[i]);
            var row = new Dictionary<string, string?>();

            for (int j = 0; j < Math.Min(headers.Count, values.Count); j++)
            {
                row[headers[j]] = values[j];
            }

            // Fill missing columns with null
            for (int j = values.Count; j < headers.Count; j++)
            {
                row[headers[j]] = null;
            }

            data.Rows.Add(row);
        }

        return data;
    }

    private List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var currentValue = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    currentValue.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(currentValue.ToString());
                currentValue.Clear();
            }
            else
            {
                currentValue.Append(c);
            }
        }

        values.Add(currentValue.ToString());
        return values;
    }

    public void ExportCsv(string filePath, CsvData data)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        // Write header
        writer.WriteLine(string.Join(",", data.Columns.Select(c => EscapeCsvField(c.Name))));

        // Write data rows
        foreach (var row in data.Rows)
        {
            var values = data.Columns.Select(c => EscapeCsvField(row.TryGetValue(c.Name, out var val) ? val ?? "" : ""));
            writer.WriteLine(string.Join(",", values));
        }
    }

    private string EscapeCsvField(string field)
    {
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        return field;
    }

    public LogStatistics AnalyzeLogFile(string filePath)
    {
        var entries = ParseLogFile(filePath);
        var stats = new LogStatistics
        {
            TotalEntries = entries.Count,
            LevelCounts = new Dictionary<string, int>(),
            FirstEntry = entries.FirstOrDefault()?.Timestamp,
            LastEntry = entries.LastOrDefault()?.Timestamp,
            Alerts = new List<LogAlert>()
        };

        // Count by level
        foreach (var entry in entries)
        {
            if (!stats.LevelCounts.ContainsKey(entry.Level))
            {
                stats.LevelCounts[entry.Level] = 0;
            }
            stats.LevelCounts[entry.Level]++;
        }

        // Find error patterns
        var errorGroups = entries
            .Where(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase) ||
                       e.Level.Equals("FATAL", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => e.Message)
            .Where(g => g.Count() >= 3);

        foreach (var group in errorGroups)
        {
            stats.Alerts.Add(new LogAlert
            {
                Message = group.Key,
                Level = "ERROR",
                Count = group.Count(),
                FirstOccurrence = group.Min(e => e.Timestamp),
                LastOccurrence = group.Max(e => e.Timestamp)
            });
        }

        return stats;
    }

    public List<LogEntry> ParseLogFile(string filePath)
    {
        var entries = new List<LogEntry>();

        try
        {
            var lines = File.ReadAllLines(filePath);

            foreach (var line in lines)
            {
                // Try to parse common log formats
                var entry = ParseLogLine(line);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
        }
        catch (Exception)
        {
            // Handle file access errors
        }

        return entries;
    }

    private LogEntry? ParseLogLine(string line)
    {
        // Common log format: [timestamp] [level] [source] message
        var patterns = new[]
        {
            @"^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\] \[(\w+)\] (?:\[([^\]]+)\])? (.+)$",
            @"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})\.?\d* \[?(\w+)\]?(?: - \[([^\]]+)\])? - (.+)$",
            @"^(\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}) (ERROR|WARN|INFO|DEBUG|FATAL) (.+)$"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, pattern);
            if (match.Success)
            {
                var timestamp = DateTime.Parse(match.Groups[1].Value);
                var level = match.Groups[2].Value;
                var source = match.Groups.Count > 3 ? match.Groups[3].Value : null;
                var message = match.Groups[^1].Value;

                return new LogEntry
                {
                    Timestamp = timestamp,
                    Level = level.ToUpper(),
                    Message = message,
                    Source = source,
                    Exception = null
                };
            }
        }

        // Fallback: simple format
        return new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = "INFO",
            Message = line,
            Source = null,
            Exception = null
        };
    }
}
