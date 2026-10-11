using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using DeerFlow.WPF.Core;
using DeerFlow.WPF.Models;
using DeerFlow.WPF.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DeerFlow.WPF.ViewModels;

/// <summary>
/// 设置页面 ViewModel，管理系统配置的加载、保存和重置
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private const string SETTINGS_FILENAME = "settings.json";

    private readonly ILoggerService _logger;
    private readonly ISecretStore _secretStore;
    private readonly AppOptions _options;
    private readonly string _settingsPath;
    private readonly IAutoEvolver? _autoEvolver;

    private string _modelProvider;
    public string ModelProvider
    {
        get => _modelProvider;
        set => SetProperty(ref _modelProvider, value);
    }

    private string _apiBaseUrl;
    public string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set => SetProperty(ref _apiBaseUrl, value);
    }

    private string _modelName;
    public string ModelName
    {
        get => _modelName;
        set => SetProperty(ref _modelName, value);
    }

    private int _agentTimeoutMinutes;
    public int AgentTimeoutMinutes
    {
        get => _agentTimeoutMinutes;
        set => SetProperty(ref _agentTimeoutMinutes, value);
    }

    private int _agentToolTimeoutSeconds;
    public int AgentToolTimeoutSeconds
    {
        get => _agentToolTimeoutSeconds;
        set => SetProperty(ref _agentToolTimeoutSeconds, value);
    }

    private int _agentMaxConsecutiveToolFailures;
    public int AgentMaxConsecutiveToolFailures
    {
        get => _agentMaxConsecutiveToolFailures;
        set => SetProperty(ref _agentMaxConsecutiveToolFailures, value);
    }

    private string _apiKey = string.Empty;
    public string ApiKey
    {
        get => _apiKey;
        set
        {
            if (SetProperty(ref _apiKey, value))
            {
                OnPropertyChanged(nameof(ApiKeyStatusText));
            }
        }
    }

    private bool _enableLocalInference = true;
    public bool EnableLocalInference
    {
        get => _enableLocalInference;
        set => SetProperty(ref _enableLocalInference, value);
    }

    private bool _enableSandbox = true;
    public bool EnableSandbox
    {
        get => _enableSandbox;
        set => SetProperty(ref _enableSandbox, value);
    }

    private bool _autoSaveMemory = true;
    public bool AutoSaveMemory
    {
        get => _autoSaveMemory;
        set => SetProperty(ref _autoSaveMemory, value);
    }

    private bool _isApiKeyVisible;
    /// <summary>API Key 是否以明文显示</summary>
    public bool IsApiKeyVisible
    {
        get => _isApiKeyVisible;
        set
        {
            if (SetProperty(ref _isApiKeyVisible, value))
            {
                OnPropertyChanged(nameof(IsApiKeyHidden));
            }
        }
    }

    /// <summary>API Key 是否以密码遮蔽（与 IsApiKeyVisible 互斥）</summary>
    public bool IsApiKeyHidden => !_isApiKeyVisible;

    /// <summary>API Key 状态描述文本，用于概览面板显示</summary>
    public string ApiKeyStatusText => string.IsNullOrWhiteSpace(ApiKey) ? "未配置（使用本地推理）" : "已配置";

    // 实验中心相关属性
    private bool _showExperimentCenter;
    public bool ShowExperimentCenter
    {
        get => _showExperimentCenter;
        set => SetProperty(ref _showExperimentCenter, value);
    }

    private ObservableCollection<Experiment> _activeExperiments = new();
    public ObservableCollection<Experiment> ActiveExperiments => _activeExperiments;

    public RelayCommand SaveSettingsCommand { get; }
    public RelayCommand ResetSettingsCommand { get; }
    public RelayCommand ToggleApiKeyVisibilityCommand { get; }
    public RelayCommand ToggleExperimentCenterCommand { get; }
    public RelayCommand CreateExperimentCommand { get; }
    public RelayCommand AdoptBestStrategyCommand { get; }
    public RelayCommand RefreshHealthCommand { get; }

    public SettingsViewModel(ILoggerService logger, ISecretStore? secretStore = null, AppOptions? options = null)
    {
        _logger = logger;
        _secretStore = secretStore ?? new DpapiSecretStore();
        _options = options ?? App.Services.GetService<IAppOptionsProvider>()?.Options ?? new AppOptions();
        _modelProvider = _options.DefaultProvider;
        _apiBaseUrl = _options.DefaultApiBaseUrl;
        _modelName = _options.DefaultModel;
        _agentTimeoutMinutes = _options.AgentTimeoutMinutes;
        _agentToolTimeoutSeconds = _options.AgentToolTimeoutSeconds;
        _agentMaxConsecutiveToolFailures = _options.AgentMaxConsecutiveToolFailures;
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeerFlow.WPF", SETTINGS_FILENAME);

        _autoEvolver = App.Services.GetService<IAutoEvolver>();

        SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        ResetSettingsCommand = new RelayCommand(_ => ResetSettings());
        ToggleApiKeyVisibilityCommand = new RelayCommand(_ => IsApiKeyVisible = !IsApiKeyVisible);
        ToggleExperimentCenterCommand = new RelayCommand(_ => ShowExperimentCenter = !ShowExperimentCenter);
        CreateExperimentCommand = new RelayCommand(_ => CreateExperiment());
        AdoptBestStrategyCommand = new RelayCommand(_ => AdoptBestStrategy());
        RefreshHealthCommand = new RelayCommand(_ => RefreshExperiments());

        LoadSettings();
        RefreshExperiments();
    }

    private void RefreshExperiments()
    {
        _activeExperiments.Clear();
        if (_autoEvolver is not null)
        {
            try
            {
                var experiments = _autoEvolver.GetActiveExperiments();
                foreach (var exp in experiments)
                {
                    _activeExperiments.Add(exp);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"刷新实验列表失败：{ex.Message}");
            }
        }
    }

    private void CreateExperiment()
    {
        _logger.Info("创建新实验 - 待实现 UI 对话框");
    }

    private void AdoptBestStrategy()
    {
        if (_autoEvolver is null)
        {
            _logger.Warn("自动演化服务未启用");
            return;
        }

        try
        {
            var bestStrategy = _autoEvolver.SelectBestStrategy();
            if (!string.IsNullOrEmpty(bestStrategy))
            {
                _logger.Info($"已采纳最优策略：{bestStrategy}");
            }
            else
            {
                _logger.Warn("没有找到符合条件的最优策略");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("采纳最优策略失败", ex);
        }
    }

    /// <summary>
    /// 从磁盘加载设置。API Key 以密文存储，加载时解密。
    /// </summary>
    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return;

            var json = File.ReadAllText(_settingsPath);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("ModelProvider", out var mp))
                ModelProvider = mp.GetString() ?? _options.DefaultProvider;
            if (root.TryGetProperty("ModelName", out var mn))
                ModelName = mn.GetString() ?? _options.DefaultModel;
            if (root.TryGetProperty("ApiBaseUrl", out var abu))
                ApiBaseUrl = abu.GetString() ?? _options.DefaultApiBaseUrl;
            if (root.TryGetProperty("ApiKey", out var ak))
                ApiKey = _secretStore.Unprotect(ak.GetString() ?? string.Empty);
            if (root.TryGetProperty("AgentTimeoutMinutes", out var atm))
                AgentTimeoutMinutes = atm.GetInt32();
            if (root.TryGetProperty("AgentToolTimeoutSeconds", out var atts))
                AgentToolTimeoutSeconds = atts.GetInt32();
            if (root.TryGetProperty("AgentMaxConsecutiveToolFailures", out var amctf))
                AgentMaxConsecutiveToolFailures = amctf.GetInt32();
            if (root.TryGetProperty("EnableLocalInference", out var eli))
                EnableLocalInference = eli.GetBoolean();
            if (root.TryGetProperty("EnableSandbox", out var es))
                EnableSandbox = es.GetBoolean();
            if (root.TryGetProperty("AutoSaveMemory", out var asm))
                AutoSaveMemory = asm.GetBoolean();

            ApplyRuntimeCredentials();
            SyncRuntimeSnapshot();
            _logger.Info("设置已从磁盘加载");
        }
        catch (Exception ex)
        {
            _logger.Error("加载设置失败", ex);
        }
    }

    /// <summary>
    /// 保存设置到磁盘。API Key 经加密后写入，避免明文落盘。
    /// </summary>
    private void SaveSettings()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var settings = new
            {
                ModelProvider,
                ModelName,
                ApiBaseUrl,
                ApiKey = _secretStore.Protect(ApiKey),
                AgentTimeoutMinutes,
                AgentToolTimeoutSeconds,
                AgentMaxConsecutiveToolFailures,
                EnableLocalInference,
                EnableSandbox,
                AutoSaveMemory
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_settingsPath, json);
            ApplyRuntimeCredentials();
            SyncRuntimeSnapshot();
            _logger.Info("设置已保存到磁盘");
        }
        catch (Exception ex)
        {
            _logger.Error("保存设置失败", ex);
        }
    }

    private void ApplyRuntimeCredentials()
    {
        if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", ApiKey);
        }

        if (!string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            Environment.SetEnvironmentVariable("OPENAI_BASE_URL", ApiBaseUrl);
            Environment.SetEnvironmentVariable("DEERFLOW_API_BASE_URL", ApiBaseUrl);
        }

        if (!string.IsNullOrWhiteSpace(ModelProvider))
        {
            Environment.SetEnvironmentVariable("DEERFLOW_API_PROVIDER", ModelProvider);
        }

        if (!string.IsNullOrWhiteSpace(ModelName))
        {
            Environment.SetEnvironmentVariable("DEERFLOW_DEFAULT_MODEL", ModelName);
        }

        ApiKeyStatusTextNotify();
    }

    private void ApiKeyStatusTextNotify() => OnPropertyChanged(nameof(ApiKeyStatusText));

    /// <summary>
    /// 把当前设置写回运行配置快照，使保存后立即对智能体循环生效。
    /// </summary>
    private void SyncRuntimeSnapshot()
    {
        if (!string.IsNullOrWhiteSpace(ModelProvider))
        {
            _options.DefaultProvider = ModelProvider;
        }

        if (!string.IsNullOrWhiteSpace(ModelName))
        {
            _options.DefaultModel = ModelName;
        }

        if (!string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            _options.DefaultApiBaseUrl = ApiBaseUrl;
        }

        _options.AgentTimeoutMinutes = AgentTimeoutMinutes < 0 ? 0 : AgentTimeoutMinutes;
        _options.AgentToolTimeoutSeconds = Math.Clamp(AgentToolTimeoutSeconds, 3, 120);
        _options.AgentMaxConsecutiveToolFailures = Math.Max(1, AgentMaxConsecutiveToolFailures);
    }

    /// <summary>
    /// 重置设置为默认值
    /// </summary>
    private void ResetSettings()
    {
        var defaults = new AppOptions();
        ModelProvider = defaults.DefaultProvider;
        ModelName = defaults.DefaultModel;
        ApiBaseUrl = defaults.DefaultApiBaseUrl;
        ApiKey = string.Empty;
        AgentTimeoutMinutes = defaults.AgentTimeoutMinutes;
        AgentToolTimeoutSeconds = defaults.AgentToolTimeoutSeconds;
        AgentMaxConsecutiveToolFailures = defaults.AgentMaxConsecutiveToolFailures;
        EnableLocalInference = true;
        EnableSandbox = true;
        AutoSaveMemory = true;
        _logger.Info("设置已重置为默认值");
    }
}
