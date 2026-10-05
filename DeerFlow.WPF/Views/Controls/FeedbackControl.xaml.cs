using System.Windows;
using System.Windows.Controls;
using DeerFlow.WPF.ViewModels;

namespace DeerFlow.WPF.Views.Controls;

public partial class FeedbackControl : UserControl
{
    private ChatViewModel? _viewModel;

    public FeedbackControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => _viewModel = DataContext as ChatViewModel;
    }

    private void LikeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.LikeFeedbackCommand.CanExecute(null) == true)
        {
            _viewModel.LikeFeedbackCommand.Execute(null);
            ShowThanksMessage();
        }
    }

    private void DislikeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.DislikeFeedbackCommand.CanExecute(null) == true)
        {
            _viewModel.DislikeFeedbackCommand.Execute(null);
            ShowThanksMessage();
        }
    }

    private void ShowThanksMessage()
    {
        LikeButton.IsEnabled = false;
        DislikeButton.IsEnabled = false;
        ThanksMessage.Visibility = Visibility.Visible;
    }
}
