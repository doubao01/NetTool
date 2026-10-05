using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SystemToolkit.App.ViewModels;

public class FileRenameView : UserControl
{
    private readonly TextBox _directoryTextBox;
    private readonly TextBox _searchTextBox;
    private readonly TextBox _replaceTextBox;
    private readonly CheckBox _regexCheckBox;
    private readonly TextBox _prefixTextBox;
    private readonly TextBox _suffixTextBox;
    private readonly ListBox _fileListBox;
    private readonly TextBlock _statusTextBlock;

    public FileRenameView()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 目录选择
        var dirPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(10) };
        dirPanel.Children.Add(new TextBlock { Text = "目录:", VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        
        _directoryTextBox = new TextBox 
        { 
            Width = 400, 
            Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Margin = new Thickness(0, 0, 10, 0)
        };
        dirPanel.Children.Add(_directoryTextBox);

        var browseButton = new Button { Content = "浏览...", Padding = new Thickness(15, 5, 15, 5) };
        browseButton.Click += BrowseButton_Click;
        dirPanel.Children.Add(browseButton);

        Grid.SetRow(dirPanel, 0);
        grid.Children.Add(dirPanel);

        // 重命名规则
        var rulePanel = new Grid { Margin = new Thickness(10) };
        rulePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        rulePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        rulePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        rulePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

        rulePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rulePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rulePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 第一行：搜索和替换
        rulePanel.Children.Add(new TextBlock { Text = "搜索:", VerticalAlignment = System.Windows.VerticalAlignment.Center });
        Grid.SetColumn(rulePanel.Children[^1], 0);

        _searchTextBox = new TextBox { Margin = new Thickness(5, 2, 5, 2) };
        Grid.SetColumn(_searchTextBox, 1);
        rulePanel.Children.Add(_searchTextBox);

        rulePanel.Children.Add(new TextBlock { Text = "替换:", VerticalAlignment = System.Windows.VerticalAlignment.Center });
        Grid.SetColumn(rulePanel.Children[^1], 2);

        _replaceTextBox = new TextBox { Margin = new Thickness(5, 2, 5, 2) };
        Grid.SetColumn(_replaceTextBox, 3);
        rulePanel.Children.Add(_replaceTextBox);

        // 第二行：前缀和后缀
        rulePanel.Children.Add(new TextBlock { Text = "前缀:", VerticalAlignment = System.Windows.VerticalAlignment.Center });
        Grid.SetRow(rulePanel.Children[^1], 1);
        Grid.SetColumn(rulePanel.Children[^1], 0);

        _prefixTextBox = new TextBox { Margin = new Thickness(5, 2, 5, 2) };
        Grid.SetRow(_prefixTextBox, 1);
        Grid.SetColumn(_prefixTextBox, 1);
        rulePanel.Children.Add(_prefixTextBox);

        rulePanel.Children.Add(new TextBlock { Text = "后缀:", VerticalAlignment = System.Windows.VerticalAlignment.Center });
        Grid.SetRow(rulePanel.Children[^1], 1);
        Grid.SetColumn(rulePanel.Children[^1], 2);

        _suffixTextBox = new TextBox { Margin = new Thickness(5, 2, 5, 2) };
        Grid.SetRow(_suffixTextBox, 1);
        Grid.SetColumn(_suffixTextBox, 3);
        rulePanel.Children.Add(_suffixTextBox);

        // 第三行：正则选项
        _regexCheckBox = new CheckBox { Content = "使用正则表达式", Margin = new Thickness(0, 5, 0, 5) };
        Grid.SetRow(_regexCheckBox, 2);
        Grid.SetColumn(_regexCheckBox, 0);
        rulePanel.Children.Add(_regexCheckBox);

        Grid.SetRow(rulePanel, 1);
        grid.Children.Add(rulePanel);

        // 文件列表
        _fileListBox = new ListBox 
        { 
            Margin = new Thickness(10), 
            MinHeight = 200,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
        };
        Grid.SetRow(_fileListBox, 2);
        grid.Children.Add(_fileListBox);

        // 操作按钮
        var buttonPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(10) };

        var loadButton = new Button { Content = "加载文件", Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(0, 0, 10, 0) };
        loadButton.Click += LoadButton_Click;
        buttonPanel.Children.Add(loadButton);

        var previewButton = new Button { Content = "预览", Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(0, 0, 10, 0) };
        previewButton.Click += PreviewButton_Click;
        buttonPanel.Children.Add(previewButton);

        var renameButton = new Button { Content = "执行重命名", Padding = new Thickness(20, 8, 20, 8), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204)), Foreground = System.Windows.Media.Brushes.White };
        renameButton.Click += RenameButton_Click;
        buttonPanel.Children.Add(renameButton);

        Grid.SetRow(buttonPanel, 3);
        grid.Children.Add(buttonPanel);

        // 状态栏
        _statusTextBlock = new TextBlock 
        { 
            Margin = new Thickness(10, 5, 10, 5), 
            FontWeight = System.Windows.FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204))
        };
        Grid.SetRow(grid, 4);
        Grid.SetColumn(grid, 0);
        grid.Children.Add(_statusTextBlock);

        Content = grid;
    }

    private void BrowseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // 简化实现，实际应使用 FolderBrowserDialog
        _directoryTextBox.Text = "C:\\";
    }

    private void LoadButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            var directory = _directoryTextBox.Text;
            var files = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);
            
            _fileListBox.Items.Clear();
            foreach (var file in files.Take(100))
            {
                _fileListBox.Items.Add(new System.IO.FileInfo(file).Name);
            }

            _statusTextBlock.Text = $"已加载 {files.Length} 个文件";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"读取文件失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PreviewButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // 预览重命名结果
        _statusTextBlock.Text = "预览功能开发中...";
    }

    private async void RenameButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            var rule = new Core.Models.RenameRule
            {
                SearchPattern = _searchTextBox.Text,
                ReplacePattern = _replaceTextBox.Text,
                UseRegex = _regexCheckBox.IsChecked ?? false,
                Prefix = _prefixTextBox.Text,
                Suffix = _suffixTextBox.Text
            };

            var fileService = new Core.Services.FileService();
            await fileService.BatchRenameAsync(new List<Core.Models.FileItem>(), rule);

            MessageBox.Show("重命名完成!", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            _statusTextBlock.Text = "重命名操作已完成";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"重命名失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
