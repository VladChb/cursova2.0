using System.Windows;
using MediaVault.ViewModels;

namespace MediaVault.Views
{
    public partial class StatsWindow : Window
    {
        public StatsWindow()
        {
            InitializeComponent();
            DataContext = new CollectionStatsViewModel();
        }

        public void SetStats(CollectionStatsViewModel viewModel)
        {
            DataContext = viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
