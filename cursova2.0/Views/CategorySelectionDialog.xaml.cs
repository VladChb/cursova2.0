using System.Windows;
using System.Windows.Controls;

namespace MediaVault.Views
{
    public partial class CategorySelectionDialog : Window
    {
        public string SelectedCategory { get; private set; }

        public CategorySelectionDialog()
        {
            InitializeComponent();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (CategoryList.SelectedItem is ListBoxItem item)
            {
                SelectedCategory = item.Tag?.ToString();
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Виберіть категорію", "Увага");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}