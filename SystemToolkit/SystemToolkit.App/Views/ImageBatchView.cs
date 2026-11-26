using System.IO;
using System.Windows;
using System.Windows.Controls;
using SystemToolkit.Core.Models;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App.Views;

public class ImageBatchView : UserControl
{
    private readonly IImageToolService _imageService;
    private readonly TextBox _maxEdgeBox;
    private readonly TextBox _scaleBox;
    private readonly ComboBox _formatBox;
    private readonly TextBox _qualityBox;
    private readonly TextBox _outputBox;
    private readonly ListBox _fileList;
    private readonly DataGrid _resultGrid;
    private readonly TextBlock _status;
    private List<string> _sourceFiles = new();

    public ImageBatchView(IImageToolService imageService)
    {
        _imageService = imageService;
        var root = new Grid { Margin = new Thickness(10) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new TextBlock
        {
            Text = "图片批量处理", FontSize = 18, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0)
        });
        var load = new Button { Content = "选择图片…", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        load.Click += (_, _) => LoadFiles();
        header.Children.Add(load);
        var loadDir = new Button { Content = "扫描目录…", Padding = new Thickness(12, 4, 12, 4) };
        loadDir.Click += (_, _) => ScanDirectory();
        header.Children.Add(loadDir);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var options = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        options.Children.Add(Label("最长边(px):"));
        _maxEdgeBox = new TextBox { Width = 70, Text = "800", Margin = new Thickness(0, 0, 12, 0) };
        options.Children.Add(_maxEdgeBox);
        options.Children.Add(Label("缩放(%):"));
        _scaleBox = new TextBox { Width = 60, Text = "", Margin = new Thickness(0, 0, 12, 0), ToolTip = "填写后优先按百分比缩放" };
        options.Children.Add(_scaleBox);
        options.Children.Add(Label("格式:"));
        _formatBox = new ComboBox { Width = 90, Margin = new Thickness(0, 0, 12, 0) };
        foreach (var format in new[] { "PNG", "JPEG", "BMP" }) _formatBox.Items.Add(format);
        _formatBox.SelectedIndex = 0;
        options.Children.Add(_formatBox);
        options.Children.Add(Label("JPEG 质量:"));
        _qualityBox = new TextBox { Width = 50, Text = "85", Margin = new Thickness(0, 0, 12, 0) };
        options.Children.Add(_qualityBox);
        options.Children.Add(Label("输出目录:"));
        _outputBox = new TextBox { Width = 220, Margin = new Thickness(0, 0, 8, 0) };
        options.Children.Add(_outputBox);
        var browseOut = new Button { Content = "选择…", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 12, 0) };
        browseOut.Click += (_, _) => ChooseOutputDirectory();
        options.Children.Add(browseOut);
        var run = new Button { Content = "开始处理", Padding = new Thickness(16, 4, 16, 4) };
        run.Click += async (_, _) => await RunAsync();
        options.Children.Add(run);
        Grid.SetRow(options, 1);
        root.Children.Add(options);

        _fileList = new ListBox { Margin = new Thickness(0, 0, 0, 6) };
        var fileBox = new GroupBox { Header = "待处理图片", Content = _fileList };
        Grid.SetRow(fileBox, 2);
        root.Children.Add(fileBox);

        _resultGrid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true };
        var resultBox = new GroupBox { Header = "处理结果", Content = _resultGrid };
        Grid.SetRow(resultBox, 3);
        root.Children.Add(resultBox);

        _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
        Grid.SetRow(_status, 4);
        root.Children.Add(_status);

        Content = root;
        if (!OperatingSystem.IsWindows())
        {
            _status.Text = "图片处理依赖 Windows GDI+，当前平台不可用。";
            run.IsEnabled = false;
        }
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0)
    };

    private void LoadFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff"
        };
        if (dialog.ShowDialog() != true) return;
        _sourceFiles = dialog.FileNames.ToList();
        RefreshList();
    }

    private void ScanDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "选择图片目录" };
        if (dialog.ShowDialog() != true) return;
        _sourceFiles = Directory.EnumerateFiles(dialog.FolderName, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => _imageService.SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .ToList();
        RefreshList();
    }

    private void RefreshList()
    {
        _fileList.ItemsSource = _sourceFiles.Select(path =>
            $"{Path.GetFileName(path)}  ({new FileInfo(path).Length / 1024.0:0.#} KB)").ToList();
        _status.Text = $"已选择 {_sourceFiles.Count} 个图片。";
    }

    private void ChooseOutputDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "选择输出目录" };
        if (dialog.ShowDialog() == true) _outputBox.Text = dialog.FolderName;
    }

    private async Task RunAsync()
    {
        if (_sourceFiles.Count == 0)
        {
            _status.Text = "请先选择图片。";
            return;
        }
        ImageResizeOptions options;
        try
        {
            options = BuildOptions();
        }
        catch (Exception ex)
        {
            _status.Text = "参数无效：" + ex.Message;
            return;
        }

        _status.Text = "正在处理…";
        try
        {
            var files = _sourceFiles.ToList();
            var summary = await Task.Run(() => _imageService.ResizeBatch(files, options));
            _resultGrid.ItemsSource = summary.Items.Select(item => new
            {
                文件 = Path.GetFileName(item.SourcePath),
                状态 = item.Success ? "成功" : "失败",
                原尺寸 = item.Success ? $"{item.OriginalWidth}×{item.OriginalHeight}" : "-",
                新尺寸 = item.Success ? $"{item.Width}×{item.Height}" : "-",
                原大小KB = item.OriginalBytes / 1024.0,
                新大小KB = item.Success ? Math.Round(item.OutputBytes / 1024.0, 1) : 0,
                说明 = item.Error ?? item.OutputPath
            }).ToList();
            _status.Text = $"完成：成功 {summary.Succeeded}，失败 {summary.Failed}。";
        }
        catch (Exception ex)
        {
            _status.Text = "处理失败：" + ex.Message;
        }
    }

    private ImageResizeOptions BuildOptions()
    {
        var options = new ImageResizeOptions
        {
            OutputFormat = _formatBox.SelectedItem?.ToString() ?? "PNG",
            OutputDirectory = string.IsNullOrWhiteSpace(_outputBox.Text) ? null : _outputBox.Text.Trim(),
            PreserveStructure = false
        };
        if (int.TryParse(_qualityBox.Text.Trim(), out var quality)) options.JpegQuality = quality;
        if (double.TryParse(_scaleBox.Text.Trim(), out var scale) && scale > 0)
            options.ScalePercent = scale;
        else if (int.TryParse(_maxEdgeBox.Text.Trim(), out var maxEdge))
            options.MaxEdge = maxEdge;

        if (options.MaxEdge is null && options.ScalePercent is null)
            throw new ArgumentException("请填写最长边像素或缩放百分比。");
        return options;
    }
}