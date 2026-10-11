using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SystemToolkit.Core.Services;
using System.Windows.Media;

namespace SystemToolkit.App.Views;

public class ProcessMonitorView : UserControl
{
    private readonly DataGrid _dataGrid;
    private readonly IProcessService _processService;

    public ProcessMonitorView(IProcessService processService)
    {
        _processService = processService;
        _dataGrid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = true,
            Margin = new Thickness(10),
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 248, 248))
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // 标题和刷新按钮
        var headerPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(10) };
        headerPanel.Children.Add(new TextBlock 
        { 
            Text = "进程监控", 
            FontSize = 20, 
            FontWeight = FontWeights.Bold,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 20, 0)
        });

        var refreshButton = new Button 
        { 
            Content = "刷新", 
            Padding = new Thickness(15, 5, 15, 5),
            Margin = new Thickness(5)
        };
        refreshButton.Click += RefreshButton_Click;
        headerPanel.Children.Add(refreshButton);

        var priorityBox = new ComboBox { Width = 110, Margin = new Thickness(5), VerticalAlignment = System.Windows.VerticalAlignment.Center };
        foreach (var name in new[] { "Idle", "BelowNormal", "Normal", "AboveNormal", "High", "Realtime" })
        {
            priorityBox.Items.Add(name);
        }
        priorityBox.SelectedIndex = 2;
        headerPanel.Children.Add(priorityBox);

        var priorityButton = new Button
        {
            Content = "设置优先级",
            Padding = new Thickness(15, 5, 15, 5),
            Margin = new Thickness(5)
        };
        priorityButton.Click += (_, _) =>
        {
            if (_dataGrid.SelectedItem is not Core.Models.ProcessInfo process)
            {
                MessageBox.Show("请先选择进程", "提示");
                return;
            }

            var level = (System.Diagnostics.ProcessPriorityClass)Enum.Parse(
                typeof(System.Diagnostics.ProcessPriorityClass), (string)priorityBox.SelectedItem);

            try
            {
                _processService.SetProcessPriority(process.Id, level);
                RefreshProcessList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法设置优先级：{ex.Message}", "错误");
            }
        };
        headerPanel.Children.Add(priorityButton);

        Grid.SetRow(headerPanel, 0);
        grid.Children.Add(headerPanel);

        // 进程列表
        _dataGrid.Columns.Add(new DataGridTextColumn 
        { 
            Header = "进程名", 
            Binding = new Binding("ProcessName"),
            Width = new DataGridLength(150)
        });

        _dataGrid.Columns.Add(new DataGridTextColumn 
        { 
            Header = "PID", 
            Binding = new Binding("Id"),
            Width = new DataGridLength(80)
        });

        _dataGrid.Columns.Add(new DataGridTextColumn 
        { 
            Header = "内存 (MB)", 
            Binding = new Binding("WorkingSet64") { StringFormat = "F0", Converter = new BytesToMBConverter() },
            Width = new DataGridLength(100)
        });

        _dataGrid.Columns.Add(new DataGridTextColumn 
        { 
            Header = "CPU 时间", 
            Binding = new Binding("TotalProcessorTime") { StringFormat = "hh\\:mm\\:ss" },
            Width = new DataGridLength(100)
        });

        _dataGrid.Columns.Add(new DataGridTextColumn 
        { 
            Header = "线程数", 
            Binding = new Binding("ThreadCount"),
            Width = new DataGridLength(70)
        });

        _dataGrid.Columns.Add(new DataGridTextColumn 
        { 
            Header = "优先级", 
            Binding = new Binding("PriorityClass"),
            Width = new DataGridLength(100)
        });

        _dataGrid.Columns.Add(CreateKillColumn());

        Grid.SetRow(_dataGrid, 1);
        grid.Children.Add(_dataGrid);

        Content = grid;

        RefreshProcessList();
    }

    private DataGridTemplateColumn CreateKillColumn()
    {
        var factory = new FrameworkElementFactory(typeof(Button));
        factory.SetValue(Button.ContentProperty, "结束进程");
        factory.SetValue(Button.PaddingProperty, new Thickness(8, 4, 8, 4));
        factory.AddHandler(Button.ClickEvent, new RoutedEventHandler((_, e) =>
        {
            if (e.OriginalSource is Button { DataContext: Core.Models.ProcessInfo process })
            {
                if (MessageBox.Show($"结束进程 {process.ProcessName} ({process.Id})？", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                {
                    return;
                }

                try
                {
                    _processService.KillProcess(process.Id);
                    RefreshProcessList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法结束进程：{ex.Message}", "错误");
                }
            }
        }));

        return new DataGridTemplateColumn
        {
            Header = "操作",
            CellTemplate = new DataTemplate { VisualTree = factory },
            Width = new DataGridLength(100)
        };
    }

    private void RefreshButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        RefreshProcessList();
    }

    private void RefreshProcessList()
    {
        var processes = _processService.GetAllProcesses();
        _dataGrid.ItemsSource = processes.OrderByDescending(p => p.WorkingSet64);
    }
}

public class BytesToMBConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is long bytes)
        {
            return (bytes / 1024.0 / 1024.0).ToString("F0");
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}
