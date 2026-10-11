using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SystemToolkit.Core.Models;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App.Views;

public class DirectoryDiffView : UserControl
{
    private readonly IDirectoryDiffService _diffService;
    private readonly TextBox _leftBox;
    private readonly TextBox _rightBox;
    private readonly CheckBox _contentCheck;
    private readonly TabControl _tabs;
    private readonly TextBlock _summary;
    private readonly Button _runButton;
    private readonly Button _cancelButton;
    private CancellationTokenSource? _cts;

    public DirectoryDiffView(IDirectoryDiffService diffService)
    {
        _diffService = diffService;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock { Text = "目录对比", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(10) };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var leftPanel = BuildPathPanel("目录 A", out _leftBox);
        Grid.SetRow(leftPanel, 1);
        root.Children.Add(leftPanel);

        var rightPanel = BuildPathPanel("目录 B", out _rightBox);
        Grid.SetRow(rightPanel, 2);
        root.Children.Add(rightPanel);

        _tabs = new TabControl { Margin = new Thickness(10, 4, 10, 8) };
        _tabs.Items.Add(CreateTab("仅 A 有", out var tabA));
        _tabs.Items.Add(CreateTab("仅 B 有", out var tabB));
        _tabs.Items.Add(CreateTab("内容不同", out var tabC));
        _tabs.Items.Add(CreateTab("相同", out var tabD));
        _tabLists[0] = tabA;
        _tabLists[1] = tabB;
        _tabLists[2] = tabC;
        _tabLists[3] = tabD;
        Grid.SetRow(_tabs, 3);
        root.Children.Add(_tabs);

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        _contentCheck = new CheckBox { Content = "比较文件内容（哈希）", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        bottom.Children.Add(_contentCheck);
        _runButton = new Button { Content = "开始对比", Padding = new Thickness(16, 4, 16, 4), Margin = new Thickness(0, 0, 8, 0) };
        _runButton.Click += async (_, _) => await RunAsync();
        bottom.Children.Add(_runButton);
        _cancelButton = new Button { Content = "取消", IsEnabled = false, Padding = new Thickness(16, 4, 16, 4), Margin = new Thickness(0, 0, 16, 0) };
        _cancelButton.Click += (_, _) => _cts?.Cancel();
        bottom.Children.Add(_cancelButton);
        _summary = FileDedupView.CreateStatus();
        _summary.Margin = new Thickness(0);
        bottom.Children.Add(_summary);
        Grid.SetRow(bottom, 4);
        root.Children.Add(bottom);

        Content = root;
        Unloaded += (_, _) => _cts?.Cancel();
    }

    private readonly ListBox[] _tabLists = new ListBox[4];

    private static StackPanel BuildPathPanel(string label, out TextBox box)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 2, 10, 2) };
        panel.Children.Add(new TextBlock { Text = label, Width = 60, VerticalAlignment = VerticalAlignment.Center });
        var pathBox = new TextBox { Width = 380, Margin = new Thickness(0, 0, 8, 0) };
        box = pathBox;
        panel.Children.Add(pathBox);
        var browse = new Button { Content = "选择…", Padding = new Thickness(8, 2, 8, 2) };
        browse.Click += (_, _) =>
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true) pathBox.Text = dialog.FolderName;
        };
        panel.Children.Add(browse);
        return panel;
    }

    private static TabItem CreateTab(string header, out ListBox list)
    {
        list = new ListBox { Margin = new Thickness(0) };
        return new TabItem { Header = header, Content = list };
    }

    private async Task RunAsync()
    {
        if (_cts != null) return;

        var left = _leftBox.Text.Trim();
        var right = _rightBox.Text.Trim();
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            MessageBox.Show("请选择两个目录", "提示");
            return;
        }

        using var cts = new CancellationTokenSource();
        _cts = cts;
        _runButton.IsEnabled = false;
        _cancelButton.IsEnabled = true;
        foreach (var list in _tabLists) list.ItemsSource = null;
        _summary.Text = "对比中…";

        try
        {
            var compareContent = _contentCheck.IsChecked == true;
            var result = await _diffService.CompareAsync(left, right, compareContent, cts.Token);
            cts.Token.ThrowIfCancellationRequested();

            _tabLists[0].ItemsSource = result.OnlyInLeft;
            _tabLists[1].ItemsSource = result.OnlyInRight;
            _tabLists[2].ItemsSource = result.Different;
            _tabLists[3].ItemsSource = result.Same;

            _summary.Text = result.IsIdentical
                ? $"{(compareContent ? "两目录文件内容一致" : "两目录文件路径和大小一致")}，共 {result.TotalCompared} 个文件"
                : $"相同 {result.Same.Count}，不同 {result.Different.Count}，仅A {result.OnlyInLeft.Count}，仅B {result.OnlyInRight.Count}";
        }
        catch (OperationCanceledException)
        {
            _summary.Text = "已取消";
        }
        catch (Exception ex)
        {
            _summary.Text = "对比失败";
            MessageBox.Show(ex.Message, "错误");
        }
        finally
        {
            _cts = null;
            _runButton.IsEnabled = true;
            _cancelButton.IsEnabled = false;
        }
    }
}
