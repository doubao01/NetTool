using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SystemToolkit.Core.Models;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App.Views;

public class FileDedupView : UserControl
{
    private readonly IFileService _fileService;
    private readonly TextBox _directoryBox;
    private readonly ListBox _resultList;
    private readonly TextBlock _status;
    private List<DuplicateFile> _duplicates = new();

    public FileDedupView(IFileService fileService)
    {
        _fileService = fileService;
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var dirPanel = CreateDirPanel(out _directoryBox);
        Grid.SetRow(dirPanel, 0);
        grid.Children.Add(dirPanel);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        var scanBtn = new Button { Content = "扫描重复文件", Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(0, 0, 10, 0) };
        scanBtn.Click += Scan_Click;
        var deleteBtn = new Button { Content = "删除所选副本", Padding = new Thickness(16, 6, 16, 6) };
        deleteBtn.Click += Delete_Click;
        btnPanel.Children.Add(scanBtn);
        btnPanel.Children.Add(deleteBtn);
        Grid.SetRow(btnPanel, 1);
        grid.Children.Add(btnPanel);

        _resultList = new ListBox { Margin = new Thickness(10), SelectionMode = SelectionMode.Extended };
        Grid.SetRow(_resultList, 2);
        grid.Children.Add(_resultList);

        _status = CreateStatus();
        Grid.SetRow(_status, 3);
        grid.Children.Add(_status);

        Content = grid;
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        var dir = _directoryBox.Text.Trim();
        if (!Directory.Exists(dir))
        {
            MessageBox.Show("目录不存在", "提示");
            return;
        }

        _status.Text = "扫描中...";
        try
        {
            _duplicates = await _fileService.FindDuplicateFilesAsync(dir);
            _resultList.Items.Clear();
            foreach (var group in _duplicates)
            {
                _resultList.Items.Add($"[{group.Hash[..8]}] {group.Paths.Count} 个文件, {group.Size} 字节");
                foreach (var path in group.Paths)
                {
                    _resultList.Items.Add("  " + path);
                }
            }
            _status.Text = $"发现 {_duplicates.Count} 组重复文件";
        }
        catch (Exception ex)
        {
            _status.Text = "扫描失败";
            MessageBox.Show(ex.Message, "错误");
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var toDelete = new List<string>();
        foreach (var group in _duplicates)
        {
            if (group.Paths.Count > 1)
            {
                toDelete.AddRange(group.Paths.Skip(1));
            }
        }

        if (toDelete.Count == 0)
        {
            MessageBox.Show("没有可删除的副本（每组保留第一个）", "提示");
            return;
        }

        if (MessageBox.Show($"将删除 {toDelete.Count} 个副本，每组保留第一个文件。确认？", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            return;
        }

        await _fileService.DeleteFilesAsync(toDelete);
        _status.Text = $"已删除 {toDelete.Count} 个副本";
        Scan_Click(sender, e);
    }

    internal static StackPanel CreateDirPanel(out TextBox box)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        panel.Children.Add(new TextBlock { Text = "目录:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        box = new TextBox { Width = 420, Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Margin = new Thickness(0, 0, 8, 0) };
        panel.Children.Add(box);
        var browse = new Button { Content = "浏览...", Padding = new Thickness(12, 4, 12, 4) };
        var captured = box;
        browse.Click += (_, _) =>
        {
            var dlg = new OpenFolderDialog { Title = "选择目录" };
            if (dlg.ShowDialog() == true)
            {
                captured.Text = dlg.FolderName;
            }
        };
        panel.Children.Add(browse);
        return panel;
    }

    internal static TextBlock CreateStatus() => new()
    {
        Margin = new Thickness(10, 4, 10, 8),
        FontWeight = FontWeights.Bold,
        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204))
    };
}

public class LargeFileFinderView : UserControl
{
    private readonly IFileService _fileService;
    private readonly TextBox _directoryBox;
    private readonly TextBox _minSizeBox;
    private readonly DataGrid _grid;
    private readonly TextBlock _status;

