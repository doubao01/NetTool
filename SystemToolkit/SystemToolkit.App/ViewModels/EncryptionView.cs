using System.Windows;
using System.Windows.Controls;

namespace SystemToolkit.App.ViewModels;

public class EncryptionView : UserControl
{
    private TabControl _tabControl;
    private TextBox _inputTextBox;
    private TextBox _outputTextBox;
    private ComboBox _algorithmComboBox;
    private PasswordBox _keyPasswordBox;
    private PasswordBox _ivPasswordBox;
    private Button _encryptButton;
    private Button _decryptButton;
    private Button _calculateHashButton;

    public EncryptionView()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Tab 控制
        _tabControl = new TabControl { Margin = new Thickness(10) };

        // 加解密 Tab
        var encryptTab = new TabItem { Header = "AES 加解密" };
        encryptTab.Content = CreateEncryptDecryptPanel();
        _tabControl.Items.Add(encryptTab);

        // Hash 计算 Tab
        var hashTab = new TabItem { Header = "Hash 计算" };
        hashTab.Content = CreateHashPanel();
        _tabControl.Items.Add(hashTab);

        // Base64 Tab
        var base64Tab = new TabItem { Header = "Base64 编解码" };
        base64Tab.Content = CreateBase64Panel();
        _tabControl.Items.Add(base64Tab);

        Grid.SetRow(_tabControl, 0);
        grid.Children.Add(_tabControl);

