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

            Services.EnvService.Load();

            var vm = new MainViewModel();
            vm.FocusSearchRequested += (s, e) => SearchBox.Focus();
            vm.OpenHelpRequested += (s, e) =>
            {
                var help = new Views.HelpWindow { Owner = this };
                help.ShowDialog();
            };

            DataContext = vm;

        }

        private void PageSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
            {
                if (int.TryParse(item.Tag?.ToString(), out int size))
                {
                    if (DataContext is MainViewModel vm)
                    {
                        vm.PageSize = size;
                    }
                }
            }
        }

        private void OpenTmdbFromCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is MediaItem item &&
                !string.IsNullOrEmpty(item.TmdbUrl))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = item.TmdbUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не вдалося відкрити посилання:\n{ex.Message}");
                }
            }
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is MediaItem item)
            {
                try
                {
                    Clipboard.SetText(item.FilePath);
                    // Можна показати коротке сповіщення
                    StatusText = $"Шлях скопійовано: {item.FilePath}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не вдалося скопіювати:\n{ex.Message}");
                }
            }
        }

        private void CopyTitle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is MediaItem item)
            {
                try
                {
                    Clipboard.SetText(item.Title);
                    StatusText = $"Назву скопійовано: {item.Title}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не вдалося скопіювати:\n{ex.Message}");
                }
            }
        }

        private void OpenTrackUrlFromCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is MediaItem item &&
                !string.IsNullOrEmpty(item.TrackUrl))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = item.TrackUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не вдалося відкрити посилання:\n{ex.Message}");
                }
            }
        }

        // ============================================================
        // КОНТЕКСТНЕ МЕНЮ: ПЕРЕЙМЕНУВАННЯ
        // ============================================================

        private async void RenameFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is MediaItem item)
            {
                // Діалог введення нової назви
                var dialog = new InputDialog("Перейменувати файл", "Введіть нову назву:", item.Title);


                if (dialog.ShowDialog() != true)
                    return;

                string newTitle = dialog.InputText?.Trim();

                if (string.IsNullOrEmpty(newTitle) || newTitle == item.Title)
                    return;

                try
                {
                    // Перевіряємо, чи існує файл
                    if (!System.IO.File.Exists(item.FilePath))
                    {
                        MessageBox.Show("Файл не знайдено на диску!", "Помилка",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Формуємо новий шлях
                    string directory = System.IO.Path.GetDirectoryName(item.FilePath);
                    string extension = System.IO.Path.GetExtension(item.FilePath);
                    string newFileName = $"{newTitle}{extension}";
                    string newFilePath = System.IO.Path.Combine(directory, newFileName);

                    // Перевіряємо, чи файл з такою назвою вже існує
                    if (System.IO.File.Exists(newFilePath))
                    {
                        MessageBox.Show("Файл з такою назвою вже існує!", "Помилка",
                                        MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Перейменовуємо файл на диску
                    System.IO.File.Move(item.FilePath, newFilePath);

                    // Оновлюємо модель
                    item.FilePath = newFilePath;
                    item.FileName = newFileName;
                    item.Title = newTitle;

                    // Оновлюємо UI
                    if (DataContext is MainViewModel vm)
                    {
                        vm.ApplyFilter();
                        await vm.SaveChangesAsync();
                    }

                    StatusText = $"Файл перейменовано: {newTitle}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не вдалося перейменувати файл:\n{ex.Message}",
                                    "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string StatusText
        {
            get => (DataContext as MainViewModel)?.StatusText ?? "";
            set
            {
                if (DataContext is MainViewModel vm)
                    vm.StatusText = value;
            }
        }

        private void ChooseCoverFromCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is MediaItem item)
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Виберіть обкладинку",
                    Filter = "Зображення|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|Всі файли|*.*"
                };

                if (dialog.ShowDialog() != true)
                    return;

                try
                {
                    string appDataFolder = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "MediaVault",
                        "Covers");

                    if (!System.IO.Directory.Exists(appDataFolder))
                        System.IO.Directory.CreateDirectory(appDataFolder);

                    string extension = System.IO.Path.GetExtension(dialog.FileName);
                    string newFileName = $"{item.Id}{extension}";
                    string newFilePath = System.IO.Path.Combine(appDataFolder, newFileName);

                    System.IO.File.Copy(dialog.FileName, newFilePath, overwrite: true);

                    item.PosterUrl = newFilePath;

                    if (DataContext is MainViewModel vm)
                        _ = vm.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка: {ex.Message}");
                }
            }
        }

        private async void MarkAsFilm_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is MediaItem item)
            {
                item.Type = MediaType.Film;
                if (DataContext is MainViewModel vm)
                {
                    vm.ApplyFilter();
                    vm.UpdateStats();
                    await vm.SaveChangesAsync();
                }
            }
        }

        private void ClearCategoryFromTools_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Views.CategorySelectionDialog();
            if (dialog.ShowDialog() == true && dialog.SelectedCategory != null)
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.ClearCategoryCommand.Execute(dialog.SelectedCategory);
                }
            }
        }

        private async void MarkAsShortVideo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is MediaItem item)
            {
                item.Type = MediaType.ShortVideo;
                if (DataContext is MainViewModel vm)
                {
                    vm.ApplyFilter();
                    vm.UpdateStats();
                    await vm.SaveChangesAsync();
                }
            }
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is MainViewModel vm && e.NewValue is MainViewModel.FolderNode node)
            {
                vm.SelectFolderNodeCommand.Execute(node);
            }
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
                MaxHeight = SystemParameters.WorkArea.Height + 16;
                MaxWidth = SystemParameters.WorkArea.Width + 16;
                WindowState = WindowState.Maximized;
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenToolsMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void OpenDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var dashboard = new Views.DashboardWindow(vm.AllMediaItems.ToList())
                {
                    Owner = this
                };
                dashboard.ShowDialog();
                vm.IsToolsMode = false;
            }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new Views.SettingsWindow { Owner = this };
            settings.ShowDialog();
            if (DataContext is MainViewModel vm)
                vm.IsToolsMode = false;
        }

        private void OpenHelp_Click(object sender, RoutedEventArgs e)
        {
            var help = new Views.HelpWindow { Owner = this };
            help.ShowDialog();
            if (DataContext is MainViewModel vm)
                vm.IsToolsMode = false;
        }

        private void OpenSecurityScanner_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var scanner = new SecurityScannerWindow(vm.AllMediaItems, null)
                {
                    Owner = this
                };
                scanner.ShowDialog();
                vm.IsToolsMode = false;
            }
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