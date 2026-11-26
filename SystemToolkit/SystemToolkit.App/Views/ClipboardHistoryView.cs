using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SystemToolkit.Core.Models;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App.Views;

public class ClipboardHistoryView : UserControl, IDisposable
{
    private readonly IClipboardHistoryService _history;
    private readonly TextBox _search;
    private readonly ListBox _list;
    private readonly TextBox _detail;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly DispatcherTimer _timer;
    private string? _lastClipboard;
    private bool _listening;

    public ClipboardHistoryView(IClipboardHistoryService history)
    {
        _history = history;
        var root = new Grid { Margin = new Thickness(10) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new TextBlock
        {
            Text = "剪贴板历史", FontSize = 18, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0)
        });
        var toggle = new Button { Content = "开始监听", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        toggle.Click += (_, _) =>
        {
            _listening = !_listening;
            toggle.Content = _listening ? "暂停监听" : "开始监听";
            _status.Text = _listening ? "正在监听剪贴板文本变化。" : "已暂停监听。";
        };
        header.Children.Add(toggle);
        var copy = new Button { Content = "复制所选", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        copy.Click += (_, _) => CopySelected();
        header.Children.Add(copy);
        var pin = new Button { Content = "固定/取消", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        pin.Click += (_, _) => TogglePin();
        header.Children.Add(pin);
        var remove = new Button { Content = "删除", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        remove.Click += (_, _) => RemoveSelected();
        header.Children.Add(remove);
        var clear = new Button { Content = "清空未固定", Padding = new Thickness(12, 4, 12, 4) };
        clear.Click += (_, _) =>
        {
            if (MessageBox.Show("清空所有未固定的记录？", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            _history.ClearUnpinned();
            Refresh();
            _status.Text = "已清空未固定记录。";
        };
        header.Children.Add(clear);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _search = new TextBox { Margin = new Thickness(0, 0, 0, 8), ToolTip = "按内容搜索历史记录" };
        _search.TextChanged += (_, _) => Refresh();
        Grid.SetRow(_search, 1);
        root.Children.Add(_search);

        var contentGrid = new Grid();
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _list = new ListBox { DisplayMemberPath = nameof(ClipboardEntry.Preview) };
        _list.SelectionChanged += (_, _) => ShowDetail();
        contentGrid.Children.Add(_list);

        _detail = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetColumn(_detail, 1);
        contentGrid.Children.Add(_detail);
        Grid.SetRow(contentGrid, 2);
        root.Children.Add(contentGrid);

        Grid.SetRow(_status, 3);
        root.Children.Add(_status);

        Content = root;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _timer.Tick += (_, _) => PollClipboard();
        if (OperatingSystem.IsWindows())
        {
            _listening = true;
            _timer.Start();
            _status.Text = "正在监听剪贴板文本变化。";
        }
        else
        {
            _status.Text = "剪贴板监听仅在 Windows 可用，当前可手动管理历史数据。";
        }

        Refresh();
    }

    private void PollClipboard()
    {
        if (!_listening) return;
        try
        {
            if (!Clipboard.ContainsText()) return;
            var text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text) || text == _lastClipboard) return;
            _lastClipboard = text;
            if (text.Length > ClipboardHistoryService.MaxContentBytes) return;
            _history.Add(text);
            Refresh();
        }
        catch (Exception)
        {
            // 剪贴板被其他进程占用时跳过本轮
        }
    }

    private void Refresh()
    {
        var selectedId = (_list.SelectedItem as ClipboardEntry)?.Id;
        var entries = _history.Search(_search.Text);
        _list.ItemsSource = entries;
        if (selectedId is not null)
            _list.SelectedItem = entries.FirstOrDefault(e => e.Id == selectedId);
        _status.Text = entries.Count == 0
            ? "暂无记录。"
            : $"共 {entries.Count} 条记录，固定项排在前面。";
    }

    private void ShowDetail()
    {
        _detail.Text = _list.SelectedItem is ClipboardEntry entry
            ? $"时间：{entry.CreatedTime:yyyy-MM-dd HH:mm:ss}\n格式：{entry.Format}\n大小：{entry.ByteSize} 字节\n固定：{(entry.IsPinned ? "是" : "否")}\n\n{entry.Content}"
            : string.Empty;
    }

    private void CopySelected()
    {
        if (_list.SelectedItem is not ClipboardEntry entry) return;
        try
        {
            Clipboard.SetText(entry.Content);
            _lastClipboard = entry.Content;
            _status.Text = "已复制到剪贴板。";
        }
        catch (Exception ex)
        {
            _status.Text = "复制失败：" + ex.Message;
        }
    }

    private void TogglePin()
    {
        if (_list.SelectedItem is not ClipboardEntry entry) return;
        _history.TogglePin(entry.Id);
        Refresh();
    }

    private void RemoveSelected()
    {
        if (_list.SelectedItem is not ClipboardEntry entry) return;
        _history.Remove(entry.Id);
        _detail.Clear();
        Refresh();
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}