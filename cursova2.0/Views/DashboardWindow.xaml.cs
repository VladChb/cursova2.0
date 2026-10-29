using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using MediaVault.Models;
using MediaVault.ViewModels;

namespace MediaVault.Views
{
    public partial class DashboardWindow : Window
    {
        public DashboardWindow(List<MediaItem> items)
        {
            InitializeComponent();
            DataContext = new DashboardViewModel(items);
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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}