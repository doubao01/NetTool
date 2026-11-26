using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using SystemToolkit.App.ViewModels;
using SystemToolkit.App.Views;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ICommandPaletteService _palette;

    public MainWindow(MainViewModel viewModel, ICommandPaletteService palette)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _palette = palette;
        MenuTreeView.ItemsSource = _viewModel.MenuItems;
        LoadDefaultContent();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
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

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            OpenCommandPalette();
            e.Handled = true;
        }
    }

    private void CommandPaletteButton_Click(object sender, RoutedEventArgs e) => OpenCommandPalette();

    private void OpenCommandPalette()
    {
        var dialog = new CommandPaletteWindow(_palette) { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.SelectedTitle)) return;
        var target = _viewModel.FindByTitle(dialog.SelectedTitle);
        if (target is null) return;
        MainContent.Content = _viewModel.CreateView(target);
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