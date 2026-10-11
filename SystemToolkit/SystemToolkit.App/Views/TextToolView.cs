using System.Windows;
using System.Windows.Controls;
using SystemToolkit.Core.Models;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App.Views;

public class TextToolView : UserControl
{
    private readonly ITextToolService _textTool;
    private readonly TextBox _input;
    private readonly TextBox _output;
    private readonly ComboBox _namingBox;
    private readonly ComboBox _lineOpBox;
    private readonly TextBlock _stats;

    public TextToolView(ITextToolService textTool)
    {
        _textTool = textTool;
        _input = new TextBox { AcceptsReturn = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(10, 0, 10, 8) };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock { Text = "文本工具", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(10) };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var tools = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 10, 10) };
        tools.Children.Add(new TextBlock { Text = "命名风格", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        _namingBox = new ComboBox { Width = 110, Margin = new Thickness(0, 0, 8, 0) };
        foreach (NamingStyle style in Enum.GetValues<NamingStyle>()) _namingBox.Items.Add(style.ToString());
        _namingBox.SelectedIndex = 0;
        tools.Children.Add(_namingBox);
        var conv = new Button { Content = "转换", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 16, 0) };
        conv.Click += (_, _) => ConvertNaming();
        tools.Children.Add(conv);

        tools.Children.Add(new TextBlock { Text = "行处理", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        _lineOpBox = new ComboBox { Width = 130, Margin = new Thickness(0, 0, 8, 0) };
        foreach (LineOperation op in Enum.GetValues<LineOperation>()) _lineOpBox.Items.Add(op.ToString());
        _lineOpBox.SelectedIndex = 0;
        tools.Children.Add(_lineOpBox);
        var lineBtn = new Button { Content = "应用", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 16, 0) };
        lineBtn.Click += (_, _) => ApplyLineOperation();
        tools.Children.Add(lineBtn);

        var half = new Button { Content = "全角转半角", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        half.Click += (_, _) => WriteOutput(_textTool.ToHalfWidth(_input.Text));
        tools.Children.Add(half);
        var full = new Button { Content = "半角转全角", Padding = new Thickness(12, 4, 12, 4) };
        full.Click += (_, _) => WriteOutput(_textTool.ToFullWidth(_input.Text));
        tools.Children.Add(full);
        Grid.SetRow(tools, 1);
        root.Children.Add(tools);

        Grid.SetRow(_input, 2);
        root.Children.Add(_input);

        _output = new TextBox { AcceptsReturn = true, IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(10, 0, 10, 8) };
        Grid.SetRow(_output, 3);
        root.Children.Add(_output);

        _stats = FileDedupView.CreateStatus();
        Grid.SetRow(_stats, 4);
        root.Children.Add(_stats);

        _input.TextChanged += (_, _) => UpdateStats();
        UpdateStats();
        Content = root;
    }

    private void ConvertNaming()
    {
        if (_namingBox.SelectedItem is not string name) return;
        WriteOutput(_textTool.ConvertNaming(_input.Text, Enum.Parse<NamingStyle>(name)));
    }

    private void ApplyLineOperation()
    {
        if (_lineOpBox.SelectedItem is not string name) return;
        WriteOutput(_textTool.TransformLines(_input.Text, Enum.Parse<LineOperation>(name)));
    }

    private void WriteOutput(string text)
    {
        _output.Text = text;
    }

    private void UpdateStats()
    {
        var s = _textTool.GetStatistics(_input.Text);
        _stats.Text = $"行 {s.Lines}（非空 {s.NonEmptyLines}）    词 {s.Words}    字符 {s.Characters}（不含空白 {s.CharactersNoWhitespace}）    UTF-8 {s.BytesUtf8} B";
    }
}