    public LargeFileFinderView(IFileService fileService)
    {
        _fileService = fileService;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var dirPanel = FileDedupView.CreateDirPanel(out _directoryBox);
        Grid.SetRow(dirPanel, 0);
        root.Children.Add(dirPanel);

        var opt = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        opt.Children.Add(new TextBlock { Text = "最小大小 (MB):", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        _minSizeBox = new TextBox { Width = 80, Text = "10", Margin = new Thickness(0, 0, 12, 0) };
        opt.Children.Add(_minSizeBox);
        var scan = new Button { Content = "扫描", Padding = new Thickness(16, 6, 16, 6) };
        scan.Click += Scan_Click;
        opt.Children.Add(scan);
        Grid.SetRow(opt, 1);
        root.Children.Add(opt);

        _grid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, Margin = new Thickness(10) };
        Grid.SetRow(_grid, 2);
        root.Children.Add(_grid);

        _status = FileDedupView.CreateStatus();
        Grid.SetRow(_status, 3);
        root.Children.Add(_status);
        Content = root;
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (!long.TryParse(_minSizeBox.Text, out var mb) || mb < 0)
        {
            MessageBox.Show("请输入有效的最小大小", "提示");
            return;
        }

        var dir = _directoryBox.Text.Trim();
        _status.Text = "扫描中...";
        try
        {
            var files = await _fileService.FindLargeFilesAsync(dir, mb * 1024 * 1024);
            _grid.ItemsSource = files.Select(f => new
            {
                f.Name,
                SizeMB = Math.Round(f.Size / 1024.0 / 1024.0, 2),
                f.Path,
                f.ModifiedTime
            }).ToList();
            _status.Text = $"找到 {files.Count} 个大文件";
        }
        catch (Exception ex)
        {
            _status.Text = "扫描失败";
            MessageBox.Show(ex.Message, "错误");
        }
    }
}

public class DiskAnalysisView : UserControl
{
    private readonly IDiskService _diskService;
    private readonly DataGrid _grid;

    public DiskAnalysisView(IDiskService diskService)
    {
        _diskService = diskService;
        _grid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, Margin = new Thickness(10) };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        header.Children.Add(new TextBlock { Text = "磁盘空间", FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) });
        var refresh = new Button { Content = "刷新", Padding = new Thickness(12, 4, 12, 4) };
        refresh.Click += (_, _) => Load();
        header.Children.Add(refresh);
        var cleanupBtn = new Button { Content = "清理建议", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(8, 0, 0, 0) };
        cleanupBtn.Click += (_, _) =>
        {
            _grid.ItemsSource = _diskService.GetCleanupSuggestions().Select(s => new
            {
                s.Category,
                s.Path,
                SizeGB = Math.Round(s.Size / 1024.0 / 1024 / 1024, 2),
                s.SafeToDelete,
                s.Description
            }).ToList();
        };
        header.Children.Add(cleanupBtn);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        Grid.SetRow(_grid, 1);
        root.Children.Add(_grid);
        Content = root;
        Load();
    }

    private void Load()
    {
        _grid.ItemsSource = _diskService.GetAllDrives().Select(d => new
        {
            d.Name,
            d.VolumeLabel,
            d.DriveFormat,
            TotalGB = Math.Round(d.TotalSize / 1024.0 / 1024 / 1024, 1),
            FreeGB = Math.Round(d.TotalFreeSpace / 1024.0 / 1024 / 1024, 1),
            UsedPercent = Math.Round(d.UsagePercentage, 1)
        }).ToList();
    }
}

public class FormatterView : UserControl
{
    private readonly IDevToolService _devTool;
    private TextBox _input = null!;
    private TextBox _output = null!;
    private TextBox _regexPattern = null!;
    private TextBox _regexInput = null!;

    public FormatterView(IDevToolService devTool)
    {
        _devTool = devTool;
        var tabs = new TabControl { Margin = new Thickness(10) };
        tabs.Items.Add(new TabItem { Header = "JSON/XML", Content = CreateFormatPanel() });
        tabs.Items.Add(new TabItem { Header = "正则测试", Content = CreateRegexPanel() });
        Content = tabs;
    }

