using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SystemToolkit.Core.Models;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App.Views;

public sealed class CommandPaletteWindow : Window
{
    private readonly ICommandPaletteService _palette;
    private readonly TextBox _query;
    private readonly ListBox _results;
    private readonly TextBlock _hint;

    public string? SelectedTitle { get; private set; }

    public CommandPaletteWindow(ICommandPaletteService palette)
    {
        _palette = palette;
        Title = "快速跳转";
        Width = 520;
        Height = 380;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.ToolWindow;

        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _query = new TextBox { Margin = new Thickness(0, 0, 0, 8), FontSize = 14, ToolTip = "输入功能名称、分类或关键词" };
        _query.TextChanged += (_, _) => UpdateResults();
        _query.PreviewKeyDown += Query_PreviewKeyDown;
        Grid.SetRow(_query, 0);
        grid.Children.Add(_query);

        _results = new ListBox { DisplayMemberPath = nameof(CommandPaletteEntry.Title) };
        _results.MouseDoubleClick += (_, _) => Accept();
        Grid.SetRow(_results, 1);
        grid.Children.Add(_results);

        _hint = new TextBlock
        {
            Text = "Enter 跳转 · Esc 取消 · ↑↓ 选择",
            Margin = new Thickness(0, 8, 0, 0), Foreground = System.Windows.Media.Brushes.DimGray
        };
        Grid.SetRow(_hint, 2);
        grid.Children.Add(_hint);

        Content = grid;
        Loaded += (_, _) => _query.Focus();
        PreviewKeyDown += Window_PreviewKeyDown;
        UpdateResults();
    }

    private void UpdateResults()
    {
        var matches = _palette.Search(_query.Text, 50);
        _results.ItemsSource = matches;
        if (matches.Count > 0) _results.SelectedIndex = 0;
    }

    private void Query_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && _results.Items.Count > 0)
        {
            _results.SelectedIndex = Math.Min(_results.SelectedIndex + 1, _results.Items.Count - 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Up && _results.Items.Count > 0)
        {
            _results.SelectedIndex = Math.Max(_results.SelectedIndex - 1, 0);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            Accept();
            e.Handled = true;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void Accept()
    {
        if (_results.SelectedItem is not CommandPaletteEntry entry) return;
        SelectedTitle = entry.Title;
        DialogResult = true;
        Close();
    }
}