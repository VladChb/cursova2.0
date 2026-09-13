using System.Windows;
using System.Windows.Input;
using MediaVault.Models;
using MediaVault.ViewModels;
using MediaVault.Views;

namespace MediaVault
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void Card_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is MediaItem item)
            {
                var detailsWindow = new DetailsWindow(item)
                {
                    Owner = this
                };

                var result = detailsWindow.ShowDialog();

                if (DataContext is MainViewModel vm)
                {
                    if (detailsWindow.IsDeleted)
                    {
                        await vm.RemoveItemAsync(item);
                    }
                    else if (result == true)
                    {
                        await vm.SaveChangesAsync();
                    }
                }
            }
        }
    }
}