    private Grid CreateFormatPanel()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var btns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var jsonBtn = new Button { Content = "格式化 JSON", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        jsonBtn.Click += (_, _) =>
        {
            var r = _devTool.FormatJson(_input.Text);
            _output.Text = r.Success ? r.FormattedText ?? string.Empty : r.Error ?? "格式化失败";
        };
        var xmlBtn = new Button { Content = "格式化 XML", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        xmlBtn.Click += (_, _) =>
        {
            var r = _devTool.FormatXml(_input.Text);
            _output.Text = r.Success ? r.FormattedText ?? string.Empty : r.Error ?? "格式化失败";
        };
        var b64e = new Button { Content = "Base64 编码", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        b64e.Click += (_, _) => _output.Text = _devTool.EncodeBase64(_input.Text);
        var b64d = new Button { Content = "Base64 解码", Padding = new Thickness(12, 4, 12, 4) };
        b64d.Click += (_, _) =>
        {
            try { _output.Text = _devTool.DecodeBase64(_input.Text); }
            catch (Exception ex) { _output.Text = ex.Message; }
        };
        btns.Children.Add(jsonBtn);
        btns.Children.Add(xmlBtn);
        btns.Children.Add(b64e);
        btns.Children.Add(b64d);
        Grid.SetRow(btns, 0);
        grid.Children.Add(btns);

        _input = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new System.Windows.Media.FontFamily("Consolas"), Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(_input, 1);
        grid.Children.Add(_input);

        _output = new TextBox { AcceptsReturn = true, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new System.Windows.Media.FontFamily("Consolas") };
        Grid.SetRow(_output, 2);
        grid.Children.Add(_output);
        return grid;
    }

    private Grid CreateRegexPanel()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(new TextBlock { Text = "正则:", Margin = new Thickness(0, 4, 0, 4) });
        _regexPattern = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(_regexPattern, 1);
        grid.Children.Add(_regexPattern);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var testBtn = new Button { Content = "测试", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        Grid.SetRow(btnPanel, 2);
        grid.Children.Add(btnPanel);

        var validateBtn = new Button { Content = "校验语法", Padding = new Thickness(12, 4, 12, 4) };
        validateBtn.Click += (_, _) =>
            MessageBox.Show(_devTool.ValidateRegex(_regexPattern.Text) ? "正则语法合法" : "正则语法非法", "校验结果");
        btnPanel.Children.Add(testBtn);
        btnPanel.Children.Add(validateBtn);

        _regexInput = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new System.Windows.Media.FontFamily("Consolas") };
        Grid.SetRow(_regexInput, 3);
        grid.Children.Add(_regexInput);

        testBtn.Click += (_, _) =>
        {
            try
            {
                var result = _devTool.TestRegex(_regexPattern.Text, _regexInput.Text, "");
                MessageBox.Show(result.IsMatch
                    ? $"匹配成功，共 {result.Matches.Count} 处，耗时 {result.ExecutionTimeMs}ms"
                    : "未匹配", "正则结果");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "正则错误");
            }
        };
        return grid;
    }
}

public class ServiceControlView : UserControl
{
    private readonly IServiceService _serviceService;
    private readonly DataGrid _grid;
    private readonly TextBox _filterBox;
    private readonly TextBlock _status;

