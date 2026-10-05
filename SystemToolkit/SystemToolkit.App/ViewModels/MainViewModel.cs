namespace SystemToolkit.App.ViewModels;

using System.Windows;
using System.Windows.Controls;
using SystemToolkit.Core.Services;
using Microsoft.Extensions.DependencyInjection;

public class MainViewModel
{
    public List<MenuItemViewModel> MenuItems { get; set; } = new();

    public MainViewModel()
    {
        InitializeMenu();
    }

    private void InitializeMenu()
    {
        // 1. 文件管理
        MenuItems.Add(new MenuItemViewModel
        {
            Name = "📁 文件管理",
            Children = new List<MenuItemViewModel>
            {
                new() { Name = "批量重命名", ViewType = typeof(FileRenameView) },
                new() { Name = "文件去重", ViewType = typeof(FileDedupView) },
                new() { Name = "大文件查找", ViewType = typeof(LargeFileFinderView) }
            }
        });

        // 2. 注册表操作
        MenuItems.Add(new MenuItemViewModel
        {
            Name = "🔧 注册表工具",
            Children = new List<MenuItemViewModel>
            {
                new() { Name = "注册表清理", ViewType = typeof(RegistryCleanupView) },
                new() { Name = "备份恢复", ViewType = typeof(RegistryBackupView) },
                new() { Name = "注册表监控", ViewType = typeof(RegistryMonitorView) }
            }
        });

        // 3. 服务管理
        MenuItems.Add(new MenuItemViewModel
        {
            Name = "⚙️ 服务管理",
            Children = new List<MenuItemViewModel>
            {
                new() { Name = "服务启停", ViewType = typeof(ServiceControlView) },
                new() { Name = "状态监控", ViewType = typeof(ServiceStatusView) }
            }
        });

        // 4. 进程管理
        MenuItems.Add(new MenuItemViewModel
        {
            Name = "🚀 进程管理",
            Children = new List<MenuItemViewModel>
            {
                new() { Name = "进程监控", ViewType = typeof(ProcessMonitorView) },
                new() { Name = "资源占用", ViewType = typeof(ProcessResourceView) }
            }
        });

        // 5. 磁盘管理
        MenuItems.Add(new MenuItemViewModel
        {
            Name = "💾 磁盘管理",
            Children = new List<MenuItemViewModel>
            {
                new() { Name = "空间分析", ViewType = typeof(DiskAnalysisView) },
                new() { Name = "磁盘清理", ViewType = typeof(DiskCleanupView) }
            }
        });

        // 6. 网络工具
        MenuItems.Add(new MenuItemViewModel
        {
            Name = "🌐 网络工具",
            Children = new List<MenuItemViewModel>
            {
                new() { Name = "Ping 工具", ViewType = typeof(PingToolView) },
                new() { Name = "端口扫描", ViewType = typeof(PortScanView) },
                new() { Name = "路由追踪", ViewType = typeof(TracertView) },
                new() { Name = "HTTP 测试", ViewType = typeof(HttpTestView) },
                new() { Name = "端口监控", ViewType = typeof(PortMonitorView) }
            }
        });

        // 7. 开发辅助
        MenuItems.Add(new MenuItemViewModel
        {
            Name = "🛠️ 开发辅助",
            Children = new List<MenuItemViewModel>
            {
                new() { Name = "代码生成", ViewType = typeof(CodeGeneratorView) },
                new() { Name = "格式化工具", ViewType = typeof(FormatterView) },
                new() { Name = "正则测试", ViewType = typeof(RegexTesterView) }
            }
        });

        // 8. 数据处理
        MenuItems.Add(new MenuItemViewModel
        {
            Name = "📊 数据处理",
            Children = new List<MenuItemViewModel>
            {
                new() { Name = "加密解密", ViewType = typeof(EncryptionView) },
                new() { Name = "CSV 处理", ViewType = typeof(CsvToolView) },
                new() { Name = "日志分析", ViewType = typeof(LogAnalyzerView) }
            }
        });

        // 9. 日常效率
        MenuItems.Add(new MenuItemViewModel
        {
            Name = "⏱️ 日常效率",
            Children = new List<MenuItemViewModel>
            {
                new() { Name = "剪贴板历史", ViewType = typeof(ClipboardView) },
                new() { Name = "截图工具", ViewType = typeof(ScreenshotView) },
                new() { Name = "定时器", ViewType = typeof(TimerView) },
                new() { Name = "快捷键", ViewType = typeof(ShortcutView) }
            }
        });

        // 10. 监控告警
        MenuItems.Add(new MenuItemViewModel
        {
            Name = "📈 系统监控",
            Children = new List<MenuItemViewModel>
            {
                new() { Name = "CPU/内存", ViewType = typeof(SystemResourceMonitorView) },
                new() { Name = "磁盘监控", ViewType = typeof(DiskMonitorView) },
                new() { Name = "网络监控", ViewType = typeof(NetworkMonitorView) },
                new() { Name = "应用监控", ViewType = typeof(ApplicationMonitorView) }
            }
        });
    }
}

public class MenuItemViewModel
{
    public string Name { get; set; } = string.Empty;
    public List<MenuItemViewModel> Children { get; set; } = new();

    public Type? ViewType { get; set; }

    private static UserControl CreateDefaultView()
    {
        var textBlock = new TextBlock
        {
            Text = "功能开发中...",
            FontSize = 24,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 128, 128))
        };

        return new UserControl
        {
            Content = textBlock,
            Margin = new Thickness(20)
        };
    }

    public UserControl CreateView()
    {
        return ViewType is null
            ? CreateDefaultView()
            : (UserControl)Activator.CreateInstance(ViewType)!;
    }
}
