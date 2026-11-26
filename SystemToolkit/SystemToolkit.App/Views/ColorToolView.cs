using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SystemToolkit.Core.Models;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App.Views;

public class ColorToolView : UserControl
{
    private readonly IColorService _colorService;
    private readonly TextBox _input;
    private readonly TextBlock _status;
    private readonly Grid _conversionGrid;
    private readonly WrapPanel _palettePanel;
    private readonly Border _previewSwatch;
    private readonly TextBox _contrastForeground;
    private readonly TextBox _contrastBackground;
    private readonly TextBlock _contrastResult;
    private readonly StackPanel _pickerPanel;
    private ColorInfo _current = null!;

    public ColorToolView(IColorService colorService)
    {
        _colorService = colorService;
        var root = new Grid { Margin = new Thickness(10) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new TextBlock
        {
            Text = "颜色工具", FontSize = 20, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0)
        });
        _input = new TextBox { Width = 260, Margin = new Thickness(0, 0, 8, 0), ToolTip = "HEX、rgb()、hsl()、hsv()、cmyk() 或颜色名" };
        _input.KeyDown += (_, e) => { if (e.Key == Key.Enter) ConvertInput(); };
        header.Children.Add(_input);
        var convert = new Button { Content = "转换", Padding = new Thickness(16, 4, 16, 4), Margin = new Thickness(0, 0, 8, 0) };
        convert.Click += (_, _) => ConvertInput();
        header.Children.Add(convert);
        var example = new Button { Content = "示例", Padding = new Thickness(12, 4, 12, 4) };
        example.Click += (_, _) => { _input.Text = "#4A90D9"; ConvertInput(); };
        header.Children.Add(example);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var body = new StackPanel();

        var previewRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        _previewSwatch = new Border
        {
            Width = 96, Height = 64, CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray
        };
        previewRow.Children.Add(_previewSwatch);
        _conversionGrid = new Grid { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        foreach (var _ in new[] { 0, 1, 2, 3, 4 })
            _conversionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _conversionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        _conversionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        previewRow.Children.Add(_conversionGrid);
        body.Children.Add(previewRow);

        body.Children.Add(SectionTitle("调色板"));
        _palettePanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        body.Children.Add(_palettePanel);

        body.Children.Add(SectionTitle("WCAG 对比度"));
        var contrastRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        contrastRow.Children.Add(new TextBlock { Text = "前景:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        _contrastForeground = new TextBox { Width = 120, Text = "#FFFFFF", Margin = new Thickness(0, 0, 12, 0) };
        contrastRow.Children.Add(_contrastForeground);
        contrastRow.Children.Add(new TextBlock { Text = "背景:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        _contrastBackground = new TextBox { Width = 120, Text = "#000000", Margin = new Thickness(0, 0, 12, 0) };
        contrastRow.Children.Add(_contrastBackground);
        var contrastBtn = new Button { Content = "计算", Padding = new Thickness(16, 4, 16, 4) };
        contrastBtn.Click += (_, _) => ComputeContrast();
        contrastRow.Children.Add(contrastBtn);
        body.Children.Add(contrastRow);
        _contrastResult = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
        body.Children.Add(_contrastResult);

        _pickerPanel = new StackPanel { Orientation = Orientation.Horizontal };
        _pickerPanel.Children.Add(SectionTitle("屏幕取色"));
        var pick = new Button { Content = "拾取屏幕颜色", Padding = new Thickness(16, 4, 16, 4), Margin = new Thickness(0, 0, 10, 0) };
        pick.Click += (_, _) => PickScreenColor();
        _pickerPanel.Children.Add(pick);
        if (!OperatingSystem.IsWindows())
        {
            pick.IsEnabled = false;
            _pickerPanel.Children.Add(new TextBlock
            {
                Text = "（仅 Windows 可用）", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray
            });
        }
        body.Children.Add(_pickerPanel);

        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 2);
        root.Children.Add(scroll);

        _status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(_status, 3);
        root.Children.Add(_status);

        Content = root;
        _input.Text = "#4A90D9";
        ConvertInput();
    }

    private static TextBlock SectionTitle(string text) => new()
    {
        Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 6)
    };

    private void ConvertInput()
    {
        try
        {
            _current = _colorService.Parse(_input.Text);
            _previewSwatch.Background = new SolidColorBrush(Color.FromRgb(
                (byte)_current.Red, (byte)_current.Green, (byte)_current.Blue));
            RenderConversions();
            RenderPalette();
            _contrastBackground.Text = _current.Hex;
            ComputeContrast();
            _status.Text = $"已识别：{_current.Hex}";
        }
        catch (Exception ex)
        {
            _status.Text = "转换失败：" + ex.Message;
        }
    }

    private void RenderConversions()
    {
        _conversionGrid.Children.Clear();
        var rows = new (string Label, string Value)[]
        {
            ("HEX", _colorService.ToHex(_current)),
            ("RGB", _colorService.ToRgbString(_current)),
            ("HSL", _colorService.ToHslString(_current)),
            ("HSV", _colorService.ToHsvString(_current)),
            ("CMYK", _colorService.ToCmykString(_current))
        };
        for (var i = 0; i < rows.Length; i++)
        {
            var label = new TextBlock
            {
                Text = rows[i].Label, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(label, i);
            Grid.SetColumn(label, 0);
            _conversionGrid.Children.Add(label);

            var value = new TextBox
            {
                Text = rows[i].Value, IsReadOnly = true, FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 1, 0, 1), BorderThickness = new Thickness(0), Background = Brushes.Transparent
            };
            Grid.SetRow(value, i);
            Grid.SetColumn(value, 1);
            _conversionGrid.Children.Add(value);
        }
    }

    private void RenderPalette()
    {
        _palettePanel.Children.Clear();
        var palette = _colorService.BuildPalette(_current);
        AddColorChips("互补", palette.Complementary);
        AddColorChips("三角", palette.Triadic);
        AddColorChips("类似", palette.Analogous);
        AddColorChips("明度阶梯", palette.Shades);
    }

    private void AddColorChips(string group, IReadOnlyList<ColorInfo> colors)
    {
        var groupPanel = new StackPanel { Margin = new Thickness(0, 0, 18, 8) };
        groupPanel.Children.Add(new TextBlock { Text = group, FontSize = 12, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 4) });
        var chips = new WrapPanel();
        foreach (var color in colors)
        {
            var chip = new Border
            {
                Width = 54, Height = 40, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 6, 6),
                Background = new SolidColorBrush(Color.FromRgb((byte)color.Red, (byte)color.Green, (byte)color.Blue)),
                ToolTip = color.Hex, Cursor = Cursors.Hand, BorderThickness = new Thickness(1), BorderBrush = Brushes.LightGray
            };
            var captured = color.Hex;
            chip.MouseLeftButtonUp += (_, _) => { _input.Text = captured; ConvertInput(); };
            chips.Children.Add(chip);
        }
        groupPanel.Children.Add(chips);
        _palettePanel.Children.Add(groupPanel);
    }

