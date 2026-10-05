using System.Windows;
using System.Windows.Controls;

namespace SystemToolkit.App.ViewModels;

public class PingToolView : UserControl
{
    private readonly TextBox _hostTextBox;
    private readonly Button _pingButton;
    private readonly TextBlock _resultTextBlock;

    public PingToolView()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // 输入区域
        var inputPanel = new StackPanel { Margin = new Thickness(10) };

        var hostLabel = new TextBlock { Text = "目标主机:", Margin = new Thickness(0, 0, 0, 5) };
        inputPanel.Children.Add(hostLabel);

        _hostTextBox = new TextBox 
        { 
            Text = "www.google.com", 
            Height = 30, 
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 10)
        };
        inputPanel.Children.Add(_hostTextBox);

        _pingButton = new Button 
        { 
            Content = "开始 Ping", 
            Height = 35, 
            FontSize = 14,
            Padding = new Thickness(20, 5, 20, 5),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left
        };
        _pingButton.Click += PingButton_Click;
        inputPanel.Children.Add(_pingButton);

        Grid.SetRow(inputPanel, 0);
        grid.Children.Add(inputPanel);

        // 结果标题
        var resultLabel = new TextBlock 
        { 
            Text = "结果:", 
            FontSize = 16, 
            FontWeight = System.Windows.FontWeights.Bold,
            Margin = new Thickness(10, 5, 10, 5)
        };
        Grid.SetRow(resultLabel, 1);
        grid.Children.Add(resultLabel);

        // 结果文本
        _resultTextBlock = new TextBlock 
        { 
            Margin = new Thickness(10), 
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            TextWrapping = System.Windows.TextWrapping.Wrap,
            VerticalAlignment = System.Windows.VerticalAlignment.Top
        };
        Grid.SetRow(_resultTextBlock, 2);
        grid.Children.Add(_resultTextBlock);

        Content = grid;
    }

    private async void PingButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var host = _hostTextBox.Text.Trim();
        if (string.IsNullOrEmpty(host))
        {
            MessageBox.Show("请输入目标主机地址", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _pingButton.IsEnabled = false;
        _resultTextBlock.Text = $"正在 Ping {host}...\n";

        try
        {
            var networkService = new Core.Services.NetworkService();
            var result = await networkService.PingAsync(host, 5000);

            _resultTextBlock.Text += $"主机：{result.Host}\n";
            _resultTextBlock.Text += $"状态：{(result.Success ? "成功 ✓" : "失败 ✗")}\n";
            _resultTextBlock.Text += $"往返时间：{result.RoundtripTime}ms\n";
            _resultTextBlock.Text += $"TTL: {result.TimeToLive}\n";

            if (!string.IsNullOrEmpty(result.Error))
            {
                _resultTextBlock.Text += $"错误：{result.Error}\n";
            }
        }
        catch (Exception ex)
        {
            _resultTextBlock.Text += $"错误：{ex.Message}\n";
        }
        finally
        {
            _pingButton.IsEnabled = true;
        }
    }
}
