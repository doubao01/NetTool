namespace DeerFlow.WPF.Models;

/// <summary>
/// 智能体配置模型
/// </summary>
public class AgentConfig
{
    /// <summary>模型提供方</summary>
    public string Provider { get; set; } = "openai";

    /// <summary>模型名称</summary>
    public string ModelName { get; set; } = "deepseek-v3";

    /// <summary>API Base URL</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434/v1";

    /// <summary>API Key</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>系统提示词</summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>温度参数</summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>最大Tokens</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>是否启用子智能体</summary>
    public bool EnableSubAgents { get; set; } = false;

    /// <summary>最大并发子智能体数</summary>
    public int MaxSubAgents { get; set; } = 3;

    /// <summary>任务超时时间（分钟）</summary>
    public int TimeoutMinutes { get; set; } = 15;

    /// <summary>单次工具调用超时（秒）</summary>
    public int ToolTimeoutSeconds { get; set; } = 15;

    /// <summary>连续工具失败达到该次数后熔断</summary>
    public int MaxConsecutiveToolFailures { get; set; } = 3;
}