        Content = grid;
    }

    private Grid CreateEncryptDecryptPanel()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // 算法选择
        grid.Children.Add(new TextBlock { Text = "算法:", Margin = new Thickness(10) });
        _algorithmComboBox = new ComboBox 
        { 
            Margin = new Thickness(60, 10, 10, 10), 
            Width = 150,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left
        };
        _algorithmComboBox.Items.Add("AES-128");
        _algorithmComboBox.Items.Add("AES-256");
        _algorithmComboBox.SelectedIndex = 0;
        grid.Children.Add(_algorithmComboBox);

        // 密钥
        grid.Children.Add(new TextBlock { Text = "密钥:", Margin = new Thickness(10, 0, 10, 10) });
        Grid.SetRow(grid.Children[^1], 1);

        _keyPasswordBox = new PasswordBox { Margin = new Thickness(60, 0, 10, 10), Width = 300, HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
        Grid.SetRow(_keyPasswordBox, 1);
        Grid.SetColumn(_keyPasswordBox, 1);
        grid.Children.Add(_keyPasswordBox);

        // IV
        grid.Children.Add(new TextBlock { Text = "IV:", Margin = new Thickness(10, 0, 10, 10) });
        Grid.SetRow(grid.Children[^1], 2);

        _ivPasswordBox = new PasswordBox { Margin = new Thickness(60, 0, 10, 10), Width = 300, HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
        Grid.SetRow(_ivPasswordBox, 2);
        Grid.SetColumn(_ivPasswordBox, 1);
        grid.Children.Add(_ivPasswordBox);

        // 输入
        grid.Children.Add(new TextBlock { Text = "输入:", Margin = new Thickness(10) });
        Grid.SetRow(grid.Children[^1], 3);

        _inputTextBox = new TextBox { Margin = new Thickness(10, 5, 10, 10), Height = 100, TextWrapping = System.Windows.TextWrapping.Wrap, AcceptsReturn = true };
        Grid.SetRow(_inputTextBox, 4);
        grid.Children.Add(_inputTextBox);

        // 输出
        _outputTextBox = new TextBox { Margin = new Thickness(10, 5, 10, 10), Height = 100, TextWrapping = System.Windows.TextWrapping.Wrap, AcceptsReturn = true, IsReadOnly = true };
        grid.Children.Add(_outputTextBox);

        // 按钮
        var buttonPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(10) };

        _encryptButton = new Button { Content = "加密", Padding = new Thickness(30, 8, 30, 8), Margin = new Thickness(0, 0, 10, 0) };
        _encryptButton.Click += EncryptButton_Click;
        buttonPanel.Children.Add(_encryptButton);

        _decryptButton = new Button { Content = "解密", Padding = new Thickness(30, 8, 30, 8) };
        _decryptButton.Click += DecryptButton_Click;
        buttonPanel.Children.Add(_decryptButton);

        grid.Children.Add(buttonPanel);

        return grid;
    }

    private Grid CreateHashPanel()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var hashAlgorithmPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(10) };
        hashAlgorithmPanel.Children.Add(new TextBlock { Text = "Hash 算法:", VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        
        var algorithmsComboBox = new ComboBox { Width = 100, Margin = new Thickness(0, 0, 10, 0) };
        algorithmsComboBox.Items.Add("MD5");
        algorithmsComboBox.Items.Add("SHA1");
        algorithmsComboBox.Items.Add("SHA256");
        algorithmsComboBox.Items.Add("SHA384");
        algorithmsComboBox.Items.Add("SHA512");
        algorithmsComboBox.SelectedIndex = 2;
        hashAlgorithmPanel.Children.Add(algorithmsComboBox);

        _calculateHashButton = new Button { Content = "计算 Hash", Padding = new Thickness(20, 8, 20, 8) };
        _calculateHashButton.Click += CalculateHashButton_Click;
        hashAlgorithmPanel.Children.Add(_calculateHashButton);

        Grid.SetRow(hashAlgorithmPanel, 0);
        grid.Children.Add(hashAlgorithmPanel);

        var inputTextBlock = new TextBlock { Text = "输入文本:", Margin = new Thickness(10, 5, 10, 5) };
        Grid.SetRow(inputTextBlock, 1);
        grid.Children.Add(inputTextBlock);

        var inputText = new TextBox { Margin = new Thickness(10), Height = 100, TextWrapping = System.Windows.TextWrapping.Wrap, AcceptsReturn = true };
        Grid.SetRow(inputText, 2);
        grid.Children.Add(inputText);

        var outputTextBlock = new TextBlock { Text = "Hash 结果:", Margin = new Thickness(10, 5, 10, 5) };
        Grid.SetRow(outputTextBlock, 2);
        Grid.SetColumn(outputTextBlock, 1);
        grid.Children.Add(outputTextBlock);

        var outputText = new TextBox { Margin = new Thickness(10), Height = 100, IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas") };
        Grid.SetRow(outputText, 3);
        grid.Children.Add(outputText);

        return grid;
    }

    private Grid CreateBase64Panel()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 编码部分
        var encodeButton = new Button { Content = "Base64 编码", Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(10) };
        encodeButton.Click += EncodeButton_Click;
        Grid.SetRow(encodeButton, 0);
        grid.Children.Add(encodeButton);

        // 解码部分
        var decodeButton = new Button { Content = "Base64 解码", Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(10) };
        decodeButton.Click += DecodeButton_Click;
        Grid.SetRow(decodeButton, 1);
        grid.Children.Add(decodeButton);

        return grid;
    }

    private void EncryptButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            var service = new Core.Services.DataToolService();
            var result = service.EncryptAes(_inputTextBox.Text, GetKey(), GetIV());

            if (result.Success)
            {
                _outputTextBox.Text = result.EncryptedData;
            }
            else
            {
                MessageBox.Show($"加密失败：{result.Error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加密失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DecryptButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            var service = new Core.Services.DataToolService();
            var result = service.DecryptAes(_inputTextBox.Text, GetKey(), GetIV());

            if (result.Success)
            {
                _outputTextBox.Text = result.DecryptedData;
            }
            else
            {
                MessageBox.Show($"解密失败：{result.Error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"解密失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CalculateHashButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // Hash 计算实现
    }

    private void EncodeButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // Base64 编码实现
    }

    private void DecodeButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // Base64 解码实现
    }

    private string GetKey() => _keyPasswordBox.Password;
    private string GetIV() => _ivPasswordBox.Password;
}
