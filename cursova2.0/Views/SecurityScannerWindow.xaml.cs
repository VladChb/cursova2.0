using System;
using System.Windows;
using MediaVault.Models;
using MediaVault.ViewModels;
using System.Collections.ObjectModel;

namespace MediaVault.Views
{
    public partial class SecurityScannerWindow : Window
    {
        public SecurityScannerWindow(ObservableCollection<MediaItem> mediaItems, object settings)
        {
            InitializeComponent();

            var vm = new SecurityScannerViewModel(mediaItems, null);
            DataContext = vm;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
