using System.IO;
using System.Windows;
using System.Windows.Controls;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App.Views;

public class ConversionView : UserControl
{
    private readonly IConversionService _conversion;
    private TextBox _yamlTabInput = null!;
    private TextBox _yamlTabOutput = null!;
    private TextBox _jsonPathInput = null!;
    private TextBox _jsonPathSource = null!;
    private TextBox _jsonPathResult = null!;
    private TextBox _markdownInput = null!;
    private TextBox _htmlOutput = null!;
    private TextBox _qrInput = null!;
    private TextBox _qrPixels = null!;
    private System.Windows.Controls.Image _qrPreview = null!;
    private TextBox _imageBase64 = null!;
    private TextBox _base64ImagePath = null!;
    private TextBlock _status = null!;

    public ConversionView(IConversionService conversion)
    {
        _conversion = conversion;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        header.Children.Add(new TextBlock
        {
            Text = "格式转换", FontSize = 18, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var tabs = new TabControl { Margin = new Thickness(10, 0, 10, 0) };
        tabs.Items.Add(new TabItem { Header = "YAML / JSON", Content = CreateYamlPanel() });
        tabs.Items.Add(new TabItem { Header = "JSONPath", Content = CreateJsonPathPanel() });
        tabs.Items.Add(new TabItem { Header = "Markdown 预览", Content = CreateMarkdownPanel() });
        tabs.Items.Add(new TabItem { Header = "二维码", Content = CreateQrPanel() });
        tabs.Items.Add(new TabItem { Header = "图片 Base64", Content = CreateImageBase64Panel() });
        Grid.SetRow(tabs, 1);
        root.Children.Add(tabs);

        _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 6, 12, 10) };
        Grid.SetRow(_status, 2);
        root.Children.Add(_status);

        Content = root;
    }

    private static TextBox Editor(bool readOnly) => new()
    {
        AcceptsReturn = true,
        AcceptsTab = true,
        IsReadOnly = readOnly,
        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        Margin = new Thickness(4)
    };

