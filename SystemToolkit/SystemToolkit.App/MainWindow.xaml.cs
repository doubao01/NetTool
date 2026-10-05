using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SystemToolkit.App.ViewModels;

namespace SystemToolkit.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        MenuTreeView.ItemsSource = _viewModel.MenuItems;
        LoadDefaultContent();
    }

    private void LoadDefaultContent()
    {
        // 默认选中第一个菜单项
        if (_viewModel.MenuItems.Count > 0)
        {
            var firstItem = _viewModel.MenuItems[0];
            if (firstItem.Children.Count > 0)
            {
                MainContent.Content = firstItem.Children[0].CreateView();
            }
        }
    }

    private void MenuTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is MenuItemViewModel menuItem)
        {
            MainContent.Content = menuItem.CreateView();
        }
    }

    private void WebsiteLink_MouseDown(object sender, MouseButtonEventArgs e)
    {
        System.Diagnostics.Process.Start(new ProcessStartInfo
        {
            FileName = "https://monkeycode-ai.com/?ic=019e4e77-519b-70dc-82ea-2a833e5e93da",
            UseShellExecute = true
        });
    }
}
