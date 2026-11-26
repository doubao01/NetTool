using System.Collections.ObjectModel;
using System.Windows;
using DeerFlow.WPF.Core;
using DeerFlow.WPF.Models;
using DeerFlow.WPF.Services;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = DeerFlow.WPF.Models.TaskStatus;

namespace DeerFlow.WPF.ViewModels;

/// <summary>
/// 主窗口 ViewModel，管理窗口导航（WeakReference 缓存）、状态栏和全局命令
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly ITaskWindowManager _taskManager;
    private readonly ILoggerService _logger;
    private readonly Dictionary<string, WeakReference<ViewModelBase>> _pageCache = new();
    private readonly Stack<string> _navigationHistory = new();

    #region 属性

    private object? _currentPage;
    /// <summary>当前显示的页面 ViewModel</summary>
    public object? CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    private string _statusMessage = "就绪";
    /// <summary>状态栏消息</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private int _activeTaskCount;
    /// <summary>活跃任务数</summary>
    public int ActiveTaskCount
    {
        get => _activeTaskCount;
        set
        {
            SetProperty(ref _activeTaskCount, value);
            TaskCountText = $"任务: {value}";
        }
    }

    private string _taskCountText = "任务: 0";
    /// <summary>任务计数文本</summary>
    public string TaskCountText
    {
        get => _taskCountText;
        set => SetProperty(ref _taskCountText, value);
    }

    private double _totalMemoryMB;
    /// <summary>总内存占用 (MB)</summary>
    public double TotalMemoryMB
    {
        get => _totalMemoryMB;
        set
        {
            SetProperty(ref _totalMemoryMB, value);
            MemoryText = $"内存: {value:F0} MB";
        }
    }

    private string _memoryText = "内存: 0 MB";
    /// <summary>内存占用文本</summary>
    public string MemoryText
    {
        get => _memoryText;
        set => SetProperty(ref _memoryText, value);
    }

    /// <summary>导航菜单项</summary>
    public ObservableCollection<MenuItemViewModel> MenuItems { get; } = new();

    private MenuItemViewModel? _selectedMenuItem;
    /// <summary>当前选中的菜单项</summary>
    public MenuItemViewModel? SelectedMenuItem
    {
        get => _selectedMenuItem;
        set => SetProperty(ref _selectedMenuItem, value);
    }

    #endregion

    #region 命令

    /// <summary>最小化窗口命令</summary>
    public RelayCommand MinimizeCommand { get; }
    /// <summary>最大化/还原窗口命令</summary>
    public RelayCommand MaximizeCommand { get; }
    /// <summary>关闭窗口命令</summary>
    public RelayCommand CloseCommand { get; }
    /// <summary>刷新命令</summary>
    public RelayCommand RefreshCommand { get; }
    /// <summary>返回上一页命令</summary>
    public RelayCommand GoBackCommand { get; }

    #endregion

    public MainViewModel(ITaskWindowManager taskManager, ILoggerService logger)
    {
        _taskManager = taskManager;
        _logger = logger;

        MinimizeCommand = new RelayCommand(p => ExecuteWindowAction(p, w => w.WindowState = WindowState.Minimized));
        MaximizeCommand = new RelayCommand(p => ExecuteWindowAction(p, w =>
        {
            w.WindowState = w.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }));
        CloseCommand = new RelayCommand(p => ExecuteWindowAction(p, w => w.Close()));
        RefreshCommand = new RelayCommand(_ => OnRefresh());
        GoBackCommand = new RelayCommand(_ => GoBack(), _ => _navigationHistory.Count > 1);

        InitMenu();
        _taskManager.TaskStatusChanged += OnTaskStatusChanged;

        // 初始化状态栏数据
        OnRefresh();
    }

    /// <summary>
    /// 初始化导航菜单
    /// </summary>
    private void InitMenu()
    {
        var menuDefs = new (string Name, string Icon, string Page)[]
        {
            ("首页", "🏠", "Home"),
            ("对话", "💬", "Chat"),
            ("任务面板", "📋", "TaskPanel"),
            ("智能体编排", "🤖", "AgentOrchestration"),
            ("沙箱管理", "🧪", "OpenSandbox"),
            ("记忆库", "🧠", "Memory"),
            ("技能商店", "🛠", "Skills"),
            ("告警中心", "🔔", "Alerts"),
            ("IM接入", "💬", "IMSettings"),
            ("设置", "⚙", "Settings")
        };

        foreach (var def in menuDefs)
        {
            MenuItems.Add(new MenuItemViewModel
            {
                DisplayName = def.Name,
                Icon = def.Icon,
                OnMenuSelected = name => NavigateTo(def.Page)
            });
        }

        NavigateTo("Home");
    }

    /// <summary>
    /// 使用 WeakReference 缓存进行页面导航，记录导航历史
    /// </summary>
    private void NavigateTo(string page)
    {
        if (_pageCache.TryGetValue(page, out var weakRef) && weakRef.TryGetTarget(out var cached))
        {
            CurrentPage = cached;
        }
        else
        {
            var viewModel = CreatePageViewModel(page);
            _pageCache[page] = new WeakReference<ViewModelBase>(viewModel);
            CurrentPage = viewModel;
        }

        _navigationHistory.Push(page);
        CleanupDeadReferences();
        StatusMessage = $"当前页面: {page}";
        GoBackCommand.RaiseCanExecuteChanged();
        _logger.Info($"导航至页面: {page}");
    }

    /// <summary>
    /// 返回上一页
    /// </summary>
    private void GoBack()
    {
        if (_navigationHistory.Count <= 1) return;

        _navigationHistory.Pop();
        var previousPage = _navigationHistory.Peek();

        if (_pageCache.TryGetValue(previousPage, out var weakRef) && weakRef.TryGetTarget(out var cached))
        {
            CurrentPage = cached;
        }

        StatusMessage = $"返回页面: {previousPage}";
        _logger.Info($"返回上一页: {previousPage}");
    }

    /// <summary>
    /// 创建指定页面的 ViewModel 实例
    /// 优先使用 DI 容器解析，减少直接 new 的硬编码依赖
    /// </summary>
    private ViewModelBase CreatePageViewModel(string page)
    {
        // HomeViewModel 有特殊的构造函数参数（导航回调和创建任务回调），需特殊处理
        if (page == "Home")
        {
            return new HomeViewModel(
                _taskManager,
                _logger,
                navigateTo: NavigateTo,
                createTask: () =>
                {
                    var taskName = $"任务_{Guid.NewGuid():N}";
                    _ = _taskManager.CreateTaskWindowAsync(new TaskModel
                    {
                        Id = taskName,
                        Name = taskName,
                        Description = "从首页创建的任务",
                        Status = TaskStatus.Pending
                    });
                });
        }

        // 其余 ViewModel 从 DI 容器解析
        return page switch
        {
            "Chat" => App.Services.GetRequiredService<ChatViewModel>(),
            "TaskPanel" => App.Services.GetRequiredService<TaskPanelViewModel>(),
            "AgentOrchestration" => App.Services.GetRequiredService<AgentOrchestrationViewModel>(),
            "OpenSandbox" => App.Services.GetRequiredService<OpenSandboxViewModel>(),
            "Memory" => App.Services.GetRequiredService<MemoryViewModel>(),
            "Skills" => App.Services.GetRequiredService<SkillsViewModel>(),
            "Alerts" => App.Services.GetRequiredService<AlertsViewModel>(),
            "IMSettings" => App.Services.GetRequiredService<IMSettingsViewModel>(),
            "Settings" => App.Services.GetRequiredService<SettingsViewModel>(),
            _ => App.Services.GetRequiredService<HomeViewModel>()
        };
    }

    /// <summary>
    /// 清理已回收的 WeakReference 条目，防止字典膨胀
    /// </summary>
    private void CleanupDeadReferences()
    {
        var deadKeys = _pageCache
            .Where(kv => !kv.Value.TryGetTarget(out _))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in deadKeys)
        {
            _pageCache.Remove(key);
        }
    }

    /// <summary>
    /// 执行窗口操作
    /// </summary>
    private static void ExecuteWindowAction(object? parameter, Action<Window> action)
    {
        if (parameter is Window window)
            action(window);
    }

    /// <summary>
    /// 任务状态变更时的处理
    /// </summary>
    private void OnTaskStatusChanged(TaskModel task)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ActiveTaskCount = _taskManager.GetActiveTasks().Count;
            TotalMemoryMB = _taskManager.GetActiveTasks().Sum(t => t.MemoryUsageMB);
        });
    }

    /// <summary>
    /// 刷新命令处理
    /// </summary>
    private void OnRefresh()
    {
        ActiveTaskCount = _taskManager.GetActiveTasks().Count;
        TotalMemoryMB = _taskManager.GetActiveTasks().Sum(t => t.MemoryUsageMB);
        StatusMessage = "已刷新";
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _taskManager.TaskStatusChanged -= OnTaskStatusChanged;

            foreach (var (_, weakRef) in _pageCache)
            {
                if (weakRef.TryGetTarget(out var vm))
                {
                    vm.Dispose();
                }
            }
            _pageCache.Clear();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// 菜单项 ViewModel
/// </summary>
public class MenuItemViewModel : ViewModelBase
{
    private string _displayName = string.Empty;
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    private string _icon = string.Empty;
    public string Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public Action<string>? OnMenuSelected { get; set; }

    private RelayCommand? _selectionCommand;
    /// <summary>菜单选择命令（缓存实例避免内存泄漏）</summary>
    public RelayCommand SelectionCommand => _selectionCommand ??= new RelayCommand(_ => OnMenuSelected?.Invoke(DisplayName));
}
