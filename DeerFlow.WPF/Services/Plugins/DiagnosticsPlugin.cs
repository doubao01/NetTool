using System.ComponentModel;
using System.Diagnostics;
using Microsoft.SemanticKernel;

namespace DeerFlow.WPF.Services.Plugins;

/// <summary>
/// 诊断插件，把内存快照能力暴露给智能体工具循环
/// </summary>
public sealed class DiagnosticsPlugin
{
    private readonly IMemorySnapshotService _snapshotService;
    private readonly Dictionary<string, MemorySnapshot> _snapshots = new();

    public DiagnosticsPlugin(IMemorySnapshotService snapshotService)
    {
        _snapshotService = snapshotService;
    }

    /// <summary>
    /// 采集一次内存快照，返回关键指标
    /// </summary>
    [KernelFunction("take_memory_snapshot")]
    [Description("采集当前进程的内存快照（工作集/托管内存/线程数/句柄数），并用指定标签保存，供后续对比")]
    public string TakeSnapshot(
        [Description("快照标签，用于后续对比，例如 before / after")]
        string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return "快照失败：标签不能为空";
        }

        var snapshot = _snapshotService.TakeSnapshot(label.Trim());
        _snapshots[snapshot.Label] = snapshot;

        return FormatSnapshot(snapshot);
    }

    /// <summary>
    /// 对比两个已保存的快照，生成差异报告
    /// </summary>
    [KernelFunction("compare_memory_snapshots")]
    [Description("对比两个已保存的内存快照标签，报告内存/线程/句柄变化及潜在泄漏")]
    public string CompareSnapshots(
        [Description("起始快照标签")]
        string beforeLabel,
        [Description("结束快照标签")]
        string afterLabel)
    {
        if (!_snapshots.TryGetValue(beforeLabel, out var before))
        {
            return $"对比失败：找不到快照 {beforeLabel}";
        }

        if (!_snapshots.TryGetValue(afterLabel, out var after))
        {
            return $"对比失败：找不到快照 {afterLabel}";
        }

        var report = _snapshotService.Compare(before, after);
        return $"{report.Summary}\n" +
               $"工作集变化: {FormatBytes(report.WorkingSetDeltaBytes)}\n" +
               $"托管内存变化: {FormatBytes(report.ManagedMemoryDeltaBytes)}\n" +
               $"线程变化: {report.ThreadDelta:+0;-0;0}\n" +
               $"句柄变化: {report.HandleDelta:+0;-0;0}\n" +
               $"GC(0/1/2): {report.Gen0Delta:+0;-0;0}/{report.Gen1Delta:+0;-0;0}/{report.Gen2Delta:+0;-0;0}\n" +
               $"潜在泄漏: {(report.PotentialLeak ? "是" : "否")}";
    }

    /// <summary>
    /// 强制执行一次完整垃圾回收
    /// </summary>
    [KernelFunction("force_gc_collect")]
    [Description("强制执行一次完整垃圾回收（含大对象与终结队列），用于内存压力诊断")]
    public string ForceGcCollect()
    {
        var before = GC.GetTotalMemory(false);
        _snapshotService.ForceGcCollect();
        var after = GC.GetTotalMemory(false);
        var freed = before - after;

        return $"垃圾回收完成，释放约 {FormatBytes(freed > 0 ? freed : 0)} 托管内存";
    }

    private static string FormatSnapshot(MemorySnapshot snapshot)
    {
        return $"快照 [{snapshot.Label}] 已保存\n" +
               $"工作集: {FormatBytes(snapshot.WorkingSetBytes)}\n" +
               $"托管内存: {FormatBytes(snapshot.ManagedMemoryBytes)}\n" +
               $"线程: {snapshot.TotalThreads}\n" +
               $"句柄: {snapshot.HandleCount}\n" +
               $"GC(0/1/2): {snapshot.Gen0Collections}/{snapshot.Gen1Collections}/{snapshot.Gen2Collections}";
    }

    private static string FormatBytes(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return bytes switch
        {
            >= GB => $"{bytes / (double)GB:F2} GB",
            >= MB => $"{bytes / (double)MB:F2} MB",
            >= KB => $"{bytes / (double)KB:F1} KB",
            _ => $"{bytes} B"
        };
    }
}