    private Grid CreateYamlPanel()
    {
        var grid = CreateSplitGrid(3);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6) };
        var yamlToJson = new Button { Content = "YAML → JSON", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        yamlToJson.Click += (_, _) => Run(() => _yamlTabOutput.Text = _conversion.YamlToJson(_yamlTabInput.Text), "YAML 已转 JSON");
        var jsonToYaml = new Button { Content = "JSON → YAML", Padding = new Thickness(12, 4, 12, 4) };
        jsonToYaml.Click += (_, _) => Run(() => _yamlTabOutput.Text = _conversion.JsonToYaml(_yamlTabInput.Text), "JSON 已转 YAML");
        buttons.Children.Add(yamlToJson);
        buttons.Children.Add(jsonToYaml);
        Grid.SetRow(buttons, 0);
        grid.Children.Add(buttons);

        _yamlTabInput = Editor(false);
        var inputBox = new GroupBox { Header = "输入（YAML 或 JSON）", Content = _yamlTabInput };
        Grid.SetRow(inputBox, 1);
        grid.Children.Add(inputBox);

        _yamlTabOutput = Editor(false);
        var outputBox = new GroupBox { Header = "结果", Content = _yamlTabOutput };
        Grid.SetRow(outputBox, 2);
        grid.Children.Add(outputBox);
        return grid;
    }

    private Grid CreateJsonPathPanel()
    {
        var grid = CreateSplitGrid(4);
        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6) };
        top.Children.Add(new TextBlock { Text = "JSONPath:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        _jsonPathInput = new TextBox { Width = 320, Text = "$.store.book[*].title", Margin = new Thickness(0, 0, 8, 0) };
        top.Children.Add(_jsonPathInput);
        var run = new Button { Content = "查询", Padding = new Thickness(16, 4, 16, 4) };
        run.Click += (_, _) => Run(() => _jsonPathResult.Text = _conversion.QueryJsonPath(_jsonPathSource.Text, _jsonPathInput.Text), "查询完成");
        top.Children.Add(run);
        Grid.SetRow(top, 0);
        grid.Children.Add(top);

        _jsonPathSource = Editor(false);
        _jsonPathSource.Text = "{\n  \"store\": {\n    \"book\": [\n      { \"title\": \"深入理解计算机系统\", \"price\": 99 },\n      { \"title\": \"代码大全\", \"price\": 128 }\n    ]\n  }\n}";
        var sourceBox = new GroupBox { Header = "JSON 源", Content = _jsonPathSource };
        Grid.SetRow(sourceBox, 1);
        grid.Children.Add(sourceBox);

        var splitter = new GridSplitter { Height = 6, HorizontalAlignment = HorizontalAlignment.Stretch, ResizeDirection = GridResizeDirection.Rows };
        Grid.SetRow(splitter, 2);
        grid.Children.Add(splitter);

        _jsonPathResult = Editor(true);
        var resultBox = new GroupBox { Header = "匹配结果（JSON 数组）", Content = _jsonPathResult };
        Grid.SetRow(resultBox, 3);
        grid.Children.Add(resultBox);
        return grid;
    }

    private Grid CreateMarkdownPanel()
    {
        var grid = CreateSplitGrid(3);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6) };
        var convert = new Button { Content = "转换为 HTML", Padding = new Thickness(12, 4, 12, 4) };
        convert.Click += (_, _) => Run(() => _htmlOutput.Text = _conversion.MarkdownToHtml(_markdownInput.Text), "Markdown 已转 HTML");
        buttons.Children.Add(convert);
        Grid.SetRow(buttons, 0);
        grid.Children.Add(buttons);

        _markdownInput = Editor(false);
        _markdownInput.Text = "# 标题\n\n- 列表项一\n- 列表项二\n\n| 列 A | 列 B |\n|---|---|\n| 1 | 2 |";
        var inputBox = new GroupBox { Header = "Markdown", Content = _markdownInput };
        Grid.SetRow(inputBox, 1);
        grid.Children.Add(inputBox);

        _htmlOutput = Editor(true);
        var outputBox = new GroupBox { Header = "HTML 输出", Content = _htmlOutput };
        Grid.SetRow(outputBox, 2);
        grid.Children.Add(outputBox);
        return grid;
    }

    private Grid CreateQrPanel()
    {
        var grid = new Grid { Margin = new Thickness(6) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        bar.Children.Add(new TextBlock { Text = "内容:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        _qrInput = new TextBox { Width = 420, Text = "https://github.com/SamHub1991/NetTool", Margin = new Thickness(0, 0, 8, 0) };
        bar.Children.Add(_qrInput);
        bar.Children.Add(new TextBlock { Text = "像素倍率:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        _qrPixels = new TextBox { Width = 50, Text = "8", Margin = new Thickness(0, 0, 8, 0) };
        bar.Children.Add(_qrPixels);
        var generate = new Button { Content = "生成", Padding = new Thickness(16, 4, 16, 4), Margin = new Thickness(0, 0, 8, 0) };
        generate.Click += (_, _) => GenerateQr();
        bar.Children.Add(generate);
        var save = new Button { Content = "另存为…", Padding = new Thickness(16, 4, 16, 4) };
        save.Click += (_, _) => SaveQr();
        bar.Children.Add(save);
        Grid.SetRow(bar, 0);
        grid.Children.Add(bar);

        _qrPreview = new System.Windows.Controls.Image
        {
            Stretch = System.Windows.Media.Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var scroll = new ScrollViewer { Content = _qrPreview, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1);
        grid.Children.Add(scroll);
        return grid;
    }

    private byte[]? _qrBytes;

    private Grid CreateImageBase64Panel()
    {
        var grid = CreateSplitGrid(4);
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6) };
        var toBase64 = new Button { Content = "图片 → Base64", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        toBase64.Click += (_, _) => ImageToBase64();
        var toImage = new Button { Content = "Base64 → 图片", Padding = new Thickness(12, 4, 12, 4) };
        toImage.Click += (_, _) => Base64ToImage();
        bar.Children.Add(toBase64);
        bar.Children.Add(toImage);
        Grid.SetRow(bar, 0);
        grid.Children.Add(bar);

        _base64ImagePath = new TextBox { Margin = new Thickness(4), ToolTip = "图片文件路径" };
        var pathBox = new GroupBox { Header = "图片文件路径", Content = _base64ImagePath };
        Grid.SetRow(pathBox, 1);
        grid.Children.Add(pathBox);

        _imageBase64 = Editor(false);
        var base64Box = new GroupBox { Header = "Base64 数据（data URI 或纯 Base64）", Content = _imageBase64 };
        Grid.SetRow(base64Box, 2);
        grid.Children.Add(base64Box);

        var hint = new TextBlock
        {
            Text = "解码图片时需填写输出路径：在“图片文件路径”中填入 .png/.jpg 目标路径后点击“Base64 → 图片”。",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6), Foreground = System.Windows.Media.Brushes.DimGray
        };
        Grid.SetRow(hint, 3);
        grid.Children.Add(hint);
        return grid;
    }

    private void GenerateQr()
    {
        try
        {
            var pixels = int.TryParse(_qrPixels.Text.Trim(), out var value) ? value : 8;
            _qrBytes = _conversion.GenerateQrCodePng(_qrInput.Text, pixels);
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            using var stream = new MemoryStream(_qrBytes);
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            _qrPreview.Source = bitmap;
            _status.Text = $"二维码已生成，{_qrBytes.Length:N0} 字节。";
        }
        catch (Exception ex)
        {
            _status.Text = "生成失败：" + ex.Message;
        }
    }

    private void SaveQr()
    {
        if (_qrBytes is null)
        {
            _status.Text = "请先生成二维码。";
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "PNG 图片|*.png", FileName = "qrcode.png" };
        if (dialog.ShowDialog() != true) return;
        File.WriteAllBytes(dialog.FileName, _qrBytes);
        _status.Text = "已保存：" + dialog.FileName;
    }

    private void ImageToBase64()
    {
        Run(() => _imageBase64.Text = _conversion.ImageFileToBase64(_base64ImagePath.Text.Trim()), "图片已转为 Base64");
    }

    private void Base64ToImage()
    {
        try
        {
            var target = _base64ImagePath.Text.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                _status.Text = "请在“图片文件路径”中填写输出路径。";
                return;
            }
            var bytes = _conversion.Base64ToImageBytes(_imageBase64.Text);
            File.WriteAllBytes(target, bytes);
            _status.Text = $"已写入 {bytes.Length:N0} 字节到 {target}";
        }
        catch (Exception ex)
        {
            _status.Text = "解码失败：" + ex.Message;
        }
    }

    private void Run(Action action, string successMessage)
    {
        try
        {
            action();
            _status.Text = successMessage;
        }
        catch (Exception ex)
        {
            _status.Text = "执行失败：" + ex.Message;
        }
    }

    private static Grid CreateSplitGrid(int rows)
    {
        var grid = new Grid();
        for (var i = 0; i < rows; i++)
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = i == 0 ? GridLength.Auto : new GridLength(1, GridUnitType.Star)
            });
        return grid;
    }
}