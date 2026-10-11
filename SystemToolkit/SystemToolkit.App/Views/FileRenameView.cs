using System.Windows;
using System.Windows.Controls;
using SystemToolkit.Core.Models;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App.Views;

public class FileRenameView : UserControl
{
    private readonly IFileService _fileService;
    private readonly TextBox _directoryTextBox;
    private readonly TextBox _searchTextBox;
    private readonly TextBox _replaceTextBox;
    private readonly CheckBox _regexCheckBox;
    private readonly TextBox _prefixTextBox;
    private readonly TextBox _suffixTextBox;
    private readonly ListBox _fileListBox;
    private readonly TextBlock _statusTextBlock;
    private List<FileItem> _files = new();

    public FileRenameView(IFileService fileService)
    {
        _fileService = fileService;
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var dirPanel = FileDedupView.CreateDirPanel(out _directoryTextBox);
        Grid.SetRow(dirPanel, 0);
        grid.Children.Add(dirPanel);

        var rulePanel = new Grid { Margin = new Thickness(10) };
        rulePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        rulePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        rulePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        rulePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        rulePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rulePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rulePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddLabeled(rulePanel, "搜索:", 0, 0, _searchTextBox = new TextBox { Margin = new Thickness(5, 2, 5, 2) }, 1);
        AddLabeled(rulePanel, "替换:", 0, 2, _replaceTextBox = new TextBox { Margin = new Thickness(5, 2, 5, 2) }, 3);
        AddLabeled(rulePanel, "前缀:", 1, 0, _prefixTextBox = new TextBox { Margin = new Thickness(5, 2, 5, 2) }, 1);
        AddLabeled(rulePanel, "后缀:", 1, 2, _suffixTextBox = new TextBox { Margin = new Thickness(5, 2, 5, 2) }, 3);

        _regexCheckBox = new CheckBox { Content = "使用正则表达式", Margin = new Thickness(0, 5, 0, 5) };
        Grid.SetRow(_regexCheckBox, 2);
        rulePanel.Children.Add(_regexCheckBox);
        Grid.SetRow(rulePanel, 1);
        grid.Children.Add(rulePanel);

        _fileListBox = new ListBox { Margin = new Thickness(10) };
        Grid.SetRow(_fileListBox, 2);
        grid.Children.Add(_fileListBox);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        var loadButton = new Button { Content = "加载文件", Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(0, 0, 8, 0) };
        loadButton.Click += LoadButton_Click;
        var previewButton = new Button { Content = "预览", Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(0, 0, 8, 0) };
        previewButton.Click += PreviewButton_Click;
        var renameButton = new Button { Content = "执行重命名", Padding = new Thickness(16, 6, 16, 6) };
        renameButton.Click += RenameButton_Click;
        buttonPanel.Children.Add(loadButton);
        buttonPanel.Children.Add(previewButton);
        buttonPanel.Children.Add(renameButton);
        Grid.SetRow(buttonPanel, 3);
        grid.Children.Add(buttonPanel);

        _statusTextBlock = FileDedupView.CreateStatus();
        Grid.SetRow(_statusTextBlock, 4);
        grid.Children.Add(_statusTextBlock);
        Content = grid;
    }

    private static void AddLabeled(Grid panel, string label, int row, int col, UIElement input, int inputCol)
    {
        var tb = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(tb, row);
        Grid.SetColumn(tb, col);
        panel.Children.Add(tb);
        Grid.SetRow(input, row);
        Grid.SetColumn(input, inputCol);
        panel.Children.Add(input);
    }

    private RenameRule CurrentRule() => new()
    {
        SearchPattern = _searchTextBox.Text,
        ReplacePattern = _replaceTextBox.Text,
        UseRegex = _regexCheckBox.IsChecked == true,
        Prefix = _prefixTextBox.Text,
        Suffix = _suffixTextBox.Text
    };

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _files = await _fileService.GetFilesAsync(_directoryTextBox.Text);
            _fileListBox.Items.Clear();
            foreach (var file in _files.Where(f => !f.IsDirectory).Take(200))
            {
                _fileListBox.Items.Add(file.Name);
            }
            _statusTextBlock.Text = $"已加载 {_files.Count} 个文件";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"读取文件失败：{ex.Message}", "错误");
        }
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_files.Count == 0)
        {
            _statusTextBlock.Text = "请先加载文件";
            return;
        }

        try
        {
            var preview = _fileService.PreviewRename(_files.Where(f => !f.IsDirectory).ToList(), CurrentRule());
            _fileListBox.Items.Clear();
            foreach (var (original, newName) in preview.Take(200))
            {
                _fileListBox.Items.Add(original == newName ? original : $"{original}  ->  {newName}");
            }
            _statusTextBlock.Text = $"预览 {preview.Count(p => p.Original != p.NewName)} 个将变更的文件";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"预览失败：{ex.Message}", "错误");
        }
    }

    private async void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (_files.Count == 0)
        {
            MessageBox.Show("请先加载文件", "提示");
            return;
        }

        if (MessageBox.Show("确认按当前规则重命名？", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var targets = _files.Where(f => !f.IsDirectory).ToList();
            await _fileService.BatchRenameAsync(targets, CurrentRule());
            _statusTextBlock.Text = "重命名完成";
            await Task.Yield();
            LoadButton_Click(sender, e);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"重命名失败：{ex.Message}", "错误");
        }
    }
}
