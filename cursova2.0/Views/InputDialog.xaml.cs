using System.Windows;
using System.Windows.Input;

namespace MediaVault.Views
{
    public partial class InputDialog : Window
    {
        public string InputText { get; private set; }

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();
            TitleText.Text = title;
            PromptText.Text = prompt;
            InputTextBox.Text = defaultValue;
            InputTextBox.SelectAll();
            InputTextBox.Focus();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            InputText = InputTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}