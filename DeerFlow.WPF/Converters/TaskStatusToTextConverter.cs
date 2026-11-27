using System.Globalization;
using System.Windows.Data;
using TaskStatus = DeerFlow.WPF.Models.TaskStatus;

namespace DeerFlow.WPF.Converters;

/// <summary>
/// 任务状态枚举转中文文本转换器
/// </summary>
[ValueConversion(typeof(TaskStatus), typeof(string))]
public sealed class TaskStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is TaskStatus status
            ? status switch
            {
                TaskStatus.Pending => "等待中",
                TaskStatus.Running => "运行中",
                TaskStatus.Completed => "已完成",
                TaskStatus.Failed => "失败",
                TaskStatus.Paused => "已暂停",
                _ => "未知"
            }
            : "未知";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}