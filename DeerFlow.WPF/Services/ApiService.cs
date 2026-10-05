using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;

namespace DeerFlow.WPF.Services;

/// <summary>
/// API 服务接口定义
/// </summary>
public interface IApiService
{
    /// <summary>发送聊天请求并返回流式响应</summary>
    IAsyncEnumerable<string> SendChatStreamAsync(string message, Models.AgentConfig config, string? taskId = null, CancellationToken cancellationToken = default);

    /// <summary>获取可用模型列表</summary>
    Task<List<string>> GetAvailableModelsAsync();

    /// <summary>健康检查</summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// API 服务实现，负责与后端 AI 服务通信
/// </summary>
public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly IAppOptionsProvider _options;

    public ApiService(HttpClient httpClient, IAppOptionsProvider options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> SendChatStreamAsync(string message, Models.AgentConfig config, string? taskId = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            model = config.ModelName,
            messages = new[]
            {
                new { role = "user", content = message }
            },
            stream = true,
            temperature = config.Temperature,
            max_tokens = config.MaxTokens
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl}/chat/completions")
        {
            Content = JsonContent.Create(requestBody)
        };

        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
        }

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
                yield break;

            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: "))
                continue;

            var data = line[6..];
            if (data == "[DONE]")
                yield break;

            string? content = null;
            try
            {
                var chunk = System.Text.Json.JsonDocument.Parse(data);
                content = chunk.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("delta")
                    .TryGetProperty("content", out var contentElement)
                    ? contentElement.GetString()
                    : null;
            }
            catch
            {
                continue;
            }

            if (!string.IsNullOrEmpty(content))
                yield return content;
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetAvailableModelsAsync()
    {
        return await Task.FromResult(new List<string>(_options.Options.AvailableModels));
    }

    /// <inheritdoc/>
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
