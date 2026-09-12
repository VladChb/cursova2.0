using System.Diagnostics;
using System.Windows;
using MediaVault.Models;

namespace MediaVault.Views
{
    public partial class DetailsWindow : Window
    {
        private readonly MediaItem _item;

        public DetailsWindow(MediaItem item)
        {
            InitializeComponent();
            _item = item;
            DataContext = item;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _item.FilePath,
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Не вдалося відкрити файл:\n{ex.Message}",
                                "Помилка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                $"Видалити \"{_item.Title}\" з колекції?\n(Файл на диску залишиться)",
                "Підтвердження",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                DialogResult = true;
                Close();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}