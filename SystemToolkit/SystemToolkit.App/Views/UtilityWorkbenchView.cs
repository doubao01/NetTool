using System.Windows;
using System.Windows.Controls;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App.Views;

public sealed class UtilityWorkbenchView : UserControl
{
    private readonly IUtilityWorkbenchService _service;
    private readonly TextBox _search = new() { Margin = new Thickness(0, 0, 8, 8), ToolTip = "搜索工具名称、分类或说明" };
    private readonly ListBox _tools = new() { DisplayMemberPath = nameof(UtilityTool.Name), Margin = new Thickness(0, 0, 8, 0) };
    private readonly TextBlock _description = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
    private readonly TextBlock _optionLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _option = new() { Width = 130, MaxLength = 64, Margin = new Thickness(8, 0, 8, 0) };
    private readonly TextBox _input = CreateEditor(false);
    private readonly TextBox _output = CreateEditor(true);
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly Button _run = new() { Content = "执行", Padding = new Thickness(16, 5, 16, 5) };
    private readonly Button _example = new() { Content = "载入示例", Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _copy = new() { Content = "复制结果", IsEnabled = false, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _reuse = new() { Content = "结果作为输入", IsEnabled = false, Margin = new Thickness(8, 0, 0, 0) };
    private bool _busy;
    private bool _loaded;

    public UtilityWorkbenchView(IUtilityWorkbenchService service)
    {
        _service = service;
        _input.MaxLength = UtilityWorkbenchService.MaxInputBytes;
        var root = new DockPanel { Margin = new Thickness(10) };
        var title = new TextBlock
        {
            Text = $"离线工具箱 · {service.Tools.Count} 项功能",
            FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8)
        };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);
        DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(_status);

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220), MinWidth = 160 });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var catalog = new DockPanel();
        var searchLabel = new TextBlock { Text = "搜索工具 / 分类", Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(searchLabel, Dock.Top);
        catalog.Children.Add(searchLabel);
        DockPanel.SetDock(_search, Dock.Top);
        catalog.Children.Add(_search);
        catalog.Children.Add(_tools);
        columns.Children.Add(catalog);

        var workspace = new Grid();
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, new GridLength(1, GridUnitType.Star) })
            workspace.RowDefinitions.Add(new RowDefinition { Height = height });
        workspace.Children.Add(_description);
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        foreach (var element in new UIElement[] { _optionLabel, _option, _run, _example, _copy, _reuse })
            actions.Children.Add(element);
        Grid.SetRow(actions, 1);
        workspace.Children.Add(actions);
        var inputBox = new GroupBox { Header = "输入（最多 256 KiB UTF-8）", Content = _input };
        Grid.SetRow(inputBox, 2);
        workspace.Children.Add(inputBox);
        var splitter = new GridSplitter { Height = 6, HorizontalAlignment = HorizontalAlignment.Stretch, ResizeDirection = GridResizeDirection.Rows };
        Grid.SetRow(splitter, 3);
        workspace.Children.Add(splitter);
        var outputBox = new GroupBox { Header = "结果（本地计算，按需复制）", Content = _output };
        Grid.SetRow(outputBox, 4);
        workspace.Children.Add(outputBox);
        Grid.SetColumn(workspace, 1);
        columns.Children.Add(workspace);
        root.Children.Add(columns);
        Content = root;

        _tools.SelectionChanged += (_, _) => SelectTool();
        _search.TextChanged += (_, _) => FilterTools();
        _run.Click += async (_, _) => await RunAsync();
        _example.Click += (_, _) => LoadExample();
        _copy.Click += (_, _) => CopyResult();
        _reuse.Click += (_, _) =>
        {
            if (_output.Text.Length > _input.MaxLength)
            {
                _status.Text = "结果超出输入字符上限，请选取需要的内容。";
                return;
            }
            _input.Text = _output.Text;
        };
        Loaded += (_, _) => _loaded = true;
        Unloaded += (_, _) => _loaded = false;
        FilterTools();
    }

    private static TextBox CreateEditor(bool readOnly) => new()
    {
        AcceptsReturn = true, AcceptsTab = true, IsReadOnly = readOnly,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
        Margin = new Thickness(4), IsUndoEnabled = !readOnly
    };

    private void FilterTools()
    {
        var selected = (_tools.SelectedItem as UtilityTool)?.Id;
        var query = _search.Text.Trim();
        var matches = _service.Tools.Where(t =>
            $"{t.Name} {t.Category} {t.Description} {t.Id}".Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        _tools.ItemsSource = matches;
        _tools.SelectedItem = matches.FirstOrDefault(t => t.Id == selected) ?? matches.FirstOrDefault();
        if (matches.Count == 0) _status.Text = "没有匹配的工具，请调整搜索词。";
    }

    private void SelectTool()
    {
        var tool = _tools.SelectedItem as UtilityTool;
        _description.Text = tool == null ? "请选择工具" : $"{tool.Category} / {tool.Name}\n{tool.Description}";
        _optionLabel.Text = tool?.OptionLabel ?? "";
        _option.Text = tool?.DefaultOption ?? "";
        _option.Visibility = string.IsNullOrEmpty(tool?.OptionLabel) ? Visibility.Collapsed : Visibility.Visible;
        _run.IsEnabled = _example.IsEnabled = tool != null && !_busy;
        _output.Clear();
        _copy.IsEnabled = _reuse.IsEnabled = false;
        _status.Text = "输入会在切换工具时保留；生成 GUID 或密码时请清空输入。";
    }

    private void LoadExample()
    {
        if (_tools.SelectedItem is not UtilityTool tool) return;
        if (_input.Text.Length > 0 && MessageBox.Show("载入示例将替换当前输入，是否继续？", "确认替换", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        _input.Text = tool.Example;
        _option.Text = tool.DefaultOption;
        _output.Clear();
        _copy.IsEnabled = _reuse.IsEnabled = false;
        _status.Text = "示例已载入，点击执行查看结果。";
    }

    private async Task RunAsync()
    {
        if (_busy || _tools.SelectedItem is not UtilityTool tool) return;
        var input = _input.Text;
        var option = _option.Text;
        _busy = true;
        _search.IsEnabled = _tools.IsEnabled = _option.IsEnabled = _run.IsEnabled = _example.IsEnabled = false;
        _input.IsReadOnly = true;
        _copy.IsEnabled = _reuse.IsEnabled = false;
        _output.Clear();
        _status.Text = $"正在执行：{tool.Name}";
        try
        {
            var result = await Task.Run(() => _service.Execute(tool.Id, input, option));
            if (!_loaded) return;
            _output.Text = result;
            _copy.IsEnabled = _reuse.IsEnabled = result.Length > 0;
            _status.Text = $"已完成：{tool.Name}，输出 {result.Length:N0} 个 UTF-16 字符。";
        }
        catch (Exception ex)
        {
            if (_loaded) _status.Text = $"执行失败：{ex.Message}";
        }
        finally
        {
            _busy = false;
            _search.IsEnabled = _tools.IsEnabled = _option.IsEnabled = _run.IsEnabled = _example.IsEnabled = true;
            _input.IsReadOnly = false;
        }
    }

    private void CopyResult()
    {
        try
        {
            Clipboard.SetText(_output.Text);
            _status.Text = "结果已复制。密码等敏感内容会进入系统剪贴板，请及时清理。";
        }
        catch (Exception ex) { _status.Text = "复制失败：" + ex.Message; }
    }
}
