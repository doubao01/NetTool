using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DeerFlow.WPF;

/// <summary>
/// 将布尔值转换为画笔颜色的转换器，用于状态指示灯显示
/// true 转换为绿色（运行中），false 转换为红色（未连接）
/// </summary>
public class BooleanToBrushConverter : IValueConverter
{
    private static readonly Brush ConnectedBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
    private static readonly Brush DisconnectedBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));

    /// <summary>
    /// 将布尔值转换为状态颜色画笔
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? ConnectedBrush : DisconnectedBrush;
    }

    /// <summary>
    /// 不支持反向转换，返回 Binding.DoNothing 表示不参与绑定更新
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
