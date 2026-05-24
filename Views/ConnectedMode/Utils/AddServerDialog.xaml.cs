using System.Windows;
using System;
using System.Windows.Input;
using Database_Hub.Views.Common;

namespace Database_Hub.Views.ConnectedMode
{
    public partial class AddServerDialog : Window
    {
        public string ServerName => txtName.Text.Trim();
        public string ServerAddress => txtAddress.Text.Trim();

        public AddServerDialog()
        {
            InitializeComponent();
            Loaded += (_, __) => ApplyResponsiveSizing();
        }

        private void ApplyResponsiveSizing()
        {
            var workArea = SystemParameters.WorkArea;

            MinWidth = 420;
            MinHeight = 250;
            MaxWidth = Math.Max(520, workArea.Width * 0.6);
            MaxHeight = Math.Max(320, workArea.Height * 0.7);

            Width = Math.Clamp(workArea.Width * 0.32, MinWidth, MaxWidth);
            Height = Math.Clamp(workArea.Height * 0.34, MinHeight, MaxHeight);
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ServerName) || string.IsNullOrWhiteSpace(ServerAddress))
            {
                var dialog = new StyledMessageDialog("Input Required", "Please enter both a display name and server address.");
                dialog.ShowDialog();
                return;
            }
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}