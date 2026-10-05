using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace DeerFlow.WPF;

/// <summary>
/// 将命令的可执行状态转换为可见性的转换器
/// 命令可执行时返回 Visible，不可执行时返回 Collapsed
/// </summary>
public class CommandToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 将 ICommand 转换为 Visibility
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ICommand command)
        {
            return command.CanExecute(parameter) ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    /// <summary>
    /// 不支持反向转换，返回 Binding.DoNothing 表示不参与绑定更新
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
