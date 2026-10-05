using System.Windows;
using System.Windows.Controls;
using SystemToolkit.Core.Services;
using System.Windows.Data;
using System.Windows.Media;

namespace SystemToolkit.App.ViewModels;

public class SystemResourceMonitorView : UserControl, IDisposable
{
    private readonly TextBlock _cpuTextBlock;
    private readonly TextBlock _memoryTextBlock;
    private readonly ProgressBar _cpuProgressBar;
    private readonly ProgressBar _memoryProgressBar;
    private readonly IMonitorService _monitorService;
    private readonly CancellationTokenSource _cts;
    private bool _disposed;

    public SystemResourceMonitorView()
    {
        _monitorService = new MonitorService();
        _cts = new CancellationTokenSource();

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // CPU 部分
        _cpuTextBlock = new TextBlock { Text = "CPU 使用率：--%", FontSize = 16, Margin = new Thickness(10) };
        _cpuProgressBar = new ProgressBar 
        { 
            Value = 0, 
            Maximum = 100, 
            Height = 30, 
            Margin = new Thickness(10),
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240))
        };

        // 内存部分
        _memoryTextBlock = new TextBlock { Text = "内存使用率：--%", FontSize = 16, Margin = new Thickness(10) };
        _memoryProgressBar = new ProgressBar 
        { 
            Value = 0, 
            Maximum = 100, 
            Height = 30, 
            Margin = new Thickness(10),
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240))
        };

        grid.Children.Add(_cpuTextBlock);
        Grid.SetRow(_cpuTextBlock, 0);

        grid.Children.Add(_cpuProgressBar);
        Grid.SetRow(_cpuProgressBar, 1);

        grid.Children.Add(_memoryTextBlock);
        Grid.SetRow(_memoryTextBlock, 2);

        grid.Children.Add(_memoryProgressBar);
        Grid.SetRow(_memoryProgressBar, 3);

        Content = grid;

        _ = StartMonitoring();
    }

    private async Task StartMonitoring()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var info = _monitorService.GetSystemResourceInfo();

                Dispatcher.Invoke(() =>
                {
                    _cpuTextBlock.Text = $"CPU 使用率：{info.CpuUsage:F1}%";
                    _cpuProgressBar.Value = info.CpuUsage;

                    _memoryTextBlock.Text = $"内存使用率：{info.MemoryUsage:F1}%";
                    _memoryProgressBar.Value = info.MemoryUsage;
                });

                await Task.Delay(1000, _cts.Token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                await Task.Delay(5000);
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts.Cancel();
            _cts.Dispose();
            _disposed = true;
        }
    }
}
