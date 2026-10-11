using System.Windows;
using System.Windows.Controls;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App.Views;

public class EncryptionView : UserControl
{
    private readonly IDataToolService _dataTool;
    private readonly IDevToolService _devTool;
    private readonly IFileService _fileService;
    private TextBox _inputTextBox = null!;
    private TextBox _outputTextBox = null!;
    private PasswordBox _keyPasswordBox = null!;
    private PasswordBox _ivPasswordBox = null!;
    private TextBox _hashInput = null!;
    private ComboBox _hashAlgo = null!;
    private TextBox _hashOutput = null!;
    private TextBox _base64Input = null!;
    private TextBox _base64Output = null!;

    public EncryptionView(IDataToolService dataTool, IDevToolService devTool, IFileService fileService)
    {
        _dataTool = dataTool;
        _devTool = devTool;
        _fileService = fileService;

        var tabs = new TabControl { Margin = new Thickness(10) };
        tabs.Items.Add(new TabItem { Header = "AES 加解密", Content = CreateEncryptPanel(out _inputTextBox, out _outputTextBox, out _keyPasswordBox, out _ivPasswordBox) });
        tabs.Items.Add(new TabItem { Header = "Hash 计算", Content = CreateHashPanel(out _hashInput, out _hashAlgo, out _hashOutput) });
        tabs.Items.Add(new TabItem { Header = "Base64 编解码", Content = CreateBase64Panel(out _base64Input, out _base64Output) });
        Content = tabs;
    }

    private Grid CreateEncryptPanel(out TextBox input, out TextBox output, out PasswordBox key, out PasswordBox iv)
    {
        var grid = new Grid();
        for (int i = 0; i < 5; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        key = new PasswordBox { Margin = new Thickness(80, 4, 10, 4), Width = 280, HorizontalAlignment = HorizontalAlignment.Left };
        iv = new PasswordBox { Margin = new Thickness(80, 4, 10, 4), Width = 280, HorizontalAlignment = HorizontalAlignment.Left };
        input = new TextBox { Margin = new Thickness(10), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 100 };
        output = new TextBox { Margin = new Thickness(10), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, IsReadOnly = true };

        AddRowLabel(grid, 0, "密钥:");
        Grid.SetRow(key, 0);
        grid.Children.Add(key);
        AddRowLabel(grid, 1, "IV:");
        Grid.SetRow(iv, 1);
        grid.Children.Add(iv);

        var btns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        var enc = new Button { Content = "加密", Padding = new Thickness(20, 6, 20, 6), Margin = new Thickness(0, 0, 8, 0) };
        var dec = new Button { Content = "解密", Padding = new Thickness(20, 6, 20, 6) };
        enc.Click += EncryptButton_Click;
        dec.Click += DecryptButton_Click;
        btns.Children.Add(enc);
        btns.Children.Add(dec);
        Grid.SetRow(btns, 2);
        grid.Children.Add(btns);

        AddRowLabel(grid, 3, "输入:");
        Grid.SetRow(input, 4);
        grid.Children.Add(input);
        Grid.SetRow(output, 5);
        grid.Children.Add(output);
        return grid;
    }

    private Grid CreateHashPanel(out TextBox input, out ComboBox algo, out TextBox output)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        row.Children.Add(new TextBlock { Text = "算法:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        algo = new ComboBox { Width = 120, Margin = new Thickness(0, 0, 8, 0) };
        foreach (var a in new[] { "MD5", "SHA1", "SHA256", "SHA384", "SHA512" })
        {
            algo.Items.Add(a);
        }
        algo.SelectedIndex = 2;
        row.Children.Add(algo);
        var calc = new Button { Content = "计算 Hash", Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(0, 0, 8, 0) };
        calc.Click += CalculateHashButton_Click;
        row.Children.Add(calc);
        var fileCalc = new Button { Content = "计算文件 Hash…", Padding = new Thickness(16, 6, 16, 6) };
        fileCalc.Click += CalculateFileHash_Click;
        row.Children.Add(fileCalc);
        Grid.SetRow(row, 0);
        grid.Children.Add(row);

        input = new TextBox { Margin = new Thickness(10), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(input, 1);
        grid.Children.Add(input);
        output = new TextBox { Margin = new Thickness(10), IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas") };
        Grid.SetRow(output, 2);
        grid.Children.Add(output);
        return grid;
    }

    private Grid CreateBase64Panel(out TextBox input, out TextBox output)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var btns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        var enc = new Button { Content = "编码", Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(0, 0, 8, 0) };
        var dec = new Button { Content = "解码", Padding = new Thickness(16, 6, 16, 6) };
        enc.Click += EncodeButton_Click;
        dec.Click += DecodeButton_Click;
        btns.Children.Add(enc);
        btns.Children.Add(dec);
        Grid.SetRow(btns, 0);
        grid.Children.Add(btns);

        input = new TextBox { Margin = new Thickness(10), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(input, 1);
        grid.Children.Add(input);
        output = new TextBox { Margin = new Thickness(10), IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas") };
        Grid.SetRow(output, 2);
        grid.Children.Add(output);
        return grid;
    }

    private static void AddRowLabel(Grid grid, int row, string text)
    {
        var tb = new TextBlock { Text = text, Margin = new Thickness(10, 8, 10, 4), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(tb, row);
        grid.Children.Add(tb);
    }

    private void EncryptButton_Click(object sender, RoutedEventArgs e)
    {
        var result = _dataTool.EncryptAes(_inputTextBox.Text, _keyPasswordBox.Password, _ivPasswordBox.Password);
        _outputTextBox.Text = result.Success ? result.EncryptedData : result.Error;
    }

    private void DecryptButton_Click(object sender, RoutedEventArgs e)
    {
        var result = _dataTool.DecryptAes(_inputTextBox.Text, _keyPasswordBox.Password, _ivPasswordBox.Password);
        _outputTextBox.Text = result.Success ? result.DecryptedData : result.Error;
    }

    private void CalculateHashButton_Click(object sender, RoutedEventArgs e)
    {
        var algo = _hashAlgo.SelectedItem?.ToString() ?? "SHA256";
        var result = _dataTool.CalculateHash(_hashInput.Text, algo);
        _hashOutput.Text = result.Hash;
    }

    private async void CalculateFileHash_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "选择要计算 Hash 的文件" };
        if (dialog.ShowDialog() != true) return;

        var algo = _hashAlgo.SelectedItem?.ToString() ?? "SHA256";
        _hashOutput.Text = "计算中…";
        try
        {
            var hash = await _fileService.CalculateFileHashAsync(dialog.FileName, algo);
            _hashOutput.Text = $"{algo}  {System.IO.Path.GetFileName(dialog.FileName)}\n{hash}";
        }
        catch (Exception ex)
        {
            _hashOutput.Text = "计算失败：" + ex.Message;
        }
    }

    private void EncodeButton_Click(object sender, RoutedEventArgs e)
    {
        _base64Output.Text = _devTool.EncodeBase64(_base64Input.Text);
    }

    private void DecodeButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _base64Output.Text = _devTool.DecodeBase64(_base64Input.Text);
        }
        catch (Exception ex)
        {
            _base64Output.Text = ex.Message;
        }
    }
}
