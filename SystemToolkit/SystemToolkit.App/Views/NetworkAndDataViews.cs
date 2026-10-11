using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using SystemToolkit.Core.Models;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App.Views;

public class PortScanView : UserControl
{
    private readonly INetworkService _network;
    private readonly TextBox _hostBox;
    private readonly TextBox _startBox;
    private readonly TextBox _endBox;
    private readonly DataGrid _grid;
    private readonly TextBlock _status;
    private CancellationTokenSource? _cts;

    public PortScanView(INetworkService network)
    {
        _network = network;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        header.Children.Add(new TextBlock { Text = "端口扫描（限授权主机）", FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) });
        _hostBox = new TextBox { Text = "127.0.0.1", Width = 140, Margin = new Thickness(0, 0, 8, 0) };
        header.Children.Add(_hostBox);
        _startBox = new TextBox { Text = "1", Width = 60, Margin = new Thickness(0, 0, 8, 0) };
        header.Children.Add(_startBox);
        _endBox = new TextBox { Text = "1024", Width = 60, Margin = new Thickness(0, 0, 8, 0) };
        header.Children.Add(_endBox);
        var scanBtn = new Button { Content = "扫描", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        scanBtn.Click += async (_, _) => await ScanAsync();
        header.Children.Add(scanBtn);
        var cancelBtn = new Button { Content = "取消", Padding = new Thickness(12, 4, 12, 4) };
        cancelBtn.Click += (_, _) => _cts?.Cancel();
        header.Children.Add(cancelBtn);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _grid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, Margin = new Thickness(10) };
        Grid.SetRow(_grid, 1);
        root.Children.Add(_grid);

        _status = FileDedupView.CreateStatus();
        Grid.SetRow(_status, 2);
        root.Children.Add(_status);
        Content = root;
    }

    private async Task ScanAsync()
    {
        if (!int.TryParse(_startBox.Text, out var start) || !int.TryParse(_endBox.Text, out var end) ||
            start < 1 || end > 65535 || start > end || end - start + 1 > 512)
        {
            MessageBox.Show("端口范围无效，单次最多扫描 512 个端口", "提示");
            return;
        }

        var ports = Enumerable.Range(start, end - start + 1).ToList();
        _cts = new CancellationTokenSource();
        _status.Text = $"扫描 {_hostBox.Text}:{start}-{end}…";

        try
        {
            var results = await _network.ScanPortsAsync(_hostBox.Text.Trim(), ports, _cts.Token);
            _grid.ItemsSource = results;
            _status.Text = $"完成，开放端口 {results.Count(r => r.IsOpen)} 个";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "已取消";
        }
        catch (Exception ex)
        {
            _status.Text = "扫描失败";
            MessageBox.Show(ex.Message, "错误");
        }
    }
}

public class TracertView : UserControl
{
    private readonly INetworkService _network;
    private readonly TextBox _hostBox;
    private readonly DataGrid _grid;
    private readonly TextBlock _status;
    private CancellationTokenSource? _cts;

    public TracertView(INetworkService network)
    {
        _network = network;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        header.Children.Add(new TextBlock { Text = "路由追踪", FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) });
        _hostBox = new TextBox { Width = 220, Margin = new Thickness(0, 0, 8, 0), Text = "example.com" };
        header.Children.Add(_hostBox);
        var startBtn = new Button { Content = "开始", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        startBtn.Click += async (_, _) => await RunAsync();
        header.Children.Add(startBtn);
        var cancelBtn = new Button { Content = "取消", Padding = new Thickness(12, 4, 12, 4) };
        cancelBtn.Click += (_, _) => _cts?.Cancel();
        header.Children.Add(cancelBtn);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _grid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, Margin = new Thickness(10) };
        Grid.SetRow(_grid, 1);
        root.Children.Add(_grid);

        _status = FileDedupView.CreateStatus();
        Grid.SetRow(_status, 2);
        root.Children.Add(_status);
        Content = root;
    }

    private async Task RunAsync()
    {
        var host = _hostBox.Text.Trim();
        if (string.IsNullOrEmpty(host))
        {
            MessageBox.Show("请输入目标主机", "提示");
            return;
        }

        _cts = new CancellationTokenSource();
        _status.Text = $"追踪 {host}…";

        try
        {
            var hops = await _network.TraceRouteAsync(host, 30, _cts.Token);
            _grid.ItemsSource = hops;
            _status.Text = $"共 {hops.Count} 跳";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "已取消";
        }
        catch (Exception ex)
        {
            _status.Text = "追踪失败";
            MessageBox.Show(ex.Message, "错误");
        }
    }
}

