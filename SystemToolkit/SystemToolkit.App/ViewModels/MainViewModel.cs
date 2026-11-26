using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SystemToolkit.App.Views;
using SystemToolkit.Core.Models;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App.ViewModels;

public class MainViewModel
{
    private readonly IServiceProvider _services;
    private readonly ICommandPaletteService? _palette;

    public List<MenuItemViewModel> MenuItems { get; } = new();

    public MainViewModel(IServiceProvider services)
    {
        _services = services;
        _palette = services.GetService(typeof(ICommandPaletteService)) as ICommandPaletteService;
        InitializeMenu();
        RegisterPaletteEntries();
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
                new() { Name = "目录对比", ViewType = typeof(DirectoryDiffView) },
                new() { Name = "图片批量处理", ViewType = typeof(ImageBatchView) }
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
                new() { Name = "Hosts 编辑", ViewType = typeof(HostsView) },
                new() { Name = "剪贴板历史", ViewType = typeof(ClipboardHistoryView) }
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
                new() { Name = "颜色工具", ViewType = typeof(ColorToolView) },
                new() { Name = "格式转换", ViewType = typeof(ConversionView) },
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

    private void RegisterPaletteEntries()
    {
        if (_palette is null) return;
        foreach (var group in MenuItems)
        {
            foreach (var child in group.Children)
            {
                _palette.Register(child.Name, group.Name, BuildKeywords(child.Name));
            }
        }
    }

    private static string BuildKeywords(string name)
    {
        return name switch
        {
            "批量重命名" => "rename file batch 重命名",
            "文件去重" => "dedup duplicate hash",
            "大文件查找" => "large file size",
            "目录对比" => "diff directory compare",
            "图片批量处理" => "image resize batch 缩放",
            "进程监控" => "process task",
            "CPU/内存" => "cpu memory resource",
            "磁盘分析" => "disk space cleanup",
            "服务管理" => "service windows",
            "告警与守护" => "alert monitor watchdog",
            "Ping 工具" => "ping network latency",
            "端口占用" => "port netstat listen",
            "端口扫描" => "port scan",
            "路由追踪" => "tracert route hop",
            "HTTP 测试" => "http request rest api",
            "文件下载" => "download url file",
            "注册表工具" => "registry regedit",
            "快速启动" => "launch run shortcut",
            "系统信息" => "system info hardware",
            "Hosts 编辑" => "hosts dns",
            "剪贴板历史" => "clipboard history paste",
            "加密解密" => "encrypt decrypt aes hash base64",
            "格式化工具" => "format json xml regex",
            "代码生成" => "codegen template",
            "文本工具" => "text case lines",
            "颜色工具" => "color hex rgb hsl contrast palette",
            "格式转换" => "convert yaml json markdown qrcode base64",
            "离线工具箱（54 项）" => "utility toolbox offline",
            "CSV 工具" => "csv parse export",
            "日志分析" => "log analyze",
            _ => name
        };
    }

    public MenuItemViewModel? FindByTitle(string title)
    {
        foreach (var group in MenuItems)
            foreach (var child in group.Children)
                if (child.Name == title) return child;
        return null;
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