    private void ComputeContrast()
    {
        try
        {
            var foreground = _colorService.Parse(_contrastForeground.Text);
            var background = _colorService.Parse(_contrastBackground.Text);
            var result = _colorService.GetContrast(foreground, background);
            string Verdict(bool value) => value ? "通过" : "不通过";
            _contrastResult.Text = string.Join("   ", new[]
            {
                $"对比度 {result.Ratio:0.00}:1",
                $"普通文本 AA {Verdict(result.NormalTextAa)}",
                $"普通文本 AAA {Verdict(result.NormalTextAaa)}",
                $"大字 AA {Verdict(result.LargeTextAa)}",
                $"大字 AAA {Verdict(result.LargeTextAaa)}",
                $"推荐前景 {result.RecommendedForeground}（{result.RecommendedRatio:0.00}:1）"
            });
        }
        catch (Exception ex)
        {
            _contrastResult.Text = "对比度计算失败：" + ex.Message;
        }
    }

    private void PickScreenColor()
    {
        if (!OperatingSystem.IsWindows())
        {
            _status.Text = "屏幕取色仅在 Windows 可用。";
            return;
        }
        _status.Text = "正在读取光标下颜色…";
        if (!ScreenColorPicker.TryPick(out var red, out var green, out var blue, out var message))
        {
            _status.Text = message;
            return;
        }
        var color = _colorService.FromRgb(red, green, blue);
        _input.Text = color.Hex;
        ConvertInput();
        _status.Text = $"已拾取 {color.Hex}";
    }

    private static class ScreenColorPicker
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out PointStruct point);

        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hdc, int x, int y);

        [StructLayout(LayoutKind.Sequential)]
        private struct PointStruct
        {
            public int X;
            public int Y;
        }

        public static bool TryPick(out int red, out int green, out int blue, out string message)
        {
            red = green = blue = 0;
            if (!GetCursorPos(out var position))
            {
                message = "无法获取光标位置。";
                return false;
            }
            var hdc = GetDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero)
            {
                message = "无法读取桌面设备上下文。";
                return false;
            }
            try
            {
                var pixel = GetPixel(hdc, position.X, position.Y);
                if (pixel == 0xFFFFFFFF)
                {
                    message = "无法读取该位置的颜色。";
                    return false;
                }
                red = (int)(pixel & 0xFF);
                green = (int)((pixel >> 8) & 0xFF);
                blue = (int)((pixel >> 16) & 0xFF);
                message = string.Empty;
                return true;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }
    }
}