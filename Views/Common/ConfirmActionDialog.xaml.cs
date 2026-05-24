using System.Windows;
using System.Windows.Input;

namespace Database_Hub.Views.Common
{
    public partial class ConfirmActionDialog : Window
    {
        public ConfirmActionDialog(string title, string message)
        {
            InitializeComponent();
            Title = title;
            TitleTextBlock.Text = title;
            MessageTextBlock.Text = message;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