public class HttpTestView : UserControl
{
    private readonly INetworkService _network;
    private readonly TextBox _urlBox;
    private readonly ComboBox _methodBox;
    private readonly TextBox _bodyBox;
    private readonly TextBox _outputBox;
    private readonly TextBlock _status;

    public HttpTestView(INetworkService network)
    {
        _network = network;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        header.Children.Add(new TextBlock { Text = "HTTP 测试", FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) });
        _methodBox = new ComboBox { Width = 90, Margin = new Thickness(0, 0, 8, 0) };
        foreach (var m in new[] { "GET", "POST", "PUT", "PATCH", "DELETE" }) _methodBox.Items.Add(m);
        _methodBox.SelectedIndex = 0;
        header.Children.Add(_methodBox);
        _urlBox = new TextBox { Width = 360, Margin = new Thickness(0, 0, 8, 0), Text = "https://" };
        header.Children.Add(_urlBox);
        var send = new Button { Content = "发送", Padding = new Thickness(12, 4, 12, 4) };
        send.Click += async (_, _) => await SendAsync();
        header.Children.Add(send);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _bodyBox = new TextBox { AcceptsReturn = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(10, 0, 10, 8) };
        Grid.SetRow(_bodyBox, 1);
        root.Children.Add(_bodyBox);

        _outputBox = new TextBox { AcceptsReturn = true, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new System.Windows.Media.FontFamily("Consolas"), Margin = new Thickness(10, 0, 10, 8) };
        Grid.SetRow(_outputBox, 2);
        root.Children.Add(_outputBox);

        _status = FileDedupView.CreateStatus();
        Grid.SetRow(_status, 3);
        root.Children.Add(_status);
        Content = root;
    }

    private async Task SendAsync()
    {
        var url = _urlBox.Text.Trim();
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            MessageBox.Show("请输入 http(s) URL", "提示");
            return;
        }

        var request = new HttpTestRequest { Url = url, Method = (string)_methodBox.SelectedItem };
        if (_bodyBox.Text.Trim().Length > 0)
        {
            request.Body = _bodyBox.Text;
            request.ContentType = "application/json";
        }

        _status.Text = $"{request.Method} {url} …";
        try
        {
            var response = await _network.SendHttpRequestAsync(request);
            var body = response.Body.Length > 8000 ? response.Body[..8000] + "…（截断）" : response.Body;
            _outputBox.Text = response.Error != null
                ? $"请求失败：{response.Error}"
                : $"{response.StatusCode} {response.StatusDescription}（{response.ResponseTime}ms）\n\n{body}";
            _status.Text = response.Error != null ? "请求失败" : $"状态 {response.StatusCode}";
        }
        catch (Exception ex)
        {
            _status.Text = "请求异常";
            MessageBox.Show(ex.Message, "错误");
        }
    }
}

public class DownloadToolView : UserControl
{
    private readonly INetworkService _network;
    private readonly TextBox _urlBox;
    private readonly TextBox _pathBox;
    private readonly ProgressBar _progress;
    private readonly TextBlock _status;
    private CancellationTokenSource? _cts;

