using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SystemToolkit.App.Views;

namespace SystemToolkit.App.ViewModels;

public class MainViewModel
{
    private readonly IServiceProvider _services;

    public List<MenuItemViewModel> MenuItems { get; } = new();

    public MainViewModel(IServiceProvider services)
    {
        _services = services;
        InitializeMenu();
    }

    private void InitializeMenu()
    {
        MenuItems.Add(new MenuItemViewModel
        {
            Name = "文件管理",
            Children =
            {
                new() { Name = "批量重命名", ViewType = typeof(FileRenameView) },
                new() { Name = "文件去重", ViewType = typeof(FileDedupView) },
                new() { Name = "大文件查找", ViewType = typeof(LargeFileFinderView) },
                new() { Name = "目录对比", ViewType = typeof(DirectoryDiffView) }
            }
        });

        MenuItems.Add(new MenuItemViewModel
        {
            Name = "进程与监控",
            Children =
            {
                new() { Name = "进程监控", ViewType = typeof(ProcessMonitorView) },
                new() { Name = "CPU/内存", ViewType = typeof(SystemResourceMonitorView) },
                new() { Name = "磁盘分析", ViewType = typeof(DiskAnalysisView) },
                new() { Name = "服务管理", ViewType = typeof(ServiceControlView) },
                new() { Name = "告警与守护", ViewType = typeof(MonitorAlertsView) }
            }
        });

        MenuItems.Add(new MenuItemViewModel
        {
            Name = "网络工具",
            Children =
            {
                new() { Name = "Ping 工具", ViewType = typeof(PingToolView) },
                new() { Name = "端口占用", ViewType = typeof(PortMonitorView) },
                new() { Name = "端口扫描", ViewType = typeof(PortScanView) },
                new() { Name = "路由追踪", ViewType = typeof(TracertView) },
                new() { Name = "HTTP 测试", ViewType = typeof(HttpTestView) },
                new() { Name = "文件下载", ViewType = typeof(DownloadToolView) }
            }
        });

        MenuItems.Add(new MenuItemViewModel
        {
            Name = "系统工具",
            Children =
            {
                new() { Name = "注册表工具", ViewType = typeof(RegistryToolView) },
                new() { Name = "快速启动", ViewType = typeof(QuickLaunchView) },
                new() { Name = "系统信息", ViewType = typeof(SystemInfoView) },
                new() { Name = "Hosts 编辑", ViewType = typeof(HostsView) }
            }
        });

        MenuItems.Add(new MenuItemViewModel
        {
            Name = "开发辅助",
            Children =
            {
                new() { Name = "加密解密", ViewType = typeof(EncryptionView) },
                new() { Name = "格式化工具", ViewType = typeof(FormatterView) },
                new() { Name = "代码生成", ViewType = typeof(CodeGenView) },
                new() { Name = "文本工具", ViewType = typeof(TextToolView) },
                new() { Name = "离线工具箱（54 项）", ViewType = typeof(UtilityWorkbenchView) }
            }
        });

        MenuItems.Add(new MenuItemViewModel
        {
            Name = "数据工具",
            Children =
            {
                new() { Name = "CSV 工具", ViewType = typeof(CsvToolView) },
                new() { Name = "日志分析", ViewType = typeof(LogAnalyzerView) }
            }
        });
    }

    public UserControl CreateView(MenuItemViewModel item)
    {
        if (item.ViewType is null)
        {
            return new UserControl
            {
                Content = new TextBlock
                {
                    Text = item.Name,
                    FontSize = 18,
                    Margin = new System.Windows.Thickness(20)
                }
            };
        }

        return (UserControl)_services.GetRequiredService(item.ViewType);
    }
}

public class MenuItemViewModel
{
    public string Name { get; set; } = string.Empty;
    public List<MenuItemViewModel> Children { get; set; } = new();
    public Type? ViewType { get; set; }
}
