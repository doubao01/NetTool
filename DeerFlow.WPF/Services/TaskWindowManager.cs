using System.Diagnostics;
using DeerFlow.WPF.Models;
using TaskStatus = DeerFlow.WPF.Models.TaskStatus;

namespace DeerFlow.WPF.Services;

/// <summary>
/// 任务窗口管理器接口
/// </summary>
public interface ITaskWindowManager
{
    /// <summary>创建新的任务窗口</summary>
    Task<TaskModel> CreateTaskWindowAsync(TaskModel task);

    /// <summary>关闭指定任务窗口</summary>
    Task<bool> CloseTaskWindowAsync(string taskId);

    /// <summary>获取所有活跃任务窗口</summary>
    List<TaskModel> GetActiveTasks();

    /// <summary>暂停任务</summary>
    Task<bool> PauseTaskAsync(string taskId);

    /// <summary>恢复任务</summary>
    Task<bool> ResumeTaskAsync(string taskId);

    /// <summary>获取任务窗口状态</summary>
    Task<TaskModel> GetTaskStatusAsync(string taskId);

    /// <summary>进程监控事件</summary>
    event Action<TaskModel>? TaskStatusChanged;
}

/// <summary>
/// 任务窗口管理器，负责独立沙箱窗口的生命周期管理
/// </summary>
public class TaskWindowManager : ITaskWindowManager
{
    private readonly ISandboxManager _sandboxManager;
    private readonly ILoggerService _logger;
    private readonly Dictionary<string, TaskModel> _tasks = new();
    private readonly Dictionary<string, Process> _processes = new();

    public event Action<TaskModel>? TaskStatusChanged;

    private const double MEMORY_LIMIT_MB = 512;
    private const double CPU_LIMIT_PERCENT = 80;

    public TaskWindowManager(ISandboxManager sandboxManager, ILoggerService logger)
    {
        _sandboxManager = sandboxManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TaskModel> CreateTaskWindowAsync(TaskModel task)
    {
        _logger.Info($"创建任务窗口: {task.Name} (ID: {task.Id})");

        if (task.IsSandboxed)
        {
            task.SandboxPath = await _sandboxManager.CreateSandboxAsync(task.Id);
            _logger.Info($"沙箱已创建: {task.SandboxPath}");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "DeerFlow.WPF.exe",
                Arguments = $"--task-id {task.Id} --sandbox-path {task.SandboxPath} --agent-provider {task.AgentConfig.Provider}",
                UseShellExecute = false,
                CreateNoWindow = false
            }
        };

        process.Start();
        task.ProcessId = process.Id;
        task.Status = TaskStatus.Running;

        _tasks[task.Id] = task;
        _processes[task.Id] = process;

        TaskStatusChanged?.Invoke(task);

        StartMonitoring(task.Id, process);

        return task;
    }

    /// <inheritdoc/>
    public async Task<bool> CloseTaskWindowAsync(string taskId)
    {
        if (!_processes.TryGetValue(taskId, out var process))
            return false;

        _logger.Info($"关闭任务窗口: {taskId}");

        try
        {
            if (!process.HasExited)
            {
                process.CloseMainWindow();
                await Task.Delay(2000);

                if (!process.HasExited)
                {
                    process.Kill();
                    await Task.Delay(500);
                }
            }

            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.Status = TaskStatus.Completed;

                if (task.IsSandboxed)
                {
                    await _sandboxManager.DestroySandboxAsync(taskId);
                }

                _tasks.Remove(taskId);
                _processes.Remove(taskId);

                TaskStatusChanged?.Invoke(task);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"关闭任务窗口失败: {taskId}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public List<TaskModel> GetActiveTasks() => _tasks.Values.ToList();

    /// <inheritdoc/>
    public Task<bool> PauseTaskAsync(string taskId)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Status = TaskStatus.Paused;
            TaskStatusChanged?.Invoke(task);
            _logger.Info($"任务已暂停: {taskId}");
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<bool> ResumeTaskAsync(string taskId)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Status = TaskStatus.Running;
            TaskStatusChanged?.Invoke(task);
            _logger.Info($"任务已恢复: {taskId}");
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<TaskModel> GetTaskStatusAsync(string taskId)
    {
        return Task.FromResult(_tasks.GetValueOrDefault(taskId, new TaskModel { Id = taskId, Status = TaskStatus.Failed }));
    }

    /// <summary>
    /// 启动任务窗口的资源监控
    /// </summary>
    private void StartMonitoring(string taskId, Process process)
    {
        _ = Task.Run(async () =>
        {
            while (!process.HasExited && _tasks.ContainsKey(taskId))
            {
                try
                {
                    if (_tasks.TryGetValue(taskId, out var task))
                    {
                        var cpuUsage = GetCpuUsage(process);
                        var memUsage = process.WorkingSet64 / (1024.0 * 1024.0);

                        task.CpuUsagePercent = cpuUsage;
                        task.MemoryUsageMB = memUsage;

                        if (memUsage > MEMORY_LIMIT_MB)
                        {
                            _logger.Warn($"任务 {taskId} 内存超限: {memUsage:F1}MB");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"任务 {taskId} 资源监控采样失败: {ex.Message}");
                }

                await Task.Delay(2000);
            }
        });
    }

    /// <summary>
    /// 获取进程 CPU 占用百分比
    /// </summary>
    private static double GetCpuUsage(Process process)
    {
        try
        {
            process.Refresh();
            var startTime = DateTime.UtcNow;
            var startCpuUsage = process.TotalProcessorTime;
            Thread.Sleep(500);
            var endTime = DateTime.UtcNow;
            var endCpuUsage = process.TotalProcessorTime;

            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            return (cpuUsedMs / (Environment.ProcessorCount * totalMsPassed)) * 100;
        }
        catch
        {
            return 0;
        }
    }
}