    public DownloadToolView(INetworkService network)
    {
        _network = network;
        var root = new StackPanel();
        root.Children.Add(new TextBlock { Text = "文件下载", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(10) });

        var urlPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 4, 10, 4) };
        urlPanel.Children.Add(new TextBlock { Text = "URL", Width = 60, VerticalAlignment = VerticalAlignment.Center });
        _urlBox = new TextBox { Width = 420 };
        urlPanel.Children.Add(_urlBox);
        root.Children.Add(urlPanel);

        var pathPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 4, 10, 4) };
        pathPanel.Children.Add(new TextBlock { Text = "保存为", Width = 60, VerticalAlignment = VerticalAlignment.Center });
        _pathBox = new TextBox { Width = 340, Margin = new Thickness(0, 0, 8, 0) };
        pathPanel.Children.Add(_pathBox);
        var browse = new Button { Content = "浏览…", Padding = new Thickness(8, 2, 8, 2) };
        browse.Click += (_, _) =>
        {
            var dialog = new SaveFileDialog();
            if (dialog.ShowDialog() == true) _pathBox.Text = dialog.FileName;
        };
        pathPanel.Children.Add(browse);
        root.Children.Add(pathPanel);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 8, 10, 8) };
        var startBtn = new Button { Content = "下载", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        startBtn.Click += async (_, _) => await StartAsync();
        actions.Children.Add(startBtn);
        var cancelBtn = new Button { Content = "取消", Padding = new Thickness(12, 4, 12, 4) };
        cancelBtn.Click += (_, _) => _cts?.Cancel();
        actions.Children.Add(cancelBtn);
        root.Children.Add(actions);

        _progress = new ProgressBar { Height = 14, Margin = new Thickness(10, 0, 10, 8), Minimum = 0, Maximum = 100 };
        root.Children.Add(_progress);

        _status = FileDedupView.CreateStatus();
        root.Children.Add(_status);
        Content = root;
    }

    private async Task StartAsync()
    {
        var url = _urlBox.Text.Trim();
        var path = _pathBox.Text.Trim();
        if (!url.StartsWith("http") || string.IsNullOrEmpty(path))
        {
            MessageBox.Show("请填写 URL 与保存路径", "提示");
            return;
        }

        _cts = new CancellationTokenSource();
        var progress = new Progress<double>(p => Dispatcher.Invoke(() => _progress.Value = p));
        _status.Text = "下载中…";

        try
        {
            var bytes = await _network.DownloadFileAsync(url, path, progress, _cts.Token);
            _progress.Value = 100;
            _status.Text = $"完成，共 {bytes / 1024.0 / 1024:F1} MB";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "已取消";
        }
        catch (Exception ex)
        {
            _status.Text = "下载失败";
            MessageBox.Show(ex.Message, "错误");
        }
    }
}

public class MonitorAlertsView : UserControl
{
    private readonly IMonitorService _monitor;
    private readonly DataGrid _alerts;
    private readonly DataGrid _apps;
    private readonly TextBox _appNameBox;
    private readonly CheckBox _autoRestart;
    private readonly TextBlock _status;
    private readonly DispatcherTimer _timer;
    private CancellationTokenSource? _cts;
    private readonly Dictionary<string, CancellationTokenSource> _appCts = new();

    public MonitorAlertsView(IMonitorService monitor)
    {
        _monitor = monitor;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        header.Children.Add(new TextBlock { Text = "告警与守护", FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) });
        var startBtn = new Button { Content = "开启监控", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        startBtn.Click += (_, _) => Start();
        header.Children.Add(startBtn);
        var stopBtn = new Button { Content = "停止监控", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 16, 0) };
        stopBtn.Click += (_, _) => Stop();
        header.Children.Add(stopBtn);
        _appNameBox = new TextBox { Width = 160, Margin = new Thickness(0, 0, 8, 0) };
        header.Children.Add(_appNameBox);
        _autoRestart = new CheckBox { Content = "崩溃自动重启", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        header.Children.Add(_autoRestart);
        var addApp = new Button { Content = "添加守护", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        addApp.Click += async (_, _) => await AddAppAsync();
        header.Children.Add(addApp);
        var removeApp = new Button { Content = "移除守护", Padding = new Thickness(12, 4, 12, 4) };
        removeApp.Click += (_, _) => RemoveSelectedApp();
        header.Children.Add(removeApp);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _alerts = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, Margin = new Thickness(10, 0, 10, 10) };
        Grid.SetRow(_alerts, 1);
        root.Children.Add(_alerts);

        var appsLabel = new TextBlock { Text = "守护应用", Margin = new Thickness(10, 0, 10, 4), FontWeight = FontWeights.SemiBold };
        Grid.SetRow(appsLabel, 2);
        root.Children.Add(appsLabel);

        _apps = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, Margin = new Thickness(10, 0, 10, 10) };
        Grid.SetRow(_apps, 3);
        root.Children.Add(_apps);

