using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using SystemToolkit.App.ViewModels;

namespace SystemToolkit.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        MenuTreeView.ItemsSource = _viewModel.MenuItems;
        LoadDefaultContent();
    }

    private void LoadDefaultContent()
    {
        if (_viewModel.MenuItems.Count > 0 && _viewModel.MenuItems[0].Children.Count > 0)
        {
            MainContent.Content = _viewModel.CreateView(_viewModel.MenuItems[0].Children[0]);
        }
    }

    private void MenuTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is MenuItemViewModel menuItem)
        {
            MainContent.Content = _viewModel.CreateView(menuItem);
        }
    }

    private void WebsiteLink_MouseDown(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://monkeycode-ai.com/?ic=019e4e77-519b-70dc-82ea-2a833e5e93da",
            UseShellExecute = true
        });
    }
}
