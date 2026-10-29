using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MediaVault.Models;
using MediaVault.Services;

namespace MediaVault.Views
{
    public partial class DuplicatesWindow : Window
    {
        private readonly List<DuplicateGroup> _groups;
        private readonly DuplicateFinder _finder;
        private readonly ObservableCollection<MediaItem> _allItems;

        public DuplicatesWindow(List<DuplicateGroup> groups, DuplicateFinder finder, ObservableCollection<MediaItem> allItems)
        {
            InitializeComponent();
            _groups = groups;
            _finder = finder;
            _allItems = allItems;


            this.KeyDown += DuplicatesWindow_KeyDown;

            GroupsList.ItemsSource = groups;

            int totalFiles = groups.Sum(g => g.Count);
            long totalSize = groups.Sum(g => g.TotalSize);
            StatsText.Text = $"Знайдено {groups.Count} груп · {totalFiles} файлів · {FormatSize(totalSize)}";
        }

        private void DuplicatesWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }

        private string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
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

        private void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is DuplicateGroup group)
            {
                var result = MessageBox.Show(
                    $"Видалити всі {group.Count} копії з диска?\n\nЦе незворотна дія!",
                    "Підтвердження",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                // Видаляємо файли з диска
                _finder.DeleteFiles(group.Items);

                // Видаляємо з колекції
                foreach (var item in group.Items.ToList())
                {
                    _allItems.Remove(item);
                }

                // Оновлюємо список
                _groups.Remove(group);
                GroupsList.ItemsSource = null;
                GroupsList.ItemsSource = _groups;

                int totalFiles = _groups.Sum(g => g.Count);
                long totalSize = _groups.Sum(g => g.TotalSize);
                StatsText.Text = $"Залишилось {_groups.Count} груп · {totalFiles} файлів · {FormatSize(totalSize)}";

                if (_groups.Count == 0)
                {
                    MessageBox.Show("Всі дублікати видалено!", "Готово",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
            }
        }

        private void DeleteAll_Click(object sender, RoutedEventArgs e)
        {
            if (_groups.Count == 0)
            {
                Close();
                return;
            }

            var result = MessageBox.Show(
                $"Видалити ВСІ дублікати з диска?\n\nБуде видалено {_groups.Sum(g => g.Count)} файлів.\n\nЦе незворотна дія!",
                "Підтвердження",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            foreach (var group in _groups.ToList())
            {
                _finder.DeleteFiles(group.Items);

                foreach (var item in group.Items.ToList())
                {
                    _allItems.Remove(item);
                }
            }

            _groups.Clear();
            GroupsList.ItemsSource = null;

            MessageBox.Show("Всі дублікати видалено!", "Готово",
                            MessageBoxButton.OK, MessageBoxImage.Information);

            Close();
        }
    }
}