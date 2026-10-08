using System.Windows;
using DeerFlow.WPF.Services;
using DeerFlow.WPF.Services.Filters;
using DeerFlow.WPF.Services.Plugins;
using DeerFlow.WPF.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace DeerFlow.WPF;

/// <summary>
/// 应用程序入口，负责 DI 容器初始化、服务和 ViewModel 注册
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 全局服务提供者
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        Services = ConfigureServices();

        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };

        mainWindow.Show();
    }

    /// <summary>
    /// 配置依赖注入容器
    /// </summary>
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // 应用配置（appsettings.json + 环境变量覆盖）
        services.AddSingleton<IAppOptionsProvider, AppOptionsProvider>();

        // 注册核心服务（单例）
        services.AddSingleton<ILoggerService, LoggerService>();
        services.AddSingleton<ISecretStore, DpapiSecretStore>();
        services.AddSingleton<ISandboxManager, SandboxManager>();
        services.AddSingleton<ITaskWindowManager, TaskWindowManager>();
        services.AddSingleton<IWatchdogService, WatchdogService>();
        services.AddSingleton<IMemorySnapshotService, MemorySnapshotService>();
        services.AddSingleton<IIMPlatformService, IMPlatformService>();
        services.AddSingleton<IOpenSandboxService, OpenSandboxService>();

        var appOptions = new AppOptionsProvider().Options;

        // 注册 Http 客户端
        services.AddHttpClient<IApiService, ApiService>(client =>
        {
            client.BaseAddress = new Uri(appOptions.DefaultApiBaseUrl);
            client.Timeout = TimeSpan.FromMinutes(appOptions.ChatTimeoutMinutes);
        });

        // 注册 WebSearch HttpClient
        services.AddHttpClient("WebSearch", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(appOptions.WebSearchTimeoutSeconds);
        });

        // === Self-Improvement System ===

        // 1. 注册自我反思服务
        services.AddSingleton<ISelfReflectionService, SelfReflectionService>();

        // 2. 注册模式挖掘服务
        services.AddSingleton<IPatternMiner, PatternMiner>();

        // 3. 注册自动演化服务
        services.AddSingleton<IAutoEvolver, AutoEvolver>();

        // 4. 注册经验记忆存储
        services.AddSingleton<IExperienceMemoryStore, ExperienceMemoryStore>();

        // 6. 注册用户反馈服务
        services.AddSingleton<IFeedbackService, FeedbackService>();

        // 7. 注册智能告警服务
        services.AddSingleton<IAlertService, AlertService>();

        // 8. 注册文档生成服务
        services.AddSingleton<IDocumentGenerationService, DocumentGenerationService>();

        // 9. 注册智能体循环执行器
        services.AddSingleton<IAgentLoop, AgentLoop>();

        // 5. 注册自我改进插件
        services.AddTransient<SelfImprovementPlugin>();

        // === Semantic Kernel 集成 ===

        // 1. 注册 Kernel
        services.AddKernel();

        // 2. 注册 OpenAI 连接器（从环境变量读取 API Key）
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
        var baseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "https://api.openai.com/v1";
        var modelId = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o";

        if (!string.IsNullOrEmpty(apiKey))
        {
            services.AddOpenAIChatCompletion(
                modelId: modelId,
                apiKey: apiKey,
                endpoint: new Uri(baseUrl));
        }

        // 3. 注册 Plugins
        services.AddTransient<SandboxPlugin>();
        services.AddSingleton<MemoryPlugin>();
        services.AddTransient<WebSearchPlugin>();
        services.AddTransient<SelfImprovementPlugin>();
        services.AddSingleton<DiagnosticsPlugin>();

        // 注册执行历史存储
        services.AddSingleton<ExecutionHistoryStore>();

        // 4. 注册 Filters（可观测性拦截点）
        services.AddSingleton<IFunctionInvocationFilter, SKLoggingFilter>();
        services.AddSingleton<IAutoFunctionInvocationFilter, ToolCallLoggingFilter>();

        // 注册 ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<TaskPanelViewModel>();
        services.AddTransient<AgentOrchestrationViewModel>();
        services.AddTransient<MemoryViewModel>();
        services.AddTransient<SkillsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<IMSettingsViewModel>();
        services.AddTransient<OpenSandboxViewModel>();

        var serviceProvider = services.BuildServiceProvider();

        // 5. 导入 Plugins 到 Kernel
        ImportPluginsToKernel(serviceProvider);

        return serviceProvider;
    }

    /// <summary>
    /// 将 DI 中的 Plugin 实例导入 Kernel
    /// </summary>
    private static void ImportPluginsToKernel(IServiceProvider serviceProvider)
    {
        var kernel = serviceProvider.GetRequiredService<Kernel>();

        var sandboxPlugin = serviceProvider.GetRequiredService<SandboxPlugin>();
        kernel.ImportPluginFromObject(sandboxPlugin, "sandbox");

        var memoryPlugin = serviceProvider.GetRequiredService<MemoryPlugin>();
        kernel.ImportPluginFromObject(memoryPlugin, "memory");

        var webSearchPlugin = serviceProvider.GetRequiredService<WebSearchPlugin>();
        kernel.ImportPluginFromObject(webSearchPlugin, "web");

        var selfImprovementPlugin = serviceProvider.GetRequiredService<SelfImprovementPlugin>();
        kernel.ImportPluginFromObject(selfImprovementPlugin, "self_improve");

        var diagnosticsPlugin = serviceProvider.GetRequiredService<DiagnosticsPlugin>();
        kernel.ImportPluginFromObject(diagnosticsPlugin, "diagnostics");
    }
}
