namespace SystemToolkit.Core.Services;
using System.Diagnostics;
using SystemToolkit.Core.Models;

public interface IProcessService
{
    List<ProcessInfo> GetAllProcesses();
    ProcessInfo? GetProcessById(int id);
    void KillProcess(int id);
    void SetProcessPriority(int id, ProcessPriorityClass priority);
    List<ProcessResourceUsage> GetProcessResourceUsage();
    List<ProcessInfo> GetProcessesByName(string name);
}

public class ProcessService : IProcessService
{
    public List<ProcessInfo> GetAllProcesses()
    {
        var processes = new List<ProcessInfo>();

        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                processes.Add(new ProcessInfo
                {
                    Id = process.Id,
                    ProcessName = process.ProcessName,
                    Path = GetProcessPath(process),
                    TotalProcessorTime = process.TotalProcessorTime,
                    WorkingSet64 = process.WorkingSet64,
                    PeakWorkingSet64 = process.PeakWorkingSet64,
                    VirtualMemorySize64 = process.VirtualMemorySize64,
                    ThreadCount = process.Threads.Count,
                    HandleCount = process.HandleCount,
                    StartTime = process.StartTime,
                    PriorityClass = process.PriorityClass.ToString()
                });
            }
            catch (Exception)
            {
                // Skip processes that can't be accessed
            }
            finally
            {
                process.Dispose();
            }
        }

        return processes;
    }

    public ProcessInfo? GetProcessById(int id)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(id);
            return new ProcessInfo
            {
                Id = process.Id,
                ProcessName = process.ProcessName,
                Path = GetProcessPath(process),
                TotalProcessorTime = process.TotalProcessorTime,
                WorkingSet64 = process.WorkingSet64,
                PeakWorkingSet64 = process.PeakWorkingSet64,
                VirtualMemorySize64 = process.VirtualMemorySize64,
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                StartTime = process.StartTime,
                PriorityClass = process.PriorityClass.ToString()
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void KillProcess(int id)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(id);
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法终止进程 (ID: {id}). 错误：{ex.Message}");
        }
    }

    public void SetProcessPriority(int id, ProcessPriorityClass priority)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(id);
            if (!process.HasExited)
            {
                process.PriorityClass = priority;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法设置进程优先级 (ID: {id}). 错误：{ex.Message}");
        }
    }

    public List<ProcessResourceUsage> GetProcessResourceUsage()
    {
        var usageList = new List<ProcessResourceUsage>();

        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                usageList.Add(new ProcessResourceUsage
                {
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName,
                    CpuPercentage = (double)process.TotalProcessorTime.Milliseconds / Environment.ProcessorCount,
                    MemoryUsage = process.WorkingSet64,
                    DiskReadBytes = 0,
                    DiskWriteBytes = 0,
                    NetworkSentBytes = 0,
                    NetworkReceivedBytes = 0
                });
            }
            catch (Exception)
            {
                // Skip processes that can't be accessed
            }
            finally
            {
                process.Dispose();
            }
        }

        return usageList.OrderByDescending(p => p.MemoryUsage).ToList();
    }

    public List<ProcessInfo> GetProcessesByName(string name)
    {
        var processes = new List<ProcessInfo>();

        foreach (var process in System.Diagnostics.Process.GetProcessesByName(name))
        {
            try
            {
                processes.Add(new ProcessInfo
                {
                    Id = process.Id,
                    ProcessName = process.ProcessName,
                    Path = GetProcessPath(process),
                    TotalProcessorTime = process.TotalProcessorTime,
                    WorkingSet64 = process.WorkingSet64,
                    PeakWorkingSet64 = process.PeakWorkingSet64,
                    VirtualMemorySize64 = process.VirtualMemorySize64,
                    ThreadCount = process.Threads.Count,
                    HandleCount = process.HandleCount,
                    StartTime = process.StartTime,
                    PriorityClass = process.PriorityClass.ToString()
                });
            }
            catch (Exception)
            {
                // Skip processes that can't be accessed
            }
            finally
            {
                process.Dispose();
            }
        }

        return processes;
    }

    private string? GetProcessPath(System.Diagnostics.Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
