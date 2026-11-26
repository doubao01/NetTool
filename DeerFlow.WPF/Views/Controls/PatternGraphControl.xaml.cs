using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DeerFlow.WPF.Models;
using DeerFlow.WPF.ViewModels;

namespace DeerFlow.WPF.Views.Controls;

public partial class PatternGraphControl : UserControl
{
    private HomeViewModel? _viewModel;
    private readonly List<Ellipse> _patternNodes = new();
    private readonly List<Line> _patternLinks = new();

    public PatternGraphControl()
    {
        InitializeComponent();
        GraphSizeChanged();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += (_, _) => RenderGraph();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Patterns.CollectionChanged -= OnPatternsChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as HomeViewModel;

        if (_viewModel is not null)
        {
            _viewModel.Patterns.CollectionChanged += OnPatternsChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        RenderGraph();
    }

    private void OnPatternsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RenderGraph();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.Patterns))
        {
            RenderGraph();
        }
    }

    private void GraphSizeChanged()
    {
        PatternCanvas.SizeChanged += (_, _) => RenderGraph();
    }

    private void RenderGraph()
    {
        if (PatternCanvas is null) return;

        PatternCanvas.Children.Clear();
        _patternNodes.Clear();
        _patternLinks.Clear();

        var patterns = _viewModel?.Patterns?.ToList() ?? new List<PatternItem>();

        var width = PatternCanvas.ActualWidth;
        var height = PatternCanvas.ActualHeight;
        if (width < 50 || height < 50)
        {
            return;
        }

        if (patterns.Count == 0)
        {
            ShowNoDataMessage(width, height);
            return;
        }

        var centerX = width / 2;
        var centerY = height / 2;
        var radius = Math.Min(centerX, centerY) * 0.7;

        for (var i = 0; i < patterns.Count; i++)
        {
            var angle = (2 * Math.PI * i) / patterns.Count - Math.PI / 2;
            var x = centerX + radius * Math.Cos(angle);
            var y = centerY + radius * Math.Sin(angle);
            CreatePatternNode(patterns[i], x, y);
        }

        CreateCenterNode(centerX, centerY);
        CreateLinks(patterns.Count, centerX, centerY, radius);
    }

    private void CreatePatternNode(PatternItem pattern, double x, double y)
    {
        var ellipse = new Ellipse
        {
            Width = 60,
            Height = 60,
            Fill = GetCategoryBrush(pattern.Category),
            Stroke = GetCategoryStroke(pattern.Category),
            StrokeThickness = 2,
            ToolTip = pattern.Name,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        Canvas.SetLeft(ellipse, x - 30);
        Canvas.SetTop(ellipse, y - 30);
        ellipse.MouseLeftButtonDown += (_, _) => OnPatternSelected(pattern);

        PatternCanvas.Children.Add(ellipse);
        _patternNodes.Add(ellipse);

        var label = new TextBlock
        {
            Text = pattern.Name,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 240)),
            Width = 110,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Canvas.SetLeft(label, x - 55);
        Canvas.SetTop(label, y + 34);
        PatternCanvas.Children.Add(label);
    }

    private void CreateCenterNode(double x, double y)
    {
        var centerEllipse = new Ellipse
        {
            Width = 80,
            Height = 80,
            Fill = new SolidColorBrush(Color.FromRgb(72, 138, 255)),
            Stroke = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            StrokeThickness = 3
        };

        Canvas.SetLeft(centerEllipse, x - 40);
        Canvas.SetTop(centerEllipse, y - 40);

        var labelText = new TextBlock
        {
            Text = "知识图谱",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            Width = 80,
            TextAlignment = TextAlignment.Center
        };

        Canvas.SetLeft(labelText, x - 40);
        Canvas.SetTop(labelText, y - 9);

        PatternCanvas.Children.Add(centerEllipse);
        PatternCanvas.Children.Add(labelText);
    }

    private void CreateLinks(int nodeCount, double centerX, double centerY, double radius)
    {
        for (var i = 0; i < nodeCount; i++)
        {
            var angle = (2 * Math.PI * i) / nodeCount - Math.PI / 2;
            var x = centerX + radius * Math.Cos(angle);
            var y = centerY + radius * Math.Sin(angle);

            var line = new Line
            {
                X1 = centerX,
                Y1 = centerY,
                X2 = x,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                StrokeThickness = 1,
                Opacity = 0.5
            };

            PatternCanvas.Children.Insert(0, line);
            _patternLinks.Add(line);
        }
    }

    private void OnPatternSelected(PatternItem pattern)
    {
        if (_viewModel is not null)
        {
            _viewModel.SelectedPattern = pattern;
        }

        SelectedPatternName.Text = pattern.Name;
        SelectedPatternCategory.Text = $"类别：{pattern.Category}";
        SelectedPatternDescription.Text = string.IsNullOrWhiteSpace(pattern.Description)
            ? pattern.ApplicableScenarios
            : pattern.Description;
        SelectedPatternUsage.Text = $"已使用 {pattern.UsageCount} 次";
        SelectedPatternSuccessRate.Value = pattern.Score * 100;

        StatusText.Text = pattern.Status;
        SelectedPatternStatus.Background = GetStatusBrush(pattern.Status);
    }

    private static Brush GetCategoryBrush(string category) => category switch
    {
        "问题解决" => new SolidColorBrush(Color.FromRgb(102, 187, 106)),
        "工具使用" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
        "流程优化" => new SolidColorBrush(Color.FromRgb(64, 169, 255)),
        "配置调优" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
        _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
    };

    private static Brush GetCategoryStroke(string category) => category switch
    {
        "问题解决" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
        "工具使用" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
        "流程优化" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
        "配置调优" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
        _ => new SolidColorBrush(Color.FromRgb(117, 117, 117))
    };

    private static Brush GetStatusBrush(string status) => status switch
    {
        "Verified" => new SolidColorBrush(Color.FromRgb(102, 187, 106)),
        "Draft" => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
        "Archived" => new SolidColorBrush(Color.FromRgb(117, 117, 117)),
        _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
    };

    private void ShowNoDataMessage(double width, double height)
    {
        var textBlock = new TextBlock
        {
            Text = "暂无模式数据\n完成若干任务后，系统会自动挖掘模式",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
            TextAlignment = TextAlignment.Center,
            Width = 260
        };

        Canvas.SetLeft(textBlock, width / 2 - 130);
        Canvas.SetTop(textBlock, height / 2 - 20);

        PatternCanvas.Children.Add(textBlock);
    }
}