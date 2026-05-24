using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;

namespace Database_Hub.Views.Common
{
    public enum ToastNotificationType
    {
        Success,
        Warning,
        Error
    }

    public partial class ToastNotificationWindow : Window
    {
        private readonly DispatcherTimer _closeTimer;

        public ToastNotificationWindow(string title, string message, ToastNotificationType type = ToastNotificationType.Success)
        {
            InitializeComponent();
            TitleTextBlock.Text = title;
            MessageTextBlock.Text = message;
            ApplyTheme(type);

            Loaded += OnLoaded;

            _closeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.6)
            };
            _closeTimer.Tick += (_, __) =>
            {
                _closeTimer.Stop();
                Close();
            };
        }

        private void ApplyTheme(ToastNotificationType type)
        {
            if (type == ToastNotificationType.Warning)
            {
                ToastBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF7ED"));
                ToastBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F79009"));
                IconCircle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F79009"));
                IconTextBlock.Text = "!";
                TitleTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B54708"));
                MessageTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9A3412"));
                return;
            }

            if (type == ToastNotificationType.Error)
            {
                ToastBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF2F2"));
                ToastBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                IconCircle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                IconTextBlock.Text = "x";
                TitleTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
                MessageTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F1D1D"));
                return;
            }

            ToastBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECFDF3"));
            ToastBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
            IconCircle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
            IconTextBlock.Text = "✓";
            TitleTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#065F46"));
            MessageTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#14532D"));
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - ActualWidth - 18;
            Top = workArea.Bottom - ActualHeight - 18;
            _closeTimer.Start();
        }
    }
}
