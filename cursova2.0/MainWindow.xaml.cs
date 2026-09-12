using MediaVault.Services;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MediaVault
{
    public partial class MainWindow : Window
    {

        private readonly FileScanner _scanner = new FileScanner();
        public MainWindow()
        {
            InitializeComponent();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void Category_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            string category = (button.Content as Grid).Children.OfType<TextBlock>().FirstOrDefault()?.Text;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Виберіть папку з медіафайлами"
            };

            if (dialog.ShowDialog() != true)
                return;

            string folderPath = dialog.FolderName;

            var files = await _scanner.ScanFolderAsync(folderPath);

            MessageBox.Show($"Знайдено {files.Count} файлів у папці:\n{folderPath}",
                            "Сканування завершено",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
        }
    }
}