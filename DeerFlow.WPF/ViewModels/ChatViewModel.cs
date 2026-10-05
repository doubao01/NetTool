using System.Diagnostics;
using DeerFlow.WPF.Core;
using DeerFlow.WPF.Models;
using DeerFlow.WPF.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace DeerFlow.WPF.ViewModels;

/// <summary>
/// 聊天对话 ViewModel，管理消息流、流式响应和资源生命周期
/// 支持 Semantic Kernel 驱动（优先）和 ApiService 直接调用两种模式
/// </summary>
public class ChatViewModel : ViewModelBase
{
    private readonly IApiService _apiService;
    private readonly ITaskWindowManager _taskManager;
    private readonly ILoggerService _logger;
    private readonly Kernel? _kernel;
    private readonly bool _useSK;
    private readonly ISelfReflectionService? _reflectionService;
    private readonly IPatternMiner? _patternMiner;
    private readonly IExperienceMemoryStore? _experienceStore;
    private readonly IFeedbackService? _feedbackService;
    private CancellationTokenSource? _sendCts;
    private string? _currentTaskId;

    public AsyncObservableCollection<ChatMessage> Messages { get; } = new();

    private string _inputText = string.Empty;
    public string InputText
    {
        get => _inputText;
        set
        {
            if (SetProperty(ref _inputText, value))
            {
                SendCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _isSending;
    public bool IsSending
    {
        get => _isSending;
        set
        {
            if (SetProperty(ref _isSending, value))
            {
                SendCommand.RaiseCanExecuteChanged();
                StopGenerationCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private AgentConfig _agentConfig = new();
    public AgentConfig AgentConfig
    {
        get => _agentConfig;
        set => SetProperty(ref _agentConfig, value);
    }

    public RelayCommand SendCommand { get; }
    public RelayCommand ClearChatCommand { get; }
    public RelayCommand StopGenerationCommand { get; }
    public RelayCommand LikeFeedbackCommand { get; }
    public RelayCommand DislikeFeedbackCommand { get; }

    public ChatViewModel(IApiService apiService, ITaskWindowManager taskManager, ILoggerService logger)
    {
        _apiService = apiService;
        _taskManager = taskManager;
        _logger = logger;

        // 尝试解析 Kernel，如果存在 AI 服务则启用 SK 模式
        _kernel = App.Services.GetService<Kernel>();
        _useSK = _kernel is not null && _kernel.GetAllServices<IChatCompletionService>().Any();

        // 解析自我迭代系统服务
        _reflectionService = App.Services.GetService<ISelfReflectionService>();
        _patternMiner = App.Services.GetService<IPatternMiner>();
        _experienceStore = App.Services.GetService<IExperienceMemoryStore>();
        _feedbackService = App.Services.GetService<IFeedbackService>();

        SendCommand = new RelayCommand(new Action(async () => await SendMessageAsync()), () => !IsSending && !string.IsNullOrWhiteSpace(InputText));
        ClearChatCommand = new RelayCommand(_ => Messages.Clear());
        StopGenerationCommand = new RelayCommand(new Action(CancelSend), () => IsSending);
        LikeFeedbackCommand = new RelayCommand(_ => SubmitFeedbackAsync("Like"));
        DislikeFeedbackCommand = new RelayCommand(_ => SubmitFeedbackAsync("Dislike"));

        // 获取智能模式提示
        var welcomeMessage = GenerateWelcomeMessage();
        var introMessage = _useSK
            ? welcomeMessage ?? "你好，我是 DeerFlow.WPF 智能助手（SK 增强模式）。我可以执行沙箱代码、搜索网络、管理记忆，还能自动调用工具完成复杂任务。有什么需要帮助的？"
            : welcomeMessage ?? "你好，我是 DeerFlow.WPF 智能助手。我可以帮你执行任务、分析代码、管理项目。有什么需要帮助的？";

        Messages.Add(new ChatMessage
        {
            Role = "system",
            Content = introMessage,
            Timestamp = DateTime.Now
        });
    }

    private string? GenerateWelcomeMessage()
    {
        if (_patternMiner is null) return null;

        try
        {
            var topPatterns = _patternMiner.GetPatterns(limit: 3).ToList();
            if (topPatterns.Count == 0) return null;

            var bestPattern = topPatterns.First();
            return $"""
                你好！我是你的智能助手。

                💡 **今日最佳实践**:
                {bestPattern.Description}

                **适用场景**: {bestPattern.ApplicableScenarios}

                开始你的任务吧！
                """;
        }
        catch (Exception ex)
        {
            _logger.Warn($"生成欢迎消息失败：{ex.Message}");
            return null;
        }
    }

    private async Task<(string message, List<string> tools)> InjectExperienceAsync(string userInput)
    {
        if (_experienceStore is null) return (userInput, new List<string>());

        try
        {
            var experiences = _experienceStore.RetrieveExperiences(userInput, limit: 3).ToList();
            if (experiences.Count == 0) return (userInput, new List<string>());

            var experienceContext = string.Join("\n", experiences.Select(e =>
                $"- {e.Summary} (置信度：{e.Confidence:P1})"));

            var enhancedInput = $"""
                当前任务：{userInput}

                相关历史经验：
                {experienceContext}

                请参考以上经验执行任务。
                """;

            return (enhancedInput, new List<string> { "experience_retrieval" });
        }
        catch (Exception ex)
        {
            _logger.Warn($"检索经验失败：{ex.Message}");
            return (userInput, new List<string>());
        }
    }

    /// <summary>
    /// 发送消息并处理流式响应，SK 模式优先，ApiService 模式兜底
    /// </summary>
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || IsSending)
            return;

        var userMessage = new ChatMessage
        {
            Role = "user",
            Content = InputText
        };

        Messages.Add(userMessage);
        InputText = string.Empty;
        IsSending = true;

        var assistantMessage = new ChatMessage
        {
            Role = "assistant",
            Content = string.Empty,
            IsStreaming = true
        };
        Messages.Add(assistantMessage);

        _sendCts?.Dispose();
        _sendCts = new CancellationTokenSource();

        var stopwatch = Stopwatch.StartNew();
        var toolsUsed = new List<string>();
        _currentTaskId = Guid.NewGuid().ToString("N");

        try
        {
            // 1. 获取模式推荐
            if (_patternMiner is not null)
            {
                var pattern = _patternMiner.RecommendPattern(userMessage.Content, "");
                if (pattern is not null)
                {
                    _logger.Info($"应用模式：{pattern.Name}");
                    toolsUsed.Add("pattern_recommendation");
                }
            }

            // 2. 注入相关经验
            var (enhancedInput, experienceTools) = await InjectExperienceAsync(userMessage.Content);
            toolsUsed.AddRange(experienceTools);

            if (_useSK && _kernel is not null)
            {
                await SendMessageViaSKAsync(enhancedInput, assistantMessage);
            }
            else
            {
                await SendMessageViaApiAsync(enhancedInput, assistantMessage);
            }

            stopwatch.Stop();

            // 3. 自动记录反馈
            if (_reflectionService is not null)
            {
                try
                {
                    await _reflectionService.ReflectOnTaskAsync(
                        taskId: Guid.NewGuid().ToString("N"),
                        taskDescription: userMessage.Content,
                        taskType: "chat",
                        isSuccess: !string.IsNullOrEmpty(assistantMessage.Content) &&
                                   !assistantMessage.Content.Contains("失败") &&
                                   !assistantMessage.Content.Contains("错误"),
                        toolsUsed: toolsUsed,
                        executionTimeMs: stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"记录反思失败：{ex.Message}");
                }
            }

            assistantMessage.IsStreaming = false;
            _logger.Info($"消息回复完成 ({stopwatch.ElapsedMilliseconds}ms)");
        }
        catch (OperationCanceledException)
        {
            assistantMessage.IsStreaming = false;
            assistantMessage.Content = string.IsNullOrEmpty(assistantMessage.Content)
                ? "（生成已取消）"
                : assistantMessage.Content + "\n\n（生成已取消）";
            _logger.Info("消息生成已取消");
        }
        catch (Exception ex)
        {
            assistantMessage.Content = $"请求失败：{ex.Message}";
            assistantMessage.IsStreaming = false;
            stopwatch.Stop();
            _logger.Error($"发送消息失败 ({stopwatch.ElapsedMilliseconds}ms)", ex);
        }
        finally
        {
            IsSending = false;
            _sendCts?.Dispose();
            _sendCts = null;
        }
    }

    /// <summary>
    /// 通过 Semantic Kernel 发送消息（支持自动工具调用）
    /// </summary>
    private async Task SendMessageViaSKAsync(string userInput, ChatMessage assistantMessage)
    {
        if (_kernel is null) return;
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true),
            Temperature = AgentConfig.Temperature,
            MaxTokens = AgentConfig.MaxTokens
        };

        var arguments = new KernelArguments(executionSettings)
        {
            ["input"] = userInput
        };

        var chatFunction = _kernel!.CreateFunctionFromPrompt(
            "{{$input}}",
            executionSettings,
            functionName: "chat",
            description: "鹿流AI聊天");

        var fullResponse = new System.Text.StringBuilder();

        await foreach (var chunk in _kernel.InvokeStreamingAsync<StreamingKernelContent>(
            chatFunction, arguments, _sendCts!.Token))
        {
            fullResponse.Append(chunk.ToString());
            assistantMessage.Content = fullResponse.ToString();
        }

        assistantMessage.Content = fullResponse.ToString();
    }

    /// <summary>
    /// 通过 ApiService 直接发送消息（SK 不可用时的兜底方案）
    /// </summary>
    private async Task SendMessageViaApiAsync(string userInput, ChatMessage assistantMessage)
    {
        await foreach (var chunk in _apiService.SendChatStreamAsync(
            userInput, AgentConfig, null, _sendCts!.Token))
        {
            assistantMessage.Content += chunk;
        }
    }

    /// <summary>
    /// 取消当前生成
    /// </summary>
    private void CancelSend()
    {
        _sendCts?.Cancel();
    }

    /// <summary>
    /// 提交用户反馈
    /// </summary>
    private async Task SubmitFeedbackAsync(string feedbackType)
    {
        if (_feedbackService is null || string.IsNullOrEmpty(_currentTaskId))
        {
            _logger.Warn("反馈服务未启用或当前任务 ID 为空");
            return;
        }

        try
        {
            var rating = feedbackType == "Like" ? 5 : 1;
            await _feedbackService.SubmitFeedbackAsync(
                taskId: _currentTaskId,
                feedbackType: feedbackType,
                rating: rating,
                comment: null);

            _logger.Info($"已提交{feedbackType}反馈");
        }
        catch (Exception ex)
        {
            _logger.Warn($"提交反馈失败：{ex.Message}");
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sendCts?.Cancel();
            _sendCts?.Dispose();
            _sendCts = null;
        }

        base.Dispose(disposing);
    }
}
