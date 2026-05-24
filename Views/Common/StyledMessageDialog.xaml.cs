using System.Windows;
using System;

namespace Database_Hub.Views.Common
{
    public partial class StyledMessageDialog : Window
    {
        public StyledMessageDialog(string title, string message)
        {
            InitializeComponent();
            Title = title;
            TitleTextBlock.Text = title;
            MessageTextBlock.Text = message;
            Loaded += (_, __) => ApplyResponsiveSizing();
        }

        private void ApplyResponsiveSizing()
        {
            var workArea = SystemParameters.WorkArea;

            DialogContentRoot.MinWidth = Math.Max(320, workArea.Width * 0.26);
            DialogContentRoot.MaxWidth = Math.Max(460, workArea.Width * 0.52);

            MaxWidth = workArea.Width * 0.85;
            MaxHeight = workArea.Height * 0.75;
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
