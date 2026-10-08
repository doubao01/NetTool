using System.IO;
using DeerFlow.WPF.Services;
using DeerFlow.WPF.Services.Plugins;
using Xunit;

namespace DeerFlow.WPF.Tests.Services;

public class DiagnosticsPluginTests
{
    private sealed class FakeSnapshotService : IMemorySnapshotService
    {
        public int GcCalls { get; private set; }

        public MemorySnapshot TakeSnapshot(string label) => new()
        {
            Label = label,
            WorkingSetBytes = 100 * 1024 * 1024,
            ManagedMemoryBytes = 50 * 1024 * 1024,
            TotalThreads = 12,
            HandleCount = 340,
            Gen0Collections = 10,
            Gen1Collections = 4,
            Gen2Collections = 1
        };

        public MemoryDiffReport Compare(MemorySnapshot before, MemorySnapshot after) => new()
        {
            BeforeLabel = before.Label,
            AfterLabel = after.Label,
            ManagedMemoryDeltaBytes = 2 * 1024 * 1024,
            ThreadDelta = 1,
            PotentialLeak = false,
            Summary = "对比完成"
        };

        public void ForceGcCollect() => GcCalls++;
    }

    private readonly FakeSnapshotService _service = new();
    private readonly DiagnosticsPlugin _plugin;

    public DiagnosticsPluginTests()
    {
        _plugin = new DiagnosticsPlugin(_service);
    }

    [Fact]
    public void TakeSnapshot_ReturnsFormattedMetrics()
    {
        var result = _plugin.TakeSnapshot("run1");

        Assert.Contains("run1", result);
        Assert.Contains("100.00 MB", result);
        Assert.Contains("50.00 MB", result);
    }

    [Fact]
    public void TakeSnapshot_BlankLabel_ReturnsError()
    {
        var result = _plugin.TakeSnapshot("  ");

        Assert.Contains("失败", result);
    }

    [Fact]
    public void CompareSnapshots_MissingBefore_ReturnsError()
    {
        _plugin.TakeSnapshot("after");

        var result = _plugin.CompareSnapshots("before", "after");

        Assert.Contains("before", result);
        Assert.Contains("找不到", result);
    }

    [Fact]
    public void CompareSnapshots_BothSaved_ReturnsReport()
    {
        _plugin.TakeSnapshot("before");
        _plugin.TakeSnapshot("after");

        var result = _plugin.CompareSnapshots("before", "after");

        Assert.Contains("对比完成", result);
        Assert.Contains("潜在泄漏: 否", result);
    }

    [Fact]
    public void ForceGcCollect_ReportsFreedMemory()
    {
        var result = _plugin.ForceGcCollect();

        Assert.Equal(1, _service.GcCalls);
        Assert.Contains("垃圾回收完成", result);
    }
}

public class ExecutionHistoryStoreTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"exec_history_test_{Guid.NewGuid():N}.db");

    private readonly ExecutionHistoryStore _store;

    public ExecutionHistoryStoreTests()
    {
        _store = new ExecutionHistoryStore(_dbPath);
    }

    [Fact]
    public async Task AddAsync_PersistsRecord()
    {
        var id = await _store.AddAsync(new ExecutionHistoryRecord
        {
            WorkflowName = "测试工作流",
            Goal = "验证落库",
            CompletedSteps = 2,
            TotalSteps = 3,
            ToolCallCount = 5,
            ToolErrorCount = 1,
            TotalElapsedMs = 1234,
            ExecutionLog = "#1 (10ms) 工具: 无",
            ResultSummary = "[1] 完成: ok",
            Status = "完成"
        });

        Assert.True(id > 0);
        Assert.Equal(1, _store.Count());

        var recent = _store.GetRecent();
        var record = Assert.Single(recent);
        Assert.Equal("测试工作流", record.WorkflowName);
        Assert.Equal(2, record.CompletedSteps);
        Assert.Equal(5, record.ToolCallCount);
        Assert.Equal(1234, record.TotalElapsedMs);
        Assert.Equal("完成", record.Status);
    }

    [Fact]
    public async Task GetRecent_ReturnsNewestFirst()
    {
        await _store.AddAsync(new ExecutionHistoryRecord { WorkflowName = "first" });
        await _store.AddAsync(new ExecutionHistoryRecord { WorkflowName = "second" });
        await _store.AddAsync(new ExecutionHistoryRecord { WorkflowName = "third" });

        var recent = _store.GetRecent(2);

        Assert.Equal(2, recent.Count);
        Assert.Equal("third", recent[0].WorkflowName);
        Assert.Equal("second", recent[1].WorkflowName);
    }

    [Fact]
    public async Task DeleteAsync_RemovesRecord()
    {
        var id = await _store.AddAsync(new ExecutionHistoryRecord { WorkflowName = "doomed" });

        await _store.DeleteAsync(id);

        Assert.Equal(0, _store.Count());
    }

    [Fact]
    public async Task AddAsync_PersistsAcrossReopen()
    {
        await _store.AddAsync(new ExecutionHistoryRecord { WorkflowName = "reopen-me" });
        _store.Dispose();

        using var reopened = new ExecutionHistoryStore(_dbPath);
        var record = Assert.Single(reopened.GetRecent());
        Assert.Equal("reopen-me", record.WorkflowName);
    }

    public void Dispose()
    {
        _store.Dispose();
        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
        }
    }
}
