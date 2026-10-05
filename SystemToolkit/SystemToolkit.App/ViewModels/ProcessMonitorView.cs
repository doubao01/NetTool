using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SystemToolkit.Core.Services;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SystemToolkit.App.ViewModels;

public class ProcessMonitorView : UserControl
{
    private readonly DataGrid _dataGrid;
    private readonly ProcessService _processService;

    public ProcessMonitorView()
    {
        _processService = new ProcessService();

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

        Grid.SetRow(headerPanel, 0);
        grid.Children.Add(headerPanel);

        // 进程列表
        _dataGrid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = true,
            Margin = new Thickness(10),
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 248, 248))
        };

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

        _dataGrid.Columns.Add(CreateTemplateColumn("操作", OnKillProcess));

        Grid.SetRow(_dataGrid, 1);
        grid.Children.Add(_dataGrid);

        Content = grid;

        RefreshProcessList();
    }

    private DataGridTemplateColumn CreateTemplateColumn(string header, Action<object> action)
    {
        var button = new Button { Content = "结束进程", Padding = new Thickness(8, 4, 8, 4) };
        button.Click += (s, e) => 
        {
            if (button.DataContext is Core.Models.ProcessInfo process)
            {
                try
                {
                    _processService.KillProcess(process.Id);
                    RefreshProcessList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法结束进程：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        };

        var template = new System.Windows.DataTemplate();
        var frameworkElementFactory = new FrameworkElementFactory(typeof(Button));
        frameworkElementFactory.SetValue(Button.ContentProperty, button.Content);
        frameworkElementFactory.SetValue(Button.PaddingProperty, button.Padding);
        frameworkElementFactory.SetValue(Button.DataContextProperty, button.DataContext);
        template.VisualTree = frameworkElementFactory;

        return new DataGridTemplateColumn 
        { 
            Header = header, 
            CellTemplate = template,
            Width = new DataGridLength(100)
        };
    }

    private void OnKillProcess(object parameter) { }

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
