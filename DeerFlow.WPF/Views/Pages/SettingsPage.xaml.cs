using System.Windows;
using System.Windows.Controls;

namespace DeerFlow.WPF.Views.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void OnApiKeyPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.SettingsViewModel vm && sender is PasswordBox box)
        {
            vm.ApiKey = box.Password;
        }
    }
}