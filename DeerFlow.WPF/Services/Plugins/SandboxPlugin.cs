using System.ComponentModel;
using DeerFlow.WPF.Services;
using Microsoft.SemanticKernel;

namespace DeerFlow.WPF.Services.Plugins;

/// <summary>
/// 沙箱工具插件，将 SandboxManager 封装为 Semantic Kernel 工具函数
/// </summary>
public sealed class SandboxPlugin
{
    private readonly ISandboxManager _sandbox;

    public SandboxPlugin(ISandboxManager sandbox)
    {
        _sandbox = sandbox;
    }

    /// <summary>
    /// 为指定任务创建隔离沙箱环境
    /// </summary>
    /// <param name="taskId">任务唯一标识</param>
    /// <returns>沙箱路径</returns>
    [KernelFunction("create_sandbox")]
    [Description("为任务创建隔离的沙箱工作目录，返回沙箱路径")]
    public async Task<string> CreateSandbox(
        [Description("任务的唯一标识 ID")]
        string taskId)
    {
        return await _sandbox.CreateSandboxAsync(taskId);
    }

    /// <summary>
    /// 销毁指定任务的沙箱并清理所有文件
    /// </summary>
    /// <param name="taskId">任务唯一标识</param>
    [KernelFunction("destroy_sandbox")]
    [Description("销毁指定任务的沙箱并清理所有文件")]
    public async Task<string> DestroySandbox(
        [Description("要销毁的沙箱对应的任务 ID")]
        string taskId)
    {
        await _sandbox.DestroySandboxAsync(taskId);
        return $"沙箱 {taskId} 已销毁";
    }

    /// <summary>
    /// 列出沙箱中的所有文件
    /// </summary>
    /// <param name="taskId">任务唯一标识</param>
    /// <returns>文件路径列表</returns>
    [KernelFunction("list_files")]
    [Description("列出指定沙箱中的所有文件")]
    public async Task<string> ListFiles(
        [Description("要列出文件的任务 ID")]
        string taskId)
    {
        var files = await _sandbox.GetSandboxFilesAsync(taskId);
        return files.Count == 0
            ? "沙箱为空"
            : string.Join("\n", files);
    }

    /// <summary>
    /// 在沙箱中执行受限命令
    /// </summary>
    /// <param name="taskId">任务唯一标识</param>
    /// <param name="command">要执行的命令</param>
    /// <returns>命令执行结果</returns>
    [KernelFunction("execute_command")]
     [Description("在沙箱中执行受限命令（白名单：dir/ls/type/cat/echo/mkdir/findstr/tree），禁止沙箱外路径")]
    public async Task<string> ExecuteCommand(
        [Description("要执行命令的目标任务 ID")]
        string taskId,
        [Description("要执行的命令，仅白名单命令可用")]
        string command)
    {
        return await _sandbox.ExecuteInSandboxAsync(taskId, command);
    }
}
