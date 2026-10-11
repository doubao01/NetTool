using FluentAssertions;
using Microsoft.Win32;
using SystemToolkit.Core.Models;
using SystemToolkit.Core.Services;
using Xunit;

namespace SystemToolkit.Tests;

public class DataToolCsvTests
{
    private static string WriteTempCsv(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "csv_test_" + Guid.NewGuid() + ".csv");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ParseCsv_QuotedFields_ParsesColumnsAndRows()
    {
        var service = new DataToolService();
        var path = WriteTempCsv("name,city,note\nAlice,\"Rome, IT\",\"says \"\"hi\"\"\"\nBob,London,\n");

        try
        {
            var data = service.ParseCsv(path);

            data.Columns.Should().HaveCount(3);
            data.Columns.Select(c => c.Name).Should().Equal("name", "city", "note");
            data.Rows.Should().HaveCount(2);
            data.Rows[0]["city"].Should().Be("Rome, IT");
            data.Rows[0]["note"].Should().Be("says \"hi\"");
            data.Rows[1]["note"].Should().BeNullOrEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExportCsv_RoundTrip_PreservesData()
    {
        var service = new DataToolService();
        var source = WriteTempCsv("id,label\n1,\"two,words\"\n2,plain\n");

        try
        {
            var data = service.ParseCsv(source);
            var target = Path.Combine(Path.GetTempPath(), "csv_out_" + Guid.NewGuid() + ".csv");

            service.ExportCsv(target, data);
            var reloaded = service.ParseCsv(target);

            reloaded.Rows.Should().HaveCount(2);
            reloaded.Rows[0]["label"].Should().Be("two,words");
            File.Delete(target);
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public void ParseCsv_MissingColumns_FillsWithNull()
    {
        var service = new DataToolService();
        var path = WriteTempCsv("a,b,c\n1,2\n");

        try
        {
            var data = service.ParseCsv(path);
            data.Rows.Should().ContainSingle();
            data.Rows[0]["c"].Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class DataToolLogTests
{
    [Theory]
    [InlineData("[2026-01-02 03:04:05] [ERROR] [App] boom")]
    [InlineData("2026-01-02T03:04:05 INFO - service started")]
    [InlineData("2026/01/02 03:04:05 WARN disk almost full")]
    public void ParseLogFile_CommonFormats_ParsesEntries(string line)
    {
        var service = new DataToolService();
        var path = Path.Combine(Path.GetTempPath(), "log_test_" + Guid.NewGuid() + ".log");
        File.WriteAllText(path, line);

        try
        {
            var entries = service.ParseLogFile(path);
            entries.Should().ContainSingle();
            entries[0].Message.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AnalyzeLogFile_CountsLevelsAndFlagsRepeatedErrors()
    {
        var service = new DataToolService();
        var path = Path.Combine(Path.GetTempPath(), "log_test_" + Guid.NewGuid() + ".log");
        var lines = new[]
        {
            "[2026-01-02 03:04:05] [INFO] boot",
            "[2026-01-02 03:04:06] [ERROR] disk failed",
            "[2026-01-02 03:04:07] [ERROR] disk failed",
            "[2026-01-02 03:04:08] [ERROR] disk failed",
        };
        File.WriteAllLines(path, lines);

        try
        {
            var stats = service.AnalyzeLogFile(path);

            stats.TotalEntries.Should().Be(4);
            stats.LevelCounts["INFO"].Should().Be(1);
            stats.LevelCounts["ERROR"].Should().Be(3);
            stats.FirstEntry.Should().NotBeNull();
            stats.LastEntry.Should().NotBeNull();
            stats.Alerts.Should().ContainSingle(a => a.Message == "disk failed" && a.Count == 3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AnalyzeLogFile_EmptyFile_ReturnsZeroStats()
    {
        var service = new DataToolService();
        var path = Path.Combine(Path.GetTempPath(), "log_empty_" + Guid.NewGuid() + ".log");
        File.WriteAllText(path, "not a parseable line\n");

        try
        {
            var stats = service.AnalyzeLogFile(path);
            stats.TotalEntries.Should().Be(0);
            stats.FirstEntry.Should().BeNull();
            stats.LastEntry.Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class DevToolCodeGenerationTests
{
    [Fact]
    public void GenerateCode_ByTemplateKey_ReplacesPlaceholders()
    {
        var service = new DevToolService();
        var code = service.GenerateCode("csharp_class", new Dictionary<string, string>
        {
            ["ClassName"] = "Widget",
            ["Properties"] = "public int Id { get; set; }",
            ["Constructor"] = "// init"
        });

        code.Should().Contain("public class Widget");
        code.Should().Contain("public int Id { get; set; }");
        code.Should().NotContain("{{ClassName}}");
    }

    [Fact]
    public void GenerateCode_ByDisplayName_FallsBackToNameMatch()
    {
        var service = new DevToolService();
        var code = service.GenerateCode("C# Class", new Dictionary<string, string> { ["ClassName"] = "Alpha" });
        code.Should().Contain("public class Alpha");
    }

    [Fact]
    public void GenerateCode_UnknownTemplate_Throws()
    {
        var service = new DevToolService();
        var act = () => service.GenerateCode("no_such_template", new Dictionary<string, string>());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetAvailableTemplates_ExposesNamesAndParameters()
    {
        var service = new DevToolService();
        var templates = service.GetAvailableTemplates();

        templates.Should().NotBeEmpty();
        templates.Should().Contain(t => t.Name == "C# Class" && t.Parameters.Count > 0);
    }
}

public class NetworkServiceTests
{
    [Fact]
    public void GetPortUsage_OnWindows_ReturnsSnapshot()
    {
        if (!OperatingSystem.IsWindows()) return;
        var service = new NetworkService();

        var ports = service.GetPortUsage();

        ports.Should().NotBeNull();
        ports.Where(p => p.State == "Listen")
            .Should().OnlyContain(p => p.Port > 0);
    }

    [Fact]
    public async Task ScanPorts_ClosedPortReportedAsNotOpen()
    {
        var service = new NetworkService();
        var results = await service.ScanPortsAsync("127.0.0.1", new List<int> { 65000 });

        results.Should().ContainSingle();
        results[0].IsOpen.Should().BeFalse();
        results[0].Host.Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task ScanPorts_InvalidRangeHonoursCancellation()
    {
        var service = new NetworkService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await service.ScanPortsAsync("127.0.0.1", new List<int> { 1, 2, 3 }, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

public class RegistryServiceTests : IDisposable
{
    private readonly RegistryService _service = new();
    private readonly string _testRoot = $@"HKEY_CURRENT_USER\Software\NetToolTests_{Guid.NewGuid():N}";

    [Fact]
    public void SetValue_AndGetRegistryValues_RoundTripsString()
    {
        if (!OperatingSystem.IsWindows()) return;
        _service.SetValue(_testRoot, "demo", "hello", RegistryValueKind.String);

        var values = _service.GetRegistryValues(_testRoot);
        values.Should().ContainSingle(v => v.ValueName == "demo");
        values.Single(v => v.ValueName == "demo").Value.Should().Be("hello");
    }

    [Fact]
    public void SetValue_DWord_KeepsNumber()
    {
        if (!OperatingSystem.IsWindows()) return;
        _service.SetValue(_testRoot, "num", 42, RegistryValueKind.DWord);

        var values = _service.GetRegistryValues(_testRoot);
        values.Single(v => v.ValueName == "num").Value.Should().Be("42");
    }

    [Fact]
    public void DeleteValue_RemovesOnlyNamedValue()
    {
        if (!OperatingSystem.IsWindows()) return;
        _service.SetValue(_testRoot, "keep", "1", RegistryValueKind.String);
        _service.SetValue(_testRoot, "drop", "2", RegistryValueKind.String);

        _service.DeleteValue(_testRoot, "drop");

        var values = _service.GetRegistryValues(_testRoot);
        values.Should().ContainSingle(v => v.ValueName == "keep");
    }

    [Fact]
    public void GetSubKeys_ListsCreatedSubKey()
    {
        if (!OperatingSystem.IsWindows()) return;
        _service.SetValue($@"{_testRoot}\Child", "x", "y", RegistryValueKind.String);

        var subKeys = _service.GetSubKeys(_testRoot);
        subKeys.Should().Contain($@"{_testRoot}\Child");
    }

    [Fact]
    public void DeleteKey_NonRecursive_FailsWhenSubKeysRemain()
    {
        if (!OperatingSystem.IsWindows()) return;
        _service.SetValue($@"{_testRoot}\Blocker", "x", "y", RegistryValueKind.String);

        var act = () => _service.DeleteKey(_testRoot, recursive: false);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void DeleteKey_RemovesEmptyKey()
    {
        if (!OperatingSystem.IsWindows()) return;
        _service.SetValue($@"{_testRoot}\Empty", "x", "y", RegistryValueKind.String);
        _service.DeleteValue($@"{_testRoot}\Empty", "x");

        _service.DeleteKey($@"{_testRoot}\Empty", recursive: false);

        _service.GetSubKeys(_testRoot).Should().NotContain($@"{_testRoot}\Empty");
    }

    [Fact]
    public void FindInvalidKeys_ReturnsSnapshotWithoutThrowing()
    {
        var acts = () => _service.FindInvalidKeys();
        acts.Should().NotThrow();
    }

    [Fact]
    public async Task BackupRegistryAsync_WritesRegFileUnderDirectory()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dir = Path.Combine(Path.GetTempPath(), "regbackup_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        _service.SetValue(_testRoot, "demo", "hello");

        try
        {
            var file = await _service.BackupRegistryAsync(_testRoot, dir);

            file.Should().EndWith(".reg");
            File.Exists(file).Should().BeTrue();
            var head = await File.ReadAllLinesAsync(file);
            head[0].Should().Contain("Windows Registry Editor Version");
            (await File.ReadAllTextAsync(file)).Should().Contain("\"demo\"=\"hello\"");
        }
        finally
        {
            foreach (var f in Directory.GetFiles(dir))
            {
                File.Delete(f);
            }
            Directory.Delete(dir);
        }
    }

    public void Dispose()
    {
        try
        {
            const string prefix = @"HKEY_CURRENT_USER\";
            var relative = _testRoot.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? _testRoot.Substring(prefix.Length)
                : _testRoot;
            Registry.CurrentUser.DeleteSubKeyTree(relative, throwOnMissingSubKey: false);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
