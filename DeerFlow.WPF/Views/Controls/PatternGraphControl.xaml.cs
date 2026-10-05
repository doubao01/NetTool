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
    private PatternItem? _selectedPattern;
    private readonly List<Ellipse> _patternNodes = new();
    private readonly List<Line> _patternLinks = new();

    public PatternGraphControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            _viewModel = DataContext as HomeViewModel;
            RenderGraph();
        };
    }

    private void RenderGraph()
    {
        if (_viewModel is null) return;

        PatternCanvas.Children.Clear();
        _patternNodes.Clear();
        _patternLinks.Clear();

        // 获取模式列表
        // 实际使用时需要从 PatternMiner 获取数据
        var patterns = GetSamplePatterns();
        
        if (patterns.Count == 0)
        {
            ShowNoDataMessage();
            return;
        }

        // 计算布局
        double centerX = PatternCanvas.ActualWidth / 2;
        double centerY = PatternCanvas.ActualHeight / 2;
        double radius = Math.Min(centerX, centerY) * 0.7;

        for (int i = 0; i < patterns.Count; i++)
        {
            double angle = (2 * Math.PI * i) / patterns.Count;
            double x = centerX + radius * Math.Cos(angle);
            double y = centerY + radius * Math.Sin(angle);

            CreatePatternNode(patterns[i], x, y);
        }

        // 创建中心节点
        CreateCenterNode(centerX, centerY);

        // 创建连线
        CreateLinks(patterns.Count);
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
            ToolTip = pattern.Name
        };

        Canvas.SetLeft(ellipse, x - 30);
        Canvas.SetTop(ellipse, y - 30);

        ellipse.MouseLeftButtonDown += (s, e) => OnPatternSelected(pattern);

        PatternCanvas.Children.Add(ellipse);
        _patternNodes.Add(ellipse);
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
            Foreground = Brushes.White
        };

        Canvas.SetLeft(labelText, x - 40);
        Canvas.SetTop(labelText, y - 6);

        PatternCanvas.Children.Add(centerEllipse);
        PatternCanvas.Children.Add(labelText);
    }

    private void CreateLinks(int nodeCount)
    {
        double centerX = PatternCanvas.ActualWidth / 2;
        double centerY = PatternCanvas.ActualHeight / 2;

        for (int i = 0; i < nodeCount; i++)
        {
            double angle = (2 * Math.PI * i) / nodeCount;
            double x = centerX + Math.Min(centerX, centerY) * 0.7 * Math.Cos(angle);
            double y = centerY + Math.Min(centerX, centerY) * 0.7 * Math.Sin(angle);

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
        _selectedPattern = pattern;
        
        SelectedPatternName.Text = pattern.Name;
        SelectedPatternCategory.Text = $"类别：{pattern.Category}";
        SelectedPatternDescription.Text = pattern.Description;
        SelectedPatternUsage.Text = $"已使用 {pattern.UsageCount} 次";
        SelectedPatternSuccessRate.Value = pattern.Score * 100;
        
        StatusText.Text = pattern.Status;
        SelectedPatternStatus.Background = GetStatusBrush(pattern.Status);
    }

    private Brush GetCategoryBrush(string category)
    {
        return category switch
        {
            "问题解决" => new SolidColorBrush(Color.FromRgb(102, 187, 106)),
            "工具使用" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            "流程优化" => new SolidColorBrush(Color.FromRgb(64, 169, 255)),
            "配置调优" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
            _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
        };
    }

    private Brush GetCategoryStroke(string category)
    {
        return category switch
        {
            "问题解决" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            "工具使用" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
            "流程优化" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            "配置调优" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
            _ => new SolidColorBrush(Color.FromRgb(117, 117, 117))
        };
    }

    private Brush GetStatusBrush(string status)
    {
        return status switch
        {
            "Verified" => new SolidColorBrush(Color.FromRgb(102, 187, 106)),
            "Draft" => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
            "Archived" => new SolidColorBrush(Color.FromRgb(117, 117, 117)),
            _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
        };
    }

    private void ShowNoDataMessage()
    {
        var textBlock = new TextBlock
        {
            Text = "暂无模式数据\n请先执行任务积累数据",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
            TextAlignment = TextAlignment.Center
        };

        Canvas.SetLeft(textBlock, PatternCanvas.ActualWidth / 2 - 100);
        Canvas.SetTop(textBlock, PatternCanvas.ActualHeight / 2 - 20);

        PatternCanvas.Children.Add(textBlock);
    }

    private List<PatternItem> GetSamplePatterns()
    {
        // 示例数据，实际应从 PatternMiner 获取
        return new List<PatternItem>
        {
            new PatternItem
            {
                Name = "高效代码审查",
                Category = "流程优化",
                Description = "使用自动化检查 + 人工审查的组合流程",
                UsageCount = 15,
                Score = 0.85,
                Status = "Verified"
            },
            new PatternItem
            {
                Name = "错误处理最佳实践",
                Category = "问题解决",
                Description = "分层错误处理策略，包含重试和回滚机制",
                UsageCount = 23,
                Score = 0.92,
                Status = "Verified"
            },
            new PatternItem
            {
                Name = "工具调用优化",
                Category = "工具使用",
                Description = "批量工具调用减少网络往返",
                UsageCount = 31,
                Score = 0.78,
                Status = "Verified"
            },
            new PatternItem
            {
                Name = "性能调优配置",
                Category = "配置调优",
                Description = "针对大模型的并发和缓存配置",
                UsageCount = 12,
                Score = 0.81,
                Status = "Verified"
            }
        };
    }
}
