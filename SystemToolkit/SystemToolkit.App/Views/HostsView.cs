using System.Windows;
using System.Windows.Controls;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App.Views;

public class HostsView : UserControl
{
    private readonly IHostsFileService _hosts;
    private readonly TextBox _editor;
    private readonly TextBlock _status;

    public HostsView(IHostsFileService hosts)
    {
        _hosts = hosts;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        header.Children.Add(new TextBlock { Text = "Hosts 编辑器", FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) });
        header.Children.Add(new TextBlock { Text = _hosts.GetHostsPath(), VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var tools = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 10, 8) };
        var reload = new Button { Content = "重新读取", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        reload.Click += async (_, _) => await ReloadAsync();
        tools.Children.Add(reload);
        var backup = new Button { Content = "备份", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        backup.Click += async (_, _) => await BackupAsync();
        tools.Children.Add(backup);
        var validate = new Button { Content = "检查语法", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        validate.Click += (_, _) => Validate();
        tools.Children.Add(validate);
        var save = new Button { Content = "保存（需管理员）", Padding = new Thickness(12, 4, 12, 4) };
        save.Click += async (_, _) => await SaveAsync();
        tools.Children.Add(save);
        Grid.SetRow(tools, 1);
        root.Children.Add(tools);

        _editor = new TextBox
        {
            AcceptsReturn = true,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(10, 0, 10, 8)
        };
        Grid.SetRow(_editor, 2);
        root.Children.Add(_editor);

        _status = FileDedupView.CreateStatus();
        Grid.SetRow(_status, 3);
        root.Children.Add(_status);

        Content = root;
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            _editor.Text = await _hosts.ReadHostsAsync();
            _status.Text = $"已读取 {_hosts.GetHostsPath()}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "读取失败");
        }
    }

    private async Task BackupAsync()
    {
        try
        {
            var target = await _hosts.BackupAsync();
            _status.Text = $"已备份到 {target}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "备份失败");
        }
    }

    private void Validate()
    {
        var problems = _hosts.ValidateLines(_editor.Text);
        _status.Text = problems.Count == 0
            ? "语法检查通过"
            : $"发现 {problems.Count} 个问题：{string.Join("；", problems.Take(3))}";
    }

    private async Task SaveAsync()
    {
        var problems = _hosts.ValidateLines(_editor.Text);
        var question = problems.Count == 0
            ? "保存会立即修改系统 hosts 文件（影响本机 DNS 解析），确认继续？"
            : $"存在 {problems.Count} 个语法问题（{string.Join("；", problems.Take(3))}），仍要保存？";

        if (MessageBox.Show(question, "确认保存", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var backup = await _hosts.BackupAsync();
            await _hosts.WriteHostsAsync(_editor.Text);
            _status.Text = $"已保存，修改前备份位于 {backup}";
        }
        catch (Exception ex)
        {
            _status.Text = "保存失败";
            MessageBox.Show(ex.Message, "错误");
        }
    }
}
