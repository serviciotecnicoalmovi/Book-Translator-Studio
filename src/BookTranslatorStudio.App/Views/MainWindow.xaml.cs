using System.Windows;
using System.Windows.Controls;
using BookTranslatorStudio.ViewModels;

namespace BookTranslatorStudio.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ApiKeyBox_OnPasswordChanged(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            sender is PasswordBox passwordBox)
        {
            viewModel.SessionApiKey = passwordBox.Password;
        }
    }
}