        _status = FileDedupView.CreateStatus();
        Grid.SetRow(_status, 4);
        root.Children.Add(_status);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Unloaded += OnUnloaded;
        Refresh();
        Content = root;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _cts?.Cancel();
        _cts = null;
        foreach (var cts in _appCts.Values) cts.Cancel();
        _appCts.Clear();
    }

    private void RemoveSelectedApp()
    {
        if (_apps.SelectedItem is not SystemToolkit.Core.Models.ApplicationMonitorInfo app)
        {
            MessageBox.Show("请先在守护应用列表中选中一项", "提示");
            return;
        }

        if (_appCts.Remove(app.ProcessName, out var cts)) cts.Cancel();
        _monitor.RemoveApplicationMonitor(app.ProcessName);
        _status.Text = $"已移除守护 {app.ProcessName}";
        Refresh();
    }

    private void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => _monitor.MonitorSystemResourcesAsync(5, 60, _cts.Token));
        _status.Text = "监控已开启";
    }

    private void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        _status.Text = "监控已停止";
    }

    private Task AddAppAsync()
    {
        var name = _appNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("请输入进程名", "提示");
            return Task.CompletedTask;
        }

        if (_appCts.ContainsKey(name))
        {
            MessageBox.Show("该进程已在守护列表中", "提示");
            return Task.CompletedTask;
        }

        bool autoRestart = _autoRestart.IsChecked == true;
        var cts = new CancellationTokenSource();
        _appCts[name] = cts;

        // 守护循环永不返回，必须脱离 UI 线程后台运行
        _ = Task.Run(() => _monitor.SetApplicationMonitorAsync(name, autoRestart, cts.Token));
        _status.Text = $"已添加守护 {name}";
        Refresh();
        return Task.CompletedTask;
    }

    private void Refresh()
    {
        _alerts.ItemsSource = _monitor.GetMonitorAlerts().OrderByDescending(a => a.Timestamp).ToList();
        _apps.ItemsSource = _monitor.GetMonitoredApplications();
    }
}

public class CsvToolView : UserControl
{
    private readonly IDataToolService _dataTool;
    private readonly DataGrid _grid;
    private readonly TextBlock _status;
    private CsvData? _csv;

    public CsvToolView(IDataToolService dataTool)
    {
        _dataTool = dataTool;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        header.Children.Add(new TextBlock { Text = "CSV 工具", FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) });
        var open = new Button { Content = "打开 CSV", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        open.Click += (_, _) => Open();
        header.Children.Add(open);
        var export = new Button { Content = "另存为", Padding = new Thickness(12, 4, 12, 4) };
        export.Click += (_, _) => Export();
        header.Children.Add(export);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _grid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, Margin = new Thickness(10) };
        Grid.SetRow(_grid, 1);
        root.Children.Add(_grid);

        _status = FileDedupView.CreateStatus();
        Grid.SetRow(_status, 2);
        root.Children.Add(_status);
        Content = root;
    }

    private void Open()
    {
        var dialog = new OpenFileDialog { Filter = "CSV 文件|*.csv" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            _csv = _dataTool.ParseCsv(dialog.FileName);
            var table = new System.Data.DataTable();
            foreach (var col in _csv.Columns) table.Columns.Add(col.Name, typeof(string));
            foreach (var row in _csv.Rows)
            {
                var values = new object?[_csv.Columns.Count];
                for (int i = 0; i < _csv.Columns.Count; i++)
                {
                    values[i] = row.TryGetValue(_csv.Columns[i].Name, out var v) ? v : null;
                }
                table.Rows.Add(values);
            }
            _grid.ItemsSource = table.DefaultView;
            _status.Text = $"{dialog.FileName}：{_csv.Rows.Count} 行 × {_csv.Columns.Count} 列";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "错误");
        }
    }

    private void Export()
    {
        if (_csv == null)
        {
            MessageBox.Show("请先打开 CSV 文件", "提示");
            return;
        }

        var dialog = new SaveFileDialog { Filter = "CSV 文件|*.csv" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            _dataTool.ExportCsv(dialog.FileName, _csv);
            _status.Text = $"已导出到 {dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "错误");
        }
    }
}

public class LogAnalyzerView : UserControl
{
    private readonly IDataToolService _dataTool;
    private readonly DataGrid _levels;
    private readonly DataGrid _entries;
    private readonly TextBlock _status;

