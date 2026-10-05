using System.Globalization;
using System.Windows.Data;

namespace DeerFlow.WPF.Converters;

/// <summary>
/// 布尔值转状态文本转换器，将 true/false 转换为对应的状态描述文本
/// </summary>
[ValueConversion(typeof(bool), typeof(string))]
public sealed class BooleanToStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? "已启用" : "已禁用";
        }

        return "未知";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
