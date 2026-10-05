using System.IO;
using System.Text.Json;

namespace DeerFlow.WPF.Services;

/// <summary>
/// 应用级配置项，来源于 appsettings.json，可通过环境变量覆盖。
/// 集中管理默认 API 地址、模型列表等，避免散落在代码中的硬编码。
/// </summary>
public sealed class AppOptions
{
    /// <summary>默认模型提供方</summary>
    public string DefaultProvider { get; set; } = "openai";

    /// <summary>默认模型名称</summary>
    public string DefaultModel { get; set; } = "deepseek-v3";

    /// <summary>默认 API Base URL</summary>
    public string DefaultApiBaseUrl { get; set; } = "http://localhost:11434/v1";

    /// <summary>可用模型列表（由配置提供，缺省时使用内置回退列表）</summary>
    public List<string> AvailableModels { get; set; } = new()
    {
        "deepseek-v3",
        "deepseek-r1",
        "gpt-4o",
        "claude-3-sonnet",
        "kimi-2.5",
        "doubao-seed-2.0-code"
    };

    /// <summary>聊天请求超时时间（分钟）</summary>
    public int ChatTimeoutMinutes { get; set; } = 5;

    /// <summary>Web 搜索请求超时时间（秒）</summary>
    public int WebSearchTimeoutSeconds { get; set; } = 15;
}

/// <summary>
/// 应用配置提供者，负责加载 appsettings.json 并应用环境变量覆盖。
/// </summary>
public interface IAppOptionsProvider
{
    /// <summary>当前生效的配置</summary>
    AppOptions Options { get; }
}

/// <summary>
/// 默认配置提供者实现。文件缺失时回退到内置默认值。
/// </summary>
public sealed class AppOptionsProvider : IAppOptionsProvider
{
    private const string ConfigFileName = "appsettings.json";

    public AppOptions Options { get; }

    public AppOptionsProvider()
    {
        Options = Load();
        ApplyEnvironmentOverrides(Options);
    }

    private static AppOptions Load()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            if (!File.Exists(path))
            {
                return new AppOptions();
            }

            var json = File.ReadAllText(path);
            var options = JsonSerializer.Deserialize<AppOptions>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new AppOptions();

            if (options.AvailableModels.Count == 0)
            {
                options.AvailableModels = new AppOptions().AvailableModels;
            }

            return options;
        }
        catch
        {
            return new AppOptions();
        }
    }

    private static void ApplyEnvironmentOverrides(AppOptions options)
    {
        var baseUrl = Environment.GetEnvironmentVariable("DEERFLOW_API_BASE_URL");
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            options.DefaultApiBaseUrl = baseUrl;
        }

        var model = Environment.GetEnvironmentVariable("DEERFLOW_DEFAULT_MODEL");
        if (!string.IsNullOrWhiteSpace(model))
        {
            options.DefaultModel = model;
        }
    }
}