    public LogAnalyzerView(IDataToolService dataTool)
    {
        _dataTool = dataTool;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        header.Children.Add(new TextBlock { Text = "日志分析", FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) });
        var open = new Button { Content = "打开日志", Padding = new Thickness(12, 4, 12, 4) };
        open.Click += (_, _) => Open();
        header.Children.Add(open);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _levels = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, Margin = new Thickness(10, 0, 10, 10) };
        _levels.Columns.Add(new DataGridTextColumn { Header = "级别", Binding = new System.Windows.Data.Binding("Level"), Width = 120 });
        _levels.Columns.Add(new DataGridTextColumn { Header = "数量", Binding = new System.Windows.Data.Binding("Count"), Width = 120 });
        Grid.SetRow(_levels, 1);
        root.Children.Add(_levels);

        var entriesLabel = new TextBlock { Text = "最后 500 条记录", Margin = new Thickness(10, 0, 10, 4), FontWeight = FontWeights.SemiBold };
        Grid.SetRow(entriesLabel, 2);
        root.Children.Add(entriesLabel);

        _entries = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, Margin = new Thickness(10, 0, 10, 10) };
        Grid.SetRow(_entries, 3);
        root.Children.Add(_entries);

        _status = FileDedupView.CreateStatus();
        Grid.SetRow(_status, 4);
        root.Children.Add(_status);
        Content = root;
    }

    private void Open()
    {
        var dialog = new OpenFileDialog { Filter = "日志文件|*.log|所有文件|*.*" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var stats = _dataTool.AnalyzeLogFile(dialog.FileName);
            _levels.ItemsSource = stats.LevelCounts.Select(kv => new { Level = kv.Key, Count = kv.Value }).ToList();

            var entries = _dataTool.ParseLogFile(dialog.FileName);
            _entries.ItemsSource = entries.TakeLast(500).ToList();

            var range = stats.FirstEntry != null && stats.LastEntry != null
                ? $"{stats.FirstEntry:HH:mm:ss} - {stats.LastEntry:HH:mm:ss}"
                : "无法解析时间戳";
            _status.Text = $"{dialog.FileName}：共 {stats.TotalEntries} 条，时间范围 {range}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "错误");
        }
    }
}

public class CodeGenView : UserControl
{
    private readonly IDevToolService _devTool;
    private readonly ComboBox _templateBox;
    private readonly TextBox _paramsBox;
    private readonly TextBox _outputBox;

    public CodeGenView(IDevToolService devTool)
    {
        _devTool = devTool;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        header.Children.Add(new TextBlock { Text = "代码生成", FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) });
        _templateBox = new ComboBox { Width = 220, Margin = new Thickness(0, 0, 8, 0) };
        foreach (var template in _devTool.GetAvailableTemplates())
        {
            _templateBox.Items.Add(new ComboBoxItem { Content = template.Name, Tag = template.Description });
        }
        if (_templateBox.Items.Count > 0) _templateBox.SelectedIndex = 0;
        header.Children.Add(_templateBox);
        var gen = new Button { Content = "生成", Padding = new Thickness(12, 4, 12, 4), IsEnabled = _templateBox.Items.Count > 0 };
        gen.Click += (_, _) => Generate();
        header.Children.Add(gen);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _paramsBox = new TextBox { AcceptsReturn = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(10, 0, 10, 8) };
        Grid.SetRow(_paramsBox, 1);
        root.Children.Add(_paramsBox);

        _outputBox = new TextBox { AcceptsReturn = true, IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(10, 0, 10, 10) };
        Grid.SetRow(_outputBox, 2);
        root.Children.Add(_outputBox);
        Content = root;
    }

    private void Generate()
    {
        if (_templateBox.SelectedItem is not ComboBoxItem item) return;

        var parameters = new Dictionary<string, string>();
        foreach (var line in _paramsBox.Text.Split('\n'))
        {
            var eq = line.IndexOf('=');
            if (eq > 0)
            {
                parameters[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
        }

        try
        {
            _paramsBox.ToolTip = item.Tag as string ?? "每行一个参数，格式 名称=值";
            _outputBox.Text = _devTool.GenerateCode(item.Content.ToString()!, parameters);
        }
        catch (Exception ex)
        {
            _outputBox.Text = ex.Message;
        }
    }
}