    public ServiceControlView(IServiceService serviceService)
    {
        _serviceService = serviceService;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        header.Children.Add(new TextBlock { Text = "Windows 服务", FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
        _filterBox = new TextBox { Width = 180, Margin = new Thickness(0, 0, 8, 0) };
        header.Children.Add(_filterBox);
        var refresh = new Button { Content = "刷新", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        refresh.Click += (_, _) => Load();
        header.Children.Add(refresh);
        var start = new Button { Content = "启动", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        start.Click += (_, _) => RunAction(s => _serviceService.StartService(s.Name), "启动");
        header.Children.Add(start);
        var stop = new Button { Content = "停止", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        stop.Click += (_, _) => RunAction(s => _serviceService.StopService(s.Name), "停止");
        header.Children.Add(stop);
        var restart = new Button { Content = "重启", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        restart.Click += (_, _) => RunAction(s => _serviceService.RestartService(s.Name), "重启");
        header.Children.Add(restart);
        var modeBox = new ComboBox { Width = 110, Margin = new Thickness(0, 0, 8, 0) };
        foreach (var mode in Enum.GetNames(typeof(ServiceStartMode)))
        {
            modeBox.Items.Add(mode);
        }
        modeBox.SelectedIndex = 3;
        header.Children.Add(modeBox);
        var applyMode = new Button { Content = "设启动类型", Padding = new Thickness(12, 4, 12, 4) };
        applyMode.Click += (_, _) => ApplyStartMode((string)modeBox.SelectedItem);
        header.Children.Add(applyMode);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, Margin = new Thickness(10) };
        _grid.Columns.Add(new DataGridTextColumn { Header = "名称", Binding = new System.Windows.Data.Binding("Name"), Width = 160 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "显示名", Binding = new System.Windows.Data.Binding("DisplayName"), Width = 220 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "状态", Binding = new System.Windows.Data.Binding("Status"), Width = 100 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "启动类型", Binding = new System.Windows.Data.Binding("StartMode"), Width = 100 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "描述", Binding = new System.Windows.Data.Binding("Description"), Width = 260 });
        Grid.SetRow(_grid, 1);
        root.Children.Add(_grid);

        _status = FileDedupView.CreateStatus();
        Grid.SetRow(_status, 2);
        root.Children.Add(_status);
        Content = root;
        Load();
    }

    private void Load()
    {
        var keyword = _filterBox.Text.Trim();
        var list = _serviceService.GetAllServices();
        if (!string.IsNullOrEmpty(keyword))
        {
            list = list.Where(s =>
                s.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                s.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        _grid.ItemsSource = list;
        _status.Text = $"共 {list.Count} 个服务";
    }

    private void RunAction(Action<ServiceInfo> action, string label)
    {
        if (_grid.SelectedItem is not ServiceInfo service)
        {
            MessageBox.Show("请先选择服务", "提示");
            return;
        }

        if (MessageBox.Show($"确认{label}服务 {service.DisplayName}？", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            action(service);
            Load();
            _status.Text = $"已{label} {service.Name}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "错误");
        }
    }

    private void ApplyStartMode(string modeName)
    {
        if (_grid.SelectedItem is not ServiceInfo service)
        {
            MessageBox.Show("请先选择服务", "提示");
            return;
        }

        var mode = modeName switch
        {
            "Automatic" => ServiceStartMode.Automatic,
            "Manual" => ServiceStartMode.Manual,
            "Disabled" => ServiceStartMode.Disabled,
            _ => ServiceStartMode.Manual
        };

        if (MessageBox.Show($"确认将服务 {service.DisplayName} 的启动类型改为 {modeName}？", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _serviceService.SetServiceStartMode(service.Name, mode);
            Load();
            _status.Text = $"已设置 {service.Name} 启动类型为 {modeName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "错误");
        }
    }
}

public class PortMonitorView : UserControl
{
    private readonly INetworkService _networkService;
    private readonly DataGrid _grid;
    private readonly TextBlock _status;

    public PortMonitorView(INetworkService networkService)
    {
        _networkService = networkService;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        header.Children.Add(new TextBlock { Text = "端口占用", FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
        var refresh = new Button { Content = "刷新", Padding = new Thickness(12, 4, 12, 4) };
        refresh.Click += (_, _) => Load();
        header.Children.Add(refresh);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _grid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, Margin = new Thickness(10) };
        Grid.SetRow(_grid, 1);
        root.Children.Add(_grid);

        _status = FileDedupView.CreateStatus();
        Grid.SetRow(_status, 2);
        root.Children.Add(_status);
        Content = root;
        Load();
    }

    private void Load()
    {
        var items = _networkService.GetPortUsage();
        _grid.ItemsSource = items;
        _status.Text = $"当前连接 {items.Count} 条";
    }
}

public class RegistryToolView : UserControl
{
    private readonly IRegistryService _registry;
    private readonly TextBox _keyBox;
    private readonly TextBox _valueNameBox;
    private readonly TextBox _valueDataBox;
    private readonly ComboBox _valueKindBox;
    private readonly DataGrid _grid;
    private readonly TextBlock _status;

    public RegistryToolView(IRegistryService registry)
    {
        _registry = registry;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock { Text = "注册表工具（默认 HKCU，删除需确认）", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(10) };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 10, 10) };
        _keyBox = new TextBox { Width = 380, Text = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", Margin = new Thickness(0, 0, 8, 0) };
        header.Children.Add(_keyBox);
        var read = new Button { Content = "读取值", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        read.Click += (_, _) => Load();
        header.Children.Add(read);
        var scan = new Button { Content = "扫描无效自启动", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        scan.Click += (_, _) => ScanInvalid();
        header.Children.Add(scan);
        var backup = new Button { Content = "备份当前分支", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        backup.Click += async (_, _) => await BackupAsync();
        header.Children.Add(backup);
        var restore = new Button { Content = "恢复备份", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        restore.Click += async (_, _) => await RestoreAsync();
        header.Children.Add(restore);
        var delete = new Button { Content = "删除选中值", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        delete.Click += (_, _) => DeleteSelected();
        header.Children.Add(delete);
        var deleteKey = new Button { Content = "删除键路径", Padding = new Thickness(12, 4, 12, 4) };
        deleteKey.Click += (_, _) => DeleteCurrentKey();
        header.Children.Add(deleteKey);
        Grid.SetRow(header, 1);
        root.Children.Add(header);

        var editPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 10, 10) };
        editPanel.Children.Add(new TextBlock { Text = "值名", Width = 44, VerticalAlignment = VerticalAlignment.Center });
        _valueNameBox = new TextBox { Width = 140, Margin = new Thickness(0, 0, 8, 10) };
        editPanel.Children.Add(_valueNameBox);
        editPanel.Children.Add(new TextBlock { Text = "数据", Width = 44, VerticalAlignment = VerticalAlignment.Center });
        _valueDataBox = new TextBox { Width = 260, Margin = new Thickness(0, 0, 8, 0) };
        editPanel.Children.Add(_valueDataBox);
        _valueKindBox = new ComboBox { Width = 100, Margin = new Thickness(0, 0, 8, 0) };
        foreach (var kind in new[] { "String", "DWord", "QWord", "ExpandString", "MultiString" }) _valueKindBox.Items.Add(kind);
        _valueKindBox.SelectedIndex = 0;
        editPanel.Children.Add(_valueKindBox);
        var write = new Button { Content = "写入值", Padding = new Thickness(12, 4, 12, 4) };
        write.Click += (_, _) => WriteValue();
        editPanel.Children.Add(write);
        Grid.SetRow(editPanel, 2);
        root.Children.Add(editPanel);

        _grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, Margin = new Thickness(10, 0, 10, 10) };
        _grid.Columns.Add(new DataGridTextColumn { Header = "键路径", Binding = new System.Windows.Data.Binding("KeyPath"), Width = 280 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "值名", Binding = new System.Windows.Data.Binding("ValueName"), Width = 180 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "数据", Binding = new System.Windows.Data.Binding("Value"), Width = 260 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "类型", Binding = new System.Windows.Data.Binding("ValueType"), Width = 100 });
        Grid.SetRow(_grid, 3);
        root.Children.Add(_grid);

        _status = FileDedupView.CreateStatus();
        Grid.SetRow(_status, 4);
        root.Children.Add(_status);
        Content = root;
        Load();
    }

    private void Load()
    {
        try
        {
            var items = _registry.GetRegistryValues(_keyBox.Text.Trim());
            _grid.ItemsSource = items;
            _status.Text = $"共 {items.Count} 个值";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "错误");
        }
    }

    private void ScanInvalid()
    {
        var items = _registry.FindInvalidKeys();
        _grid.ItemsSource = items;
        _status.Text = $"发现 {items.Count} 个指向缺失文件的自启动项";
    }

    private async Task BackupAsync()
    {
        var dialog = new OpenFolderDialog { Title = "选择备份目录" };
        if (dialog.ShowDialog() != true) return;

        var keyPath = _keyBox.Text.Trim().TrimEnd('\\');
        var hive = HiveOf(keyPath);
        if (hive is null || keyPath.Length == hive.Length)
        {
            MessageBox.Show("请选择以 HKEY_ 根开头的具体分支，备份范围为该分支及子键。", "提示");
            return;
        }

        try
        {
            _status.Text = "备份中…";
            var file = await _registry.BackupRegistryAsync(keyPath, dialog.FolderName);
            _status.Text = $"已备份到 {file}";
        }
        catch (Exception ex)
        {
            _status.Text = "备份失败";
            MessageBox.Show(ex.Message, "错误");
        }
    }

    private static string? HiveOf(string keyPath)
    {
        foreach (var hive in new[] { @"HKEY_CURRENT_USER", @"HKEY_LOCAL_MACHINE", @"HKEY_CLASSES_ROOT", @"HKEY_USERS", @"HKEY_CURRENT_CONFIG" })
        {
            if (keyPath.Equals(hive, StringComparison.OrdinalIgnoreCase) ||
                keyPath.StartsWith(hive + "\\", StringComparison.OrdinalIgnoreCase)) return hive;
        }
        return null;
    }

    private void DeleteSelected()
    {
        if (_grid.SelectedItem is not RegistryItem item)
        {
            MessageBox.Show("请先选择要删除的值", "提示");
            return;
        }

        if (MessageBox.Show($"确认删除 {item.KeyPath}\\{item.ValueName}？", "确认删除", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _registry.DeleteValue(item.KeyPath, item.ValueName);
            _status.Text = $"已删除 {item.ValueName}";
            Load();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "错误");
        }
    }

    private void WriteValue()
    {
        var name = _valueNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("请输入值名", "提示");
            return;
        }

        var keyPath = _keyBox.Text.Trim();
        if (MessageBox.Show($"确认在 {keyPath} 下写入/覆盖值 {name}？", "确认写入", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var kind = (Microsoft.Win32.RegistryValueKind)Enum.Parse(typeof(Microsoft.Win32.RegistryValueKind), _valueKindBox.SelectedItem as string ?? "String");
            object data = kind switch
            {
                Microsoft.Win32.RegistryValueKind.DWord => checked((int)ParseNumber(_valueDataBox.Text.Trim())),
                Microsoft.Win32.RegistryValueKind.QWord => ParseNumber(_valueDataBox.Text.Trim()),
                Microsoft.Win32.RegistryValueKind.MultiString => _valueDataBox.Text.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray(),
                _ => _valueDataBox.Text
            };
            _registry.SetValue(keyPath, name, data, kind);
            _status.Text = $"已写入 {name}";
            Load();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "错误");
        }
    }

    private static long ParseNumber(string text)
    {
        var hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        var digits = hex ? text[2..] : text;
        var style = hex ? System.Globalization.NumberStyles.AllowHexSpecifier : System.Globalization.NumberStyles.Integer;
        if (long.TryParse(digits, style, System.Globalization.CultureInfo.InvariantCulture, out var value)) return value;
        throw new FormatException("请输入有效的十进制整数或以 0x 开头的十六进制整数。");
    }

    private void DeleteCurrentKey()
    {
        var keyPath = _keyBox.Text.Trim();
        if (HiveOf(keyPath) is null || keyPath.Count(c => c == '\\') < 1)
        {
            MessageBox.Show("请选择有效的子键路径（不能删除根键）", "提示");
            return;
        }

        if (MessageBox.Show($"确认删除键 {keyPath}（仅当它没有子键时才会成功）？", "确认删除", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _registry.DeleteKey(keyPath, recursive: false);
            _status.Text = $"已删除键 {keyPath}";
            Load();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "错误");
        }
    }

    private async Task RestoreAsync()
    {
        var dialog = new OpenFileDialog { Title = "选择要恢复的 .reg 备份", Filter = "注册表文件|*.reg" };
        if (dialog.ShowDialog() != true) return;

        if (MessageBox.Show($"确认将 {dialog.FileName} 合并写回注册表？此操作会修改系统配置。", "确认恢复", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _status.Text = "恢复中…";
            await _registry.RestoreRegistryAsync(dialog.FileName);
            _status.Text = "恢复完成";
            Load();
        }
        catch (Exception ex)
        {
            _status.Text = "恢复失败";
            MessageBox.Show(ex.Message, "错误");
        }
    }
}

public class QuickLaunchView : UserControl
{
    private readonly IAppLauncherService _launcher;
    private readonly TextBox _appBox;
    private readonly TextBox _argsBox;
    private readonly CheckBox _adminCheck;
    private readonly TextBox _urlBox;
    private readonly TextBlock _status;

    public QuickLaunchView(IAppLauncherService launcher)
    {
        _launcher = launcher;
        _status = FileDedupView.CreateStatus();
        var root = new StackPanel();
        root.Children.Add(new TextBlock { Text = "快速启动", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(10) });

        var appPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 4, 10, 4) };
        appPanel.Children.Add(new TextBlock { Text = "程序", Width = 60, VerticalAlignment = VerticalAlignment.Center });
        _appBox = new TextBox { Width = 300, Margin = new Thickness(0, 0, 8, 0) };
        appPanel.Children.Add(_appBox);
        var browseApp = new Button { Content = "选择…", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 8, 0) };
        browseApp.Click += (_, _) =>
        {
            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true) _appBox.Text = dialog.FileName;
        };
        appPanel.Children.Add(browseApp);
        _adminCheck = new CheckBox { Content = "管理员", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        appPanel.Children.Add(_adminCheck);
        var launch = new Button { Content = "启动", Padding = new Thickness(12, 4, 12, 4) };
        launch.Click += (_, _) => LaunchApp();
        appPanel.Children.Add(launch);
        root.Children.Add(appPanel);

        var argsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 4, 10, 4) };
        argsPanel.Children.Add(new TextBlock { Text = "参数", Width = 60, VerticalAlignment = VerticalAlignment.Center });
        _argsBox = new TextBox { Width = 420 };
        argsPanel.Children.Add(_argsBox);
        root.Children.Add(argsPanel);

        var urlPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 4, 10, 4) };
        urlPanel.Children.Add(new TextBlock { Text = "网址", Width = 60, VerticalAlignment = VerticalAlignment.Center });
        _urlBox = new TextBox { Width = 300, Text = "https://" };
        urlPanel.Children.Add(_urlBox);
        var openUrl = new Button { Content = "打开", Padding = new Thickness(12, 4, 12, 4) };
        openUrl.Click += (_, _) =>
        {
            try
            {
                _launcher.OpenUrl(_urlBox.Text.Trim());
                _status.Text = $"已打开 {_urlBox.Text}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "错误");
            }
        };
        urlPanel.Children.Add(openUrl);
        root.Children.Add(urlPanel);

        var folderPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 4, 10, 4) };
        folderPanel.Children.Add(new TextBlock { Text = "文件夹", Width = 60, VerticalAlignment = VerticalAlignment.Center });
        var folderBox = new TextBox { Width = 300, Margin = new Thickness(0, 0, 8, 0) };
        folderPanel.Children.Add(folderBox);
        var openFolder = new Button { Content = "在资源管理器打开", Padding = new Thickness(12, 4, 12, 4) };
        openFolder.Click += (_, _) =>
        {
            try
            {
                _launcher.OpenFolder(folderBox.Text.Trim());
                _status.Text = $"已打开 {folderBox.Text}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "错误");
            }
        };
        folderPanel.Children.Add(openFolder);
        root.Children.Add(folderPanel);

        root.Children.Add(_status);
        Content = root;
    }

    private void LaunchApp()
    {
        var path = _appBox.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            MessageBox.Show("请选择要启动的程序", "提示");
            return;
        }

        if (_adminCheck.IsChecked == true &&
            MessageBox.Show($"确认以管理员权限启动 {path}？", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _launcher.LaunchApp(path, _argsBox.Text, _adminCheck.IsChecked == true);
            _status.Text = $"已启动 {path}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "错误");
        }
    }
}
