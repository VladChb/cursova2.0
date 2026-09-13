using System;
using System.Windows;
using System.Windows.Controls;
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
            if (e.OriginalSource is Button ||
                (e.OriginalSource is FrameworkElement fe && fe.TemplatedParent is Button))
            {
                return;
            }

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
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
                MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;
                WindowState = WindowState.Maximized;
            }
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

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is MediaItem item)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = item.FilePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не вдалося відкрити файл:\n{ex.Message}");
                }
            }
        }

        private void ShowInFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is MediaItem item)
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe",
                        $"/select,\"{item.FilePath}\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не вдалося відкрити папку:\n{ex.Message}");
                }
            }
        }

        private async void ToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is MediaItem item &&
                DataContext is MainViewModel vm)
            {
                item.IsFavorite = !item.IsFavorite;
                await vm.SaveChangesAsync();
            }
        }

        private async void RemoveFromCollection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is MediaItem item &&
                DataContext is MainViewModel vm)
            {
                var result = MessageBox.Show(
                    $"Видалити \"{item.Title}\" з колекції?",
                    "Підтвердження",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await vm.RemoveItemAsync(item);
                }
            }
        }

        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            if (DataContext is MainViewModel vm)
            {
                await vm.SaveChangesAsync();
            }
        }


    }
}