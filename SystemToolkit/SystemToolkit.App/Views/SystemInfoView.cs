using System.Windows;
using System.Windows.Controls;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App.Views;

public class SystemInfoView : UserControl
{
    private readonly ISystemInfoService _systemInfo;
    private readonly TextBlock _overview;
    private readonly DataGrid _disks;
    private readonly DataGrid _adapters;

    public SystemInfoView(ISystemInfoService systemInfo)
    {
        _systemInfo = systemInfo;
        var root = new Grid();
        for (int i = 0; i < 6; i++)
        {
            root.RowDefinitions.Add(new RowDefinition
            {
                Height = i is 3 or 5 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto
            });
        }

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        header.Children.Add(new TextBlock { Text = "系统信息", FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) });
        var refresh = new Button { Content = "刷新", Padding = new Thickness(12, 4, 12, 4) };
        refresh.Click += (_, _) => Load();
        header.Children.Add(refresh);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _overview = new TextBlock { Margin = new Thickness(10, 0, 10, 10), FontFamily = new System.Windows.Media.FontFamily("Consolas") };
        Grid.SetRow(_overview, 1);
        root.Children.Add(_overview);

        var diskLabel = new TextBlock { Text = "磁盘", Margin = new Thickness(10, 0, 10, 4), FontWeight = FontWeights.SemiBold };
        Grid.SetRow(diskLabel, 2);
        root.Children.Add(diskLabel);

        _disks = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, Margin = new Thickness(10, 0, 10, 10) };
        Grid.SetRow(_disks, 3);
        root.Children.Add(_disks);

        var adapterLabel = new TextBlock { Text = "网络适配器", Margin = new Thickness(10, 0, 10, 4), FontWeight = FontWeights.SemiBold };
        Grid.SetRow(adapterLabel, 4);
        root.Children.Add(adapterLabel);

        _adapters = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, Margin = new Thickness(10) };
        Grid.SetRow(_adapters, 5);
        root.Children.Add(_adapters);

        Content = root;
        Load();
    }

    private void Load()
    {
        var info = _systemInfo.GetOverview();

        var os = string.IsNullOrEmpty(info.ProductName)
            ? info.OsCaption
            : $"{info.ProductName} {info.ProductVersion} (Build {info.BuildNumber})";

        _overview.Text = string.Join("\n", new[]
        {
            $"计算机：{info.MachineName}    用户：{info.UserName}",
            $"系统：{os}",
            $"CPU：{info.CpuName}（{info.ProcessorCount} 逻辑处理器）",
            $"内存：总 {Fmt(info.TotalMemoryBytes)} / 可用 {Fmt(info.AvailableMemoryBytes)}",
            $"运行时长：{info.Uptime.Days} 天 {info.Uptime.Hours} 小时    CLR：{info.ClrVersion}"
        });

        _disks.ItemsSource = _systemInfo.GetDisks().Select(d => new
        {
            驱动器 = d.Name,
            卷标 = d.VolumeLabel,
            类型 = d.DriveType,
            文件系统 = d.FilesystemFormat,
            总容量 = Fmt(d.TotalBytes),
            可用 = Fmt(d.FreeBytes),
            已用 = $"{d.UsedPercentage:F0}%"
        }).ToList();

        _adapters.ItemsSource = _systemInfo.GetNetworkAdapters();
    }

    private static string Fmt(long bytes)
    {
        if (bytes <= 0) return "-";
        double gb = bytes / 1024.0 / 1024.0 / 1024.0;
        return gb >= 1024 ? $"{gb / 1024:F1} TB" : $"{gb:F1} GB";
    }
}
